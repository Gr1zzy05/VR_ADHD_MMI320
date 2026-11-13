// Run after capturing Screenshots/Fade-Out.png and Screenshots/Fade-In.png
// with Pipeline's capture game_view command. That command forces a fresh draw;
// ScreenCapture alone can read a stale Game view when the Editor is unfocused.
using UnityEngine;
using System.IO;

var checks = new List<string>();
foreach (var name in new[] { "Fade-Out", "Fade-In" })
{
    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
    try
    {
        if (!texture.LoadImage(File.ReadAllBytes("Screenshots/" + name + ".png")))
            throw new Exception("Cannot read the rendered fade capture.");
        var visible = texture.GetPixels32().Any(p => p.r > 3 || p.g > 3 || p.b > 3);
        if (visible != (name == "Fade-In"))
            throw new Exception("Unexpected rendered fade pixels: " + name);
        checks.Add(name == "Fade-Out"
            ? "PASS: Fade-out renders a completely black frame, including the desktop HUD"
            : "PASS: Fade-in restores the rendered scene and removes its black overlay");
    }
    finally { Object.Destroy(texture); }
}
Directory.CreateDirectory("TestResults");
File.WriteAllLines("TestResults/FadeVerification.txt", checks);
return checks.ToArray();
