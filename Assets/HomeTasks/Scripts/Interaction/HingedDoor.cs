using UnityEngine;

namespace HomeTasks
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider), typeof(HomeAction))]
    public sealed class HingedDoor : MonoBehaviour
    {
        [Tooltip("Hinge position in the door mesh's local coordinates.")]
        public Vector3 hinge;
        public float swingAngle = 85f;
        public bool startsOpen;
        [Min(0.05f)] public float swingSeconds = 0.35f;

        public bool IsOpen { get; private set; }

        Rigidbody doorBody;
        HomeAction action;
        Vector3 hingePosition;
        Vector3 offsetFromHinge;
        Quaternion startingRotation;
        float closedAngle;
        float openAngle;
        float currentAngle;

        void Awake()
        {
            doorBody = GetComponent<Rigidbody>();
            action = GetComponent<HomeAction>();
            hingePosition = transform.TransformPoint(hinge);
            offsetFromHinge = doorBody.position - hingePosition;
            startingRotation = doorBody.rotation;
            closedAngle = startsOpen ? -swingAngle : 0;
            openAngle = closedAngle + swingAngle;
            IsOpen = startsOpen;
            UpdatePrompt();
        }

        public void Toggle()
        {
            IsOpen = !IsOpen;
            UpdatePrompt();
        }

        void FixedUpdate()
        {
            var targetAngle = IsOpen ? openAngle : closedAngle;
            if (currentAngle == targetAngle) return;

            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, Mathf.Abs(swingAngle) * Time.fixedDeltaTime / swingSeconds);
            var rotation = Quaternion.AngleAxis(currentAngle, Vector3.up);
            doorBody.Move(hingePosition + rotation * offsetFromHinge, rotation * startingRotation);
        }

        void UpdatePrompt() => action.instruction = IsOpen ? "close door" : "open door";
    }
}
