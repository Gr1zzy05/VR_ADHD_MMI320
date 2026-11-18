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
        public UnityEngine.UI.Button sit;
        public UnityEngine.UI.Button leaveSeat;
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
            var requiresSeat = meeting.seat != null;
            var seated = requiresSeat && meeting.seat.IsPlayerInSeat;
            var listening = state == MeetingState.Listening;
            var answering = state == MeetingState.Quiz;
            if (state == MeetingState.Ready && !unlocked)
            {
                connection.text = "QUEST 4  /  LOCKED";
                progress.text = "FINISH THE EARLIER QUESTS TO JOIN";
                body.text = session.CurrentObjective;
                result.text = requiresSeat ? "Sit in the chair when the meeting opens." : "One meeting and one quiz attempt per session.";
            }
            else if (state == MeetingState.Ready && requiresSeat)
            {
                connection.text = "MEETING READY";
                progress.text = "SIT IN THE CHAIR TO START";
                body.text = "Select the chair at the desk. Stay seated through the briefing and the quiz.";
                result.text = meeting.Feedback;
            }
            else
            {
                connection.text = listening ? "LIVE CALL  /  MAYA IS SPEAKING" : state == MeetingState.Complete ? "CALL COMPLETE  /  THANK YOU" : "SIMULATED CALL  /  AUDIO + CAPTIONS";
                progress.text = listening ? $"LISTENING  {meeting.ListeningProgress:P0}" : answering ? $"QUESTION {meeting.QuestionIndex + 1} / {meeting.content.questions.Length}" : "LISTEN FIRST. ANSWER ONCE AT THE END.";
                if (requiresSeat && (listening || answering)) progress.text += "  /  STAY SEATED";
                if (answering) body.text = meeting.CurrentQuestion.prompt;
                else if (state == MeetingState.Complete) body.text = "Thanks for listening.\nSee you at the neighbourhood event!";
                else body.text = meeting.settings.showCaptions || state == MeetingState.Ready ? meeting.CurrentCaption : "Listen to Maya's briefing. The questions unlock when the call ends.";
                result.text = meeting.Feedback;
            }
            if (begin != null)
            {
                begin.gameObject.SetActive(!requiresSeat && state == MeetingState.Ready);
                begin.interactable = unlocked && !requiresSeat && state == MeetingState.Ready;
                beginLabel.text = unlocked ? "JOIN MEETING  /  ONE ATTEMPT" : "LOCKED  /  COMPLETE QUESTS 1-3";
            }
            if (retry != null) retry.gameObject.SetActive(false);
            if (sit != null) sit.gameObject.SetActive(requiresSeat && unlocked && state == MeetingState.Ready);
            if (leaveSeat != null) leaveSeat.gameObject.SetActive(seated && (listening || answering));
            for (var i = 0; i < options.Length; i++)
            {
                options[i].gameObject.SetActive(answering && (!requiresSeat || seated));
                if (answering) optionLabels[i].text = $"{i + 1}   {meeting.CurrentQuestion.answers[i]}";
            }
        }
    }
}
