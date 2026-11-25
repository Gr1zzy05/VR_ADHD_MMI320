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
        public MeetingSeat seat;
        public MeetingState State { get; private set; } = MeetingState.Ready;
        public bool Completed => State == MeetingState.Complete;
        public bool Attended { get; private set; }
        public int QuestionIndex { get; private set; }
        public int CorrectAnswers { get; private set; }
        public bool Passed => Completed && CorrectAnswers >= RequiredScore;
        public float ListeningProgress { get; private set; }
        public string CurrentCaption => State switch
        {
            MeetingState.Listening => GameText.Get(caption),
            MeetingState.Quiz or MeetingState.Complete => GameText.Get("The briefing has ended. What do you remember?"),
            _ => GameText.Get("Join the meeting, listen to the full briefing, then answer three questions.")
        };
        public string Feedback => Completed ? GameText.Format("{0}/{1} correct. Your quiz is complete.", CorrectAnswers, content.questions.Length) :
            leftSeat ? GameText.Get("You left the chair. Sit again to restart the briefing.") : string.Empty;
        public ListeningQuestion CurrentQuestion => State == MeetingState.Quiz && QuestionIndex < content.questions.Length ? content.questions[QuestionIndex] : null;
        public int RequiredScore => Mathf.Clamp(settings.correctAnswersRequired, 1, content.questions.Length);
        AudioSource voice;
        AudioClip originalClip;
        float originalVolume;
        bool originalLoop;
        Coroutine listening;
        string caption;
        bool leftSeat;

        void Awake()
        {
            voice = GetComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0;
            originalClip = voice.clip;
            originalVolume = voice.volume;
            originalLoop = voice.loop;
        }

        public void Begin()
        {
            if (!CanStart()) return;
            if (seat != null && !seat.IsPlayerInSeat)
            {
                HomeSession.Instance?.Notify(GameText.Get("Select the chair at the meeting desk to sit and start listening."));
                return;
            }

            listening = StartCoroutine(Listen());
        }

        internal bool CanStart()
        {
            if (State != MeetingState.Ready || !isActiveAndEnabled) return false;
            if (HomeSession.Instance != null && !HomeSession.Instance.CanPerform(HomeQuest.Meeting, true)) return false;
            if (content == null || content.questions.Length == 0 ||
                (content.recording != null ? content.captions.Length == 0 : content.segments.Length == 0))
            {
                HomeSession.Instance?.Notify(GameText.Get("Add meeting segments and quiz questions to the meeting content asset."));
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
            leftSeat = false;
            ListeningProgress = 0;
            caption = string.Empty;
            if (content.recording != null) yield return ListenToRecording();
            else yield return ListenToSegments();

            StopVoice();
            listening = null;
            Attended = true;
            ListeningProgress = 1;
            State = MeetingState.Quiz;
            HomeSession.Instance?.Notify(seat != null ? GameText.Get("Stay seated and answer the quiz on the screen.") :
                GameText.Get("Meeting finished. Return to the screen for the listening quiz."), true);
        }

        IEnumerator ListenToRecording()
        {
            var recording = content.recording;
            var captions = content.captions;
            voice.clip = recording;
            voice.loop = false;
            voice.volume = originalVolume * settings.meetingVolume;
            voice.Play();

            var cueIndex = 0;
            while (voice.isPlaying)
            {
                var seconds = (float)voice.timeSamples / recording.frequency;
                while (cueIndex < captions.Length && seconds >= captions[cueIndex].endTime) cueIndex++;
                caption = cueIndex < captions.Length && seconds >= captions[cueIndex].startTime
                    ? captions[cueIndex].text
                    : string.Empty;
                ListeningProgress = Mathf.Clamp01(seconds / recording.length);
                yield return null;
            }
        }

        IEnumerator ListenToSegments()
        {
            var total = 0f;
            foreach (var segment in content.segments) total += segment.Duration;
            var elapsed = 0f;
            foreach (var segment in content.segments)
            {
                caption = segment.speaker + ": " + segment.transcript;
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
            HomeSession.Instance?.Notify(GameText.Get("Meeting complete. Preparing your results."), true);
        }

        internal void SeatVacated()
        {
            if (State != MeetingState.Listening && State != MeetingState.Quiz) return;
            ResetInProgressMeeting();
            leftSeat = true;
            HomeSession.Instance?.Notify(Feedback);
        }

        void ResetInProgressMeeting()
        {
            if (listening != null) StopCoroutine(listening);
            listening = null;
            StopVoice();
            State = MeetingState.Ready;
            Attended = false;
            ListeningProgress = 0;
            QuestionIndex = 0;
            CorrectAnswers = 0;
            leftSeat = false;
            caption = null;
        }

        void StopVoice()
        {
            if (voice == null) return;
            voice.Stop();
            if (content == null || content.recording == null) return;
            voice.clip = originalClip;
            voice.volume = originalVolume;
            voice.loop = originalLoop;
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
