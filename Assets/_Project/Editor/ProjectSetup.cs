using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HR.Editor
{
    public static class ProjectSetup
    {
        public const string CompanyName = "Gor903";
        public const string ProductName = "Hypnotic Rings";
        public const string ApplicationId = "com.gor903.hypnoticrings";
        public const string VersionName = "1.0.0";

        public const int MinSdk = 26;
        public const int TargetSdk = 36;

        public const float Gravity = -14f;
        public const int PhysicsHz = 90;

        public const float BounceThreshold = 0.01f;

        const string RenderingDir = "Assets/_Project/Config/Rendering";
        const string RendererPath = RenderingDir + "/UniversalRenderer.asset";
        const string PipelinePath = RenderingDir + "/URP-HypnoticRings.asset";

        public const int InputHandlerPackage = 1;

        const string Physics2DAsset = "ProjectSettings/Physics2DSettings.asset";
        const string TimeAsset = "ProjectSettings/TimeManager.asset";
        const string PlayerSettingsAsset = "ProjectSettings/ProjectSettings.asset";

        [MenuItem("Hypnotic Rings/Setup/Apply project settings")]
        public static void ApplyAll()
        {
            ApplyRenderPipeline();
            ApplyPlayerSettings();
            ApplyStripping();
            ApplyPhysicsAndTime();
            AssetDatabase.SaveAssets();
            Debug.Log("[HR] Project settings applied.");
        }

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

            int handler = ActiveInputHandler();
            Expect(handler == InputHandlerPackage,
                   $"activeInputHandler is {handler}; the game's input is written against the package");

            Expect(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset,
                   "default render pipeline is not URP");

            Expect(Mathf.Approximately(Physics2D.gravity.y, Gravity), $"gravity is {Physics2D.gravity.y}");

            Expect(Mathf.Abs(Physics2D.bounceThreshold - BounceThreshold) < 1e-6f,
                   $"bounce threshold is {Physics2D.bounceThreshold}, expected {BounceThreshold}");

            float expected = 1f / PhysicsHz;
            Expect(Mathf.Abs(Time.fixedDeltaTime - expected) < 1e-6f,
                   $"fixed timestep is {Time.fixedDeltaTime:F6}, expected {expected:F6}");

            var scenes = EditorBuildSettings.scenes;
            Expect(scenes.Length == 2, $"build list holds {scenes.Length} scene(s)");
            if (scenes.Length == 2)
            {
                Expect(scenes[0].path == SceneBuilder.BootScene && scenes[0].enabled, "Boot is not the first enabled scene");
                Expect(scenes[1].path == SceneBuilder.MainScene && scenes[1].enabled, "Main is not the second enabled scene");
            }

            Expect(PlayerSettings.stripEngineCode, "engine code stripping is off");
            Expect(PlayerSettings.stripUnusedMeshComponents, "unused mesh components are kept");
            Expect(PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android)
                       == ManagedStrippingLevel.High,
                   $"managed stripping is {PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android)}");
            Expect(PlayerSettings.GetIl2CppCompilerConfiguration(NamedBuildTarget.Android)
                       == Il2CppCompilerConfiguration.Master,
                   "IL2CPP is not building in Master configuration");

            foreach (string directory in Directory.GetDirectories("Assets", "Resources",
                                                                  SearchOption.AllDirectories))
            {
                foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".meta")) continue;

                    string relative = file.Substring(directory.Length)
                                          .TrimStart('/', '\\')
                                          .Replace('\\', '/');

                    Expect(ShippedFromResources.Contains(relative),
                           $"{directory} holds {relative}; everything there ships");
                }
            }

            return problems;
        }

        static readonly HashSet<string> ShippedFromResources = new HashSet<string>
        {
            "Fonts/Unbounded-Bold.ttf",
            "Fonts/Manrope-Medium.ttf",
            "Fonts/OFL-Unbounded.txt",
            "Fonts/OFL-Manrope.txt",
            "GoogleMobileAdsSettings.asset",
        };

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

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

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

            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.startInFullscreen = true;

            EditProjectSetting(PlayerSettingsAsset, so =>
                so.FindProperty("activeInputHandler").intValue = InputHandlerPackage);
        }

        public static void ApplyStripping()
        {
            var android = NamedBuildTarget.Android;

            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.High);
            PlayerSettings.SetIl2CppCompilerConfiguration(android, Il2CppCompilerConfiguration.Master);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.stripUnusedMeshComponents = true;
        }

        public static void ApplyPhysicsAndTime()
        {
            EditProjectSetting(Physics2DAsset, so =>
            {
                so.FindProperty("m_Gravity").vector2Value = new Vector2(0f, Gravity);

                var threshold = so.FindProperty("m_BounceThreshold")
                             ?? so.FindProperty("m_VelocityThreshold");
                if (threshold == null)
                    throw new InvalidOperationException("Physics2D settings carry no bounce threshold");
                threshold.floatValue = BounceThreshold;
            });

            EditProjectSetting(TimeAsset, so => SetFixedTimestep(so.FindProperty("Fixed Timestep")));
        }

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

            long divisor = den * PhysicsHz;
            if (ticksPerSecond % divisor != 0)
                throw new InvalidOperationException(
                    $"{PhysicsHz} Hz is not exactly representable at {ticksPerSecond}/{den} ticks per second");

            count.longValue = ticksPerSecond / divisor;
        }

        static int ActiveInputHandler()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(PlayerSettingsAsset);
            if (assets == null || assets.Length == 0 || assets[0] == null) return -1;
            var prop = new SerializedObject(assets[0]).FindProperty("activeInputHandler");
            return prop?.intValue ?? -1;
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
