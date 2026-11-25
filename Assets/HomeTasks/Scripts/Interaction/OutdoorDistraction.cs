using UnityEngine;

namespace HomeTasks
{
    public sealed class OutdoorDistraction : MonoBehaviour
    {
        public DistractionKind kind;
        public PlayerModeRouter player;
        public BoxCollider area;
        public AudioSource sound;
        public BoxCollider[] audioInteriors;
        public Transform gazeTarget;
        public Collider transparentBoundary;
        [Range(3f, 30f)] public float gazeAngle = 9f;
        [Min(0.1f)] public float dwellSeconds = 1f;

        readonly RaycastHit[] sightHits = new RaycastHit[16];
        float pendingSeconds;
        float outdoorVolume;
        bool engaged;

        void Awake()
        {
            if (sound != null) outdoorVolume = sound.volume;
        }

        void LateUpdate()
        {
            var session = HomeSession.Instance;
            var view = player != null ? player.ActiveCamera : null;
            var playing = session != null && !session.WaitingForStart && !session.Finished && view != null;
            var attentive = playing && Contains(area, view.transform.position) &&
                (gazeTarget == null || CanSeeTarget(view.transform));

            if (sound != null)
            {
                var audible = audioInteriors == null || audioInteriors.Length == 0 ? attentive :
                    playing && CanHearOutside(view.transform.position);
                UpdateSound(audible);
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

        bool CanHearOutside(Vector3 listener)
        {
            if ((sound.transform.position - listener).sqrMagnitude >= sound.maxDistance * sound.maxDistance) return false;
            foreach (var interior in audioInteriors)
                if (Contains(interior, listener)) return false;
            return true;
        }

        void UpdateSound(bool audible)
        {
            if (audible && !sound.isPlaying)
            {
                sound.volume = 0f;
                sound.Play();
            }
            if (!sound.isPlaying) return;
            const float fadeSeconds = 0.18f;
            sound.volume = Mathf.MoveTowards(sound.volume, audible ? outdoorVolume : 0f, outdoorVolume * Time.deltaTime / fadeSeconds);
            if (!audible && sound.volume == 0f) sound.Stop();
        }

        static bool Contains(BoxCollider box, Vector3 point)
        {
            if (box == null) return false;
            var relative = box.transform.InverseTransformPoint(point) - box.center;
            var half = box.size * 0.5f;
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
            if (sound != null)
            {
                sound.Stop();
                sound.volume = outdoorVolume;
            }
            pendingSeconds = 0;
            engaged = false;
        }
    }
}
