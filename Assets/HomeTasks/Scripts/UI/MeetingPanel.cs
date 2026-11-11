using TMPro;
using UnityEngine;

namespace HomeTasks
{
    public sealed class MeetingPanel : MonoBehaviour
    {
        public MeetingController meeting;
        public TMP_Text connection;
        public TMP_Text body;
        public TMP_Text progress;
        public TMP_Text result;
        public UnityEngine.UI.Button begin;
        public TMP_Text beginLabel;
        public UnityEngine.UI.Button retry;
        public UnityEngine.UI.Button[] options;
        public TMP_Text[] optionLabels;
        float nextUpdate;

        void Update()
        {
            if (meeting == null || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.08f;
            var state = meeting.State;
            connection.text = state == MeetingState.Listening ? "LIVE CALL  /  MAYA IS SPEAKING" : state == MeetingState.Complete ? "CALL COMPLETE  /  THANK YOU" : "SIMULATED CALL  /  AUDIO + CAPTIONS";
            progress.text = state == MeetingState.Listening ? $"LISTENING  {meeting.ListeningProgress:P0}" : state == MeetingState.Quiz ? $"QUESTION {meeting.QuestionIndex + 1} / {meeting.content.questions.Length}" : "LISTEN FIRST. ANSWER AT THE END.";
            if (state == MeetingState.Quiz) body.text = meeting.CurrentQuestion.prompt;
            else if (state == MeetingState.Complete) body.text = "Thanks for listening.\nSee you at the neighbourhood event!";
            else if (state == MeetingState.Review) body.text = "You can listen again or take another try. There is no time limit.";
            else body.text = meeting.settings.showCaptions || state == MeetingState.Ready ? meeting.CurrentCaption : "Listen to Maya's briefing. The questions unlock when the call ends.";
            result.text = meeting.Feedback;
            begin.gameObject.SetActive(state != MeetingState.Listening && state != MeetingState.Complete);
            beginLabel.text = state == MeetingState.Ready ? "JOIN MEETING" : "REPLAY BRIEFING";
            retry.gameObject.SetActive(state == MeetingState.Review);
            for (var i = 0; i < options.Length; i++)
            {
                options[i].gameObject.SetActive(state == MeetingState.Quiz);
                if (state == MeetingState.Quiz) optionLabels[i].text = $"{i + 1}   {meeting.CurrentQuestion.answers[i]}";
            }
        }
    }
}
