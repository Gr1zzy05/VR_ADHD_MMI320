using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace HomeTasks
{
    internal sealed class StudyServerSetup : EditorWindow
    {
        const string ResourcePath = "Assets/HomeTasks/Resources/FocusShiftServer.txt";
        const string KeyPath = "Assets/HomeTasks/Resources/FocusShiftEnrollmentKey.txt";
        [SerializeField] string server = "";
        string enrollmentKey = "";

        [MenuItem("Home Focus/Set study server for builds")]
        static void Open()
        {
            var window = GetWindow<StudyServerSetup>("Focus Shift Server");
            window.server = File.Exists(ResourcePath) ? File.ReadAllText(ResourcePath).Trim() : "";
            window.enrollmentKey = File.Exists(KeyPath) ? File.ReadAllText(KeyPath).Trim() : "";
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Use the deployment URL and its game enrollment key. Keep the resulting build within the supervised study.", MessageType.Info);
            server = EditorGUILayout.TextField("Website URL", server);
            enrollmentKey = EditorGUILayout.PasswordField("Game enrollment key", enrollmentKey);
            var origin = GameServerOrigin.Normalize(server);
            using (new EditorGUI.DisabledScope(origin == null || !Regex.IsMatch(enrollmentKey ?? "", @"\A[A-Za-z0-9_-]{43}\z")))
            {
                if (GUILayout.Button("Save for future builds"))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ResourcePath));
                    File.WriteAllText(ResourcePath, origin);
                    File.WriteAllText(KeyPath, enrollmentKey);
                    AssetDatabase.ImportAsset(ResourcePath);
                    AssetDatabase.ImportAsset(KeyPath);
                    Close();
                }
            }
        }
    }
}
