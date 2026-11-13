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
            crosshair.SetActive(player.MouseCaptured);
            prompt.text = player.Prompt;
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.1f;
            taskSummary.text = $"<b>{session.CurrentQuestName.ToUpperInvariant()}</b>\n\n01 Wash    {session.QuestStatus(HomeQuest.Washing, $"{session.Washed}/{session.dishes.Count}")}\n02 Organize    {session.QuestStatus(HomeQuest.Organizing, $"{session.Organized}/{session.dishes.Count}")}\n03 Trash    {session.QuestStatus(HomeQuest.Trash, $"{session.Disposed}/{session.rubbish.Count}")}\n04 Meeting    {session.QuestStatus(HomeQuest.Meeting, session.meeting.State.ToString())}";
            toast.text = session.Toast;
        }
    }
}
