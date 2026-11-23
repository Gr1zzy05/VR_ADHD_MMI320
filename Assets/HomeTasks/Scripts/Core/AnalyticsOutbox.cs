using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HomeTasks
{
    // Private per-installation spool. Active checkpoints are never sent while the session is live.
    internal sealed class AnalyticsOutbox
    {
        internal const string BindingFile = "focus-shift-binding.json";
        internal const string DeviceFile = "focus-shift-device.json";
        internal const string NoticeVersion = "2026-01";
        const int MaxPacketBytes = 64 * 1024;
        readonly string root;
        readonly string directory;

        internal AnalyticsOutbox(string persistentDataPath)
        {
            root = persistentDataPath;
            directory = Path.Combine(root, "focus-shift-outbox");
        }

        string DevicePath => Path.Combine(root, DeviceFile);
        internal bool HasDeviceSetup => File.Exists(DevicePath);

        internal static bool TryReadServerPack(JObject pack, string server, DateTimeOffset now, out List<JObject> permits)
        {
            permits = new List<JObject>();
            if (!(pack["bindings"] is JArray bindings) || bindings.Count < 1 || bindings.Count > 30) return false;
            foreach (var value in bindings)
            {
                if (!(value is JObject binding) || !ValidBinding(binding, now) ||
                    (string)binding["endpoint"] != server + "/api/submissions") return false;
                permits.Add(binding);
            }
            return permits.Select(b => (string)b["participantId"]).Distinct().Count() == 1 &&
                permits.Select(b => (string)b["sessionId"]).Distinct().Count() == permits.Count;
        }

        internal (string code, int remaining) DeviceStatus(DateTimeOffset now)
        {
            var pack = Read(DevicePath);
            var bindings = AvailableBindings(pack, now);
            return ((string)pack["participantCode"], bindings.Count);
        }

        List<JObject> AvailableBindings(JObject pack, DateTimeOffset now)
        {
            var code = (string)pack["participantCode"];
            if (string.IsNullOrEmpty(code) || code.Length < 4 || code.Length > 16 || code[0] != 'P' ||
                !code.Skip(1).All(c => c >= '0' && c <= '9') ||
                !(pack["bindings"] is JArray bindings) || bindings.Count > 30)
                throw new InvalidDataException("Invalid VR device setup.");
            var available = bindings.OfType<JObject>().Where(b => ValidBinding(b, now) && !HasPacket(b)).ToList();
            if (available.Select(b => (string)b["participantId"]).Distinct().Count() > 1 ||
                available.Select(b => (string)b["sessionId"]).Distinct().Count() != available.Count)
                throw new InvalidDataException("Inconsistent VR device setup.");
            return available;
        }

        internal JObject BeginFromDevice(Func<JObject, JObject> snapshot, DateTimeOffset now, string expectedCode)
        {
            var pack = Read(DevicePath);
            if ((string)pack["participantCode"] != expectedCode)
                throw new InvalidDataException("The participant changed. Restart to verify the new setup.");
            var bindings = AvailableBindings(pack, now);
            if (bindings.Count == 0) return null;
            var next = bindings[0];
            var payload = snapshot(next);
            pack["bindings"] = new JArray(bindings.Skip(1).Select(b => b.DeepClone()));
            // Consume first. An interruption between these writes may waste a slot, but cannot reuse one.
            WriteAtomically(DevicePath, pack);
            Save(next, payload);
            return next;
        }

        bool HasPacket(JObject binding) => new[] { ".active", ".pending", ".rejected" }
            .Any(ext => File.Exists(PathFor(binding, ext)));

        internal static string Timestamp(DateTimeOffset time) =>
            time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        internal static JObject Read(string path)
        {
            if (new FileInfo(path).Length > MaxPacketBytes) throw new InvalidDataException("Oversized analytics file.");
            using var reader = File.OpenText(path);
            return ReadObject(reader);
        }

        internal static JObject ReadObject(TextReader reader)
        {
            // Preserve UTC as strings for both on-disk submissions and server credentials.
            using var json = new JsonTextReader(reader) { DateParseHandling = DateParseHandling.None, MaxDepth = 8 };
            return JObject.Load(json);
        }

        internal static bool ValidBinding(JObject binding, DateTimeOffset now)
        {
            if (binding == null) return false;
            foreach (var key in new[] { "endpoint", "sessionId", "participantId", "token", "expiresAt", "noticeVersion" })
                if (binding[key]?.Type != JTokenType.String) return false;
            var token = (string)binding["token"];
            return (string)binding["noticeVersion"] == NoticeVersion &&
                Guid.TryParseExact((string)binding["sessionId"], "D", out _) &&
                Guid.TryParseExact((string)binding["participantId"], "D", out _) &&
                token.Length >= 16 && token.Length <= 1024 && token.All(c => c > 32 && c < 127) &&
                Uri.TryCreate((string)binding["endpoint"], UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 &&
                uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.AbsolutePath == "/api/submissions" &&
                DateTimeOffset.TryParse((string)binding["expiresAt"], CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var expiry) && expiry > now;
        }

        string PathFor(JObject binding, string extension)
        {
            var id = Guid.ParseExact((string)binding["sessionId"], "D");
            return Path.Combine(directory, id.ToString("D") + extension);
        }

        // Claim by rename so cleanup cannot delete a second file being provisioned concurrently.
        internal JObject Begin(Func<JObject, JObject> snapshot, DateTimeOffset now)
        {
            var input = Path.Combine(root, BindingFile);
            if (!File.Exists(input)) return null;
            var claimed = input + ".claimed";
            File.Move(input, claimed);
            try
            {
                var binding = Read(claimed);
                if (!ValidBinding(binding, now)) throw new InvalidDataException("Invalid or expired binding.");
                var existing = HasPacket(binding);
                if (!existing) Save(binding, snapshot(binding));
                return existing ? null : binding;
            }
            finally { File.Delete(claimed); }
        }

        internal void Save(JObject binding, JObject payload)
        {
            Directory.CreateDirectory(directory);
            var path = PathFor(binding, ".active");
            var packet = new JObject { ["binding"] = binding.DeepClone(), ["payload"] = payload };
            WriteAtomically(path, packet);
        }

        static void WriteAtomically(string path, JObject document)
        {
            var temporary = path + ".tmp";
            var bytes = Encoding.UTF8.GetBytes(document.ToString(Formatting.None));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        internal void Promote(JObject binding) => PromotePath(PathFor(binding, ".active"));

        internal static void Quarantine(string pending) => File.Move(pending, Path.ChangeExtension(pending, ".rejected"));

        static void PromotePath(string active)
        {
            if (!File.Exists(active)) return;
            var pending = Path.ChangeExtension(active, ".pending");
            if (File.Exists(pending)) File.Delete(active);
            else File.Move(active, pending);
        }

        internal void Recover()
        {
            // A killed process may have claimed a binding before its first measurement. Never assign
            // that credential to whoever uses the device next.
            var claimed = Path.Combine(root, BindingFile + ".claimed");
            if (File.Exists(claimed)) File.Delete(claimed);
            if (!Directory.Exists(directory)) return;
            foreach (var path in Directory.GetFiles(directory, "*.active")) PromotePath(path);
            foreach (var path in Directory.GetFiles(directory, "*.tmp")) File.Delete(path);
        }

        internal IEnumerable<string> Pending() => Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.pending").OrderBy(path => path, StringComparer.Ordinal)
            : Array.Empty<string>();

        internal void DiscardExpiredRejections(DateTimeOffset now)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var path in Directory.GetFiles(directory, "*.rejected"))
            {
                try
                {
                    if (!ValidBinding(Read(path)["binding"] as JObject, now)) File.Delete(path);
                }
                catch (JsonException) { File.Delete(path); }
                catch (InvalidDataException) { File.Delete(path); }
            }
        }

        internal static bool ApplyResponse(string pending, long status, string response = null)
        {
            if (status == 200 || status == 201 || status == 401 || status == 403 || status == 410)
            {
                // An intermediary can return a sign-in page with HTTP 200. Require the API receipt.
                if (string.IsNullOrWhiteSpace(response) || response.Length > 4096) return false;
                JObject body;
                try { body = JObject.Parse(response); }
                catch (JsonException) { return false; }
                if (status == 200 || status == 201)
                {
                    if (body["saved"]?.Type != JTokenType.Boolean || !(bool)body["saved"] ||
                        body["id"]?.Type != JTokenType.String ||
                        !string.Equals((string)body["id"], Path.GetFileNameWithoutExtension(pending), StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else if (body["error"]?.Type != JTokenType.String) return false;
                File.Delete(pending);
            }
            else if (status == 400 || status == 409 || status == 413 || status == 422 || (status >= 300 && status < 400))
                Quarantine(pending);
            else return false; // Includes network errors, throttling and temporary server failures.
            return true;
        }
    }
}
