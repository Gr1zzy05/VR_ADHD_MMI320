using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace HomeTasks.Editor
{
    public static class QuestProjectSetup
    {
        [MenuItem("Home Focus/Configure Meta Quest")]
        public static void Configure()
        {
            Directory.CreateDirectory("Assets/XR/Settings");
            AssetDatabase.Refresh();
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget targets))
            {
                targets = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(targets, "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, targets, true);
            }
            // Use the package's own factory; its enclosing type is internal in OpenXR 1.19.
            var factory = Type.GetType("UnityEditor.XR.OpenXR.OpenXRPackageSettings, Unity.XR.OpenXR.Editor", true);
            factory.GetMethod("GetOrCreateInstance", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            foreach (var target in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                if (!targets.HasSettingsForBuildTarget(target)) targets.CreateDefaultSettingsForBuildTarget(target);
                if (!targets.HasManagerSettingsForBuildTarget(target)) targets.CreateDefaultManagerSettingsForBuildTarget(target);
                var general = targets.SettingsForBuildTarget(target);
                general.InitManagerOnStart = target == BuildTargetGroup.Android;
                if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", target))
                    throw new InvalidOperationException("Could not assign the OpenXR loader to " + target);
                var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(target);
                if (openXR == null) throw new InvalidOperationException("OpenXR settings were not created for " + target);
                openXR.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                var touch = openXR.GetFeature<OculusTouchControllerProfile>();
                if (touch == null) throw new InvalidOperationException("Oculus Touch interaction profile is missing.");
                touch.enabled = true;
                EditorUtility.SetDirty(touch);
                if (target == BuildTargetGroup.Android)
                {
                    openXR.latencyOptimization = OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;
                    var quest = openXR.GetFeature<MetaQuestFeature>();
                    if (quest == null) throw new InvalidOperationException("Meta Quest support feature is missing.");
                    quest.enabled = true;
                    quest.AddTargetDevice("quest", "Quest", false);
                    quest.AddTargetDevice("quest2", "Quest 2", true);
                    quest.AddTargetDevice("cambria", "Quest Pro", true);
                    quest.AddTargetDevice("eureka", "Quest 3", true);
                    quest.AddTargetDevice("quest3s", "Quest 3S", true);
                    // AddTargetDevice only initializes new entries; explicitly update existing selections too.
                    var devices = new SerializedObject(quest);
                    var list = devices.FindProperty("targetDevices");
                    for (var i = 0; i < list.arraySize; i++)
                    {
                        var device = list.GetArrayElementAtIndex(i);
                        device.FindPropertyRelative("enabled").boolValue = device.FindPropertyRelative("manifestName").stringValue != "quest";
                    }
                    devices.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(quest);
                }
                EditorUtility.SetDirty(openXR);
                EditorUtility.SetDirty(general);
                EditorUtility.SetDirty(general.Manager);
            }
            PlayerSettings.productName = "Home Focus";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.homefocus.vr");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            AssetDatabase.SaveAssets();
            Debug.Log("[Home Focus] Quest configured: ARM64 / IL2CPP / Vulkan / OpenXR + Touch. Desktop testing remains available.");
        }

        [MenuItem("Home Focus/Quest Link play mode/Enable")]
        public static void EnableQuestLink() => SetQuestLink(true);

        [MenuItem("Home Focus/Quest Link play mode/Disable (desktop testing)")]
        public static void DisableQuestLink() => SetQuestLink(false);

        static void SetQuestLink(bool enabled)
        {
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (settings == null) { Configure(); settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone); }
            settings.InitManagerOnStart = enabled;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Home Focus/Build Quest APK (Android must be active)")]
        public static void BuildQuest()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Switch the active Build Profile to Android / Meta Quest first.");
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(EditorBuildSettings.scenes, scene => scene.enabled), scene => scene.path)),
                locationPathName = "Builds/HomeFocus-Quest.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            File.WriteAllText("Builds/QuestBuildReport.txt", $"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nTime: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Quest build failed; inspect the Unity Console and Builds/QuestBuildReport.txt.");
            Debug.Log("[Home Focus] Quest APK ready: " + Path.GetFullPath("Builds/HomeFocus-Quest.apk"));
        }
    }
}
