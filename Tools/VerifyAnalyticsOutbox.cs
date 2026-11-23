// Synthetic fixtures only. Run Tools/VerifyAnalyticsOutbox.ps1 after opening the Unity project.
using System;
using System.IO;
using System.Linq;
using HomeTasks;
using Newtonsoft.Json.Linq;

internal static class VerifyAnalyticsOutbox
{
    static int count;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS " + message);
        count++;
    }

    static JObject Binding(DateTimeOffset now) => new JObject
    {
        ["endpoint"] = "https://example.invalid/api/submissions",
        ["sessionId"] = Guid.NewGuid().ToString(), ["participantId"] = Guid.NewGuid().ToString(),
        ["token"] = "synthetic-test-credential", ["expiresAt"] = AnalyticsOutbox.Timestamp(now.AddHours(4)),
        ["noticeVersion"] = AnalyticsOutbox.NoticeVersion
    };

    public static void Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "focus-shift-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var input = Path.Combine(root, AnalyticsOutbox.BindingFile);
            var queue = new AnalyticsOutbox(root);
            JObject Sample(JObject credential) => new JObject { ["id"] = (string)credential["sessionId"],
                ["status"] = "interrupted", ["endedAt"] = AnalyticsOutbox.Timestamp(now), ["durationSeconds"] = 0 };

            Check(queue.Begin(Sample, now) == null && !Directory.EnumerateFileSystemEntries(root).Any(),
                "unbound play writes no files");
            var first = Binding(now);
            var second = Binding(now);
            second["participantId"] = (string)first["participantId"];
            File.WriteAllText(input, first.ToString());
            var active = queue.Begin(credential => {
                File.WriteAllText(input, second.ToString());
                return Sample(credential);
            }, now);
            Check((string)active["sessionId"] == (string)first["sessionId"] &&
                (string)AnalyticsOutbox.Read(input)["sessionId"] == (string)second["sessionId"], "one-use claim preserves a newly staged binding");
            Check(!queue.Pending().Any(), "live checkpoint cannot upload");
            var observed = Sample(first);
            observed["durationSeconds"] = 12.375;
            queue.Save(first, observed);
            var reopened = new AnalyticsOutbox(root);
            reopened.Recover();
            var path = reopened.Pending().Single();
            Check(JToken.DeepEquals(AnalyticsOutbox.Read(path)["payload"], observed),
                "hard-stop recovery promotes the last durable observation with its original timestamp");
            foreach (var status in new long[] { 0, 408, 429, 500, 503 })
                Check(!AnalyticsOutbox.ApplyResponse(path, status) && JToken.DeepEquals(AnalyticsOutbox.Read(path)["payload"], observed),
                    "retry leaves identical data queued for response " + status);
            var next = reopened.Begin(Sample, now);
            Check(next != null && !File.Exists(input), "next session consumes its own credential");
            var completed = Sample(second);
            completed["status"] = "completed";
            reopened.Save(next, completed);
            reopened.Promote(next);
            reopened.Recover();
            var other = reopened.Pending().Single(p => p != path);
            Check(reopened.Pending().Count() == 2 &&
                (string)AnalyticsOutbox.Read(other)["payload"]["status"] == "completed", "two queued sessions for one participant remain separate");
            File.WriteAllText(input, second.ToString());
            Check(reopened.Begin(Sample, now) == null && reopened.Pending().Count() == 2, "duplicate binding cannot overwrite pending data");
            string Receipt(JObject credential) => new JObject { ["saved"] = true, ["id"] = (string)credential["sessionId"] }.ToString();
            Check(!AnalyticsOutbox.ApplyResponse(path, 200, "<html>Sign in</html>") && File.Exists(path),
                "HTTP 200 proxy page never acknowledges an upload");
            Check(!AnalyticsOutbox.ApplyResponse(path, 201, Receipt(second)) && File.Exists(path),
                "receipt for a different session never acknowledges an upload");
            Check(!AnalyticsOutbox.ApplyResponse(path, 200, new JObject { ["saved"] = false, ["id"] = (string)first["sessionId"] }.ToString()),
                "receipt must confirm storage");
            Check(AnalyticsOutbox.ApplyResponse(path, 200, Receipt(first)) && !File.Exists(path), "confirmed idempotent retry clears first session");
            Check(AnalyticsOutbox.ApplyResponse(other, 201, Receipt(second)) && !reopened.Pending().Any(), "confirmed insertion clears second session");

            var third = Binding(now);
            File.WriteAllText(input, third.ToString());
            reopened.Begin(Sample, now);
            reopened.Promote(third);
            var rejected = reopened.Pending().Single();
            Check(AnalyticsOutbox.ApplyResponse(rejected, 409) && !reopened.Pending().Any() &&
                File.Exists(Path.ChangeExtension(rejected, ".rejected")), "conflicting data is isolated until credential expiry");
            reopened.DiscardExpiredRejections(now.AddDays(1));
            Check(!File.Exists(Path.ChangeExtension(rejected, ".rejected")), "expired rejected data is removed");
            var invalid = Binding(now);
            foreach (var endpoint in new[] { "http://example.invalid/api/submissions", "https://a:b@example.invalid/api/submissions",
                "https://example.invalid/api/submissions?x=1" })
            {
                invalid["endpoint"] = endpoint;
                Check(!AnalyticsOutbox.ValidBinding(invalid, now), "binding rejects unsafe endpoint format");
            }
            var expiry = Binding(now);
            Check(!AnalyticsOutbox.ValidBinding(expiry, now.AddDays(1)), "expired credential is ineligible for upload");
            File.WriteAllText(input, expiry.ToString());
            try { reopened.Begin(Sample, now.AddDays(1)); }
            catch (InvalidDataException) { }
            Check(!File.Exists(input) && !reopened.Pending().Any(), "expired binding creates no session");
            File.WriteAllText(input, third.ToString());
            reopened.Begin(Sample, now);
            reopened.Promote(third);
            Check(AnalyticsOutbox.ApplyResponse(reopened.Pending().Single(), 403, new JObject { ["error"] = "synthetic denial" }.ToString()) && !reopened.Pending().Any(),
                "revoked credential and its local measurements are removed");
            File.WriteAllText(input + ".claimed", first.ToString());
            reopened.Recover();
            Check(!File.Exists(input + ".claimed") && reopened.Begin(Sample, now) == null, "a crash during claim cannot bind the next player");

            var devicePath = Path.Combine(root, AnalyticsOutbox.DeviceFile);
            var runOne = Binding(now);
            var runTwo = Binding(now);
            runTwo["participantId"] = runOne["participantId"].DeepClone();
            File.WriteAllText(devicePath, new JObject
            {
                ["participantCode"] = "P001",
                ["bindings"] = new JArray(runOne, runTwo)
            }.ToString());
            Check(reopened.DeviceStatus(now) == ("P001", 2), "device pack displays the assigned participant and number of available runs");
            try { reopened.BeginFromDevice(Sample, now, "P002"); }
            catch (InvalidDataException) { }
            Check(reopened.DeviceStatus(now).remaining == 2 && !reopened.Pending().Any(),
                "a changed participant cannot consume a slot or start recording");
            var firstRun = reopened.BeginFromDevice(Sample, now, "P001");
            reopened.Promote(firstRun);
            var nextLaunch = new AnalyticsOutbox(root);
            nextLaunch.Recover();
            var secondRun = nextLaunch.BeginFromDevice(Sample, now, "P001");
            nextLaunch.Promote(secondRun);
            Check((string)firstRun["sessionId"] != (string)secondRun["sessionId"] &&
                JToken.DeepEquals(firstRun["participantId"], secondRun["participantId"]) &&
                nextLaunch.Pending().Count() == 2, "one device setup records two distinct offline rounds across launches");
            Check(nextLaunch.DeviceStatus(now) == ("P001", 0) && nextLaunch.BeginFromDevice(Sample, now, "P001") == null,
                "exhausted device setup cannot start a third analytics round");
            Console.WriteLine("Passed " + count + " outbox checks.");
        }
        finally { Directory.Delete(root, true); }
    }
}
