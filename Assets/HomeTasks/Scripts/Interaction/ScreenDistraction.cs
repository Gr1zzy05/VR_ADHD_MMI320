using TMPro;
using UnityEngine;

namespace HomeTasks
{
    public sealed class ScreenDistraction : MonoBehaviour
    {
        public DistractionKind kind;
        public PlayerModeRouter player;
        public Transform displayPoint;
        public GameObject content;
        public TMP_Text buttonLabel;
        public string inactiveLabel;
        public string activeLabel;
        [Min(0.1f)] public float viewingDistance = 5f;
        [Range(5f, 45f)] public float viewingAngle = 25f;
        public bool closeWhenOutOfRange;

        public bool IsOpen => content != null && content.activeSelf;
        HomeAction action;

        void Awake() => action = GetComponent<HomeAction>();

        void OnEnable()
        {
            GameText.Changed += RefreshLabel;
            RefreshLabel();
        }

        void OnDisable() => GameText.Changed -= RefreshLabel;

        public void Toggle()
        {
            if (IsOpen)
            {
                SetOpen(false);
                return;
            }

            var view = player.ActiveCamera;
            var session = HomeSession.Instance;
            if (view == null || (session != null && session.Finished) || !IsNear(view)) return;
            SetOpen(true);
            session?.RecordDistraction(kind, true, 0f);
        }

        void LateUpdate()
        {
            if (!IsOpen) return;
            var view = player.ActiveCamera;
            if (view == null || !IsNear(view))
            {
                if (closeWhenOutOfRange) SetOpen(false);
                return;
            }

            var offset = displayPoint.position - view.transform.position;
            var direction = offset.normalized;
            if (Vector3.Dot(view.transform.forward, direction) < Mathf.Cos(viewingAngle * Mathf.Deg2Rad) ||
                Vector3.Dot(displayPoint.forward, direction) <= 0f) return;

            if (Physics.Raycast(view.transform.position, direction, Mathf.Max(0f, offset.magnitude - 0.025f),
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;

            HomeSession.Instance?.RecordDistraction(kind, false, Time.deltaTime);
        }

        bool IsNear(Camera view) => (displayPoint.position - view.transform.position).sqrMagnitude <= viewingDistance * viewingDistance;

        void SetOpen(bool open)
        {
            content.SetActive(open);
            RefreshLabel();
        }

        void RefreshLabel()
        {
            var label = IsOpen ? activeLabel : inactiveLabel;
            buttonLabel.text = GameText.Get(label);
            if (action != null) action.instruction = label.ToLowerInvariant();
        }
    }
}
