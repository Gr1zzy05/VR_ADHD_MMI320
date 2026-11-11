// Run in a live Play session with: unity command script eval --file Tools/VerifyGameplay.cs
// Exercises the real scene, physical task volumes, washing updates, and full meeting duration.
using HomeTasks;
using UnityEngine;
using System.IO;

var checks = new List<string>();
void Check(bool condition, string description)
{
    if (!condition) throw new Exception("FAILED: " + description);
    checks.Add("PASS: " + description);
}

var s = HomeSession.Instance;
Check(s != null && Application.isPlaying, "Playable scene is running");
var initialFrame = Time.frameCount;
await Wait.Until(() => Time.frameCount > initialFrame + 3, timeoutMs: 10000);
Check(true, "Player loop advances frames");
Check(s.dishes.Count == 6 && s.rubbish.Count == 3, "Six dishes and three bags are registered");
Check(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 1, "Exactly one UI event system");
var m = s.meeting;
if (m.State != MeetingState.Listening) m.Begin();
m.Answer(0);
m.RetryQuiz();
Check(m.State == MeetingState.Listening && m.QuestionIndex == 0 && !m.Completed, "Answers and retry are locked during the briefing");
var progress = m.ListeningProgress;
m.Begin();
Check(m.ListeningProgress >= progress, "Repeated join input does not restart an active briefing");

var station = Object.FindFirstObjectByType<WashStation>();
var racks = Object.FindObjectsByType<DishRack>(FindObjectsSortMode.None);
var first = s.dishes[0];
var firstRack = racks.First(r => r.accepts == first.kind);
first.transform.position = firstRack.volume.bounds.center;
Physics.SyncTransforms();
Check(!firstRack.TryStore(first), "Dirty dishes are rejected by the sorting shelves");
station.sponge.SetDesktopHeld(true);
Check(!station.Scrub(first, 10) && !first.IsClean, "Water-off washing cannot advance a dish");
var tap = station.GetComponentInChildren<HomeAction>();
tap.Use();
Check(station.WaterOn, "The tap action is wired to running water");
Check(!station.Scrub(first, 10) && !first.IsClean, "A dish outside the sink cannot be washed");
// Emulate the desktop player's held E input. A polled test cannot supply a tracked
// controller pose every rendered frame, but uses the same station update/contact path.
station.sponge.DesktopScrubbing = true;

foreach (var dish in s.dishes)
{
    dish.transform.position = station.washVolume.transform.position + Vector3.down * 0.2f;
    dish.Body.linearVelocity = Vector3.zero;
    Physics.SyncTransforms();
    var settle = Time.time + 0.3f;
    await Wait.Until(() => Time.time > settle, timeoutMs: 3000);
    await Wait.Until(() =>
    {
        station.sponge.transform.position = dish.transform.position + new Vector3(Mathf.Sin(Time.time * 16) * 0.045f, dish.Shape.bounds.size.y + 0.025f, 0);
        Physics.SyncTransforms();
        return dish.IsClean;
    }, timeoutMs: 8000);
    Check(dish.IsClean && dish.dirtMarks.All(mark => !mark.enabled), dish.name + " becomes clean from sponge contact and held scrub input in the running sink");
    var rack = racks.First(r => r.accepts == dish.kind);
    var wrongRack = racks.First(r => r.accepts != dish.kind);
    dish.transform.position = wrongRack.volume.bounds.center;
    Physics.SyncTransforms();
    Check(!wrongRack.TryStore(dish), dish.kind + " is rejected by the wrong shelf");
    dish.transform.position = rack.volume.bounds.center;
    dish.SetDesktopHeld(true);
    Physics.SyncTransforms();
    Check(!rack.TryStore(dish), dish.kind + " cannot snap out of a player's hand");
    dish.SetDesktopHeld(false);
    Check(rack.TryStore(dish), dish.kind + " is accepted after release on its matching shelf");
    var count = rack.StoredCount;
    Check(!rack.TryStore(dish) && rack.StoredCount == count, "Repeated shelf contact does not count the same dish twice");
}
station.sponge.DesktopScrubbing = false;
station.sponge.SetDesktopHeld(false);
station.sponge.ReturnHome();
station.ToggleWater();
Check(s.WashingComplete && s.SortingComplete, "Washing and sorting both reach 6/6");
Check(!s.AllComplete, "Session cannot finish while another task is incomplete");

var bin = Object.FindFirstObjectByType<TrashBin>();
Check(bin.transform.position.z < -4, "The disposal station is outside the house");
foreach (var bag in s.rubbish)
{
    Check(!bin.TryDeposit(bag), "An indoor bag is not counted as disposed");
    bag.transform.position = bin.volume.bounds.center;
    bag.SetDesktopHeld(true);
    Physics.SyncTransforms();
    Check(!bin.TryDeposit(bag), "A held bag is not taken from the player's hand");
    bag.SetDesktopHeld(false);
    Check(bin.TryDeposit(bag) && bag.IsDisposed && !bag.gameObject.activeSelf, "Released bag is deposited in the outdoor bin");
    Check(!bin.TryDeposit(bag), "Repeated bin contact does not count a bag twice");
}
Check(s.Disposed == 3 && s.TrashComplete, "Trash task reaches 3/3");
await Wait.Until(() => m.State == MeetingState.Quiz, timeoutMs: 90000);
Check(m.Attended && m.ListeningProgress == 1, "Quiz unlocks after the complete spoken briefing");
foreach (var question in m.content.questions) m.Answer((question.correctAnswer + 1) % 3);
Check(m.State == MeetingState.Review && !m.Completed, "Incorrect answers require a retry and cannot complete the meeting");
m.RetryQuiz();
Check(m.State == MeetingState.Quiz && m.CorrectAnswers == 0 && m.QuestionIndex == 0, "Quiz retry resets only quiz progress");
foreach (var question in m.content.questions) m.Answer(question.correctAnswer);
Check(m.Completed && m.CorrectAnswers == 3, "Three correct answers complete the listening task");
await Wait.Until(() => s.Finished, timeoutMs: 3000);
Check(s.AllComplete && s.CompletedTasks == 4, "All four tasks produce session completion");
Directory.CreateDirectory("TestResults");
File.WriteAllLines("TestResults/GameplayVerification.txt", checks);
return checks.ToArray();
