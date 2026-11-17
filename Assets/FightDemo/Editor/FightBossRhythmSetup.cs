using System;
using System.IO;
using System.Linq;
using RhythmHunter.FightDemo;
using RhythmHunter.FightDemo.Art2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightBossRhythmSetup
    {
        static FightBossRhythmSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/BossRhythmSetup.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(request);
            try
            {
                const string basePath = "Assets/FightDemo/Prefabs/VFX/BossRhythm";
                Directory.CreateDirectory(basePath); AssetDatabase.Refresh();
                string chartPath = "Assets/FightDemo/Levels/GoblinKing_PhaseTwo_Chart.asset";
                var chart = AssetDatabase.LoadAssetAtPath<FightBossRhythmChart>(chartPath);
                if (!chart) { chart = ScriptableObject.CreateInstance<FightBossRhythmChart>(); chart.GeneratePrototype(); AssetDatabase.CreateAsset(chart, chartPath); }
                string materialPath = basePath + "/BossRhythm_Unlit.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!material)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); material.SetFloat("_ZWrite", 0);
                    material.SetOverrideTag("RenderType", "Transparent"); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue = 3100; AssetDatabase.CreateAsset(material, materialPath);
                }
                var slash = Visual(basePath + "/BossPhaseTwoSlash.prefab", material, false);
                var guard = Visual(basePath + "/BossPhaseTwoGuard.prefab", material, true);
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                if (!scene.isLoaded) throw new Exception("Open Art2_FightScene first.");
                var objects = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
                var fight = objects.Select(x => x.GetComponent<FightCombatController>()).First(x => x);
                var intro = new SerializedObject(fight.GetComponent<FightBossEntrance>());
                var camera = (Camera)intro.FindProperty("view").objectReferenceValue;
                var transition = fight.GetComponent<FightBossPhaseTwoTransition>();
                var battle = fight.GetComponent<FightBossRhythmBattle>(); bool firstSetup = !battle;
                if (!battle) battle = fight.gameObject.AddComponent<FightBossRhythmBattle>();
                var explosion = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/VFX/SmokeExplosion_Dark.prefab");
                var so = new SerializedObject(battle);
                so.FindProperty("fight").objectReferenceValue = fight; so.FindProperty("transition").objectReferenceValue = transition;
                so.FindProperty("view").objectReferenceValue = camera; so.FindProperty("chart").objectReferenceValue = chart;
                so.FindProperty("slashPrefab").objectReferenceValue = slash; so.FindProperty("guardPrefab").objectReferenceValue = guard;
                so.FindProperty("victoryExplosion").objectReferenceValue = explosion; so.ApplyModifiedPropertiesWithoutUndo();
                so = new SerializedObject(transition);
                so.FindProperty("bossIdleAnimation").stringValue = "Chop_Idle";
                so.FindProperty("landingExplosion").objectReferenceValue = explosion;
                so.FindProperty("cameraController").objectReferenceValue = camera.GetComponent<Art2CameraController>();
                if (firstSetup)
                {
                    so.FindProperty("bossJumpBeats").floatValue = 1; so.FindProperty("bossJumpHeight").floatValue = 14;
                    so.FindProperty("heroesJumpBeats").floatValue = 1;
                    var targets = so.FindProperty("heroDestinations");
                    for (int i = 0; i < targets.arraySize; i++)
                    {
                        var target = (Transform)targets.GetArrayElementAtIndex(i).objectReferenceValue;
                        var point = camera.WorldToViewportPoint(target.position); point.y -= .06f;
                        var ray = camera.ViewportPointToRay(point); var plane = new Plane(Vector3.up, target.position);
                        if (plane.Raycast(ray, out float distance)) target.position = ray.GetPoint(distance);
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(battle); EditorUtility.SetDirty(transition);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
                if (!File.ReadAllText(scene.path).Contains("chopIdle: Chop_Idle")) throw new Exception("Rhythm battle component did not persist.");
                File.WriteAllText("Temp/BossRhythmSetup.result", "PASS: chart " + chart.notes.Count + " unique notes; pooled slash/guard prefabs; quick vertical jump, explosion/shake, lower lanes, authored Chop animations, battle controller saved.");
            }
            catch (Exception e) { File.WriteAllText("Temp/BossRhythmSetup.result", "FAIL: " + e); }
        }
        static FightBossRhythmVisual Visual(string path, Material material, bool guard)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing) return existing.GetComponent<FightBossRhythmVisual>();
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            try
            {
                var line = root.AddComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = false;
                line.widthMultiplier = guard ? .055f : .11f; line.numCapVertices = 3; line.numCornerVertices = 3;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                int count = guard ? 5 : 13; line.positionCount = count;
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)(count - 1);
                    line.SetPosition(i, guard ? new Vector3(Mathf.Sin(t * Mathf.PI * 2) * .65f, Mathf.Cos(t * Mathf.PI * 2) * .8f, 0)
                        : new Vector3(Mathf.Lerp(-.8f, .8f, t), Mathf.Sin(t * Mathf.PI) * .35f, 0));
                }
                root.AddComponent<FightBossRhythmVisual>();
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<FightBossRhythmVisual>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
