# Home Focus verification

Verified on 12 November 2026 (Asia/Bangkok), using Unity 6000.3.9f1.

- **196 gameplay checks passed:** ordered quest unlocks, blocked early grabs and direct task callbacks, real sink washing, correct sorting, outdoor trash disposal, the full original meeting, one quiz attempt, both 0/3 and 3/3 score paths, fade initiation, results data across scene loads, and repeated restart input.
- **27 home-scene checks passed:** saved gameplay references, locked meeting controls, results build registration, no missing scripts, desktop/XR input, Quest configuration, and rotated station volumes.
- **16 results-scene checks passed:** both desktop and OpenXR rigs, wired results/restart UI, tracked controller raycasting, disabled menu locomotion, and no references to the unloaded home.
- **2 rendered fade checks passed:** a full black frame covers the scene and desktop HUD, and fade-in restores the view. Verification uses fresh camera captures with overlay UI; an unfocused Editor can return stale frames from `ScreenCapture` alone.
- Script compilation completed with no compile errors. The results screen and initial locked-quest layout were visually inspected.

Reports: `GameplayVerification.txt`, `SceneValidation.txt`, `ResultsSceneValidation.txt`, and `FadeVerification.txt`.

Captures: `Screenshots/OrderedQuests-Start.png`, `Screenshots/OrderedQuests-Results.png`, `Screenshots/Fade-Out.png`, and `Screenshots/Fade-In.png`.

Physical Quest controller interaction, stereo fade coverage, comfort, and device frame rate still require a headset test. The APK described below was built on 10 November and has not been rebuilt for the 12 November quest-flow changes.

## Previous APK verification

Verified on 10 November 2026 (Asia/Bangkok), using Unity 6000.3.9f1.

- **62 gameplay and restart checks passed:** real sink contact and wash updates, six dishes, correct/incorrect sorting, held-object rejection, three outdoor bag deposits, full meeting playback, early-answer rejection, failed quiz, retry, all-task completion, and restart.
- **24 saved-scene checks passed:** saved references and callbacks, no missing scripts, both-hand teleport/snap-turn input, XR loader and features, ARM64/IL2CPP/Vulkan settings, and station volumes under rotation and scaling.
- **Quest APK build succeeded with zero errors.** Output: `Builds/HomeFocus-Quest.apk` (47,875,582 bytes, approximately 48 MB).
- APK package identifier: `com.homefocus.vr`; version `0.1.0` / code `1`; native ABI `arm64-v8a`; Android minimum API 32 / target API 36.
- APK signature verified successfully with Android's `apksigner` using APK Signature Scheme v2.
- The release manifest includes the VR launch category, required VR head tracking, and Meta Quest supported-device metadata.
- The two-hand teleport overrides were saved and checked again after reopening the scene.

The build emitted two non-blocking warnings. OpenXR sees the original template PC renderer's SSAO dependency; the Android Mobile renderer has no renderer features and does not use SSAO. Unity also notes that diagnostic crash reports will lack detailed native symbols with the current build settings.

Physical Quest comfort, controller feel, and device frame rate have not been tested. These require a headset playtest.

Detailed results: `GameplayVerification.txt`, `SceneValidation.txt`, `QuestBuildReport.json`, `QuestApkMetadata.txt`, and `QuestApkSignature.txt`.

APK SHA-256:

```text
5A2673D034B9A26E727962DB2D091307B29EC0CBB45C73491F0E73EAFFC292D9
```
