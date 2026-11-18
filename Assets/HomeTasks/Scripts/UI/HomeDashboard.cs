using TMPro;
using UnityEngine;

namespace HomeTasks
{
    public sealed class HomeDashboard : MonoBehaviour
    {
        public HomeSession session;
        public TMP_Text status;
        public TMP_Text timer;
        public TMP_Text[] counts;
        public UnityEngine.UI.Image[] bars;
        public TMP_Text footer;
        float nextUpdate;

        void Update()
        {
            if (session == null) session = HomeSession.Instance;
            if (session == null || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.12f;
            var meeting = session.meeting;
            status.text = session.Finished ? "HOME COMPLETE" : $"QUEST {(int)session.CurrentQuest + 1} OF 4  /  {session.CurrentQuestName.ToUpperInvariant()}";
            var seconds = Mathf.FloorToInt(session.Elapsed);
            timer.text = $"YOUR PACE  {seconds / 60:00}:{seconds % 60:00}";
            counts[0].text = session.QuestStatus(HomeQuest.Washing, $"{session.Washed} / {session.dishes.Count}");
            counts[1].text = session.QuestStatus(HomeQuest.Organizing, $"{session.Organized} / {session.dishes.Count}");
            counts[2].text = session.QuestStatus(HomeQuest.Trash, $"{session.Disposed} / {session.rubbish.Count}");
            counts[3].text = session.QuestStatus(HomeQuest.Meeting, meeting.State == MeetingState.Listening ? "LIVE" : meeting.Attended ? "QUIZ" : "READY");
            var values = new[] { session.Washed / (float)Mathf.Max(1, session.dishes.Count), session.Organized / (float)Mathf.Max(1, session.dishes.Count), session.Disposed / (float)Mathf.Max(1, session.rubbish.Count), meeting.Completed ? 1 : meeting.Attended ? 0.8f : meeting.ListeningProgress * 0.75f };
            for (var i = 0; i < bars.Length; i++)
            {
                // A rect width works without relying on an Image sprite for fill geometry.
                bars[i].rectTransform.anchorMax = new Vector2(values[i], 1);
            }
            footer.text = !string.IsNullOrEmpty(session.Toast) ? session.Toast : session.CurrentObjective;
        }
    }
}
