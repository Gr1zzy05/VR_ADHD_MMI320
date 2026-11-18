using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HomeTasks
{
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public abstract class HomeItem : MonoBehaviour
    {
        public string displayName = "Item";
        public bool DesktopHeld { get; private set; }
        public bool IsHeld => DesktopHeld || (Grab != null && Grab.isSelected);
        public bool IsLocked { get; protected set; }
        public Rigidbody Body { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        public Collider Shape { get; private set; }
        public bool CanInteract => !IsLocked && (HomeSession.Instance == null || (HomeSession.Instance.IsQuestActive(HomeSession.Instance.CurrentQuest) && AvailableFor(HomeSession.Instance.CurrentQuest)));
        Vector3 homePosition;
        Quaternion homeRotation;

        protected abstract bool AvailableFor(HomeQuest quest);

        void Start() => UpdateAvailability();

        void UpdateAvailability()
        {
            if (Grab != null && Grab.enabled != CanInteract) Grab.enabled = CanInteract;
        }

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Grab = GetComponent<XRGrabInteractable>();
            Grab.farAttachMode = InteractableFarAttachMode.Near;
            Grab.attachEaseInTime = 0f;
            Grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            Shape = GetComponent<Collider>();
            homePosition = transform.position;
            homeRotation = transform.rotation;
        }

        protected virtual void LateUpdate()
        {
            UpdateAvailability();
            if (!IsHeld && !IsLocked && (transform.position.y < -3 || Vector3.Distance(transform.position, homePosition) > 40))
                ReturnHome();
        }

        public void SetDesktopHeld(bool held)
        {
            if (IsLocked || (held && (!CanInteract || Grab.isSelected))) return;
            DesktopHeld = held;
            Body.isKinematic = held;
            Body.useGravity = !held;
            if (!held) Body.WakeUp();
        }

        public void LockTo(Transform slot)
        {
            if (IsHeld || IsLocked) return;
            IsLocked = true;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.isKinematic = true;
            Body.useGravity = false;
            Grab.enabled = false;
            transform.SetPositionAndRotation(slot.position, slot.rotation);
        }

        public void ReturnHome()
        {
            if (IsHeld || IsLocked || Body == null) return;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(homePosition, homeRotation);
            Body.WakeUp();
        }
    }
}
