using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace HomeTasks
{
    public sealed class VRMovementOptions : MonoBehaviour
    {
        const string WalkingPreference = "HomeFocus.ControllerWalking";
        public ControllerInputActionManager leftController;
        public ControllerInputActionManager rightController;
        public ContinuousMoveProvider moveProvider;
        [Range(0.5f, 3)] public float walkingSpeed = 1.5f;
        public bool rememberChoice = true;
        public bool WalkingEnabled { get; private set; }
        public string ModeLabel => WalkingEnabled ? GameText.Get("MOVEMENT: WALK + TELEPORT") : GameText.Get("MOVEMENT: TELEPORT");

        void Start() => SetWalkingEnabled(rememberChoice && PlayerPrefs.GetInt(WalkingPreference, 0) == 1);

        public void ToggleWalking()
        {
            SetWalkingEnabled(!WalkingEnabled);
            if (rememberChoice) { PlayerPrefs.SetInt(WalkingPreference, WalkingEnabled ? 1 : 0); PlayerPrefs.Save(); }
            HomeSession.Instance?.Notify(WalkingEnabled ? GameText.Get("Left stick: walk. Right stick: teleport and snap turn.") :
                GameText.Get("Teleport mode. Push either stick forward, aim at the floor, then release."));
        }

        public void SetWalkingEnabled(bool enabled)
        {
            WalkingEnabled = enabled;
            // Reserve the right stick for teleport and snap-turn in both modes.
            if (leftController != null) { leftController.smoothMotionEnabled = enabled; leftController.smoothTurnEnabled = false; }
            if (rightController != null) { rightController.smoothMotionEnabled = false; rightController.smoothTurnEnabled = false; }
            if (moveProvider != null)
            {
                moveProvider.moveSpeed = walkingSpeed;
                moveProvider.enableFly = false;
                moveProvider.enableStrafe = true;
                moveProvider.enabled = enabled;
            }
        }
    }
}
