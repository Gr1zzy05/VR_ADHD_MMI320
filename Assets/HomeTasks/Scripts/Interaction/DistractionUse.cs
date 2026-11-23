using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HomeTasks
{
    public sealed class DistractionUse : MonoBehaviour
    {
        public DistractionKind kind;

        XRGrabInteractable grab;
        HomeItem item;
        FidgetCubeController cube;
        FidgetControl[] controls;
        bool wasEngaged;
        float pulseUntil;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            item = GetComponent<HomeItem>();
            cube = GetComponent<FidgetCubeController>();
            controls = GetComponentsInChildren<FidgetControl>(true);
        }

        // A successful tap can play with the toy without anyone holding it.
        public void Pulse(float seconds)
        {
            if (isActiveAndEnabled && HomeSession.Instance?.WaitingForStart != true)
                pulseUntil = Mathf.Max(pulseUntil, Time.time + seconds);
        }

        void LateUpdate()
        {
            var session = HomeSession.Instance;
            if (session?.WaitingForStart == true) return;
            var engaged = Time.time < pulseUntil || (item != null && item.IsHeld) ||
                (cube != null && cube.IsHeld) || (grab != null && grab.isSelected) || AnyControlEngaged();
            if (engaged) session?.RecordDistraction(kind, !wasEngaged, Time.deltaTime);
            wasEngaged = engaged;
        }

        bool AnyControlEngaged()
        {
            foreach (var control in controls)
                if (control != null && control.IsEngaged) return true;
            return false;
        }

        void OnDisable()
        {
            wasEngaged = false;
            pulseUntil = 0f;
        }
    }
}
