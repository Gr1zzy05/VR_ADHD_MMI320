// Run in Edit mode after opening HomeFocus.unity.
using HomeTasks;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using UnityEditor.XR.Management;
using System.IO;

var checks = new List<string>();
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    checks.Add("PASS: " + name);
}
Check(!Application.isPlaying, "Validation runs in Edit mode");
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
Check(scene.path == HomeTasks.Editor.HomeSceneBuilder.ScenePath, "HomeFocus is the active saved scene");
var roots = scene.GetRootGameObjects();
var transforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
Check(transforms.All(t => UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "No missing scripts in the saved scene");
var s = Object.FindFirstObjectByType<HomeSession>();
Check(s != null && s.settings != null && s.meeting != null, "Session, task settings, and meeting references are assigned");
Check(s.dishes.Count == 6 && s.dishes.All(d => d != null), "All six dish references are saved");
Check(s.rubbish.Count == 3 && s.rubbish.All(t => t != null), "All three trash references are saved");
Check(Enum.GetValues(typeof(DishKind)).Cast<DishKind>().All(k => s.dishes.Count(d => d.kind == k) == 2), "Two dishes of each type are supplied");
Check(Object.FindObjectsByType<DishRack>(FindObjectsSortMode.None).All(r => r.slots.Length == 2 && r.slots.All(slot => slot != null) && r.volume.isTrigger), "All three racks have two saved slots and trigger volumes");
Check(s.meeting.content.segments.Length == 4 && s.meeting.content.segments.All(p => p.voice != null && !string.IsNullOrWhiteSpace(p.transcript)), "All meeting clips and captions are present");
Check(s.meeting.content.questions.Length == 3 && s.meeting.content.questions.All(q => q.answers.Length == 3 && q.correctAnswer >= 0 && q.correctAnswer < 3), "The listening quiz contains three valid questions");
Check(Object.FindObjectsByType<HomeAction>(FindObjectsSortMode.None).All(a => a.onUse.GetPersistentEventCount() > 0), "Tap and meeting keyboard actions have saved callbacks");
Check(Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(b => b.onClick.GetPersistentEventCount() > 0), "All interactive screen buttons have saved callbacks");
Check(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 1 && Object.FindFirstObjectByType<XRUIInputModule>() != null, "One EventSystem supports XR and desktop UI");
var router = Object.FindFirstObjectByType<PlayerModeRouter>();
Check(router != null && router.questRig != null && router.desktopRig != null, "Both player modes are assigned");
Check(router.questRig.GetComponent<InputActionManager>().actionAssets.Count > 0, "The Quest rig has an input actions asset");
var controllerInputs = router.questRig.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b.GetType().Name == "ControllerInputActionManager").ToArray();
Check(controllerInputs.Length == 2 && controllerInputs.All(b => !new SerializedObject(b).FindProperty("m_SmoothMotionEnabled").boolValue && !new SerializedObject(b).FindProperty("m_SmoothTurnEnabled").boolValue), "Both controllers use teleport and snap-turn, with continuous movement disabled");
Check(router.questRig.GetComponentInChildren<VRTaskDisplay>(true) != null, "VR wrist progress and feedback are wired");
Check(Object.FindObjectsByType<TeleportationArea>(FindObjectsSortMode.None).Length >= 3 && Object.FindObjectsByType<TeleportationArea>(FindObjectsSortMode.None).All(a => a.interactionLayers.value == unchecked((int)0x80000000)), "House, garden, and patio floors use the rig's teleport layer");
var xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
Check(xr != null && xr.InitManagerOnStart && xr.Manager.activeLoaders.Any(l => l is UnityEngine.XR.OpenXR.OpenXRLoader), "Android initializes the saved OpenXR loader on startup");
var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
Check(openXR.GetFeature<MetaQuestFeature>().enabled && openXR.GetFeature<OculusTouchControllerProfile>().enabled, "Meta Quest and Touch controller OpenXR features are enabled");
Check(PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64 && PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP, "Android is ARM64 with IL2CPP");
Check(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Contains(UnityEngine.Rendering.GraphicsDeviceType.Vulkan), "Android uses Vulkan graphics");
var test = new GameObject("Temporary rotated-volume check");
try
{
    test.transform.SetPositionAndRotation(new Vector3(13, 2, -7), Quaternion.Euler(0, 73, 0));
    test.transform.localScale = new Vector3(2, 1.3f, 0.5f);
    var volume = test.AddComponent<BoxCollider>();
    volume.center = new Vector3(0.2f, 0.4f, 0);
    volume.size = new Vector3(1, 1, 1);
    Check(TaskVolume.Contains(volume, test.transform.TransformPoint(volume.center + Vector3.one * 0.2f)), "Station volumes accept interior points after rotation and non-uniform scaling");
    Check(!TaskVolume.Contains(volume, test.transform.TransformPoint(volume.center + Vector3.forward * 0.8f)), "Station volumes reject exterior points after rotation and scaling");
}
finally { Object.DestroyImmediate(test); }
Directory.CreateDirectory("TestResults");
File.WriteAllLines("TestResults/SceneValidation.txt", checks);
return checks.ToArray();
