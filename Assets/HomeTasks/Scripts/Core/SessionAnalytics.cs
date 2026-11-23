using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace HomeTasks
{
    // Installed on demand without scene edits. Survives the fade into the results scene.
    internal sealed class SessionAnalytics : MonoBehaviour
    {
        static SessionAnalytics instance;
        AnalyticsOutbox outbox;
        JObject binding;
        HomeSession source;
        string startedAt;
        float nextCheckpoint;
        float nextAttempt;
        string server;
        string enrollmentKey;
        bool uploadsEnabled = true;
        internal bool ServerConfigured => server != null && enrollmentKey?.Length == 43;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSingleton() => instance = null;

        internal static SessionAnalytics Initialize()
        {
            try
            {
                if (instance == null)
                {
                    var go = new GameObject("Focus Shift session uploads");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<SessionAnalytics>();
                    instance.server = GameServerOrigin.Normalize(Resources.Load<TextAsset>("FocusShiftServer")?.text);
                    instance.enrollmentKey = Resources.Load<TextAsset>("FocusShiftEnrollmentKey")?.text.Trim();
                    instance.PrepareRun();
                    instance.outbox = new AnalyticsOutbox(Application.persistentDataPath);
                    try { instance.outbox.Recover(); }
                    catch (Exception e) { Warn(e); }
                    instance.StartCoroutine(instance.SendPending());
                }
                return instance;
            }
            catch (Exception e) { Warn(e); return null; }
        }

        internal bool HasDeviceSetup => outbox.HasDeviceSetup;
        internal void PrepareRun()
        {
            if (ServerConfigured) uploadsEnabled = false;
        }

        internal IEnumerator Enroll(string requestId, Action<string, JObject> ready)
        {
            var payload = new JObject
            {
                ["requestId"] = requestId, ["noticeVersion"] = AnalyticsOutbox.NoticeVersion, ["consent"] = true
            };
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var request = SessionSubmissionRequest.CreateJson(server + "/api/game/start", "POST", payload, enrollmentKey);
                request.timeout = 5;
                yield return request.SendWebRequest();
                var response = ReadResponse(request);
                if (response != null && response["participantCode"]?.Type == JTokenType.String &&
                    AnalyticsOutbox.TryReadServerPack(response, server, DateTimeOffset.UtcNow, out var prepared) &&
                    prepared.Count == 1)
                {
                    ready((string)response["participantCode"], prepared[0]);
                    yield break;
                }
                if (request.responseCode is >= 400 and < 500) break;
            }
            ready(null, null);
        }

        static JObject ReadResponse(UnityWebRequest request)
        {
            if (request.responseCode != 200 || request.result != UnityWebRequest.Result.Success ||
                request.downloadedBytes > 64 * 1024) return null;
            try
            {
                using var reader = new StringReader(request.downloadHandler.text);
                return AnalyticsOutbox.ReadObject(reader);
            }
            catch (JsonException e) { Warn(e); return null; }
        }

        internal (string code, int remaining) DeviceStatus()
        {
            try { return outbox.DeviceStatus(DateTimeOffset.UtcNow); }
            catch (Exception e) { Warn(e); return (null, 0); }
        }

        internal bool Begin(HomeSession session, string participantCode = null, JObject ticket = null)
        {
            try
            {
                if (binding != null) Release(source);
                var now = DateTimeOffset.UtcNow;
                startedAt = AnalyticsOutbox.Timestamp(now);
                JObject FirstSnapshot(JObject ticket) => Snapshot(session, ticket, null);
                if (ticket != null)
                {
                    outbox.Save(ticket, FirstSnapshot(ticket));
                    binding = ticket;
                    uploadsEnabled = true;
                    nextAttempt = 0f;
                }
                else
                {
                    binding = participantCode == null
                        ? outbox.Begin(FirstSnapshot, now)
                        : outbox.BeginFromDevice(FirstSnapshot, now, participantCode);
                }
                if (binding == null) return false;
                source = session;
                nextCheckpoint = Time.realtimeSinceStartup + 5f;
                return true;
            }
            catch (Exception e) { Warn(e); return false; }
        }

        static void Warn(Exception error) =>
            Debug.LogWarning("Focus Shift analytics could not process a device file or request (" + error.GetType().Name + ").");

        static JObject Chore(string id, bool completed, int done, int total) => new JObject
        {
            ["id"] = id, ["completed"] = completed, ["itemsDone"] = done, ["itemsTotal"] = total
        };

        JObject Snapshot(HomeSession session, JObject credentials, HomeSessionResult result)
        {
            var meeting = session.meeting;
            var distractions = new JArray();
            foreach (var row in result?.Distractions ?? session.SnapshotDistractions())
                distractions.Add(new JObject { ["kind"] = row.Kind.ToString(), ["count"] = row.Uses, ["seconds"] = row.Seconds });
            var dishCount = result?.DishCount ?? session.dishes.Count;
            var trashCount = result?.TrashCount ?? session.rubbish.Count;
            var payload = new JObject
            {
                ["id"] = (string)credentials["sessionId"], ["participantId"] = (string)credentials["participantId"],
                ["startedAt"] = startedAt, ["endedAt"] = AnalyticsOutbox.Timestamp(DateTimeOffset.UtcNow),
                ["status"] = result == null ? "interrupted" : "completed",
                ["durationSeconds"] = result?.Elapsed ?? session.Elapsed, ["gameVersion"] = Application.version,
                ["missions"] = new JArray(
                    Chore("washing", session.WashingComplete, result?.Washed ?? session.Washed, dishCount),
                    Chore("organizing", session.SortingComplete, result?.Organized ?? session.Organized, dishCount),
                    Chore("trash", session.TrashComplete, result?.Disposed ?? session.Disposed, trashCount),
                    new JObject { ["id"] = "meeting", ["completed"] = meeting != null && meeting.Completed }),
                ["distractions"] = distractions
            };
            var answered = result?.QuestionCount ?? (meeting != null ? meeting.QuestionIndex : 0);
            if (meeting != null && meeting.content != null && meeting.content.questions != null &&
                (result != null || meeting.State == MeetingState.Quiz || meeting.Completed))
                payload["quiz"] = new JObject
                {
                    ["total"] = result?.QuestionCount ?? meeting.content.questions.Length, ["answered"] = answered,
                    ["correct"] = result?.CorrectAnswers ?? meeting.CorrectAnswers
                };
            return payload;
        }

        internal void Checkpoint(HomeSession session, bool immediate = false)
        {
            if (source != session || binding == null || (!immediate && Time.realtimeSinceStartup < nextCheckpoint)) return;
            nextCheckpoint = Time.realtimeSinceStartup + 5f;
            try { outbox.Save(binding, Snapshot(session, binding, null)); }
            catch (Exception e) { Warn(e); }
        }

        internal void End(HomeSession session, HomeSessionResult result)
        {
            if (source != session || binding == null) return;
            try { outbox.Save(binding, Snapshot(session, binding, result)); }
            catch (Exception e) { Warn(e); }
            Release(session);
        }

        internal void Release(HomeSession session)
        {
            if (source != session || binding == null) return;
            try { outbox.Promote(binding); }
            catch (Exception e) { Warn(e); } // Recovery on next launch can promote a stranded checkpoint.
            binding = null;
            source = null;
            nextAttempt = 0f;
        }

        IEnumerator SendPending()
        {
            var retryDelay = 2f;
            while (true)
            {
                while (!uploadsEnabled || Time.realtimeSinceStartup < nextAttempt) yield return new WaitForSecondsRealtime(1f);
                System.Collections.Generic.IEnumerable<string> paths = Array.Empty<string>();
                try
                {
                    outbox.DiscardExpiredRejections(DateTimeOffset.UtcNow);
                    paths = outbox.Pending();
                }
                catch (Exception e) { Warn(e); }

                var retry = false;
                foreach (var path in paths)
                {
                    if (!uploadsEnabled) break;
                    UnityWebRequest request = null;
                    UnityWebRequestAsyncOperation operation = null;
                    try
                    {
                        var packet = AnalyticsOutbox.Read(path);
                        var ticket = packet["binding"] as JObject;
                        if (!AnalyticsOutbox.ValidBinding(ticket, DateTimeOffset.UtcNow))
                        {
                            File.Delete(path);
                            Debug.LogWarning("Focus Shift removed an expired or invalid queued submission.");
                            continue;
                        }
                        if (!(packet["payload"] is JObject payload)) throw new InvalidDataException();
                        request = SessionSubmissionRequest.Create(ticket, payload);
                        operation = request.SendWebRequest();
                    }
                    catch (Exception e) when (e is JsonException || e is InvalidDataException)
                    {
                        Warn(e);
                        try { AnalyticsOutbox.Quarantine(path); }
                        catch (Exception io) { Warn(io); retry = true; }
                    }
                    catch (Exception e) { Warn(e); retry = true; }
                    if (operation == null) { request?.Dispose(); continue; }
                    using (request)
                    {
                        yield return operation;
                        try
                        {
                            var code = request.responseCode;
                            if (code >= 300 && code < 500 && code != 429)
                                Debug.LogWarning("Focus Shift submission returned HTTP " + code + ".");
                            retry |= !AnalyticsOutbox.ApplyResponse(path, code, request.downloadHandler.text);
                        }
                        catch (Exception e) { Warn(e); retry = true; }
                    }
                }
                retryDelay = retry ? Mathf.Min(120f, retryDelay * 2f) : 2f;
                nextAttempt = Time.realtimeSinceStartup + (retry ? retryDelay : 15f);
            }
        }
    }
}
