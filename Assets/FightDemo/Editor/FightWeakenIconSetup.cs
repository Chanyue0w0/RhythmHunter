using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace RhythmHunter.FightDemo.Editor
{
    [InitializeOnLoad]
    static class FightWeakenIconSetup
    {
        static FightWeakenIconSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/WeakenIconSetup.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(request);
            try
            {
                const string path = "Assets/FightDemo/Prefabs/StatusIcons/WeakenEffect.prefab";
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var fight = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FightCombatController>(true)).Single();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab)
                {
                    var source = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "WeakenEffect");
                    if (!source) throw new Exception("WeakenEffect not found in the active scene; preserve the user's artwork.");
                    var clone = UnityEngine.Object.Instantiate(source.gameObject);
                    try
                    {
                        clone.name = "WeakenEffect";
                        clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        clone.transform.localScale = source.localScale;
                        var icon = clone.GetComponent<FightStatusIcon>() ?? clone.AddComponent<FightStatusIcon>();
                        var template = AssetDatabase.LoadAssetAtPath<FightStatusIcon>("Assets/FightDemo/Prefabs/StatusIcons/BleedEffect.prefab");
                        EditorUtility.CopySerialized(template, icon);
                        var fields = new SerializedObject(icon);
                        fields.FindProperty("triggerPulseBeats").floatValue = .3f;
                        fields.FindProperty("triggerPulseScale").floatValue = 1.2f;
                        fields.ApplyModifiedPropertiesWithoutUndo();
                        foreach (var sprite in clone.GetComponentsInChildren<SpriteRenderer>(true)) sprite.sortingOrder = 120;
                        clone.SetActive(true);
                        prefab = PrefabUtility.SaveAsPrefabAsset(clone, path);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(clone); }
                }
                var data = new SerializedObject(fight);
                data.FindProperty("weakenEffectIcon").objectReferenceValue = prefab.GetComponent<FightStatusIcon>();
                data.ApplyModifiedPropertiesWithoutUndo();
                foreach (var sample in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "WeakenEffect"))
                    sample.gameObject.SetActive(false); // Keep the authored sample; only pooled status instances render in battle.
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
                File.WriteAllText("Temp/WeakenIconSetup.result", "PASS: WeakenEffect prefab created from scene artwork and assigned to pooled status icons.");
            }
            catch (Exception e) { File.WriteAllText("Temp/WeakenIconSetup.result", "FAIL: " + e); }
        }
    }
}
