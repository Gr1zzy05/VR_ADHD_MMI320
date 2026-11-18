# NewModel VR fixes — 18 November 2026

The custom environment and task placements were kept. Scene backup: `Backups/VRFix-20261118/NewModel.unity`.

## Changes

- Added the missing XR UI Input Module to NewModel's EventSystem, enabled tracked-controller and mouse input, and bound world-space canvases to the active player camera.
- Corrected the teleport interaction layer and explicitly assigned the floor colliders, interaction manager, and teleport provider. House, footpath, and outdoor plane support teleporting; vertical surfaces are rejected by their hit normal.
- Added optional head-relative controller walking at 1.5 m/s. The left stick walks in Walk + Teleport mode; the right stick keeps teleport and snap-turn in both modes. The movement choice is stored on the device.
- Added an interactive left-forearm task board with live quest progress, objective, timer, movement mode, Restart, and Recover Items. Y hides/reopens it. VR hides the wall board; desktop mode keeps it.
- Reconnected the wrist/session data and added missing-session safeguards to the HUD scripts.
- Restart and results now return to the map where the session began. The Quest build command uses the enabled scenes, so NewModel and HomeResults are included.
- Preserved ordered quests and the single meeting/quiz attempt. The meeting button remains locked until the first three quests are complete.

## Verification

**62 new regression checks passed**:

- `NewModelVRVerification.txt`: 29 checks, including tracked UI button dispatch, Y input, real thumbstick action bindings, ray selection and teleport on release, walking movement, camera/mode switching, and the arm-board Restart button.
- `NewModelMeetingUIVerification.txt`: 33 checks, including desktop mouse clicks, the locked/unlocked meeting button, full narration, all quiz answer buttons, results, and the results-screen restart to NewModel.

Both real Near-Far controller interactors were also confirmed registered with the XR UI module, with their UI trigger actions enabled. No new runtime errors were reported during these checks.

The input tests use simulated devices in the running Unity Editor. Physical headset comfort and controller feel still require a Quest playtest.

## Retest on Quest

Install the rebuilt `Builds/HomeFocus-Quest.apk`. Press Y to open the arm board. Point with the right controller and press its trigger. Use the Movement button to enable walking. In Teleport mode, push a stick forward, aim at a valid floor, and release. In Walk + Teleport mode, use the left stick to walk and the right stick to teleport/snap-turn.

The scene can be repaired again with **Home Focus > Repair VR controls in active scene** after duplicating the player/gameplay setup into another map.

## Quest build

The updated Android ARM64 APK was built successfully from NewModel and HomeResults, with zero build errors. The APK signature verifies successfully. Build details, package metadata, and signature verification are saved in `NewModelQuestBuildReport.json`, `NewModelQuestApkMetadata.txt`, and `NewModelQuestApkSignature.txt`.

Unity reported two non-blocking warnings about SSAO performance and crash-report debug symbols; their full messages are preserved in the build report.
