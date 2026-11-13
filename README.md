# Home Focus

A single-player 3D VR home game for standalone Meta Quest. All four activities share one continuous map. The supplied home is an editable blockout for replacing with your own 3D house and environment.

## Play in Unity

Open `Assets/HomeTasks/Scenes/HomeFocus.unity` with **Unity 6000.3.9f1** and press **Play**. With Quest Link initialization disabled, the scene automatically uses the desktop player. The scene is first in the build scene list; the original SampleScene is preserved.

| Desktop control | Action |
| --- | --- |
| Click the Game view | Capture the mouse |
| WASD / mouse | Walk / look |
| Left click | Pick up or release an item |
| Q | Drop the held item |
| E | Use the gold tap control or the blue meeting keyboard |
| Hold E while holding the sponge and aiming at a dish | Scrub a dish inside the running sink |
| Mouse wheel / R | Adjust held distance / rotate the held item |
| Tab | Toggle the free cursor to click screen buttons |
| Escape | Release the mouse |
| 1 / 2 / 3 near the meeting desk | Choose a quiz answer after the briefing |

## Play on Meta Quest

The project is configured for **Android, ARM64, IL2CPP, Vulkan, OpenXR, Single Pass Instanced, Meta Quest Support, and Oculus Touch controllers**. Quest 2, Quest Pro, Quest 3, and Quest 3S are enabled in the OpenXR feature. The original Quest is excluded.

Use the controllers: grip to grab items, release grip to let go, point and grip to toggle the gold tap control, and point and squeeze the trigger for screen buttons. Push a thumbstick forward to aim a teleport, then release it to travel. Use the free hand to teleport while the other holds a dish or bag. Push the right stick sideways for a 45-degree snap turn. Continuous movement is disabled in the supplied comfort setup. The left controller has a small wrist progress display.

For a standalone build, switch the active Build Profile to **Android / Meta Quest**, then use **Home Focus > Build Quest APK (Android must be active)**. Output: `Builds/HomeFocus-Quest.apk`. Build and Run requires a developer-enabled headset connected and authorized for USB debugging. To test through Quest Link instead, use **Home Focus > Quest Link play mode > Enable** before pressing Play. Disable it again to return to desktop testing.

## The four tasks

1. **Wash dishes:** six dirty items start on the kitchen counter: two plates, two bowls, and two cups. Place a dish in the sink, turn on the gold tap control, pick up the sponge, and rub it against the dish. In VR, both one-handed work on a dish resting in the basin and holding a dish with the other hand are supported. The stains shrink as washing progresses. Contact must occur in the sink with running water and a held sponge.
2. **Organize dishes:** release clean dishes inside their matching plate, bowl, or cup shelf. Dirty dishes and incorrect types are rejected. Accepted dishes snap to a free slot and stay organized.
3. **Take out the trash:** carry the three bags near the entry through the open doorway and release them inside the green outdoor bin. A bag is only counted after release inside the bin.
4. **Meeting and listening quiz:** after finishing the first three quests, join on the meeting screen or use the blue keyboard shortcut on the desk. Maya gives a roughly 71-second spoken briefing with captions. Three questions unlock only after the entire briefing. There is **one briefing and one quiz attempt per session**. The target is three correct answers by default. Submitting all three answers completes the quest and records the score, including a below-target score. The briefing and answers cannot be replayed or reset within that session.

Quests unlock in order: **wash every dish → organize every dish → dispose of every bag → meeting and quiz**. Locked quests cannot advance through desktop controls, VR grabs, task volumes, or direct task callbacks. The wall board, desktop HUD, and wrist display show the current objective and locked quests.

Completing the final quiz stops the timer, fades to black, and opens `Assets/HomeTasks/Scenes/HomeResults.unity`. The results show time taken, washing/sorting/trash totals, the recorded quiz score, and whether the listening target was reached. Results support desktop mouse controls and VR controller UI. **Start New Session** fades back to the home, clears results, and unlocks washing and a fresh meeting attempt. The results scene is included in both the Editor build scene list and the Quest APK build command.

**Restart Session** resets the home during gameplay. **Recover Loose Items** returns unheld, incomplete items to their starting positions while preserving completed progress. Items that fall below the map also return automatically.

## Replace the placeholder home

The saved scene has two separate roots:

- `Environment_Placeholder_ReplaceWithYourHome`: blockout house, ground, furniture, plants, lighting, and teleportable floors. Disable or remove this root when your model is ready.
- `Gameplay_KeepWhenReplacingEnvironment`: washing station, sorting shelves, indoor rubbish, outdoor bin, meeting screen, wall board, and the welcome controls panel. Keep these objects and move them to fit the replacement home.

Import your house/environment assets normally and use **1 Unity unit = 1 metre**. Add colliders to floors and walls. Add a `TeleportationArea` to walkable floor colliders with **Interaction Layers = bit 31** (the same layer used by the supplied teleport interactors). Keep the XR player, desktop player, automatic mode router, XR Interaction Manager, EventSystem, and session manager.

Move the named gameplay station roots to the appropriate places in your home. Their trigger volumes and slot transforms work in local coordinates, including rotated and scaled stations. Position `03_OutdoorBin_MoveOutsideYourHome` outside the house. Move both player roots to the same unobstructed starting position at floor height. Keep the desktop camera at eye height and the XR Origin in Floor tracking mode.

The station meshes are placeholders too. To use your own sink, shelves, or bin, replace or hide the station's decorative mesh children while retaining its controller component, trigger volume, tap action, and shelf slot/deposit transforms. Do not disable the whole gameplay station. Place the dishes and sponge on the new counter, update the rubbish starting positions, and verify the wash volume encloses the basin. Starting positions for recovery are captured when Play starts.

## Customize content

- `Assets/HomeTasks/Config/TaskSettings.asset`: wash duration, sponge contact tolerance, movement threshold, meeting volume, captions, and quiz pass score.
- `Assets/HomeTasks/Config/NeighbourhoodMeeting.asset`: speaker names, caption text, voice clips, segment minimum times, questions, answer choices, and correct answer indices. Each question uses three choices. Keep the narration and captions consistent.
- `Assets/HomeTasks/Prefabs`: reusable dirty plate, bowl, cup, sponge, and rubbish bag prefabs. Keep each item's Rigidbody, grab interactable, collider, and task component when replacing its art.
- `Assets/HomeTasks/Scripts`: separate task, interaction, player, UI, and session logic.

The placeholder narration is included as four WAV files, generated offline using the installed Windows speech voice. Replace the WAV clips with your own recordings or edit and run `Tools/GenerateMeetingAudio.ps1` on Windows, then update the matching transcript text. No speech service or API key is needed at runtime.

The meeting is a simulation and runs offline. It does not connect to a conferencing service.

## Verification and tools

`Tools/VerifyGameplay.cs` is a repeatable Play-mode integration script for the Unity Pipeline CLI. It exercises real station volumes and washing updates, rejects out-of-order quests, waits for the full original meeting, and verifies a single failed attempt still reaches results. It then starts a new session and checks the successful score path with a short, isolated test briefing. Both sessions test the results and restart buttons. Run it from a **fresh** Play session:

```powershell
unity command script eval --caller plugin --skill unity-cli --file Tools/VerifyGameplay.cs --timeout 240
```

The report is written to `TestResults/GameplayVerification.txt`. The current flow passes **196 gameplay checks**, **27 home-scene checks**, and **16 results-scene checks**. Rendered fade captures are checked by `Tools/VerifyFade.cs`, with results in `TestResults/FadeVerification.txt`. Screenshots are in `Screenshots/`. Hardware comfort and controller feel still need validation on a physical Quest.

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
