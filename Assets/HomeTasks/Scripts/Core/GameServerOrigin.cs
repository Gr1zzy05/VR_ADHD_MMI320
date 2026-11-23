using System;

namespace HomeTasks
{
    public static class GameServerOrigin
    {
        public static string Normalize(string text)
        {
            if (!Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(uri.Host) ||
                uri.AbsolutePath != "/" || uri.UserInfo.Length > 0 ||
                uri.Query.Length > 0 || uri.Fragment.Length > 0)
                return null;
            return uri.GetLeftPart(UriPartial.Authority);
        }
    }
}
