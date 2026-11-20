using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmHunter.FightDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightFeverSetup
    {
        static FightFeverSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            if (File.Exists("Temp/FeverTuning.request") && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                File.Delete("Temp/FeverTuning.request");
                try
                {
                    ValidateCharge();
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                    if (!scene.isLoaded) throw new Exception("Open Art2_FightScene first.");
                    var fever = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightFeverController>(true)).Single();
                    var data = new SerializedObject(fever);
                    var charge = data.FindProperty("charge");
                    charge.FindPropertyRelative("initialGain").floatValue = .05f;
                    charge.FindPropertyRelative("finalGain").floatValue = .016f;
                    charge.FindPropertyRelative("decayPerBeat").floatValue = .024f;
                    charge.FindPropertyRelative("limitBeats").floatValue = 16;
                    data.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    File.WriteAllText("Temp/FeverTuning.result", "PASS: 16-beat challenge and charge tuning saved; charge checks passed.");
                }
                catch (Exception e) { File.WriteAllText("Temp/FeverTuning.result", "FAIL: " + e); }
                return;
            }
            if (File.Exists("Temp/FeverRefresh.request") && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                File.Delete("Temp/FeverRefresh.request"); AssetDatabase.Refresh(); return;
            }
            if (!File.Exists("Temp/FeverSetup.request") || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete("Temp/FeverSetup.request");
            try { Configure(); File.WriteAllText("Temp/FeverSetup.result", "PASS: charge checks; Fever tutorial selected, full opening mana, camera and VFX wired; scene saved."); }
            catch (Exception e) { File.WriteAllText("Temp/FeverSetup.result", "FAIL: " + e); }
        }
        [MenuItem("Rhythm Hunter/Fever/Configure Tutorial and Validate")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ValidateCharge();
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            if (!scene.isLoaded) throw new Exception("Open Art2_FightScene first.");
            var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
            var stage = AssetDatabase.LoadAssetAtPath<FightStageDefinition>("Assets/FightDemo/Levels/Chapter01_Stage05_Tutorial.asset");
            var stageData = new SerializedObject(stage);
            stageData.FindProperty("teachesFever").boolValue = true;
            stageData.FindProperty("includeInProgression").boolValue = true;
            stageData.FindProperty("displayName").stringValue = "Chapter 1 - Stage 5 - Fever";
            stageData.FindProperty("waves").GetArrayElementAtIndex(0).FindPropertyRelative("front").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/TrainingDummy.prefab");
            stageData.ApplyModifiedProperties();
            var planner = fight.GetComponent<FightLevelPlanner>(); var plan = new SerializedObject(planner);
            plan.FindProperty("chapter").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FightChapterDefinition>("Assets/FightDemo/Levels/Chapter01.asset");
            plan.FindProperty("stageIndex").intValue = 4; plan.ApplyModifiedProperties();
            var data = new SerializedObject(fight); data.FindProperty("suspendFeverActivation").boolValue = false; data.ApplyModifiedProperties();
            var fever = fight.GetComponent<FightFeverController>() ?? Undo.AddComponent<FightFeverController>(fight.gameObject);
            var feverData = new SerializedObject(fever);
            feverData.FindProperty("chargeEffectPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/VFX/GemEnergyChargeEffect.prefab");
            feverData.FindProperty("battleCamera").objectReferenceValue = data.FindProperty("damageNumberCamera").objectReferenceValue;
            feverData.ApplyModifiedProperties();
            if (!fight.GetComponent<FightGamepadRumble>()) Undo.AddComponent<FightGamepadRumble>(fight.gameObject);
            if (!stage.Validate(out string error)) throw new Exception(error);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        public static void ValidateCharge()
        {
            var c = new FightFeverCharge();
            c.Reset(); c.Press(); float first = c.Progress;
            for (int i = 0; i < 12; i++) c.Press();
            float before = c.Progress; c.Press();
            Check(c.Progress - before < first, "Resistance must grow");
            before = c.Progress; c.Advance(1); Check(c.Progress < before, "Idle progress must decay");
            c.Reset(); c.Advance(15.99); Check(!c.Expired, "Must allow input before 16 beats");
            c.Advance(.01); Check(c.Expired && !c.Press() && !c.Won, "16-beat deadline rejects input");
            c.Reset(); int presses = 0;
            while (!c.Won && !c.Expired) { c.Advance(.2); c.Press(); presses++; }
            Check(c.Won && c.Elapsed < 16 && presses > 20, "Rapid input must win with resistance");
            c.Reset(); while (!c.Expired && !c.Won) { c.Advance(2); c.Press(); }
            Check(c.Expired, "Slow input should fail");
            c.Reset(); Check(c.Progress == 0 && !c.Won && !c.Expired, "Retry reset");
        }
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
