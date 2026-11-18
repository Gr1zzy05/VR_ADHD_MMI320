// Exercise chair interaction and interrupted meetings through the desktop and XR input paths.
using HomeTasks;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

class MeetingUiTestPointer : IUIInteractor
{
    public XRUIInputModule module;
    public Vector3 from, to;
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
File.WriteAllText("TestResults/NewModelMeetingUIVerification.txt", string.Empty);
void Check(bool value, string name)
{
    if (!value) throw new Exception("FAILED: " + name);
    checks.Add("PASS: " + name);
    File.AppendAllText("TestResults/NewModelMeetingUIVerification.txt", checks.Last() + "\n");
}
async Task Frames(int count = 4)
{
    var start = Time.frameCount;
    await Wait.Until(() => Time.frameCount >= start + count, timeoutMs: 5000, pollMs: 16);
}
void SimulateHeadset(PlayerModeRouter router)
{
    router.SetVRMode(true);
    foreach (var driver in router.questRig.GetComponentsInChildren<TrackedPoseDriver>(true)) driver.enabled = false;
    foreach (var behaviour in router.questRig.GetComponentsInChildren<MonoBehaviour>(true))
        if (behaviour.GetType().Name == "XRInputModalityManager") behaviour.enabled = false;
    var origin = router.questRig.GetComponent<XROrigin>();
    var height = router.questRig.GetComponent<VRHeightAdjustment>();
    var offset = height != null ? height.OffsetCentimeters * 0.01f : 0;
    origin.Camera.transform.localPosition = Vector3.up * (1.65f + offset - origin.CameraFloorOffsetObject.transform.localPosition.y);
    var arm = router.questRig.GetComponentInChildren<VRArmTaskBoard>(true);
    if (arm != null) arm.SetOpen(false);
}
async Task Click(MeetingUiTestPointer pointer, UnityEngine.UI.Button button)
{
    var rect = (RectTransform)button.transform;
    var center = rect.TransformPoint(rect.rect.center);
    Camera.main.transform.LookAt(center);
    pointer.from = center - button.transform.forward * 0.35f + Vector3.up * 0.5f;
    pointer.to = pointer.from + button.transform.forward * 0.38f;
    pointer.pressed = false;
    await Frames(2);
    pointer.from = center - button.transform.forward * 0.35f;
    pointer.to = center + button.transform.forward * 0.03f;
    Canvas.ForceUpdateCanvases();
    await Frames();
    Check(pointer.module.GetTrackedDeviceModel(pointer, out var model) && model.currentRaycast.gameObject != null && model.currentRaycast.gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button, "Tracked pointer hits " + button.name);
    pointer.pressed = true;
    await Frames();
    pointer.pressed = false;
    await Frames();
}

void FinishChores(HomeSession session)
{
    // Use the same task APIs as normal play to unlock the chair.
    var wash = Object.FindFirstObjectByType<WashStation>();
    wash.ToggleWater();
    wash.sponge.SetDesktopHeld(true);
    foreach (var item in session.dishes)
    {
        item.SetDesktopHeld(true);
        item.transform.position = wash.washVolume.bounds.center;
        Physics.SyncTransforms();
        wash.sponge.transform.position = item.Shape.bounds.center;
        Physics.SyncTransforms();
        Check(wash.Scrub(item, session.settings.scrubSecondsPerDish + 0.1f), "Sink contact prepares " + item.name);
        item.SetDesktopHeld(false);
    }
    wash.sponge.SetDesktopHeld(false);
    wash.sponge.ReturnHome();
    var racks = Object.FindObjectsByType<DishRack>(FindObjectsSortMode.None);
    foreach (var item in session.dishes)
    {
        var rack = racks.Single(r => r.accepts == item.kind);
        item.transform.position = rack.volume.bounds.center;
        Physics.SyncTransforms();
        Check(rack.TryStore(item), "Matching shelf accepts " + item.name);
    }
    var bin = Object.FindFirstObjectByType<TrashBin>();
    foreach (var bag in session.rubbish)
    {
        bag.transform.position = bin.volume.bounds.center;
        Physics.SyncTransforms();
        Check(bin.TryDeposit(bag), "Outdoor bin accepts a released bag");
    }
}

await Frames();
var session = HomeSession.Instance;
var router = Object.FindFirstObjectByType<PlayerModeRouter>();
Check(session != null && session.CurrentQuest == HomeQuest.Washing && session.gameObject.scene.path.EndsWith("NewModel.unity"), "The UI test starts in a fresh NewModel session");
router.SetVRMode(false);
var desktop = router.desktopRig.GetComponent<DesktopPlayer>();
var meeting = session.meeting;
var panel = meeting.GetComponent<MeetingPanel>();
var seat = meeting.seat;
var chairAction = seat.chairCollider.GetComponent<HomeAction>();
var voice = meeting.GetComponent<AudioSource>();
bool ResetAfterLeaving() => meeting.State == MeetingState.Ready && !meeting.Attended &&
    meeting.ListeningProgress == 0 && meeting.QuestionIndex == 0 && meeting.CorrectAnswers == 0 &&
    !voice.isPlaying && !seat.IsPlayerInSeat;
chairAction.Use();
Check(!panel.sit.gameObject.activeSelf && !seat.IsPlayerInSeat && meeting.State == MeetingState.Ready,
    "The chair is locked until quests 1–3 are done");
var wallRecover = router.worldTaskBoard.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "Recover loose items");
var recoverRect = (RectTransform)wallRecover.transform;
var recoverCenter = recoverRect.TransformPoint(recoverRect.rect.center);
Cursor.lockState = CursorLockMode.None;
Cursor.visible = true;
var eyeOffset = desktop.view.transform.position - router.desktopRig.transform.position;
router.desktopRig.transform.position = recoverCenter - wallRecover.transform.forward * 2 - eyeOffset;
desktop.view.transform.LookAt(recoverCenter);
Canvas.ForceUpdateCanvases();
var screenPoint = (Vector2)desktop.view.WorldToScreenPoint(recoverCenter);
InputSystem.RegisterLayout(@"{""name"":""HomeFocusUITestMouse"",""extend"":""Mouse"",""canRunInBackground"":true}");
InputSystem.RegisterLayout(@"{""name"":""HomeFocusSeatTestKeyboard"",""extend"":""Keyboard"",""canRunInBackground"":true}");
var mouse = (Mouse)InputSystem.AddDevice("HomeFocusUITestMouse");
var keyboard = (Keyboard)InputSystem.AddDevice("HomeFocusSeatTestKeyboard");
try
{
    InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPoint });
    await Frames(6);
    var hitData = new PointerEventData(EventSystem.current) { position = screenPoint };
    var hits = new List<RaycastResult>();
    EventSystem.current.RaycastAll(hitData, hits);
    Check(hits.Any(h => h.gameObject.GetComponentInParent<UnityEngine.UI.Button>() == wallRecover), "The desktop pointer is on the front face of the wall-board button");
    InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPoint }.WithButton(MouseButton.Left, true));
    await Frames(4);
    InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPoint });
    await Frames(6);
    Check(session.Toast.StartsWith("Loose items returned"), "A mouse click reaches the wall-board Recover Items button through the input module");

    FinishChores(session);
    await Wait.Until(() => panel.sit.gameObject.activeInHierarchy, timeoutMs: 5000);
    Check(session.CurrentQuest == HomeQuest.Meeting && panel.begin == null, "Completing the chores unlocks the chair as the meeting entry point");
    meeting.Begin();
    Check(meeting.State == MeetingState.Ready && !voice.isPlaying, "The old meeting callback cannot skip the chair");

    desktop.transform.SetPositionAndRotation(seat.standPoint.position, seat.standPoint.rotation);
    var chairButton = (RectTransform)panel.sit.transform;
    var chairCenter = chairButton.TransformPoint(chairButton.rect.center);
    desktop.view.transform.LookAt(chairCenter);
    screenPoint = (Vector2)desktop.view.WorldToScreenPoint(chairCenter);
    InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPoint });
    await Frames(6);
    InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPoint }.WithButton(MouseButton.Left, true));
    await Frames(4);
    InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPoint });
    await Frames(6);
    Check(seat.IsPlayerInSeat && meeting.State == MeetingState.Listening &&
        Mathf.Abs(desktop.view.transform.localPosition.y - seat.seatedEyeHeight) < 0.01f,
        "Clicking the chair on desktop seats the player and starts the briefing");
    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
    await Wait.Until(() => meeting.State == MeetingState.Ready, timeoutMs: 5000);
    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
    Check(ResetAfterLeaving() && Mathf.Abs(desktop.view.transform.localPosition.y - eyeOffset.y) < 0.01f,
        "Walking away on desktop interrupts playback and restores standing height");
}
finally
{
    InputSystem.RemoveDevice(mouse);
    InputSystem.RemoveDevice(keyboard);
    InputSystem.RemoveLayout("HomeFocusUITestMouse");
    InputSystem.RemoveLayout("HomeFocusSeatTestKeyboard");
}

SimulateHeadset(router);
await Frames();
var origin = router.questRig.GetComponent<XROrigin>();
var height = router.questRig.GetComponent<VRHeightAdjustment>();
var standingEyeHeight = origin.CameraInOriginSpaceHeight;
var movement = router.questRig.GetComponent<VRMovementOptions>();
var left = movement.leftController.transform;
var right = movement.rightController.transform;
left.gameObject.SetActive(true);
right.gameObject.SetActive(true);
var module = EventSystem.current.GetComponent<XRUIInputModule>();
var pointer = new MeetingUiTestPointer { module = module, from = Camera.main.transform.position, to = Camera.main.transform.position + Vector3.forward };
module.RegisterInteractor(pointer);
InputSystem.RegisterLayout(@"{""name"":""HomeFocusSeatTestController"",""extend"":""XRController"",""controls"":[{""name"":""thumbstick"",""layout"":""Vector2"",""usages"":[""Primary2DAxis""]}]}");
var leftDevice = InputSystem.AddDevice("HomeFocusSeatTestController");
var rightDevice = InputSystem.AddDevice("HomeFocusSeatTestController");
InputSystem.SetDeviceUsage(leftDevice, "LeftHand");
InputSystem.SetDeviceUsage(rightDevice, "RightHand");
var leftStick = (UnityEngine.InputSystem.Controls.Vector2Control)leftDevice["thumbstick"];
var rightStick = (UnityEngine.InputSystem.Controls.Vector2Control)rightDevice["thumbstick"];
var originalContent = meeting.content;
var originalHeadPose = origin.Camera.transform.localPosition;
Transform teleportRayOrigin = null;
MeetingContent quickBriefing = null;

async Task SitInVR()
{
    var roomOffset = Vector3.ProjectOnPlane(origin.Camera.transform.position - origin.Origin.transform.position, Vector3.up);
    origin.Origin.transform.position = seat.standPoint.position - roomOffset;
    await Wait.Until(() => panel.sit.gameObject.activeInHierarchy, timeoutMs: 5000);
    await Click(pointer, panel.sit);
    Check(seat.IsPlayerInSeat && meeting.State == MeetingState.Listening, "The tracked chair button seats the player and starts listening");
}

try
{
    movement.SetWalkingEnabled(false);
    await SitInVR();
    Check(Mathf.Abs(origin.CameraInOriginSpaceHeight - seat.seatedEyeHeight - height.OffsetCentimeters * 0.01f) < 0.01f &&
        Vector3.Dot(Vector3.ProjectOnPlane(origin.Camera.transform.forward, Vector3.up).normalized, seat.transform.forward) > 0.99f,
        "VR seating aligns the tracked player toward the screen at the chair height");
    await Wait.Until(() => panel.leaveSeat.gameObject.activeInHierarchy && voice.isPlaying, timeoutMs: 5000);
    await Click(pointer, panel.leaveSeat);
    Check(ResetAfterLeaving() && Vector3.Distance(Vector3.ProjectOnPlane(origin.Camera.transform.position - seat.standPoint.position, Vector3.up), Vector3.zero) < 0.08f &&
        Mathf.Abs(origin.CameraInOriginSpaceHeight - standingEyeHeight) < 0.01f,
        "The Leave Chair button returns VR players to the floor and stops the audio");

    await SitInVR();
    movement.SetWalkingEnabled(true);
    InputSystem.QueueDeltaStateEvent(leftStick, Vector2.down);
    await Wait.Until(() => meeting.State == MeetingState.Ready, timeoutMs: 6000);
    InputSystem.QueueDeltaStateEvent(leftStick, Vector2.zero);
    Check(ResetAfterLeaving(), "Left-stick walking out of the seat area stops and resets the meeting");
    movement.SetWalkingEnabled(false);

    await SitInVR();
    var ray = right.GetComponentsInChildren<XRRayInteractor>(true).Single(r => r.name.Contains("Teleport"));
    ray.lineType = XRRayInteractor.LineType.StraightLine;
    teleportRayOrigin = new GameObject("Seat test teleport ray").transform;
    teleportRayOrigin.SetPositionAndRotation(seat.standPoint.position + Vector3.up * 1.5f, Quaternion.LookRotation(Vector3.down, Vector3.forward));
    ray.rayOriginTransform = teleportRayOrigin;
    InputSystem.QueueDeltaStateEvent(rightStick, Vector2.up);
    await Frames(8);
    Check(ray.gameObject.activeInHierarchy && ray.interactablesSelected.Any(i => i is TeleportationArea), "The seated player can aim and select the nearby floor for teleport");
    InputSystem.QueueDeltaStateEvent(rightStick, Vector2.zero);
    await Wait.Until(() => meeting.State == MeetingState.Ready, timeoutMs: 5000);
    Check(ResetAfterLeaving(), "Teleporting away stops and resets the meeting");

    quickBriefing = Object.Instantiate(originalContent);
    quickBriefing.segments = new[] { new MeetingSegment { transcript = "Short briefing for the interruption test.", minimumDuration = 1 } };
    meeting.content = quickBriefing;
    await SitInVR();
    await Wait.Until(() => meeting.State == MeetingState.Quiz, timeoutMs: 5000);
    var firstAnswer = meeting.CurrentQuestion.correctAnswer;
    await Click(pointer, panel.options[firstAnswer]);
    Check(meeting.QuestionIndex == 1 && meeting.CorrectAnswers == 1, "Submitting an answer while seated advances the quiz");
    origin.Camera.transform.localPosition += Vector3.right * (seat.seatRadius + 0.2f);
    await Frames(8);
    Check(ResetAfterLeaving() && panel.options.All(b => !b.gameObject.activeSelf), "A room-scale step away during the quiz discards its answers and locks the questions");
    origin.Camera.transform.localPosition = originalHeadPose;
    meeting.content = originalContent;

    await SitInVR();
    Check(meeting.CurrentCaption == originalContent.segments[0].speaker + ": " + originalContent.segments[0].transcript && voice.isPlaying,
        "Returning to the chair restarts the spoken briefing at the first segment");
    meeting.Answer(0);
    Check(meeting.QuestionIndex == 0 && !meeting.Attended, "Quiz answers remain blocked during narration");
    await Wait.Until(() => meeting.State == MeetingState.Quiz, timeoutMs: 100000);
    Check(meeting.Attended && seat.IsPlayerInSeat, "The quiz unlocks after the complete spoken briefing while seated");
    for (var index = 0; index < originalContent.questions.Length; index++)
    {
        var expected = index;
        await Wait.Until(() => panel.progress.text.StartsWith("QUESTION " + (expected + 1)), timeoutMs: 5000);
        var answer = session.meeting.CurrentQuestion.correctAnswer;
        await Click(pointer, panel.options[answer]);
    }
    await Wait.Until(() => SceneManager.GetActiveScene().path == HomeSession.ResultsScenePath, timeoutMs: 15000);
    await Frames();
    Check(HomeSession.LastResult != null && HomeSession.LastResult.CorrectAnswers == 3, "Tracked quiz clicks produce the correct results");
    Check(HomeSession.LastGameplayScenePath.EndsWith("NewModel.unity"), "The results screen remembers NewModel as the gameplay map");
    router = Object.FindFirstObjectByType<PlayerModeRouter>();
    SimulateHeadset(router);
    await Frames();
    module = EventSystem.current.GetComponent<XRUIInputModule>();
    Check(module != null, "Results has its own working XR UI input module");
    pointer = new MeetingUiTestPointer { module = module, from = Camera.main.transform.position, to = Camera.main.transform.position + Vector3.forward };
    module.RegisterInteractor(pointer);
    var results = Object.FindObjectsByType<HomeResultsPanel>(FindObjectsSortMode.None).Single(p => p.gameObject.GetComponentInParent<Canvas>().renderMode == RenderMode.WorldSpace);
    await Wait.Until(() => results.restart.interactable, timeoutMs: 5000);
    await Click(pointer, results.restart);
    await Wait.Until(() => HomeSession.Instance != null && HomeSession.Instance.gameObject.scene.path.EndsWith("NewModel.unity"), timeoutMs: 15000);
    Check(HomeSession.Instance.CurrentQuest == HomeQuest.Washing && HomeSession.LastResult == null, "The results-screen button starts a fresh session in NewModel");
}
finally
{
    if (module != null) module.UnregisterInteractor(pointer);
    if (meeting != null) meeting.content = originalContent;
    if (quickBriefing != null) Object.Destroy(quickBriefing);
    if (teleportRayOrigin != null) Object.Destroy(teleportRayOrigin.gameObject);
    InputSystem.RemoveDevice(leftDevice);
    InputSystem.RemoveDevice(rightDevice);
    InputSystem.RemoveLayout("HomeFocusSeatTestController");
}
return checks.ToArray();
