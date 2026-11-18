using System.Collections;
using UnityEngine;

namespace HomeTasks
{
    public enum MeetingState { Ready, Listening, Quiz, Review, Complete }

    [RequireComponent(typeof(AudioSource))]
    public sealed class MeetingController : MonoBehaviour
    {
        const string RestartMessage = "You left the chair. Sit again to restart the briefing.";

        public MeetingContent content;
        public HomeTaskSettings settings;
        public MeetingSeat seat;
        public MeetingState State { get; private set; } = MeetingState.Ready;
        public bool Completed => State == MeetingState.Complete;
        public bool Attended { get; private set; }
        public int QuestionIndex { get; private set; }
        public int CorrectAnswers { get; private set; }
        public bool Passed => Completed && CorrectAnswers >= RequiredScore;
        public float ListeningProgress { get; private set; }
        public string CurrentCaption { get; private set; } = "Join the meeting, listen to the full briefing, then answer three questions.";
        public string Feedback { get; private set; } = string.Empty;
        public ListeningQuestion CurrentQuestion => State == MeetingState.Quiz && QuestionIndex < content.questions.Length ? content.questions[QuestionIndex] : null;
        public int RequiredScore => Mathf.Clamp(settings.correctAnswersRequired, 1, content.questions.Length);
        AudioSource voice;
        Coroutine listening;

        void Awake()
        {
            voice = GetComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0;
        }

        public void Begin()
        {
            if (!CanStart()) return;
            if (seat != null && !seat.IsPlayerInSeat)
            {
                HomeSession.Instance?.Notify("Select the chair at the meeting desk to sit and start listening.");
                return;
            }

            listening = StartCoroutine(Listen());
        }

        internal bool CanStart()
        {
            if (State != MeetingState.Ready || !isActiveAndEnabled) return false;
            if (HomeSession.Instance != null && !HomeSession.Instance.CanPerform(HomeQuest.Meeting, true)) return false;
            if (content == null || content.segments.Length == 0 || content.questions.Length == 0)
            {
                HomeSession.Instance?.Notify("Add meeting segments and quiz questions to the meeting content asset.");
                return false;
            }

            return true;
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
            listening = null;
            Attended = true;
            ListeningProgress = 1;
            CurrentCaption = "The briefing has ended. What do you remember?";
            State = MeetingState.Quiz;
            HomeSession.Instance?.Notify(seat != null ? "Stay seated and answer the quiz on the screen." : "Meeting finished. Return to the screen for the listening quiz.", true);
        }

        public void Answer(int option)
        {
            if (HomeSession.Instance != null && !HomeSession.Instance.IsQuestActive(HomeQuest.Meeting)) return;
            var question = CurrentQuestion;
            if (!Attended || question == null || option < 0 || option >= question.answers.Length) return;
            if (seat != null && !seat.IsPlayerInSeat)
            {
                SeatVacated();
                return;
            }
            if (option == question.correctAnswer) CorrectAnswers++;
            QuestionIndex++;
            if (QuestionIndex < content.questions.Length) return;
            State = MeetingState.Complete;
            Feedback = $"{CorrectAnswers}/{content.questions.Length} correct. Your quiz is complete.";
            HomeSession.Instance?.Notify("Meeting complete. Preparing your results.", true);
        }

        internal void SeatVacated()
        {
            if (State != MeetingState.Listening && State != MeetingState.Quiz) return;
            ResetInProgressMeeting();
            Feedback = RestartMessage;
            HomeSession.Instance?.Notify(RestartMessage);
        }

        void ResetInProgressMeeting()
        {
            if (listening != null) StopCoroutine(listening);
            listening = null;
            if (voice != null) voice.Stop();
            State = MeetingState.Ready;
            Attended = false;
            ListeningProgress = 0;
            QuestionIndex = 0;
            CorrectAnswers = 0;
            Feedback = string.Empty;
            CurrentCaption = "Join the meeting, listen to the full briefing, then answer three questions.";
        }

        public void RetryQuiz()
        {
            // Legacy button callbacks never skip the briefing.
        }

        public void AnswerFirst() => Answer(0);
        public void AnswerSecond() => Answer(1);
        public void AnswerThird() => Answer(2);
        void OnDisable()
        {
            if (State == MeetingState.Listening || State == MeetingState.Quiz) ResetInProgressMeeting();
        }
    }
}
