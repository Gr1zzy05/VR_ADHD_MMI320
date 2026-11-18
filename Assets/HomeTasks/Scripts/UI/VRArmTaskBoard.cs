using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HomeTasks
{
    public sealed class VRArmTaskBoard : MonoBehaviour
    {
        public GameObject content;
        public VRMovementOptions movement;
        public TMP_Text movementLabel;
        public VRHeightAdjustment heightAdjustment;
        public TMP_Text heightLabel;
        public bool IsOpen => content != null && content.activeSelf;
        InputAction toggleMenu;
        float nextUpdate;

        void Awake()
        {
            toggleMenu = new InputAction("Toggle arm task board", InputActionType.Button, "<XRController>{LeftHand}/{SecondaryButton}");
        }

        void OnEnable() => toggleMenu?.Enable();
        void OnDisable() => toggleMenu?.Disable();

        void Update()
        {
            if (toggleMenu != null && toggleMenu.WasPressedThisFrame()) SetOpen(!IsOpen);
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.1f;
            if (movementLabel != null && movement != null) movementLabel.text = movement.ModeLabel;
            if (heightLabel != null && heightAdjustment != null) heightLabel.text = $"HEIGHT {heightAdjustment.OffsetCentimeters:+0;-0;0} cm / RESET";
        }

        public void SetOpen(bool open) { if (content != null) content.SetActive(open); }
        void OnDestroy() => toggleMenu?.Dispose();
    }
}
