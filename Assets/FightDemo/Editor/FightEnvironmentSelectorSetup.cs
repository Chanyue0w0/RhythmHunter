using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmHunter.FightDemo;
using RhythmHunter.FightDemo.Art2;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightEnvironmentSelectorSetup
    {
        static FightEnvironmentSelectorSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/EnvironmentSelectorSetup.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(request);
            try
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                if (all.Any(t => t.name == "ForestEnvironment_2_5D"))
                {
                    File.WriteAllText("Temp/EnvironmentSelectorSetup.result", "SKIPPED: Forest adaptation already exists; preserve current environment bindings.");
                    return;
                }
                var planner = all.Select(t => t.GetComponent<FightLevelPlanner>()).Single(p => p != null);
                var selector = planner.GetComponent<FightEnvironmentSelector>() ?? Undo.AddComponent<FightEnvironmentSelector>(planner.gameObject);
                var cave = AssetDatabase.LoadAssetAtPath<FightEnvironmentDefinition>("Assets/FightDemo/Levels/Environment_Cave.asset");
                var forest = AssetDatabase.LoadAssetAtPath<FightEnvironmentDefinition>("Assets/FightDemo/Levels/Environment_Forest.asset");
                GameObject PrefabRoot(string path) => all.Select(t => t.gameObject).First(g => PrefabUtility.IsAnyPrefabInstanceRoot(g) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g) == path);
                var caveRoot = PrefabRoot("Assets/FightDemo/Prefabs/ArtBattle/CaveEnvironment_2_5D.prefab");
                var forestRoot = PrefabRoot("Assets/FightDemo/Prefabs/ArtBattle/ForestEnvironment.prefab");
                string[] names = { "Art2_Atmosphere", "ShadowTestLight_MoveMe", "ShadowTest_AimTarget", "PressureRedAccentLight_AdjustMe", "SceneBeatMotionManager" };
                var caveRoots = names.Select(n => all.Single(t => t.name == n).gameObject).Prepend(caveRoot).ToList();
                var ambient = all.FirstOrDefault(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).EndsWith("CFXR3 Ambient Glows.prefab", StringComparison.Ordinal));
                if (ambient) caveRoots.Add(ambient.gameObject);
                var controllers = all.SelectMany(t => t.GetComponents<Behaviour>()).Where(b => b is Art2SceneLightingTuning || b is Art2CinematicFocus).ToArray();
                var data = new SerializedObject(selector);
                data.FindProperty("fallbackEnvironment").objectReferenceValue = cave;
                var bindings = data.FindProperty("bindings"); bindings.arraySize = 2;
                void Bind(int index, FightEnvironmentDefinition profile, bool ready, GameObject[] roots, Behaviour[] behaviours)
                {
                    var b = bindings.GetArrayElementAtIndex(index);
                    b.FindPropertyRelative("environment").objectReferenceValue = profile;
                    b.FindPropertyRelative("ready").boolValue = ready;
                    var r = b.FindPropertyRelative("roots"); var enabled = b.FindPropertyRelative("rootEnabled"); r.arraySize = enabled.arraySize = roots.Length;
                    for (int i = 0; i < roots.Length; i++) { r.GetArrayElementAtIndex(i).objectReferenceValue = roots[i]; enabled.GetArrayElementAtIndex(i).boolValue = roots[i].activeSelf; }
                    r = b.FindPropertyRelative("controllers"); enabled = b.FindPropertyRelative("controllerEnabled"); r.arraySize = enabled.arraySize = behaviours.Length;
                    for (int i = 0; i < behaviours.Length; i++) { r.GetArrayElementAtIndex(i).objectReferenceValue = behaviours[i]; enabled.GetArrayElementAtIndex(i).boolValue = behaviours[i].enabled; }
                }
                Bind(0, cave, true, caveRoots.ToArray(), controllers);
                Bind(1, forest, false, new[] { forestRoot }, Array.Empty<Behaviour>());
                data.ApplyModifiedProperties();
                var plan = new SerializedObject(planner); plan.FindProperty("environmentSelector").objectReferenceValue = selector; plan.ApplyModifiedProperties();
                var positions = all.Select(t => t.position).ToArray(); var states = all.Select(t => t.gameObject.activeSelf).ToArray();
                if (!selector.TryApply(cave, out var error)) throw new Exception(error);
                if (selector.TryApply(forest, out _)) throw new Exception("Unprepared Forest must not activate");
                for (int i = 0; i < all.Length; i++) if (all[i].position != positions[i] || all[i].gameObject.activeSelf != states[i]) throw new Exception("Environment setup changed authored scene layout: " + all[i].name);
                if (planner.SelectedEnvironment != cave) throw new Exception("Active Boss Stage no longer selects Cave");
                var chapter = AssetDatabase.LoadAssetAtPath<FightChapterDefinition>("Assets/FightDemo/Levels/Chapter01.asset");
                if (chapter.DefaultEnvironment != forest) throw new Exception("Chapter 1 must inherit Forest");
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                File.WriteAllText("Temp/EnvironmentSelectorSetup.result", $"PASS: Chapter 1 Forest; Boss Cave; {caveRoots.Count} Cave roots and {controllers.Length} controllers bound; layout and activation preserved; unprepared Forest rejected without mutation.");
            }
            catch (Exception e) { File.WriteAllText("Temp/EnvironmentSelectorSetup.result", "FAIL: " + e); }
        }
    }
}
