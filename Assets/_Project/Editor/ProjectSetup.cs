using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HR.Editor
{
    /// <summary>
    /// Project settings as code. Nothing here is a checkbox someone has to remember ticking.
    /// Apply writes; Verify reads back and reports what did not take. Both exist because an
    /// assignment can fail silently — Unity 6 changed Fixed Timestep from a float to a rational,
    /// and setting floatValue on it logged a line nobody was reading and changed nothing.
    /// Release-specific settings arrive with ReleaseTool in phase 10.
    /// </summary>
    public static class ProjectSetup
    {
        public const string CompanyName = "Gor903";
        public const string ProductName = "Hypnotic Rings";
        public const string ApplicationId = "com.gor903.hypnoticrings";
        public const string VersionName = "1.0.0";

        public const int MinSdk = 26;
        public const int TargetSdk = 36;

        // CONTRACT.md §2.1 and §2.3. Expressed as a rate because the engine stores it as an
        // exact rational now, and 90 divides the tick rate without remainder.
        public const float Gravity = -14f;
        public const int PhysicsHz = 90;

        const string RenderingDir = "Assets/_Project/Config/Rendering";
        const string RendererPath = RenderingDir + "/Renderer2D.asset";
        const string PipelinePath = RenderingDir + "/URP-HypnoticRings.asset";

        const string Physics2DAsset = "ProjectSettings/Physics2DSettings.asset";
        const string TimeAsset = "ProjectSettings/TimeManager.asset";

        [MenuItem("Hypnotic Rings/Setup/Apply project settings")]
        public static void ApplyAll()
        {
            ApplyRenderPipeline();
            ApplyPlayerSettings();
            ApplyPhysicsAndTime();
            AssetDatabase.SaveAssets();
            Debug.Log("[HR] Project settings applied.");
        }

        /// <summary>Reads every applied value back. Empty list means the project is as declared.</summary>
        public static List<string> Verify()
        {
            var problems = new List<string>();
            void Expect(bool ok, string what) { if (!ok) problems.Add(what); }

            Expect(PlayerSettings.companyName == CompanyName, $"companyName is '{PlayerSettings.companyName}'");
            Expect(PlayerSettings.productName == ProductName, $"productName is '{PlayerSettings.productName}'");
            Expect(PlayerSettings.bundleVersion == VersionName, $"bundleVersion is '{PlayerSettings.bundleVersion}'");

            string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            Expect(id == ApplicationId, $"applicationIdentifier is '{id}'");

            Expect(PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait,
                   $"orientation is {PlayerSettings.defaultInterfaceOrientation}");
            Expect((int)PlayerSettings.Android.minSdkVersion == MinSdk,
                   $"minSdk is {(int)PlayerSettings.Android.minSdkVersion}");
            Expect((int)PlayerSettings.Android.targetSdkVersion == TargetSdk,
                   $"targetSdk is {(int)PlayerSettings.Android.targetSdkVersion}");
            Expect(PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64,
                   $"architectures are {PlayerSettings.Android.targetArchitectures}");
            Expect(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP,
                   "scripting backend is not IL2CPP");
            Expect(PlayerSettings.colorSpace == ColorSpace.Linear, $"colorSpace is {PlayerSettings.colorSpace}");

            Expect(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset,
                   "default render pipeline is not URP");

            Expect(Mathf.Approximately(Physics2D.gravity.y, Gravity), $"gravity is {Physics2D.gravity.y}");

            float expected = 1f / PhysicsHz;
            Expect(Mathf.Abs(Time.fixedDeltaTime - expected) < 1e-6f,
                   $"fixed timestep is {Time.fixedDeltaTime:F6}, expected {expected:F6}");

            var scenes = EditorBuildSettings.scenes;
            Expect(scenes.Length == 2, $"build list holds {scenes.Length} scene(s)");
            if (scenes.Length == 2)
            {
                // Order matters, not just membership: the first enabled scene is the one that loads.
                Expect(scenes[0].path == SceneBuilder.BootScene && scenes[0].enabled, "Boot is not the first enabled scene");
                Expect(scenes[1].path == SceneBuilder.MainScene && scenes[1].enabled, "Main is not the second enabled scene");
            }

            return problems;
        }

        [MenuItem("Hypnotic Rings/Setup/Verify project settings")]
        static void VerifyFromMenu()
        {
            var problems = Verify();
            if (problems.Count == 0) Debug.Log("[HR] Project settings verified.");
            else Debug.LogError("[HR] Project settings problems:\n  " + string.Join("\n  ", problems));
        }

        public static void ApplyRenderPipeline()
        {
            Directory.CreateDirectory(RenderingDir);

            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            // The pipeline has to be both the graphics default and the quality-level pipeline:
            // leaving the quality level empty silently falls back to Built-in on device.
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int restore = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(restore, false);
        }

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = VersionName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);

            // Portrait, locked. CONTRACT.md G-03 — every screenshot's aspect ratio depends on it.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.useAnimatedAutorotation = false;

            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinSdk;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetSdk;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            // URP expects linear; gamma would wash every theme palette out.
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.startInFullscreen = true;
        }

        public static void ApplyPhysicsAndTime()
        {
            // These live in ProjectSettings assets rather than behind a scripting API that
            // persists, so they are edited through SerializedObject and saved explicitly.
            EditProjectSetting(Physics2DAsset, so =>
                so.FindProperty("m_Gravity").vector2Value = new Vector2(0f, Gravity));

            EditProjectSetting(TimeAsset, so => SetFixedTimestep(so.FindProperty("Fixed Timestep")));
        }

        /// <summary>
        /// Unity 6 stores the timestep as an exact tick count against a tick rate; older versions
        /// stored a float. Both shapes are handled, and the tick rate is read from the asset
        /// rather than hardcoded, so a future rate change does not silently skew the physics.
        /// </summary>
        static void SetFixedTimestep(SerializedProperty prop)
        {
            if (prop == null) throw new InvalidOperationException("TimeManager has no 'Fixed Timestep'");

            if (prop.propertyType == SerializedPropertyType.Float)
            {
                prop.floatValue = 1f / PhysicsHz;
                return;
            }

            var count = prop.FindPropertyRelative("m_Count");
            var numerator = prop.FindPropertyRelative("m_Rate.m_Numerator");
            var denominator = prop.FindPropertyRelative("m_Rate.m_Denominator");
            if (count == null || numerator == null || denominator == null)
                throw new InvalidOperationException("Unexpected 'Fixed Timestep' layout: " + prop.propertyType);

            long ticksPerSecond = numerator.longValue;
            long den = denominator.longValue <= 0 ? 1 : denominator.longValue;

            // seconds = count * den / ticksPerSecond, so count = ticksPerSecond / (den * Hz).
            long divisor = den * PhysicsHz;
            if (ticksPerSecond % divisor != 0)
                throw new InvalidOperationException(
                    $"{PhysicsHz} Hz is not exactly representable at {ticksPerSecond}/{den} ticks per second");

            count.longValue = ticksPerSecond / divisor;
        }

        static void EditProjectSetting(string assetPath, Action<SerializedObject> edit)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (assets == null || assets.Length == 0 || assets[0] == null)
                throw new InvalidOperationException($"Could not open {assetPath}");

            var so = new SerializedObject(assets[0]);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
