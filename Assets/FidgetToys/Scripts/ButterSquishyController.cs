using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace FocusShift.Toys
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class ButterSquishyController : MonoBehaviour
    {
        public SkinnedMeshRenderer foam;
        [Range(0f, 1f)] public float maximumCompression = 1f;
        [Min(0.1f)] public float squeezeSpeed = 10f;
        [Min(0.1f)] public float recoverySpeed = 5f;
        [Range(.3f, 2f)] public float softness = 1f;

        XRGrabInteractable grab;
        float amount, velocity, desktopPressure;
        public void SetDesktopPressure(float pressure) => desktopPressure = Mathf.Clamp01(pressure);
        void Awake() => grab = GetComponent<XRGrabInteractable>();
        void Update()
        {
            float pressure = desktopPressure;
            if (grab.firstInteractorSelecting is XRBaseInputInteractor hand)
                pressure = Mathf.Max(pressure, hand.activateInput.ReadValue(), hand.activateInput.ReadIsPerformed() ? 1f : 0f);
            float target = Mathf.Clamp01(pressure) * maximumCompression;
            float speed = target > amount ? squeezeSpeed : recoverySpeed;
            amount = Mathf.SmoothDamp(amount, target, ref velocity, softness / speed, Mathf.Infinity, Time.deltaTime);
            if (Mathf.Abs(amount - target) < .0002f) { amount = target; velocity = 0f; }
            foam.SetBlendShapeWeight(0, amount * 100f);
        }
        void OnDisable()
        {
            desktopPressure = amount = velocity = 0f;
            if (foam != null) foam.SetBlendShapeWeight(0, 0f);
        }
    }
}
