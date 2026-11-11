using UnityEngine;

namespace HomeTasks
{
    public enum DishKind { Plate, Bowl, Cup }

    public sealed class DishItem : HomeItem
    {
        public DishKind kind;
        public Renderer[] dirtMarks;
        public float WashProgress { get; private set; }
        public bool IsClean => WashProgress >= 1;
        public bool IsStored { get; private set; }
        Vector3[] originalDirtSizes;

        protected override void Awake()
        {
            base.Awake();
            originalDirtSizes = new Vector3[dirtMarks.Length];
            for (var i = 0; i < dirtMarks.Length; i++)
                originalDirtSizes[i] = dirtMarks[i].transform.localScale;
        }

        public void ApplyScrub(float seconds)
        {
            if (seconds <= 0 || IsClean || IsStored) return;
            var duration = HomeSession.Instance != null ? HomeSession.Instance.settings.scrubSecondsPerDish : 3.5f;
            WashProgress = Mathf.Clamp01(WashProgress + seconds / Mathf.Max(0.5f, duration));
            for (var i = 0; i < dirtMarks.Length; i++)
            {
                dirtMarks[i].transform.localScale = originalDirtSizes[i] * Mathf.Lerp(1, 0.15f, WashProgress);
                dirtMarks[i].enabled = !IsClean;
            }
            if (IsClean) HomeSession.Instance?.Notify($"{kind} washed. Place it on the {kind.ToString().ToLower()} shelf.", true);
        }

        public bool Store(Transform slot)
        {
            if (!IsClean || IsHeld || IsStored || slot == null) return false;
            LockTo(slot);
            IsStored = true;
            HomeSession.Instance?.Notify($"Clean {kind.ToString().ToLower()} organized.", true);
            return true;
        }
    }
}
