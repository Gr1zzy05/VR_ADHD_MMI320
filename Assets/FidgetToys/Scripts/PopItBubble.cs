using UnityEngine;

namespace FocusShift.Toys
{
    public sealed class PopItBubble : MonoBehaviour
    {
        public SkinnedMeshRenderer membrane;
        public Vector2 centerOnBoard;
        public bool IsInverted { get; private set; }
        float elapsed, start, target, duration;
        int lastToggle = -1;

        public bool Toggle(float seconds, float depth)
        {
            if (lastToggle == Time.frameCount) return false;
            lastToggle = Time.frameCount;
            IsInverted = !IsInverted;
            Animate(seconds, IsInverted ? depth * 100f : 0f);
            return true;
        }
        public void ResetBubble(float seconds)
        {
            IsInverted = false;
            Animate(seconds, 0f);
        }
        void Animate(float seconds, float weight)
        {
            start = membrane.GetBlendShapeWeight(0);
            target = weight;
            elapsed = 0f;
            duration = Mathf.Max(.01f, seconds);
            enabled = true;
        }
        void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Min(1f, elapsed / duration);
            // A fast snap through the plane and a small damped overshoot.
            float f = t == 1f ? 1f : 1f - Mathf.Exp(-5.5f*t) * Mathf.Cos(5f*t);
            membrane.SetBlendShapeWeight(0, Mathf.LerpUnclamped(start, target, f));
            if (t == 1f) enabled = false;
        }
    }
}
