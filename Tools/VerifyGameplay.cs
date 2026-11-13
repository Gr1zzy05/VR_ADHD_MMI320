// Run from a fresh Play session using the Unity Pipeline CLI (allow 240 seconds).
// Exercises the physical stations, ordered quests, one attempt, results, and restart.
using HomeTasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

var checks = new List<string>();
Directory.CreateDirectory("TestResults");
File.WriteAllText("TestResults/GameplayVerification.txt", string.Empty);
void Check(bool condition, string description)
{
    if (!condition) throw new Exception("FAILED: " + description);
    checks.Add("PASS: " + description);
    File.AppendAllText("TestResults/GameplayVerification.txt", checks.Last() + "\n");
}

for (var attempt = 0; attempt < 2; attempt++)
{
    var s = HomeSession.Instance;
    Check(s != null && Application.isPlaying, "Playable home is running");
    var initialFrame = Time.frameCount;
    await Wait.Until(() => Time.frameCount > initialFrame + 3, timeoutMs: 10000);
    Check(s.CurrentQuest == HomeQuest.Washing && HomeSession.LastResult == null, "Fresh session starts with washing and clears the previous results");
    Check(s.dishes.Count == 6 && s.rubbish.Count == 3, "Six dishes and three bags are registered");
    Check(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 1, "Exactly one UI event system");
    var m = s.meeting;
    var meetingPanel = Object.FindFirstObjectByType<MeetingPanel>();
    m.Begin();
    m.Answer(0);
    m.RetryQuiz();
    Check(m.State == MeetingState.Ready && m.QuestionIndex == 0 && !m.Completed, "Joining and answering the meeting are blocked before quest 4");
    Check(!meetingPanel.begin.interactable && (meetingPanel.retry == null || !meetingPanel.retry.gameObject.activeSelf), "The locked meeting cannot be joined or retried through its UI");
    Check(s.rubbish.All(b => !b.CanInteract && !b.Grab.enabled), "Future-quest bags cannot be grabbed in desktop or VR");
    var board = Object.FindFirstObjectByType<HomeDashboard>();
    Check(board.counts.Skip(1).All(t => t.text == "LOCKED"), "The wall board identifies the three locked quests");
    var station = Object.FindFirstObjectByType<WashStation>();
    var racks = Object.FindObjectsByType<DishRack>(FindObjectsSortMode.None);
    var bin = Object.FindFirstObjectByType<TrashBin>();
    var bag = s.rubbish[0];
    bag.SetDesktopHeld(true);
    Check(!bag.DesktopHeld, "Direct desktop grab cannot select a locked bag");
    bag.transform.position = bin.volume.bounds.center;
    Physics.SyncTransforms();
    Check(!bin.TryDeposit(bag) && !bag.DisposeAt(bin.depositPoint) && s.Disposed == 0, "Station and item APIs reject early trash disposal");
    bag.ReturnHome();
    var first = s.dishes[0];
    var firstRack = racks.First(r => r.accepts == first.kind);
    Check(!firstRack.TryStore(first), "Sorting is blocked while washing is active");
    station.sponge.SetDesktopHeld(true);
    Check(!station.Scrub(first, 10) && !first.IsClean, "Water-off washing cannot advance a dish");
    station.ToggleWater();
    Check(station.WaterOn, "The tap can be turned on during the washing quest");
    first.transform.position = firstRack.volume.bounds.center;
    Physics.SyncTransforms();
    Check(!station.Scrub(first, 10) && !first.IsClean, "A dish outside the sink cannot be washed");
    station.sponge.DesktopScrubbing = true;
    foreach (var dish in s.dishes)
    {
        dish.SetDesktopHeld(true);
        dish.transform.position = station.washVolume.transform.position + Vector3.down * 0.2f;
        Physics.SyncTransforms();
        if (attempt == 0)
        {
            await Wait.Until(() =>
            {
                station.sponge.transform.position = dish.transform.position + new Vector3(Mathf.Sin(Time.time * 16) * 0.045f, dish.Shape.bounds.size.y + 0.025f, 0);
                Physics.SyncTransforms();
                return dish.IsClean;
            }, timeoutMs: 8000);
        }
        else
        {
            station.sponge.transform.position = dish.Shape.bounds.center;
            Physics.SyncTransforms();
            Check(station.Scrub(dish, s.settings.scrubSecondsPerDish), "Second session uses the real sink contact API");
        }
        Check(dish.IsClean && dish.dirtMarks.All(mark => !mark.enabled), dish.name + " becomes clean in the running sink");
        dish.SetDesktopHeld(false);
        if (!s.WashingComplete)
        {
            var rack = racks.First(r => r.accepts == dish.kind);
            dish.transform.position = rack.volume.bounds.center;
            Physics.SyncTransforms();
            Check(!rack.TryStore(dish) && !dish.Store(rack.slots[0]) && !dish.IsStored, "Clean dishes cannot be organized until every dish is washed");
            dish.ReturnHome();
            m.Begin();
            Check(m.State == MeetingState.Ready, "Partial washing does not unlock the meeting");
        }
    }
    Check(s.WashingComplete && !s.SortingComplete && s.CurrentQuest == HomeQuest.Organizing, "Washing all dishes unlocks only organizing");
    station.sponge.DesktopScrubbing = false;
    station.sponge.SetDesktopHeld(false);
    station.sponge.ReturnHome();
    initialFrame = Time.frameCount;
    await Wait.Until(() => Time.frameCount > initialFrame + 2, timeoutMs: 5000);
    Check(!station.WaterOn && !station.sponge.CanInteract && !station.sponge.Grab.enabled, "The completed washing station turns off and locks its sponge");
    foreach (var dish in s.dishes)
    {
        var rack = racks.First(r => r.accepts == dish.kind);
        var wrongRack = racks.First(r => r.accepts != dish.kind);
        dish.transform.position = wrongRack.volume.bounds.center;
        Physics.SyncTransforms();
        Check(!wrongRack.TryStore(dish), dish.kind + " is rejected by the wrong shelf");
        dish.SetDesktopHeld(true);
        dish.transform.position = rack.volume.bounds.center;
        Physics.SyncTransforms();
        Check(!rack.TryStore(dish), dish.kind + " cannot snap out of a player's hand");
        dish.SetDesktopHeld(false);
        Check(rack.TryStore(dish), dish.kind + " is accepted on its matching shelf after release");
        var count = rack.StoredCount;
        Check(!rack.TryStore(dish) && rack.StoredCount == count, "Repeated contact cannot store the same dish twice");
        if (!s.SortingComplete)
        {
            m.Begin();
            Check(m.State == MeetingState.Ready && !bag.DisposeAt(bin.depositPoint), "Partial organizing does not unlock trash or the meeting");
        }
    }
    Check(s.CurrentQuest == HomeQuest.Trash && s.SortingComplete, "Organizing every dish unlocks only trash");
    foreach (var item in s.rubbish)
    {
        Check(!bin.TryDeposit(item), "An indoor bag is not counted as disposed");
        item.SetDesktopHeld(true);
        item.transform.position = bin.volume.bounds.center;
        Physics.SyncTransforms();
        Check(!bin.TryDeposit(item), "A held bag is not taken from the player's hand");
        item.SetDesktopHeld(false);
        Check(bin.TryDeposit(item) && item.IsDisposed && !item.gameObject.activeSelf, "A released bag is deposited in the outdoor bin");
        Check(!bin.TryDeposit(item), "Repeated contact cannot dispose of the same bag twice");
        if (!s.TrashComplete)
        {
            m.Begin();
            Check(m.State == MeetingState.Ready, "Partial trash disposal does not unlock the meeting");
        }
    }
    Check(s.Disposed == 3 && s.CurrentQuest == HomeQuest.Meeting && !s.AllComplete, "All bags must be disposed of before the meeting unlocks");
    if (attempt == 1)
    {
        // The first session verifies all original audio. Use an isolated short content
        // copy for the second score path without changing the saved asset.
        var shortContent = ScriptableObject.CreateInstance<MeetingContent>();
        shortContent.questions = m.content.questions;
        shortContent.segments = new[] { new MeetingSegment { minimumDuration = 0.25f, transcript = "Test briefing" } };
        m.content = shortContent;
    }
    m.Begin();
    Check(m.State == MeetingState.Listening, "Quest 4 starts the meeting");
    m.Answer(0);
    m.RetryQuiz();
    Check(m.State == MeetingState.Listening && m.QuestionIndex == 0 && !m.Completed, "Early answers and retry cannot bypass listening");
    var progress = m.ListeningProgress;
    m.Begin();
    Check(m.ListeningProgress >= progress, "Repeated join does not restart the active briefing");
    await Wait.Until(() => m.State == MeetingState.Quiz, timeoutMs: 90000);
    Check(m.Attended && m.ListeningProgress == 1, "Quiz unlocks after the entire briefing");
    m.Begin();
    Check(m.State == MeetingState.Quiz && m.ListeningProgress == 1, "The completed briefing cannot be replayed");
    m.Answer(-1);
    m.Answer(99);
    Check(m.QuestionIndex == 0, "Invalid options do not consume an answer");
    m.Answer(attempt == 0 ? (m.CurrentQuestion.correctAnswer + 1) % 3 : m.CurrentQuestion.correctAnswer);
    m.RetryQuiz();
    m.Begin();
    Check(m.State == MeetingState.Quiz && m.QuestionIndex == 1, "Neither retry nor replay can erase a recorded answer");
    while (m.State == MeetingState.Quiz)
        m.Answer(attempt == 0 ? (m.CurrentQuestion.correctAnswer + 1) % 3 : m.CurrentQuestion.correctAnswer);
    var expectedScore = attempt == 0 ? 0 : m.content.questions.Length;
    Check(m.Completed && m.CorrectAnswers == expectedScore && m.Passed == (attempt == 1), "A single quiz attempt completes the quest and preserves the correct pass result");
    m.Begin();
    m.RetryQuiz();
    m.Answer(0);
    Check(m.Completed && m.CorrectAnswers == expectedScore, "Completed attempts cannot be restarted or rescored");
    await Wait.Until(() => s.Finished, timeoutMs: 5000);
    var snapshot = HomeSession.LastResult;
    Check(s.AllComplete && s.CompletedTasks == 4 && snapshot != null, "All four quests finish the session and capture a results snapshot");
    Check(Object.FindObjectsByType<CanvasGroup>(FindObjectsSortMode.None).Any(g => g.name == "Scene fade"), "Completion starts the fade before changing scene");
    var elapsed = s.Elapsed;
    Check(snapshot.Elapsed == elapsed, "The result uses the stopped session timer");
    await Wait.Until(() => SceneManager.GetActiveScene().path == HomeSession.ResultsScenePath, timeoutMs: 10000);
    var resultsPanel = Object.FindFirstObjectByType<HomeResultsPanel>();
    Check(resultsPanel != null && HomeSession.Instance == null && HomeSession.LastResult == snapshot, "The dedicated results scene retains the snapshot after the home is unloaded");
    await Wait.Until(() => resultsPanel.restart.interactable, timeoutMs: 10000);
    Check(resultsPanel.score.text.Contains($"{expectedScore} / {snapshot.QuestionCount}") && resultsPanel.chores.text.Contains("6/6") && resultsPanel.chores.text.Contains("3/3"), "Results show the actual quiz score and all chore totals");
    Check(snapshot.QuizPassed == (attempt == 1), "A below-target score still reaches results and is recorded accurately");
    Check(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 1, "Results have one working UI event system");
    Check(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.enabled) == 1, "Results have one active audio listener");
    if (attempt == 1)
    {
        Directory.CreateDirectory("Screenshots");
        ScreenCapture.CaptureScreenshot("Screenshots/OrderedQuests-Results.png");
        initialFrame = Time.frameCount;
        await Wait.Until(() => Time.frameCount > initialFrame + 2, timeoutMs: 5000);
    }
    resultsPanel.restart.onClick.Invoke();
    resultsPanel.restart.onClick.Invoke();
    await Wait.Until(() => HomeSession.Instance != null && HomeSession.Instance != s, timeoutMs: 10000);
    Check(HomeSession.LastResult == null && HomeSession.Instance.CurrentQuest == HomeQuest.Washing && HomeSession.Instance.meeting.State == MeetingState.Ready, "The results restart button starts exactly one fresh ordered session with a new meeting attempt");
}
return new { passed = checks.Count, report = "TestResults/GameplayVerification.txt" };
