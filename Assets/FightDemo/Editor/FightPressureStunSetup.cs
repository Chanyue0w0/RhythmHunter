using System;
using System.IO;
using System.Linq;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightPressureStunSetup
    {
        const string Path = "Assets/FightDemo/Prefabs/VFX/StunEffect.prefab";
        static FightPressureStunSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/PressureStunSetup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Configure(); }
            catch (Exception e) { File.WriteAllText("Temp/PressureStunSetup.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/VFX/Configure Pressure Rest Stun Effect")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play first.");
            var scene = SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            if (!scene.isLoaded) throw new Exception("Open Art2_FightScene first.");
            var objects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var source = objects.Single(t => t.name == "StunEffect").gameObject;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            if (prefab == null)
            {
                var clone = UnityEngine.Object.Instantiate(source);
                try
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(clone))
                        PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    clone.name = "StunEffect";
                    clone.transform.position = Vector3.zero;
                    clone.SetActive(true);
                    prefab = PrefabUtility.SaveAsPrefabAsset(clone, Path);
                }
                finally { UnityEngine.Object.DestroyImmediate(clone); }
            }
            if (prefab == null || !prefab.activeSelf || prefab.GetComponentsInChildren<ParticleSystem>(true).Length == 0)
                throw new Exception("Stun prefab missing active root or particles.");
            var visuals = objects.Select(t => t.GetComponent<FightPressureVisuals>()).Single(v => v != null);
            var settings = new SerializedObject(visuals);
            settings.FindProperty("stunEffectPrefab").objectReferenceValue = prefab;
            settings.FindProperty("showStunEffect").boolValue = true;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = prefab;
            File.WriteAllText("Temp/PressureStunSetup.result", "PASS: standalone active StunEffect prefab saved from scene, original preview preserved; Pressure rest head effect bound.");
        }
    }
}
