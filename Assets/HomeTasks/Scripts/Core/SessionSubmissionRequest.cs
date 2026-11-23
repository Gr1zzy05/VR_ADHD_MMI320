using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace HomeTasks
{
    internal static class SessionSubmissionRequest
    {
        internal static UnityWebRequest Create(JObject ticket, JObject payload)
            => CreateJson((string)ticket["endpoint"], UnityWebRequest.kHttpVerbPOST, payload, (string)ticket["token"]);

        internal static UnityWebRequest CreateJson(string url, string method, JObject payload, string secret)
        {
            var request = new UnityWebRequest(url, method);
            try
            {
                request.redirectLimit = 0;
                request.timeout = 15;
                request.downloadHandler = new DownloadHandlerBuffer();
                if (payload != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload.ToString(Formatting.None)));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (secret != null) request.SetRequestHeader("Authorization", "Bearer " + secret);
                return request;
            }
            catch { request.Dispose(); throw; }
        }
    }
}
