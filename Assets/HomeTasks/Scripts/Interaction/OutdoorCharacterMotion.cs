using UnityEngine;

namespace HomeTasks
{
    public sealed class OutdoorCharacterMotion : MonoBehaviour
    {
        public enum Activity { Talking, Dancing, Walking }

        public Activity activity;
        public Transform torso;
        public Transform leftArm;
        public Transform rightArm;
        public Transform leftLeg;
        public Transform rightLeg;
        public float phase;
        [Min(0.1f)] public float walkSpan = 4f;

        Vector3 restPosition;
        Quaternion restRotation;

        void Awake()
        {
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
        }

        void Update()
        {
            var t = Time.time + phase;
            if (activity == Activity.Walking)
            {
                var distance = t * 0.9f;
                var forward = Mathf.Repeat(distance, walkSpan * 2f) < walkSpan;
                var travel = Mathf.PingPong(distance, walkSpan) - walkSpan * 0.5f;
                var stride = Mathf.Sin(t * 7.8f);
                transform.localPosition = restPosition + new Vector3(travel, Mathf.Abs(stride) * 0.035f, 0);
                transform.localRotation = restRotation * Quaternion.Euler(0, forward ? 90 : -90, 0);
                leftLeg.localRotation = Quaternion.Euler(30 * stride, 0, 0);
                rightLeg.localRotation = Quaternion.Euler(-30 * stride, 0, 0);
                leftArm.localRotation = Quaternion.Euler(-27 * stride, 0, 4);
                rightArm.localRotation = Quaternion.Euler(27 * stride, 0, -4);
            }
            else if (activity == Activity.Dancing)
            {
                var beat = Mathf.Sin(t * Mathf.PI * 112f / 30f);
                torso.localRotation = Quaternion.Euler(0, beat * 9f, beat * 8f);
                leftArm.localRotation = Quaternion.Euler(12f * beat, 0, 22f + 12f * beat);
                rightArm.localRotation = Quaternion.Euler(-12f * beat, 0, -22f + 12f * beat);
            }
            else
            {
                torso.localRotation = Quaternion.Euler(Mathf.Sin(t * 2.1f) * 3f, 0, 0);
                rightArm.localRotation = Quaternion.Euler(-18f - 15f * Mathf.Sin(t * 2.1f), 0, -13f);
            }
        }
    }
}
