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
            var hasRecording = meeting.content != null && meeting.content.recording != null;
            if (state == MeetingState.Ready && !unlocked)
            {
                connection.text = GameText.Get("QUEST 4  /  LOCKED");
                progress.text = GameText.Get("FINISH THE EARLIER QUESTS TO JOIN");
                body.text = session.CurrentObjective;
                result.text = requiresSeat ? GameText.Get("Sit in the chair when the meeting opens.") :
                    GameText.Get("One meeting and one quiz attempt per session.");
            }
            else if (state == MeetingState.Ready && requiresSeat)
            {
                connection.text = GameText.Get("MEETING READY");
                progress.text = GameText.Get("SIT IN THE CHAIR TO START");
                body.text = GameText.Get("Select the chair at the desk. Stay seated through the briefing and the quiz.");
                result.text = meeting.Feedback;
            }
            else
            {
                connection.text = listening ? (hasRecording ? GameText.Get("MEETING IN PROGRESS") : GameText.Get("LIVE CALL  /  MAYA IS SPEAKING")) :
                    state == MeetingState.Complete ?
                    GameText.Get("CALL COMPLETE  /  THANK YOU") : GameText.Get("SIMULATED CALL  /  AUDIO + CAPTIONS");
                progress.text = listening ? GameText.Format("LISTENING  {0:P0}", meeting.ListeningProgress) : answering ?
                    GameText.Format("QUESTION {0} / {1}", meeting.QuestionIndex + 1, meeting.content.questions.Length) :
                    GameText.Get("LISTEN FIRST. ANSWER ONCE AT THE END.");
                if (requiresSeat && (listening || answering)) progress.text += "  /  " + GameText.Get("STAY SEATED");
                if (answering) body.text = GameText.Get(meeting.CurrentQuestion.prompt);
                else if (state == MeetingState.Complete) body.text = hasRecording ? GameText.Get("Thanks for listening.") :
                    GameText.Get("Thanks for listening.\nSee you at the neighbourhood event!");
                else body.text = meeting.settings.showCaptions || state == MeetingState.Ready ? meeting.CurrentCaption :
                    hasRecording ? GameText.Get("The questions open when the recording ends.") :
                    GameText.Get("Listen to Maya's briefing. The questions unlock when the call ends.");
                result.text = meeting.Feedback;
            }
            if (begin != null)
            {
                begin.gameObject.SetActive(!requiresSeat && state == MeetingState.Ready);
                begin.interactable = unlocked && !requiresSeat && state == MeetingState.Ready;
                beginLabel.text = unlocked ? GameText.Get("JOIN MEETING  /  ONE ATTEMPT") : GameText.Get("LOCKED  /  COMPLETE QUESTS 1-3");
            }
            if (retry != null) retry.gameObject.SetActive(false);
            if (sit != null) sit.gameObject.SetActive(requiresSeat && unlocked && state == MeetingState.Ready);
            if (leaveSeat != null) leaveSeat.gameObject.SetActive(seated && (listening || answering));
            for (var i = 0; i < options.Length; i++)
            {
                options[i].gameObject.SetActive(answering && (!requiresSeat || seated));
                if (answering) optionLabels[i].text = $"{i + 1}   {GameText.Get(meeting.CurrentQuestion.answers[i])}";
            }
        }
    }
}
