// Play-mode regression: tracked XR UI pointer -> input module -> raycaster -> real buttons,
// real ray-based teleport requests, continuous movement, arm menu, and current-map restart.
using HomeTasks;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

class TrackedUiTestPointer : IUIInteractor
{
    public XRUIInputModule module;
    public Vector3 from;
    public Vector3 to;
    public bool pressed;
    public void UpdateUIModel(ref TrackedDeviceModel model)
    {
        model.position = from;
        model.orientation = Quaternion.LookRotation(to - from);
        model.raycastPoints = new List<Vector3> { from, to };
        model.raycastLayerMask = ~0;
        model.interactionType = UIInteractionType.Ray;
        model.select = pressed;
    }
    public bool TryGetUIModel(out TrackedDeviceModel model) => module.GetTrackedDeviceModel(this, out model);
}

var checks = new List<string>();
Directory.CreateDirectory("TestResults");
File.WriteAllText("TestResults/NewModelVRVerification.txt", string.Empty);
void Check(bool value, string name)
{
    if (!value) throw new Exception("FAILED: " + name);
    checks.Add("PASS: " + name);
    File.AppendAllText("TestResults/NewModelVRVerification.txt", checks.Last() + "\n");
}
async Task Frames(int count = 3)
{
    var frame = Time.frameCount;
    await Wait.Until(() => Time.frameCount >= frame + count, timeoutMs: 5000, pollMs: 16);
}

await Frames();
var router = Object.FindFirstObjectByType<PlayerModeRouter>();
Check(Application.isPlaying && router.gameObject.scene.path.EndsWith("NewModel.unity"), "NewModel is running");
Check(EventSystem.current != null && EventSystem.current.GetComponent<XRUIInputModule>() != null, "The scene has a tracked XR input module");
Check(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(e => e.enabled) == 1, "Exactly one active EventSystem handles UI");
Check(HomeSession.LastGameplayScenePath.EndsWith("NewModel.unity"), "Session remembers the custom map for restart");

router.SetVRMode(true);
var rig = router.questRig;
// A tracked headset is not required for these engine-path tests. Substitute stable poses.
foreach (var driver in rig.GetComponentsInChildren<TrackedPoseDriver>(true)) driver.enabled = false;
foreach (var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
    if (behaviour.GetType().Name == "XRInputModalityManager") behaviour.enabled = false;
var left = rig.GetComponentsInChildren<Transform>(true).First(t => t.name == "Left Controller");
var right = rig.GetComponentsInChildren<Transform>(true).First(t => t.name == "Right Controller");
left.gameObject.SetActive(true);
right.gameObject.SetActive(true);
left.localPosition = new Vector3(-0.22f, 1.15f, 0.50f);
right.localPosition = new Vector3(0.22f, 1.2f, 0.45f);
var origin = rig.GetComponent<Unity.XR.CoreUtils.XROrigin>();
var arm = rig.GetComponentInChildren<VRArmTaskBoard>(true);
var movement = rig.GetComponent<VRMovementOptions>();
var height = rig.GetComponent<VRHeightAdjustment>();
origin.Camera.transform.localPosition = Vector3.up * (1.65f - origin.CameraFloorOffsetObject.transform.localPosition.y + height.OffsetCentimeters * 0.01f);
const string heightPreference = "HomeFocus.HeightOffsetCm";
var hadHeightPreference = PlayerPrefs.HasKey(heightPreference);
var previousHeightPreference = PlayerPrefs.GetInt(heightPreference);
movement.rememberChoice = false;
await Frames();
Check(arm != null && arm.gameObject.activeInHierarchy && arm.content.activeInHierarchy, "Task board is active on the left arm in VR");
Check(arm.transform.IsChildOf(left) && !router.worldTaskBoard.activeSelf, "VR uses the arm board and hides the wall board");
Check(arm.GetComponent<HomeDashboard>().session == HomeSession.Instance, "Arm task progress is bound to this scene's session");
Check(!router.desktopRig.activeSelf && rig.activeSelf, "VR and desktop players do not run simultaneously");
Check(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.renderMode == RenderMode.WorldSpace).All(c => c.worldCamera == origin.Camera), "World UI uses the active VR camera");

var module = EventSystem.current.GetComponent<XRUIInputModule>();
var pointer = new TrackedUiTestPointer { module = module, from = origin.Camera.transform.position, to = origin.Camera.transform.position + Vector3.forward };
module.RegisterInteractor(pointer);
InputSystem.RegisterLayout(@"{""name"":""HomeFocusVRTestController"",""extend"":""XRController"",""controls"":[{""name"":""thumbstick"",""layout"":""Vector2"",""usages"":[""Primary2DAxis""]},{""name"":""secondaryButton"",""layout"":""Button"",""format"":""FLT"",""usages"":[""SecondaryButton""]}]}");
var leftDevice = InputSystem.AddDevice("HomeFocusVRTestController");
var rightDevice = InputSystem.AddDevice("HomeFocusVRTestController");
InputSystem.SetDeviceUsage(leftDevice, "LeftHand");
InputSystem.SetDeviceUsage(rightDevice, "RightHand");
var leftStick = (UnityEngine.InputSystem.Controls.Vector2Control)leftDevice["thumbstick"];
var rightStick = (UnityEngine.InputSystem.Controls.Vector2Control)rightDevice["thumbstick"];
var menuButton = (UnityEngine.InputSystem.Controls.ButtonControl)leftDevice["secondaryButton"];
Transform rayOrigin = null;
await Frames();
async Task Point(UnityEngine.UI.Button button)
{
    var rect = (RectTransform)button.transform;
    var center = rect.TransformPoint(rect.rect.center);
    Camera.main.transform.LookAt(center);
    pointer.from = center - button.transform.forward * 0.35f;
    pointer.to = center + button.transform.forward * 0.03f;
    pointer.pressed = false;
    Canvas.ForceUpdateCanvases();
    await Frames(4);
    Check(module.GetTrackedDeviceModel(pointer, out var model) && model.currentRaycast.gameObject != null && model.currentRaycast.gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button, "Tracked controller ray hits " + button.name);
}
async Task Click(UnityEngine.UI.Button button)
{
    await Point(button);
    pointer.pressed = true;
    await Frames(3);
    pointer.pressed = false;
    await Frames(3);
}

try
{
    var toggle = arm.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "Toggle controller walking");
    movement.SetWalkingEnabled(false);
    await Click(toggle);
    Check(movement.WalkingEnabled && movement.moveProvider.enabled && movement.leftController.smoothMotionEnabled, "A tracked trigger click enables controller walking through the real button");
    Check(!movement.rightController.smoothMotionEnabled && !movement.rightController.smoothTurnEnabled, "Right-hand teleport and snap-turn remain enabled while walking");
    await Click(toggle);
    Check(!movement.WalkingEnabled && !movement.moveProvider.enabled && !movement.leftController.smoothMotionEnabled, "A second tracked click returns to teleport mode");
    var heightButtons = arm.GetComponentsInChildren<UnityEngine.UI.Button>(true);
    var raise = heightButtons.Single(b => b.name == "Raise VR height");
    var lower = heightButtons.Single(b => b.name == "Lower VR height");
    var reset = heightButtons.Single(b => b.name == "Reset VR height");
    await Click(reset);
    var baselineEye = origin.CameraInOriginSpaceHeight;
    var baselineHand = rig.transform.InverseTransformPoint(right.position).y;
    await Click(raise);
    Check(height.OffsetCentimeters == 5 && Mathf.Abs(origin.CameraInOriginSpaceHeight - baselineEye - 0.05f) < 0.002f &&
        Mathf.Abs(rig.transform.InverseTransformPoint(right.position).y - baselineHand - 0.05f) < 0.002f,
        "Height + raises the view and controller together by 5 cm");
    await Click(lower);
    await Click(lower);
    Check(height.OffsetCentimeters == -5 && Mathf.Abs(origin.CameraInOriginSpaceHeight - baselineEye + 0.05f) < 0.002f, "Height - lowers the view below the baseline");
    await Click(reset);
    Check(height.OffsetCentimeters == 0 && Mathf.Abs(origin.CameraInOriginSpaceHeight - baselineEye) < 0.002f, "Pressing the height value resets the calibration");
    await Click(raise);
    InputSystem.QueueDeltaStateEvent(menuButton, 1f);
    await Frames(4);
    Check(!arm.content.activeInHierarchy, "Arm menu can be hidden without disabling its input component");
    InputSystem.QueueDeltaStateEvent(menuButton, 0f);
    await Frames();
    InputSystem.QueueDeltaStateEvent(menuButton, 1f);
    await Frames(4);
    InputSystem.QueueDeltaStateEvent(menuButton, 0f);
    await Frames();
    Check(arm.content.activeInHierarchy, "The actual left-controller secondary-button binding reopens the arm menu");
    var recover = arm.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "Recover loose items");
    var dish = HomeSession.Instance.dishes[0];
    var initialPosition = dish.transform.position;
    dish.transform.position += Vector3.up * 1.0f;
    await Click(recover);
    Check(Vector3.Distance(dish.transform.position, initialPosition) < 0.25f, "Tracked clicking Recover Items calls the real session action");

    var candidatePoints = new[] { new Vector3(0, 0, -20), new Vector3(1, 0, -24), new Vector3(-3, 0, -18) };
    RaycastHit destinationHit = default;
    var foundFloor = candidatePoints.Any(p => Physics.Raycast(p + Vector3.up * 3, Vector3.down, out destinationHit, 6, ~0, QueryTriggerInteraction.Ignore) && destinationHit.collider.GetComponent<TeleportationArea>() != null);
    Check(foundFloor, "An unobstructed outdoor walking surface exists in the new map");
    var plane = destinationHit.collider.GetComponent<TeleportationArea>();
    var ray = right.GetComponentsInChildren<XRRayInteractor>(true).Single(r => r.name.Contains("Teleport"));
    ray.lineType = XRRayInteractor.LineType.StraightLine;
    rayOrigin = new GameObject("Temporary test ray origin").transform;
    ray.rayOriginTransform = rayOrigin;
    var destination = destinationHit.point;
    rayOrigin.SetPositionAndRotation(destination + Vector3.up * 2, Quaternion.LookRotation(Vector3.down, Vector3.forward));
    var beforeTeleport = origin.Camera.transform.position;
    InputSystem.QueueDeltaStateEvent(rightStick, Vector2.up);
    await Frames(8);
    Check(ray.gameObject.activeInHierarchy, "Right thumbstick input activates the teleport interactor");
    Check(ray.TryGetCurrent3DRaycastHit(out var floorHit) && floorHit.collider.GetComponent<TeleportationArea>() == plane, "The real teleport ray hits the new map's outdoor floor");
    Check((ray.interactionLayers.value & plane.interactionLayers.value) != 0 && plane.colliders.Contains(floorHit.collider), "Teleport ray and assigned floor collider share the teleport interaction layer");
    Check(ray.interactablesSelected.Contains(plane), "Holding the right thumbstick selects the valid floor through XR Interaction Manager");
    InputSystem.QueueDeltaStateEvent(rightStick, Vector2.zero);
    await Frames(8);
    var ground = origin.Camera.transform.position;
    Check(Vector2.Distance(new Vector2(ground.x, ground.z), new Vector2(floorHit.point.x, floorHit.point.z)) < 0.25f && Vector3.Distance(beforeTeleport, ground) > 2, "Selecting and releasing the floor teleports the XR player through the provider and body transformer");
    Object.Destroy(rayOrigin.gameObject);
    movement.SetWalkingEnabled(true);
    InputSystem.QueueDeltaStateEvent(leftStick, new Vector2(0, 0.7f));
    var walkStart = rig.transform.position;
    var walkUntil = Time.time + 0.7f;
    await Wait.Until(() => Time.time >= walkUntil, timeoutMs: 5000);
    InputSystem.QueueDeltaStateEvent(leftStick, Vector2.zero);
    Check(Vector2.Distance(new Vector2(walkStart.x, walkStart.z), new Vector2(rig.transform.position.x, rig.transform.position.z)) > 0.3f, "Controller movement input translates the player on the new map");
    movement.SetWalkingEnabled(false);
    router.SetVRMode(false);
    await Frames();
    Check(router.worldTaskBoard.activeSelf && !rig.activeSelf && router.desktopRig.activeSelf, "Desktop mode retains the original wall board and disables the VR rig");
    Check(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.renderMode == RenderMode.WorldSpace).All(c => c.worldCamera == router.desktopRig.GetComponentInChildren<Camera>()), "Desktop screen buttons use the desktop camera");
    router.SetVRMode(true);
    await Frames();
    var previousSession = HomeSession.Instance;
    var restart = arm.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "Restart session");
    await Click(restart);
    await Wait.Until(() => HomeSession.Instance != null && HomeSession.Instance != previousSession && HomeSession.Instance.gameObject.scene.path.EndsWith("NewModel.unity"), timeoutMs: 15000);
    await Frames(6);
    Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path.EndsWith("NewModel.unity") && HomeSession.Instance.CompletedTasks == 0, "Restart returns to NewModel with a fresh session");
    var freshRouter = Object.FindFirstObjectByType<PlayerModeRouter>();
    freshRouter.SetVRMode(true);
    Check(freshRouter.questRig.GetComponent<VRHeightAdjustment>().OffsetCentimeters == 5, "The saved height adjustment is loaded in a fresh session");
}
finally
{
    if (module != null) module.UnregisterInteractor(pointer);
    InputSystem.RemoveDevice(leftDevice);
    InputSystem.RemoveDevice(rightDevice);
    InputSystem.RemoveLayout("HomeFocusVRTestController");
    if (rayOrigin != null) Object.Destroy(rayOrigin.gameObject);
    if (hadHeightPreference) PlayerPrefs.SetInt(heightPreference, previousHeightPreference);
    else PlayerPrefs.DeleteKey(heightPreference);
    PlayerPrefs.Save();
}
return checks.ToArray();
