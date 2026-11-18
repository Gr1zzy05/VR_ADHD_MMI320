# Home Focus

A single-player 3D VR home game for standalone Meta Quest. All four activities share one continuous map. The current custom map is **NewModel**; the original HomeFocus blockout is also preserved.

## Play in Unity

Open `Assets/HomeTasks/Scenes/NewModel.unity` with **Unity 6000.3.9f1** and press **Play**. With Quest Link initialization disabled, the scene automatically uses the desktop player. NewModel and HomeResults are the enabled build scenes. The original HomeFocus and SampleScene assets are preserved.

| Desktop control | Action |
| --- | --- |
| Click the Game view | Capture the mouse |
| WASD / mouse | Walk / look |
| Left click | Pick up or release an item |
| Q | Drop the held item |
| E | Open or close a door, use the tap, or sit in the meeting chair |
| Hold E while holding the sponge and aiming at a dish | Scrub a dish inside the running sink |
| Mouse wheel / R | Adjust held distance / rotate the held item |
| Tab | Toggle the free cursor to click screen buttons |
| Escape | Release the mouse |
| 1 / 2 / 3 near the meeting desk | Choose a quiz answer after the briefing |

## Play on Meta Quest

The project is configured for **Android, ARM64, IL2CPP, Vulkan, OpenXR, Single Pass Instanced, Meta Quest Support, and Oculus Touch controllers**. Quest 2, Quest Pro, Quest 3, and Quest 3S are enabled in the OpenXR feature. The original Quest is excluded.

In **NewModel**, the task board is attached above the **left forearm** in VR. It shows quest progress, the current objective, movement and height controls, Restart, and Recover Items. Press **Y** to hide or reopen it. Point with the right controller and squeeze the trigger to press its buttons. Desktop mode keeps the wall-mounted task board.

| Quest control | Action |
| --- | --- |
| Grip / release grip | Snap a pointed item to your hand / release it |
| Point at a door + grip | Open or close the door |
| Point + trigger | Press arm-board, chair, meeting, and results buttons |
| Y (left controller) | Hide / show the arm task board |
| Movement button on the arm board | Switch between Teleport and Walk + Teleport |
| − / + on the arm board | Adjust headset and controller height in 5 cm steps, up to 50 cm in either direction |
| Height value on the arm board | Reset to the default height |
| Either stick forward, then release (Teleport mode) | Aim at a valid floor and teleport |
| Left stick (Walk + Teleport mode) | Walk in the direction you are looking, including strafing |
| Right stick forward, then release | Teleport in either movement mode |
| Right stick sideways | 45-degree snap turn |

Walking defaults to 1.5 metres per second and can be tuned on `VRMovementOptions`. The device remembers movement and height settings. Move your hand to position a held dish, sponge, or bag. The thumbsticks control locomotion. Teleport remains available on the right controller when walking is enabled.

For a standalone build, switch the active Build Profile to **Android / Meta Quest**, then use **Home Focus > Build Quest APK (Android must be active)**. This command builds the enabled scenes, including NewModel and HomeResults. Output: `Builds/HomeFocus-Quest.apk`. Install the updated APK on your headset to receive these fixes. Build and Run requires a developer-enabled headset connected and authorized for USB debugging. To test through Quest Link instead, use **Home Focus > Quest Link play mode > Enable** before pressing Play. Disable it again to return to desktop testing.

The meeting chair unlocks after quests 1–3 are complete. Restart reloads the map where the session started, including when returning from results.

If duplicating this setup into another map, **Home Focus > Repair VR controls in active scene** wires the XR input module, floor teleport colliders/provider/layers, arm board, and movement controls without rebuilding the environment. The November 18 scene backup is in `Backups/VRFix-20261118`.

## The four tasks

1. **Wash dishes:** six dirty items start on the kitchen counter: two plates, two bowls, and two cups. Place a dish in the sink, turn on the gold tap control, pick up the sponge, and rub it against the dish. In VR, both one-handed work on a dish resting in the basin and holding a dish with the other hand are supported. The stains shrink as washing progresses. Contact must occur in the sink with running water and a held sponge.
2. **Organize dishes:** release clean dishes inside their matching plate, bowl, or cup shelf. Dirty dishes and incorrect types are rejected. Accepted dishes snap to a free slot and stay organized.
3. **Take out the trash:** carry the three bags near the entry through the front door and release them inside the green outdoor bin. A bag is only counted after release inside the bin.
4. **Meeting and listening quiz.** After finishing the first three quests, press **Sit in chair** at the meeting desk or use the chair directly with grip in VR or E on desktop. This seats the player and starts the roughly 71-second briefing. Stay in the seat area through all three questions. **Leave Chair**, walking away, or teleporting away stops the briefing and clears any answers already given. Sit again to restart the briefing from the beginning. Submitting all three answers records the score and completes the quest.

Quests unlock in order: **wash every dish → organize every dish → dispose of every bag → meeting and quiz**. Locked quests cannot advance through desktop controls, VR grabs, task volumes, or direct task callbacks. The wall board, desktop HUD, and wrist display show the current objective and locked quests.

Completing the final quiz stops the timer, fades to black, and opens `Assets/HomeTasks/Scenes/HomeResults.unity`. The results show time taken, washing/sorting/trash totals, the recorded quiz score, and whether the listening target was reached. Results support desktop mouse controls and VR controller UI. **Start New Session** fades back to the home, clears results, and unlocks washing and a fresh meeting attempt. The results scene is included in both the Editor build scene list and the Quest APK build command.

**Restart Session** resets the home during gameplay. **Recover Loose Items** returns unheld, incomplete items to their starting positions while preserving completed progress. Items that fall below the map also return automatically.

## Replace the placeholder home

The original HomeFocus blockout has two separate roots:

- `Environment_Placeholder_ReplaceWithYourHome`: blockout house, ground, furniture, plants, lighting, and teleportable floors. Disable or remove this root when your model is ready.
- `Gameplay_KeepWhenReplacingEnvironment`: washing station, sorting shelves, indoor rubbish, outdoor bin, meeting screen, wall board, and the welcome controls panel. Keep these objects and move them to fit the replacement home.

Import your house/environment assets normally and use **1 Unity unit = 1 metre**. Add colliders to floors and walls. Add a `TeleportationArea` to walkable floor colliders with **Interaction Layers = bit 31** (the same layer used by the supplied teleport interactors). Keep the XR player, desktop player, automatic mode router, XR Interaction Manager, EventSystem, and session manager.

Move the named gameplay station roots to the appropriate places in your home. Their trigger volumes and slot transforms work in local coordinates, including rotated and scaled stations. Position `03_OutdoorBin_MoveOutsideYourHome` outside the house. Move both player roots to the same unobstructed starting position at floor height. Keep the desktop camera at eye height and the XR Origin in Floor tracking mode.

The station meshes are placeholders too. To use your own sink, shelves, or bin, replace or hide the station's decorative mesh children while retaining its controller component, trigger volume, tap action, and shelf slot/deposit transforms. Do not disable the whole gameplay station. Place the dishes and sponge on the new counter, update the rubbish starting positions, and verify the wash volume encloses the basin. Starting positions for recovery are captured when Play starts.

NewModel's `Meeting seat` is attached to `Home/chair 1`. When moving the desk, keep the seat and its `Stand point` at floor level and face the meeting screen. The `MeetingSeat` component controls seated eye height and the distance a player can move before the meeting resets.

## Customize content

- `Assets/HomeTasks/Config/TaskSettings.asset`: wash duration, sponge contact tolerance, movement threshold, meeting volume, captions, and quiz pass score.
- `Assets/HomeTasks/Config/NeighbourhoodMeeting.asset`: speaker names, caption text, voice clips, segment minimum times, questions, answer choices, and correct answer indices. Each question uses three choices. Keep the narration and captions consistent.
- `Assets/HomeTasks/Prefabs`: reusable dirty plate, bowl, cup, sponge, and rubbish bag prefabs. Keep each item's Rigidbody, grab interactable, collider, and task component when replacing its art.
- `Assets/HomeTasks/Scripts`: separate task, interaction, player, UI, and session logic.

The placeholder narration is included as four WAV files, generated offline using the installed Windows speech voice. Replace the WAV clips with your own recordings or edit and run `Tools/GenerateMeetingAudio.ps1` on Windows, then update the matching transcript text. No speech service or API key is needed at runtime.

The meeting is a simulation and runs offline. It does not connect to a conferencing service.

## Verification and tools

NewModel regression reports are in `TestResults/NewModelVRVerification.txt` and `TestResults/NewModelMeetingUIVerification.txt`. The checks cover height adjustment and persistence, controller movement, mouse and VR chair clicks, interruptions during the briefing and quiz, and completion through the results screen. Run the corresponding scripts from a fresh NewModel Play session:

```powershell
unity command script eval --caller plugin --skill unity-cli --file Tools/VerifyNewModelVR.cs --timeout 90
unity command script eval --caller plugin --skill unity-cli --file Tools/VerifyNewModelMeetingUI.cs --timeout 180
```

These are simulated input tests in the Editor. Headset comfort and real controller feel still need an on-device playtest. XR UI setup follows the [XR Interaction Toolkit canvas/input-module requirements](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.7/manual/ui-setup.html).

`Tools/VerifyGameplay.cs` is the original HomeFocus integration script. It exercises station volumes, ordered quests, the original meeting entry, and the results and restart buttons. Use the NewModel scripts above for the seated meeting flow.

```powershell
unity command script eval --caller plugin --skill unity-cli --file Tools/VerifyGameplay.cs --timeout 240
```

The earlier HomeFocus flow recorded **196 gameplay checks**, **27 home-scene checks**, and **16 results-scene checks** in the existing reports. The earlier VR fixes are described in `TestResults/NewModelVRFixes.md`. Rendered fade captures are checked by `Tools/VerifyFade.cs`, with results in `TestResults/FadeVerification.txt`. Screenshots are in `Screenshots/`.

To check saved references, input configuration, Quest settings, and rotated station volumes in Edit mode:

```powershell
unity command script eval --caller plugin --skill unity-cli --file Tools/ValidateScene.cs
unity command script eval --caller plugin --skill unity-cli --file Tools/ValidateResultsScene.cs
```

`Home Focus > Create playable home scene` is the original scene generator. It deliberately refuses to overwrite an existing HomeFocus scene or discard unsaved scene changes. The scene and all generated assets are already included; running the generator is unnecessary for normal play or art replacement. `Home Focus > Configure Meta Quest` reapplies the documented Quest settings.

`Home Focus > Apply ordered quests and results scene` updates the existing home UI in place and creates the results scene if missing. It preserves an existing results scene and other scenes in the build list.

Unity-generated `Library`, `Temp`, `Logs`, local settings, and build output are excluded by `.gitignore`. Source assets and their `.meta` files should be versioned together.

## Package versions

| Package | Installed version |
| --- | --- |
| Universal Render Pipeline | 17.3.0 |
| Input System | 1.18.0 |
| XR Interaction Toolkit | 3.7.0 |
| OpenXR | 1.19.0 |
| XR Plug-in Management | 4.7.0 |
| XR Core Utilities | 2.5.3 |
| uGUI / TextMeshPro | 2.0.0 |

Setup references: [Unity OpenXR Meta Quest support](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.19/manual/features/metaquest.html), [XR Interaction Toolkit Starter Assets](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.7/manual/samples-starter-assets.html), and [Meta project setup](https://developers.meta.com/vr/documentation/unity/unity-project-setup/).
