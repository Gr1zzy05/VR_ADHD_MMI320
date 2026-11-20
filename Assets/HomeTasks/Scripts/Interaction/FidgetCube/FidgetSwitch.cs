using UnityEngine;

namespace HomeTasks
{
    public sealed class FidgetSwitch : FidgetControl
    {
        [SerializeField] Vector3 hingeAxis = Vector3.right;
        [SerializeField] float offAngle = -12f;
        [SerializeField] float onAngle = 12f;
        [SerializeField, Min(1f)] float degreesPerSecond = 240f;
        [SerializeField] bool isOn;

        Quaternion restRotation;
        float angle;
        public bool IsOn => isOn;

        void Awake()
        {
            restRotation = visualPivot.localRotation;
            angle = isOn ? onAngle : offAngle;
            ApplyAngle();
        }

        protected override void OnContactStarted(Vector3 localPoint) => isOn = !isOn;

        void Update()
        {
            angle = Mathf.MoveTowards(angle, isOn ? onAngle : offAngle, degreesPerSecond * Time.deltaTime);
            ApplyAngle();
        }

        void ApplyAngle() => visualPivot.localRotation = Quaternion.AngleAxis(angle, hingeAxis.normalized) * restRotation;
    }
}
