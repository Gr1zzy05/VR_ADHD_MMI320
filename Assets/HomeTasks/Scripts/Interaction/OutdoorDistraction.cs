using UnityEngine;

namespace HomeTasks
{
    public sealed class OutdoorDistraction : MonoBehaviour
    {
        public DistractionKind kind;
        public PlayerModeRouter player;
        public BoxCollider area;
        public AudioSource sound;
        public Transform gazeTarget;
        public Collider transparentBoundary;
        [Range(3f, 30f)] public float gazeAngle = 9f;
        [Min(0.1f)] public float dwellSeconds = 1f;

        readonly RaycastHit[] sightHits = new RaycastHit[16];
        float pendingSeconds;
        bool engaged;

        void LateUpdate()
        {
            var session = HomeSession.Instance;
            var view = player.ActiveCamera;
            var attentive = session != null && !session.Finished && view != null && IsInArea(view.transform.position) &&
                (gazeTarget == null || CanSeeTarget(view.transform));

            if (sound != null && sound.isPlaying != attentive)
            {
                if (attentive) sound.Play();
                else sound.Stop();
            }

            if (!attentive)
            {
                pendingSeconds = 0;
                engaged = false;
                return;
            }

            if (engaged)
            {
                session.RecordDistraction(kind, false, Time.deltaTime);
                return;
            }

            pendingSeconds += Time.deltaTime;
            if (pendingSeconds < dwellSeconds) return;
            engaged = true;
            session.RecordDistraction(kind, true, pendingSeconds);
        }

        bool IsInArea(Vector3 point)
        {
            var relative = area.transform.InverseTransformPoint(point) - area.center;
            var half = area.size * 0.5f;
            return Mathf.Abs(relative.x) <= half.x && Mathf.Abs(relative.y) <= half.y && Mathf.Abs(relative.z) <= half.z;
        }

        bool CanSeeTarget(Transform view)
        {
            var offset = gazeTarget.position - view.position;
            var direction = offset.normalized;
            if (Vector3.Dot(view.forward, direction) < Mathf.Cos(gazeAngle * Mathf.Deg2Rad)) return false;

            var distance = Mathf.Max(0f, offset.magnitude - 0.12f);
            var count = Physics.RaycastNonAlloc(view.position, direction, sightHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (var i = 0; i < count; i++)
                if (sightHits[i].collider != transparentBoundary) return false;
            return true;
        }

        void OnDisable()
        {
            if (sound != null) sound.Stop();
            pendingSeconds = 0;
            engaged = false;
        }
    }
}
