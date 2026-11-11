using System.Collections;
using UnityEngine;

namespace HomeTasks
{
    public enum MeetingState { Ready, Listening, Quiz, Review, Complete }

    [RequireComponent(typeof(AudioSource))]
    public sealed class MeetingController : MonoBehaviour
    {
        public MeetingContent content;
        public HomeTaskSettings settings;
        public MeetingState State { get; private set; } = MeetingState.Ready;
        public bool Completed => State == MeetingState.Complete;
        public bool Attended { get; private set; }
        public int QuestionIndex { get; private set; }
        public int CorrectAnswers { get; private set; }
        public float ListeningProgress { get; private set; }
        public string CurrentCaption { get; private set; } = "Join the meeting, listen to the full briefing, then answer three questions.";
        public string Feedback { get; private set; } = string.Empty;
        public ListeningQuestion CurrentQuestion => State == MeetingState.Quiz && QuestionIndex < content.questions.Length ? content.questions[QuestionIndex] : null;
        public int RequiredScore => Mathf.Clamp(settings.correctAnswersRequired, 1, content.questions.Length);
        AudioSource voice;

        void Awake()
        {
            voice = GetComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0; // The call remains audible while the player works elsewhere in this map.
        }

        public void Begin()
        {
            if (State == MeetingState.Listening || Completed) return;
            if (content == null || content.segments.Length == 0 || content.questions.Length == 0)
            {
                HomeSession.Instance?.Notify("Add meeting segments and quiz questions to the meeting content asset.");
                return;
            }
            StartCoroutine(Listen());
        }

        IEnumerator Listen()
        {
            State = MeetingState.Listening;
            Attended = false;
            QuestionIndex = 0;
            CorrectAnswers = 0;
            Feedback = string.Empty;
            ListeningProgress = 0;
            var total = 0f;
            foreach (var segment in content.segments) total += segment.Duration;
            var elapsed = 0f;
            foreach (var segment in content.segments)
            {
                CurrentCaption = segment.speaker + ": " + segment.transcript;
                if (segment.voice != null) voice.PlayOneShot(segment.voice, settings.meetingVolume);
                var segmentElapsed = 0f;
                while (segmentElapsed < segment.Duration)
                {
                    yield return null;
                    var step = Mathf.Min(Time.deltaTime, segment.Duration - segmentElapsed);
                    segmentElapsed += step;
                    elapsed += step;
                    ListeningProgress = Mathf.Clamp01(elapsed / total);
                }
            }
            voice.Stop();
            Attended = true;
            ListeningProgress = 1;
            CurrentCaption = "The briefing has ended. What do you remember?";
            State = MeetingState.Quiz;
            HomeSession.Instance?.Notify("Meeting finished. Return to the screen for the listening quiz.", true);
        }

        public void Answer(int option)
        {
            var question = CurrentQuestion;
            if (!Attended || question == null || option < 0 || option >= question.answers.Length) return;
            if (option == question.correctAnswer) CorrectAnswers++;
            QuestionIndex++;
            if (QuestionIndex < content.questions.Length) return;
            if (CorrectAnswers >= RequiredScore)
            {
                State = MeetingState.Complete;
                Feedback = $"{CorrectAnswers}/{content.questions.Length} correct. Meeting task complete.";
                HomeSession.Instance?.Notify("Listening quiz passed. Meeting complete!", true);
            }
            else
            {
                State = MeetingState.Review;
                Feedback = $"{CorrectAnswers}/{content.questions.Length} correct. Aim for {RequiredScore}. Replay the briefing or try the quiz again.";
                HomeSession.Instance?.Notify("You can replay the briefing and try again at your own pace.");
            }
        }

        public void RetryQuiz()
        {
            if (!Attended || State != MeetingState.Review) return;
            CorrectAnswers = 0;
            QuestionIndex = 0;
            Feedback = string.Empty;
            State = MeetingState.Quiz;
        }

        public void AnswerFirst() => Answer(0);
        public void AnswerSecond() => Answer(1);
        public void AnswerThird() => Answer(2);
        void OnDisable() { StopAllCoroutines(); if (voice != null) voice.Stop(); }
    }
}
