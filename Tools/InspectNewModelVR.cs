using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var nodes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
var report = new Dictionary<string, object>();
report["scene"] = scene.path;
report["canvases"] = nodes.Select(t => t.GetComponent<Canvas>()).Where(c => c != null).Select(c => new Dictionary<string, object>
{
    ["path"] = PathOf(c.transform), ["active"] = c.gameObject.activeInHierarchy, ["mode"] = c.renderMode.ToString(),
    ["camera"] = c.worldCamera != null ? PathOf(c.worldCamera.transform) : "none", ["position"] = c.transform.position.ToString("F3"),
    ["scale"] = c.transform.lossyScale.ToString("F5"), ["size"] = ((RectTransform)c.transform).rect.size.ToString(),
    ["raycasters"] = c.GetComponents<BaseRaycaster>().Select(r => r.GetType().Name + ":" + r.enabled).ToArray(),
    ["groups"] = c.GetComponentsInParent<CanvasGroup>(true).Select(g => $"{g.name}:interactable={g.interactable},blocks={g.blocksRaycasts},alpha={g.alpha}").ToArray()
}).ToArray();
report["buttons"] = nodes.Select(t => t.GetComponent<UnityEngine.UI.Button>()).Where(b => b != null).Select(b => new Dictionary<string, object>
{
    ["path"] = PathOf(b.transform), ["active"] = b.gameObject.activeInHierarchy, ["interactable"] = b.IsInteractable(),
    ["graphic"] = b.targetGraphic != null ? b.targetGraphic.raycastTarget : false,
    ["calls"] = Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Select(i => $"{b.onClick.GetPersistentTarget(i)?.name}.{b.onClick.GetPersistentMethodName(i)}").ToArray()
}).ToArray();
report["teleportAreas"] = nodes.Select(t => t.GetComponent<TeleportationArea>()).Where(a => a != null).Select(a => new Dictionary<string, object>
{
    ["path"] = PathOf(a.transform), ["enabled"] = a.enabled, ["active"] = a.gameObject.activeInHierarchy,
    ["mask"] = a.interactionLayers.value, ["colliders"] = a.colliders.Select(c => c != null ? c.name + ":" + c.enabled : "null").ToArray(),
    ["provider"] = a.teleportationProvider != null ? PathOf(a.teleportationProvider.transform) : "none"
}).ToArray();
report["colliders"] = nodes.Select(t => t.GetComponent<Collider>()).Where(c => c != null && !c.isTrigger && c.bounds.size.x > 2 && c.bounds.size.z > 2).Select(c => $"{PathOf(c.transform)} | {c.GetType().Name} | {c.bounds.center:F2} | {c.bounds.size:F2}").ToArray();
var interesting = new[] { "XRUIInputModule", "InputSystemUIInputModule", "StandaloneInputModule", "EventSystem", "NearFarInteractor", "XRRayInteractor", "ControllerInputActionManager", "InputActionManager", "TeleportationProvider", "LocomotionMediator", "XRBodyTransformer", "DynamicMoveProvider", "SnapTurnProvider", "XROrigin", "PlayerModeRouter", "TrackedDeviceGraphicRaycaster", "SceneFade" };
var components = nodes.SelectMany(t => t.GetComponents<MonoBehaviour>()).Where(b => b != null && interesting.Contains(b.GetType().Name)).ToArray();
report["components"] = components.Select(b => new Dictionary<string,object> { ["path"] = PathOf(b.transform), ["type"] = b.GetType().Name, ["enabled"] = b.enabled, ["active"] = b.gameObject.activeInHierarchy, ["data"] = EditorJsonUtility.ToJson(b) }).ToArray();
Directory.CreateDirectory("TestResults");
File.WriteAllText("TestResults/NewModelVR-Before.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
return new Dictionary<string,object> { ["scene"] = scene.path, ["canvases"] = report["canvases"], ["buttons"] = report["buttons"], ["teleportAreas"] = report["teleportAreas"], ["floorColliders"] = report["colliders"], ["rigComponents"] = components.Where(b => b.GetType().Name != "TrackedDeviceGraphicRaycaster").Select(b => $"{PathOf(b.transform)} | {b.GetType().Name} | enabled={b.enabled} | active={b.gameObject.activeInHierarchy}").ToArray() };
