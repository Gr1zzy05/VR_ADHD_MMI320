// Editor-only migration for the supplied Starter Assets rig. Run outside Play mode.
using HomeTasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

if (Application.isPlaying || BuildPipeline.isBuildingPlayer) throw new Exception("Finish the current play/build operation first.");
var router = Object.FindFirstObjectByType<PlayerModeRouter>();
var managers = router.questRig.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b.GetType().Name == "ControllerInputActionManager").ToArray();
if (managers.Length != 2) throw new Exception("Expected two controller input managers.");
foreach (var manager in managers)
{
    var serialized = new SerializedObject(manager);
    serialized.FindProperty("m_SmoothMotionEnabled").boolValue = false;
    serialized.FindProperty("m_SmoothTurnEnabled").boolValue = false;
    serialized.ApplyModifiedPropertiesWithoutUndo();
    PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
}
EditorSceneManager.MarkSceneDirty(router.gameObject.scene);
EditorSceneManager.SaveScene(router.gameObject.scene);
EditorSceneManager.OpenScene(HomeTasks.Editor.HomeSceneBuilder.ScenePath);
router = Object.FindFirstObjectByType<PlayerModeRouter>();
managers = router.questRig.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b.GetType().Name == "ControllerInputActionManager").ToArray();
if (managers.Any(b => new SerializedObject(b).FindProperty("m_SmoothMotionEnabled").boolValue || new SerializedObject(b).FindProperty("m_SmoothTurnEnabled").boolValue))
    throw new Exception("Controller overrides did not survive reloading the scene.");
return "Both controller inputs persist teleport and snap-turn after saving and reopening the scene.";
