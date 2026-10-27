using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HR.Editor
{
    /// <summary>
    /// Apply, Verify and Build — each reachable from the menu and from the
    /// command line. Nothing the release depends on is a checkbox someone has to
    /// remember ticking; it is all set here by script.
    ///
    /// Build calls Verify first and refuses to run when Verify returns anything.
    /// </summary>
    public static class ReleaseTool
    {
        const string Tag = "[HypnoticRings]";

        // ---- apply ------------------------------------------------------------

        [MenuItem("Hypnotic Rings/Release/Apply Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = ReleaseSettings.CompanyName;
            PlayerSettings.productName = ReleaseSettings.ProductName;
            PlayerSettings.SetApplicationIdentifier(
                UnityEditor.Build.NamedBuildTarget.Android, ReleaseSettings.ApplicationId);

            ApplyOrientation();

            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)ReleaseSettings.MinSdk;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)ReleaseSettings.TargetSdk;

            // 64-bit is mandatory on the store; 32-bit keeps older minSdk devices.
            // ARM64 alone. The template ships ARMv7 as well, which is right for a game that
            // still supports 32-bit devices — this one does not: minSdk is 26 and the store has
            // required a 64-bit library for years. Keeping ARMv7 would add a whole second native
            // library to the bundle for devices the game already refuses to install on.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            // Deferred rather than repeated. ProjectSetup already decides stripping and the
            // IL2CPP configuration, and this file setting them too is how the two drifted apart
            // in the first place: Release here against Master there, and whichever ran last won.
            ProjectSetup.ApplyStripping();

            // These two are settings, not manifest attributes, whatever the custom manifest
            // says. Verified by reading the merged manifest out of a built bundle: it carried
            // extractNativeLibs="true" and resizeableActivity="true" while the file on disk
            // asked for false on both. Unity's own values win the merge, so they have to be set
            // where Unity reads them.
            //
            // Native-library packaging is not a PlayerSetting in Unity 6000.6 — there is no
            // useLegacyPackaging any more — so it is set in gradleTemplate.properties instead,
            // next to the custom manifest. Checked by reading the editor assembly for the
            // symbol rather than assuming the old API survived.

            // A portrait-locked phone game has nothing sensible to do in a resizable window.
            PlayerSettings.Android.resizeableActivity = false;

            // Draw inside the cutout rather than letterboxing around it; the
            // interface keeps itself clear with SafeAreaFitter.
            PlayerSettings.Android.renderOutsideSafeArea = true;

            PlayerSettings.Android.forceInternetPermission = ReleaseSettings.RequiresInternet;
            PlayerSettings.Android.forceSDCardPermission = false;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;

            EditorBuildSettings.scenes = ReleaseSettings.ScenePaths
                .Select(p => new EditorBuildSettingsScene(p, true))
                .ToArray();

            PlayerSettings.bundleVersion = ReleaseSettings.VersionName;
            // Only ever climbs: the store rejects a code it has already seen.
            if (PlayerSettings.Android.bundleVersionCode < 1)
                PlayerSettings.Android.bundleVersionCode = 1;

            ApplyIcons();

            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Release settings applied.");
        }

        static void ApplyOrientation()
        {
            bool portrait = ReleaseSettings.Orientation == ReleaseOrientation.Portrait;
            bool landscape = ReleaseSettings.Orientation == ReleaseOrientation.Landscape;
            bool auto = ReleaseSettings.Orientation == ReleaseOrientation.Auto;

            PlayerSettings.defaultInterfaceOrientation =
                auto ? UIOrientation.AutoRotation
                : portrait ? UIOrientation.Portrait
                : UIOrientation.LandscapeLeft;

            PlayerSettings.allowedAutorotateToPortrait = portrait || auto;
            // Upside-down stays off even in Auto: a phone flipped end over end
            // puts the earpiece at the bottom and nothing else expects it.
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = landscape || auto;
            PlayerSettings.allowedAutorotateToLandscapeRight = landscape || auto;
        }

        /// <summary>
        /// Adaptive is the only non-obsolete Android icon kind in Unity 6 —
        /// Legacy and Round both redirect to it — and it takes two layers rather
        /// than one flattened image.
        /// </summary>
        static void ApplyIcons()
        {
            var target = UnityEditor.Build.NamedBuildTarget.Android;

            var flat = AssetDatabase.LoadAssetAtPath<Texture2D>(ReleaseSettings.IconPath);
            if (flat != null)
            {
                int[] sizes = PlayerSettings.GetIconSizes(target, IconKind.Application);
                var icons = new Texture2D[sizes.Length];
                for (int i = 0; i < icons.Length; i++) icons[i] = flat;
                PlayerSettings.SetIcons(target, icons, IconKind.Application);
            }

            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(ReleaseSettings.AdaptiveForegroundPath);
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(ReleaseSettings.AdaptiveBackgroundPath);
            if (foreground == null || background == null) return;

            PlatformIconKind kind = UnityEditor.Android.AndroidPlatformIconKind.Adaptive;
            PlatformIcon[] adaptive = PlayerSettings.GetPlatformIcons(target, kind);

            foreach (PlatformIcon icon in adaptive)
                icon.SetTextures(background, foreground);

            PlayerSettings.SetPlatformIcons(target, kind, adaptive);
        }

        // ---- verify -----------------------------------------------------------

        /// <summary>
        /// Returns a list of problems. Empty means the project is releasable.
        /// Checks the same things Apply sets, plus the ones that go wrong
        /// silently — a deleted manifest, a blank icon, a debug flag left on.
        /// </summary>
        public static List<string> Verify()
        {
            var problems = new List<string>();
            var target = UnityEditor.Build.NamedBuildTarget.Android;

            string id = PlayerSettings.GetApplicationIdentifier(target);
            if (id != ReleaseSettings.ApplicationId)
                problems.Add($"Application id is '{id}', expected '{ReleaseSettings.ApplicationId}'.");

            if (PlayerSettings.productName != ReleaseSettings.ProductName)
                problems.Add($"Product name is '{PlayerSettings.productName}', expected '{ReleaseSettings.ProductName}'.");

            if (PlayerSettings.companyName != ReleaseSettings.CompanyName)
                problems.Add($"Company name is '{PlayerSettings.companyName}', expected '{ReleaseSettings.CompanyName}'.");

            if ((int)PlayerSettings.Android.minSdkVersion != ReleaseSettings.MinSdk)
                problems.Add($"minSdk is {(int)PlayerSettings.Android.minSdkVersion}, expected {ReleaseSettings.MinSdk}.");

            if ((int)PlayerSettings.Android.targetSdkVersion != ReleaseSettings.TargetSdk)
                problems.Add($"targetSdk is {(int)PlayerSettings.Android.targetSdkVersion}, expected {ReleaseSettings.TargetSdk}.");

            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
                problems.Add("ARM64 is not in the target architectures; the store requires a 64-bit library.");

            if (PlayerSettings.GetScriptingBackend(target) != ScriptingImplementation.IL2CPP)
                problems.Add("Scripting backend is not IL2CPP.");

            problems.AddRange(VerifyOrientation());

            if (!EditorUserBuildSettings.buildAppBundle)
                problems.Add("Output format is APK; the store needs an app bundle.");

            if (EditorUserBuildSettings.development)
                problems.Add("Development build is on.");

            if (EditorUserBuildSettings.allowDebugging)
                problems.Add("Script debugging is on.");

            if (EditorUserBuildSettings.connectProfiler)
                problems.Add("Autoconnect profiler is on.");

            if (PlayerSettings.Android.forceInternetPermission != ReleaseSettings.RequiresInternet)
                problems.Add($"forceInternetPermission is {PlayerSettings.Android.forceInternetPermission}, " +
                             $"expected {ReleaseSettings.RequiresInternet}.");

            // The trap this check exists for: deleting the manifest restores the
            // network permission with no warning anywhere.
            if (!File.Exists(ReleaseSettings.ManifestPath))
                problems.Add($"Custom manifest is missing at {ReleaseSettings.ManifestPath}; " +
                             "without it Unity adds the network permission back.");
            else
            {
                string manifest = File.ReadAllText(ReleaseSettings.ManifestPath);
                if (!ReleaseSettings.RequiresInternet && !manifest.Contains("permission.INTERNET"))
                    problems.Add("Custom manifest no longer removes the INTERNET permission.");

                // This file replaces unityLibrary's manifest, so Unity contributes no
                // activity of its own. Without one the app installs with no icon and
                // cannot be started, and every other check here still passes.
                if (!manifest.Contains("android.intent.category.LAUNCHER"))
                    problems.Add("Custom manifest declares no LAUNCHER activity; " +
                                 "the app would install with no icon and no way to start.");

                if (!manifest.Contains(ReleaseSettings.ActivityClass))
                    problems.Add($"Custom manifest does not name {ReleaseSettings.ActivityClass}, " +
                                 "which is the activity the GameActivity application entry needs.");
            }

            problems.AddRange(VerifyScenes());

            problems.AddRange(VerifyIcons(target));

            if (PlayerSettings.Android.bundleVersionCode <= 0)
                problems.Add("Bundle version code must be a positive integer.");

            if (PlayerSettings.bundleVersion != ReleaseSettings.VersionName)
                problems.Add($"Version name is '{PlayerSettings.bundleVersion}', " +
                             $"expected '{ReleaseSettings.VersionName}'.");

            return problems;
        }

        static IEnumerable<string> VerifyOrientation()
        {
            bool portrait = ReleaseSettings.Orientation == ReleaseOrientation.Portrait;
            bool landscape = ReleaseSettings.Orientation == ReleaseOrientation.Landscape;

            UIOrientation expected =
                ReleaseSettings.Orientation == ReleaseOrientation.Auto ? UIOrientation.AutoRotation
                : portrait ? UIOrientation.Portrait
                : UIOrientation.LandscapeLeft;

            if (PlayerSettings.defaultInterfaceOrientation != expected)
                yield return $"Default orientation is {PlayerSettings.defaultInterfaceOrientation}, " +
                             $"expected {expected} for {ReleaseSettings.Orientation}.";

            // A locked game that still autorotates ships a layout nobody designed.
            if (portrait && (PlayerSettings.allowedAutorotateToLandscapeLeft ||
                             PlayerSettings.allowedAutorotateToLandscapeRight))
                yield return "Orientation is Portrait but landscape autorotation is still allowed.";

            if (landscape && PlayerSettings.allowedAutorotateToPortrait)
                yield return "Orientation is Landscape but portrait autorotation is still allowed.";
        }

        static IEnumerable<string> VerifyScenes()
        {
            string[] expected = ReleaseSettings.ScenePaths;
            string[] actual = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            // Order matters: the first enabled scene is the one that loads.
            if (!actual.SequenceEqual(expected))
            {
                yield return $"Build list is [{string.Join(", ", actual)}], " +
                             $"expected [{string.Join(", ", expected)}].";
            }

            foreach (string path in expected)
            {
                if (!File.Exists(path))
                    yield return $"Scene missing at {path}.";
            }
        }

        static IEnumerable<string> VerifyIcons(UnityEditor.Build.NamedBuildTarget target)
        {
            PlatformIconKind kind = UnityEditor.Android.AndroidPlatformIconKind.Adaptive;
            PlatformIcon[] adaptive = PlayerSettings.GetPlatformIcons(target, kind);

            if (adaptive == null || adaptive.Length == 0)
                yield return "No adaptive icon slots were returned; the Android module may be missing.";
            else if (adaptive.Any(i => i.GetTextures().All(t => t == null)))
                yield return "An adaptive icon layer is empty; the build would ship Unity's default icon.";

            foreach (string path in new[]
                     {
                         ReleaseSettings.IconPath,
                         ReleaseSettings.AdaptiveForegroundPath,
                         ReleaseSettings.AdaptiveBackgroundPath
                     })
            {
                if (!File.Exists(path))
                    yield return $"Icon source missing at {path}.";
                else if (new FileInfo(path).Length == 0)
                    yield return $"Icon at {path} is an empty file.";
            }
        }

        [MenuItem("Hypnotic Rings/Release/Verify")]
        public static void VerifyMenu()
        {
            List<string> problems = Verify();
            if (problems.Count == 0)
            {
                Debug.Log($"{Tag} Verify passed: the project is releasable.");
                return;
            }

            Debug.LogError($"{Tag} Verify found {problems.Count} problem(s):\n  - " +
                           string.Join("\n  - ", problems));
        }

        // ---- build ------------------------------------------------------------

        [MenuItem("Hypnotic Rings/Release/Build Bundle")]
        public static void BuildMenu() => Build();

        public static bool Build()
        {
            List<string> problems = Verify();
            if (problems.Count > 0)
            {
                Debug.LogError($"{Tag} BUILD REFUSED — {problems.Count} preflight problem(s):\n  - " +
                               string.Join("\n  - ", problems));
                return false;
            }

            Directory.CreateDirectory(ReleaseSettings.OutputDirectory);

            bool signed = ApplySigningFromEnvironment(out string signingNote);
            string fileName = signed
                ? ReleaseSettings.BundleName
                : Path.GetFileNameWithoutExtension(ReleaseSettings.BundleName) + "-UNSIGNED.aab";
            string output = Path.Combine(ReleaseSettings.OutputDirectory, fileName);

            Debug.Log($"{Tag} Building {output} ({signingNote})");

            var options = new BuildPlayerOptions
            {
                scenes = ReleaseSettings.ScenePaths,
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                // Passwords never linger in the project settings file.
                WipeSigningFromProjectSettings();
            }

            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log(ok
                ? $"{Tag} Build succeeded: {output}"
                : $"{Tag} Build FAILED: {report.summary.result}, {report.summary.totalErrors} error(s)");

            // The size in a build report measures the intermediate project, not
            // the artifact. Read the file itself instead.
            if (ok && File.Exists(output))
                Debug.Log($"{Tag} Artifact on disk: {new FileInfo(output).Length} bytes");

            return ok;
        }

        /// <summary>
        /// Signing credentials come from the environment, never from a file. An
        /// unsigned build is still produced, but named so nobody can mistake it
        /// for a releasable one.
        /// </summary>
        static bool ApplySigningFromEnvironment(out string note)
        {
            string keystore = Environment.GetEnvironmentVariable(ReleaseSettings.EnvKeystore);
            string keystorePass = Environment.GetEnvironmentVariable(ReleaseSettings.EnvKeystorePass);
            string alias = Environment.GetEnvironmentVariable(ReleaseSettings.EnvKeyAlias);
            string aliasPass = Environment.GetEnvironmentVariable(ReleaseSettings.EnvKeyPass);

            if (string.IsNullOrEmpty(keystore) || string.IsNullOrEmpty(keystorePass) ||
                string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(aliasPass))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                note = $"unsigned — no {ReleaseSettings.EnvKeystore}* variables in the environment";
                return false;
            }

            if (!File.Exists(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                note = $"unsigned — keystore not found at {keystore}";
                return false;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = aliasPass;

            note = $"signed with alias '{alias}'";
            return true;
        }

        static void WipeSigningFromProjectSettings()
        {
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasPass = "";
            AssetDatabase.SaveAssets();
        }

        // ---- command line -------------------------------------------------------

        public static void CIApply()
        {
            Apply();
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }

        public static void CIVerify()
        {
            List<string> problems = Verify();
            foreach (string p in problems) Debug.LogError($"{Tag} PREFLIGHT: {p}");

            Debug.Log(problems.Count == 0
                ? $"{Tag} PREFLIGHT OK"
                : $"{Tag} PREFLIGHT FAILED with {problems.Count} problem(s)");

            EditorApplication.Exit(problems.Count == 0 ? 0 : 1);
        }

        public static void CIBuild()
        {
            bool ok = Build();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Every upload needs a higher code; bumped here, never by hand.</summary>
        public static void CIBumpVersionCode()
        {
            PlayerSettings.Android.bundleVersionCode++;
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Version code is now {PlayerSettings.Android.bundleVersionCode}");
            EditorApplication.Exit(0);
        }
    }
}
