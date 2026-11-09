using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightLevelPlannerSetup
    {
        static FightLevelPlannerSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/LevelPlannerSetup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(request); } catch (IOException) { return; }
            try { Run(); } catch (Exception e) { File.WriteAllText("Temp/LevelPlannerSetup.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Levels/Migrate Current Art2 Roster")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.path.EndsWith("Art2_FightScene.unity")) throw new Exception("Open Art2 outside Play Mode.");
            var roots = scene.GetRootGameObjects();
            var roster = roots.SelectMany(r => r.GetComponentsInChildren<FightRosterManager>(true)).Single();
            var clock = roots.SelectMany(r => r.GetComponentsInChildren<FmodBeatClock>(true)).Single();
            var source = new SerializedObject(roster);
            if (!AssetDatabase.IsValidFolder("Assets/FightDemo/Levels")) AssetDatabase.CreateFolder("Assets/FightDemo", "Levels");
            const string folder = "Assets/FightDemo/Levels/";
            var stage = AssetDatabase.LoadAssetAtPath<FightStageDefinition>(folder + "Stage01.asset");
            if (!stage)
            {
                stage = ScriptableObject.CreateInstance<FightStageDefinition>();
                var data = new SerializedObject(stage);
                data.FindProperty("musicEventPath").stringValue = clock.MusicEventPath;
                var initial = source.FindProperty("enemyPrefabs");
                var extra = source.FindProperty("additionalWaves");
                var slots = source.FindProperty("enemySpawnSlots");
                int[] order = Enumerable.Range(0, 3).OrderByDescending(i => ((FightUnitSlot)slots.GetArrayElementAtIndex(i).objectReferenceValue).SlotIndex).ToArray();
                var waves = data.FindProperty("waves"); waves.arraySize = 1 + extra.arraySize;
                for (int w = 0; w < waves.arraySize; w++)
                {
                    var input = w == 0 ? initial : extra.GetArrayElementAtIndex(w - 1).FindPropertyRelative("enemies");
                    var wave = waves.GetArrayElementAtIndex(w);
                    wave.FindPropertyRelative("name").stringValue = "Wave " + (w + 1);
                    string[] names = { "front", "middle", "back" };
                    for (int p = 0; p < 3; p++) wave.FindPropertyRelative(names[p]).objectReferenceValue = input.GetArrayElementAtIndex(order[p]).objectReferenceValue;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                if (!stage.Validate(out string error)) throw new Exception(error);
                AssetDatabase.CreateAsset(stage, folder + "Stage01.asset");
            }
            var chapter = AssetDatabase.LoadAssetAtPath<FightChapterDefinition>(folder + "Chapter01.asset");
            if (!chapter)
            {
                chapter = ScriptableObject.CreateInstance<FightChapterDefinition>();
                var data = new SerializedObject(chapter);
                var stages = data.FindProperty("stages"); stages.arraySize = 1;
                stages.GetArrayElementAtIndex(0).objectReferenceValue = stage;
                data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(chapter, folder + "Chapter01.asset");
            }
            ValidateSnapshots(stage, source);
            var planner = roster.GetComponent<FightLevelPlanner>();
            if (!planner) planner = Undo.AddComponent<FightLevelPlanner>(roster.gameObject);
            Undo.RecordObject(planner, "Select initial Stage");
            var setup = new SerializedObject(planner);
            setup.FindProperty("chapter").objectReferenceValue = chapter;
            setup.FindProperty("stageIndex").intValue = 0;
            setup.FindProperty("roster").objectReferenceValue = roster;
            setup.FindProperty("beatClock").objectReferenceValue = clock;
            setup.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed");
            File.WriteAllText("Temp/LevelPlannerSetup.result", "PASS: Chapter01/Stage01 migrated from existing song and enemies; front/back mapping, two-wave snapshot isolation, invalid-wave rejection and battle lock verified. Art2 planner assigned; legacy configuration retained.");
        }

        private static void ValidateSnapshots(FightStageDefinition sourceStage, SerializedObject sceneRoster)
        {
            var test = new GameObject("Stage Data Validation") { hideFlags = HideFlags.HideAndDontSave };
            var stage = UnityEngine.Object.Instantiate(sourceStage);
            try
            {
                var roster = test.AddComponent<FightRosterManager>();
                var data = new SerializedObject(roster);
                data.FindProperty("requireBattlePreparation").boolValue = true;
                var slots = data.FindProperty("enemySpawnSlots"); slots.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    var slotObject = new GameObject("Validation Enemy " + i);
                    slotObject.transform.SetParent(test.transform);
                    var slot = slotObject.AddComponent<FightUnitSlot>();
                    var slotData = new SerializedObject(slot);
                    var original = (FightUnitSlot)sceneRoster.FindProperty("enemySpawnSlots").GetArrayElementAtIndex(i).objectReferenceValue;
                    slotData.FindProperty("slotIndex").intValue = original.SlotIndex;
                    slotData.ApplyModifiedPropertiesWithoutUndo();
                    slots.GetArrayElementAtIndex(i).objectReferenceValue = slot;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                var stageData = new SerializedObject(stage);
                stageData.FindProperty("waves").arraySize = 2; stageData.ApplyModifiedPropertiesWithoutUndo();
                if (!roster.TryConfigureStage(stage, out string error) || roster.WaveCount != 2) throw new Exception(error ?? "Two-wave configuration failed");
                var field = typeof(FightRosterManager).GetField("stageWaves", BindingFlags.NonPublic | BindingFlags.Instance);
                var snapshot = (FightCharacterDefinition[][])field.GetValue(roster);
                for (int i = 0; i < 3; i++)
                {
                    var slot = (FightUnitSlot)slots.GetArrayElementAtIndex(i).objectReferenceValue;
                    int rank = Enumerable.Range(0, 3).Count(j => ((FightUnitSlot)slots.GetArrayElementAtIndex(j).objectReferenceValue).SlotIndex > slot.SlotIndex);
                    if (snapshot[0][i] != sourceStage.Waves[0].At(rank)) throw new Exception("Front/back mapping changed");
                }
                var before = (FightCharacterDefinition[])snapshot[1].Clone();
                foreach (string name in new[] { "front", "middle", "back" }) stageData.FindProperty("waves").GetArrayElementAtIndex(1).FindPropertyRelative(name).objectReferenceValue = null;
                stageData.ApplyModifiedPropertiesWithoutUndo();
                if (!before.SequenceEqual(snapshot[1])) throw new Exception("Live asset edit mutated battle snapshot");
                if (roster.TryConfigureStage(stage, out _) || roster.WaveCount != 2 || field.GetValue(roster) != snapshot) throw new Exception("Invalid wave replaced valid Stage");
                roster.SpawnConfiguredRoster();
                if (roster.ActiveEnemies.Count != snapshot[0].Count(p => p != null)) throw new Exception("Initial wave spawn failed");
                typeof(FightRosterManager).GetField("<BattleStarted>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(roster, true);
                if (roster.TryConfigureStage(sourceStage, out _)) throw new Exception("Battle lock failed");
                bool advanced = (bool)typeof(FightRosterManager).GetMethod("SpawnNextEnemyWave", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(roster, new object[] { 1 });
                if (!advanced || roster.ActiveEnemies.Count != before.Count(p => p != null)) throw new Exception("Second wave spawn failed");
                for (int i = 0; i < 3; i++)
                {
                    var slot = (FightUnitSlot)slots.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (before[i] != null && (slot.CharacterDefinition == null || slot.CharacterDefinition.CharacterId != before[i].CharacterId))
                        throw new Exception("Second wave used edited asset instead of snapshot");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(stage); UnityEngine.Object.DestroyImmediate(test); }
        }
    }
}
