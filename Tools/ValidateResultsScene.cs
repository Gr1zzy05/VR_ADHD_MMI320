// Run in Edit mode. Inspect the saved results scene without changing either scene.
using HomeTasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using System.IO;

var checks = new List<string>();
void Check(bool condition, string description)
{
    if (!condition) throw new Exception("FAILED: " + description);
    checks.Add("PASS: " + description);
}
Check(!Application.isPlaying, "Results validation runs in Edit mode");
var home = SceneManager.GetActiveScene();
var scene = EditorSceneManager.OpenScene(HomeSession.ResultsScenePath, OpenSceneMode.Additive);
try
{
    var roots = scene.GetRootGameObjects();
    var objects = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
    Check(objects.All(go => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) == 0), "The results scene has no missing scripts");
    var router = roots.SelectMany(r => r.GetComponentsInChildren<PlayerModeRouter>(true)).Single();
    Check(router.questRig != null && router.desktopRig != null, "Both results player modes are saved");
    Check(router.questRig.GetComponent<Unity.XR.CoreUtils.XROrigin>() != null, "Results include an OpenXR player rig");
    Check(!router.questRig.activeSelf && router.desktopRig.activeSelf, "Saved player defaults support desktop with automatic VR routing");
    var panels = roots.SelectMany(r => r.GetComponentsInChildren<HomeResultsPanel>(true)).ToArray();
    Check(panels.Length == 2, "Desktop and VR each have a results panel");
    Check(panels.All(p => p.title != null && p.duration != null && p.chores != null && p.score != null && p.feedback != null && p.restart != null), "All results labels and restart buttons are wired");
    Check(panels.All(p => p.restart.onClick.GetPersistentEventCount() == 1 && p.restart.onClick.GetPersistentTarget(0) == p), "Both restart buttons have their own saved callback");
    var vr = panels.Single(p => p.transform.IsChildOf(router.questRig.transform));
    Check(vr.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace && vr.GetComponent<TrackedDeviceGraphicRaycaster>() != null, "The VR results panel supports tracked controller UI");
    var desktop = panels.Single(p => p.transform.IsChildOf(router.desktopRig.transform));
    Check(desktop.GetComponentInParent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay && desktop.GetComponentInParent<UnityEngine.UI.GraphicRaycaster>() != null, "The desktop results panel supports mouse UI");
    Check(panels.All(p => p.GetComponent<RectTransform>().rect.width > 0 && p.GetComponent<RectTransform>().rect.height > 0), "Both results panels have visible dimensions");
    Check(roots.SelectMany(r => r.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)).Count() == 1 && roots.SelectMany(r => r.GetComponentsInChildren<XRUIInputModule>(true)).Count() == 1, "One EventSystem supports both results modes");
    Check(!objects.Any(go => go.GetComponent<HomeSession>() != null || go.GetComponent<DesktopPlayer>() != null), "Results contain no gameplay session or desktop movement controller");
    Check(router.questRig.GetComponentsInChildren<LocomotionProvider>(true).All(p => !p.enabled), "VR locomotion is disabled while viewing results");
    Check(router.questRig.GetComponentsInChildren<VRTaskDisplay>(true).All(d => d.session == null && !d.gameObject.activeSelf), "Copied wrist displays cannot refer back to the unloaded home");
    foreach (var component in objects.SelectMany(go => go.GetComponents<Component>()).Where(c => c != null))
    {
        var property = new SerializedObject(component).GetIterator();
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            var reference = property.objectReferenceValue;
            var target = reference is GameObject gameObject ? gameObject : reference is Component targetComponent ? targetComponent.gameObject : null;
            if (target != null && target.scene.IsValid() && target.scene != scene)
                throw new Exception("Cross-scene reference: " + component.name + "." + property.propertyPath);
        }
    }
    Check(true, "Saved results references stay within the results scene or asset files");
}
finally
{
    SceneManager.SetActiveScene(home);
    EditorSceneManager.CloseScene(scene, true);
}
Directory.CreateDirectory("TestResults");
File.WriteAllLines("TestResults/ResultsSceneValidation.txt", checks);
return checks.ToArray();
