using UnityEngine;

namespace HomeTasks
{
    [CreateAssetMenu(menuName = "Home Focus/Task settings")]
    public sealed class HomeTaskSettings : ScriptableObject
    {
        [Min(0.5f)] public float scrubSecondsPerDish = 3.5f;
        [Min(0.02f)] public float spongeContactDistance = 0.16f;
        [Min(0.001f)] public float minimumScrubSpeed = 0.025f;
        [Range(1, 3)] public int correctAnswersRequired = 3;
        [Range(0, 1)] public float meetingVolume = 0.85f;
        public bool showCaptions = true;
    }
}
