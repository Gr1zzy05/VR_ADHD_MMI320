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
            var session = HomeSession.Instance;
            var unlocked = session == null || session.IsQuestActive(HomeQuest.Meeting);
            if (state == MeetingState.Ready && !unlocked)
            {
                connection.text = "QUEST 4  /  LOCKED";
                progress.text = "FINISH THE EARLIER QUESTS TO JOIN";
                body.text = session.CurrentObjective;
                result.text = "One meeting and one quiz attempt per session.";
            }
            else
            {
                connection.text = state == MeetingState.Listening ? "LIVE CALL  /  MAYA IS SPEAKING" : state == MeetingState.Complete ? "CALL COMPLETE  /  THANK YOU" : "SIMULATED CALL  /  AUDIO + CAPTIONS";
                progress.text = state == MeetingState.Listening ? $"LISTENING  {meeting.ListeningProgress:P0}" : state == MeetingState.Quiz ? $"QUESTION {meeting.QuestionIndex + 1} / {meeting.content.questions.Length}  /  ONE ATTEMPT" : "LISTEN FIRST. ANSWER ONCE AT THE END.";
                if (state == MeetingState.Quiz) body.text = meeting.CurrentQuestion.prompt;
                else if (state == MeetingState.Complete) body.text = "Thanks for listening.\nSee you at the neighbourhood event!";
                else body.text = meeting.settings.showCaptions || state == MeetingState.Ready ? meeting.CurrentCaption : "Listen to Maya's briefing. The questions unlock when the call ends.";
                result.text = meeting.Feedback;
            }
            begin.gameObject.SetActive(state == MeetingState.Ready);
            begin.interactable = unlocked && state == MeetingState.Ready;
            beginLabel.text = unlocked ? "JOIN MEETING  /  ONE ATTEMPT" : "LOCKED  /  COMPLETE QUESTS 1-3";
            if (retry != null) retry.gameObject.SetActive(false);
            for (var i = 0; i < options.Length; i++)
            {
                options[i].gameObject.SetActive(state == MeetingState.Quiz);
                if (state == MeetingState.Quiz) optionLabels[i].text = $"{i + 1}   {meeting.CurrentQuestion.answers[i]}";
            }
        }
    }
}
