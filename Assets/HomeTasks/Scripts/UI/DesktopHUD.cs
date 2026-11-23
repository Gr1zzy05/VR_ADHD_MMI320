using TMPro;
using UnityEngine;

namespace HomeTasks
{
    public sealed class DesktopHUD : MonoBehaviour
    {
        public HomeSession session;
        public DesktopPlayer player;
        public TMP_Text taskSummary;
        public TMP_Text prompt;
        public TMP_Text toast;
        public GameObject crosshair;
        float nextUpdate;

        void Update()
        {
            if (session == null) session = HomeSession.Instance;
            if (player == null) player = GetComponentInParent<DesktopPlayer>();
            if (session == null || player == null) return;
            crosshair.SetActive(player.MouseCaptured);
            prompt.text = player.Prompt;
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.1f;
            var meetingStatus = session.meeting.State switch
            {
                MeetingState.Listening => GameText.Get("Listening"),
                MeetingState.Quiz => GameText.Get("Quiz"),
                MeetingState.Complete => GameText.Get("Complete"),
                _ => GameText.Get("Ready")
            };
            taskSummary.text = GameText.Format("<b>{0}</b>\n\n01 Wash    {1}\n02 Organize    {2}\n03 Trash    {3}\n04 Meeting    {4}",
                session.CurrentQuestName.ToUpperInvariant(),
                session.QuestStatus(HomeQuest.Washing, $"{session.Washed}/{session.dishes.Count}"),
                session.QuestStatus(HomeQuest.Organizing, $"{session.Organized}/{session.dishes.Count}"),
                session.QuestStatus(HomeQuest.Trash, $"{session.Disposed}/{session.rubbish.Count}"),
                session.QuestStatus(HomeQuest.Meeting, meetingStatus));
            toast.text = session.Toast;
        }
    }
}
