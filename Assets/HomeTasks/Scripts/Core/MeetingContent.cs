using System;
using UnityEngine;

namespace HomeTasks
{
    [Serializable]
    public sealed class MeetingSegment
    {
        public string speaker = "Maya";
        [TextArea(2, 6)] public string transcript;
        public AudioClip voice;
        [Min(1)] public float minimumDuration = 4;
        public float Duration => Mathf.Max(minimumDuration, voice != null ? voice.length + 0.6f : 0);
    }

    [Serializable]
    public sealed class ListeningQuestion
    {
        [TextArea] public string prompt;
        public string[] answers = new string[3];
        [Range(0, 2)] public int correctAnswer;
        [TextArea] public string explanation;
    }

    [CreateAssetMenu(menuName = "Home Focus/Meeting content")]
    public sealed class MeetingContent : ScriptableObject
    {
        public string meetingTitle = "Neighbourhood check-in";
        public MeetingSegment[] segments = Array.Empty<MeetingSegment>();
        public ListeningQuestion[] questions = Array.Empty<ListeningQuestion>();
    }
}
