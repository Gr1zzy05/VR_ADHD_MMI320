// Copied into a temporary Unity project by VerifyAnalyticsOutbox.ps1 -NetworkSmokeTest.
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HomeTasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public static class VerifyAnalyticsTransport
{
    static AnalyticsOutbox queue;
    static JObject snapshot;
    static UnityWebRequest request;
    static int attempts;
    static double deadline;
    static string project;
    static string sessionId;
    static string nextSessionId;
    static string origin;
    static string enrollmentKey;
    static JObject nextGrant;
    static int pairingStage;
    static byte[] expectedHash;

    // This verifier lives outside Assets. Pin only this run's ephemeral loopback certificate.
    sealed class LoopbackCertificate : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(certificateData).SequenceEqual(expectedHash);
        }
    }

    public static void Run()
    {
        try
        {
            project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            origin = File.ReadAllText(Path.Combine(project, "server-origin.txt"));
            var uri = new Uri(origin);
            if (uri.Host != "127.0.0.1" || uri.Scheme != "https") throw new Exception("Test requires loopback HTTPS");
            using var sha = SHA256.Create();
            expectedHash = sha.ComputeHash(File.ReadAllBytes(Path.Combine(project, "test-server.der")));
            queue = new AnalyticsOutbox(Path.Combine(project, "synthetic-device"));
            deadline = EditorApplication.timeSinceStartup + 75;
            enrollmentKey = File.ReadAllText(Path.Combine(project, "enrollment.fixture"));
            RequestPlayer();
            EditorApplication.update += Poll;
        }
        catch (Exception error) { Fail(error); }
    }

    static void RequestPlayer()
    {
        var payload = new JObject
        {
            ["requestId"] = Guid.NewGuid().ToString("D"), ["noticeVersion"] = AnalyticsOutbox.NoticeVersion, ["consent"] = true
        };
        request = SessionSubmissionRequest.CreateJson(origin + "/api/game/start", "POST", payload, enrollmentKey);
        request.certificateHandler = new LoopbackCertificate();
        request.SendWebRequest();
    }

    static void CheckPairing()
    {
        if (pairingStage == 2)
        {
            if (File.Exists(Path.Combine(project, "api.offline"))) { pairingStage = 3; Send(); }
            return;
        }
        if (!request.isDone) return;
        var status = request.responseCode;
        using var reader = new StringReader(request.downloadHandler.text);
        var body = status == 204 ? null : AnalyticsOutbox.ReadObject(reader);
        request.Dispose();
        request = null;
        if (status != 200 || !AnalyticsOutbox.TryReadServerPack(body, origin, DateTimeOffset.UtcNow, out var bindings) || bindings.Count != 1)
            throw new Exception("Automatic enrollment did not return a valid study round (HTTP " + status + ")");
        var grant = bindings[0];
        if (pairingStage == 0)
        {
            if ((string)body["participantCode"] != "P001") throw new Exception("Expected first player code");
            StageFirstRound(grant);
            pairingStage = 1;
            RequestPlayer();
            return;
        }
        if ((string)body["participantCode"] != "P002") throw new Exception("Expected next player code");
        nextGrant = grant;
        pairingStage = 2;
        File.WriteAllText(Path.Combine(project, "enrollment-received"), "");
    }

    static void StageFirstRound(JObject grant)
    {
        sessionId = (string)grant["sessionId"];
        var end = DateTimeOffset.UtcNow;
        snapshot = new JObject {
                ["id"] = sessionId, ["participantId"] = (string)grant["participantId"], ["status"] = "completed",
                ["startedAt"] = AnalyticsOutbox.Timestamp(end.AddSeconds(-75.5)), ["endedAt"] = AnalyticsOutbox.Timestamp(end),
                ["durationSeconds"] = 75.5, ["gameVersion"] = "synthetic-transport-test",
                ["missions"] = new JArray(new[] { "washing", "organizing", "trash", "meeting" }
                    .Select(id => new JObject { ["id"] = id, ["completed"] = true })),
                ["distractions"] = new JArray(
                    new JObject { ["kind"] = "Phone", ["count"] = 2, ["seconds"] = 3.125 },
                    new JObject { ["kind"] = "Television", ["count"] = 0, ["seconds"] = 0 }),
                ["quiz"] = new JObject { ["total"] = 3, ["answered"] = 3, ["correct"] = 2 }
        };
        queue.Save(grant, snapshot);
        queue.Promote(grant);
    }

    static void Send()
    {
        var packet = AnalyticsOutbox.Read(queue.Pending().Single());
        request = SessionSubmissionRequest.Create((JObject)packet["binding"], (JObject)packet["payload"]);
        request.certificateHandler = new LoopbackCertificate();
        request.SendWebRequest();
        attempts++;
    }

    static void Poll()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Transport timeout");
            if (pairingStage < 3) { CheckPairing(); return; }
            if (request == null)
            {
                if (File.Exists(Path.Combine(project, "api.online"))) Send();
                return;
            }
            if (!request.isDone) return;
            var path = queue.Pending().Single();
            if (!JToken.DeepEquals(AnalyticsOutbox.Read(path)["payload"], snapshot)) throw new Exception("Queued payload changed");
            if (attempts <= 2)
            {
                if (request.result != UnityWebRequest.Result.ConnectionError ||
                    AnalyticsOutbox.ApplyResponse(path, request.responseCode, request.downloadHandler.text))
                    throw new Exception("Offline retry did not retain the packet");
            }
            else
            {
                var expected = attempts == 4 ? 200 : 201;
                if (request.responseCode != expected) throw new Exception("Expected HTTP " + expected + ", received " + request.responseCode + " " + request.error);
                var receipt = JObject.Parse(request.downloadHandler.text);
                var expectedId = attempts == 5 ? nextSessionId : sessionId;
                if ((string)receipt["id"] != expectedId || !(bool)receipt["saved"]) throw new Exception("Receipt mismatch");
                // Simulate a crash after the first server commit and before local acknowledgement.
                if (attempts >= 4 && (!AnalyticsOutbox.ApplyResponse(path, request.responseCode, request.downloadHandler.text) || queue.Pending().Any()))
                    throw new Exception("Verified replay did not acknowledge the queued packet");
            }
            request.Dispose();
            request = null;
            if (attempts == 2) { File.WriteAllText(Path.Combine(project, "enable-api"), ""); return; }
            if (attempts < 4) { Send(); return; }
            if (attempts == 4)
            {
                nextSessionId = (string)nextGrant["sessionId"];
                if (nextSessionId == sessionId) throw new Exception("Second study round requires a different ID");
                snapshot = (JObject)snapshot.DeepClone();
                snapshot["id"] = nextSessionId;
                snapshot["participantId"] = (string)nextGrant["participantId"];
                snapshot["durationSeconds"] = 42;
                queue.Save(nextGrant, snapshot);
                queue.Promote(nextGrant);
                Send();
                return;
            }
            var receiptPath = Path.Combine(project, "verified-receipts.json");
            File.WriteAllText(receiptPath + ".tmp", new JObject
            {
                ["id"] = sessionId, ["nextId"] = nextSessionId, ["first"] = 201, ["replay"] = 200, ["next"] = 201
            }.ToString());
            File.Move(receiptPath + ".tmp", receiptPath);
            EditorApplication.update -= Poll;
            Debug.Log("FOCUS_SHIFT_TRANSPORT_PASS: two automatic player codes, offline retries, and identical replay");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Fail(error); }
    }

    static void Fail(Exception error)
    {
        EditorApplication.update -= Poll;
        request?.Dispose();
        Debug.LogException(error);
        EditorApplication.Exit(1);
    }
}
#endif
