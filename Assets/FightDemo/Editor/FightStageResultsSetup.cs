using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightStageResultsSetup
    {
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        static FightStageResultsSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string path = "Temp/StageResultsSetup.request";
            if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(path); }
            catch (IOException) { return; } // The request writer may still hold the file for this frame.
            try { Run(); File.WriteAllText("Temp/StageResultsSetup.result", "PASS: setup saved; timing pause/finalization/overtime/unknown length; victory and defeat UI; partial waves; independent visibility; duplicate result and cleanup."); }
            catch (Exception e) { File.WriteAllText("Temp/StageResultsSetup.result", "FAIL: " + e); Debug.LogException(e); }
        }
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Reference(SerializedObject data, string name, UnityEngine.Object value) => data.FindProperty(name).objectReferenceValue = value;
        private static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.path.EndsWith("Art2_FightScene.unity")) throw new Exception("Open Art2 outside Play Mode.");
            var roots = scene.GetRootGameObjects();
            var fight = roots.SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
            var planner = roots.SelectMany(r => r.GetComponentsInChildren<FightLevelPlanner>(true)).Single();
            var clock = roots.SelectMany(r => r.GetComponentsInChildren<FmodBeatClock>(true)).Single();
            var results = fight.GetComponent<FightStageResults>() ?? Undo.AddComponent<FightStageResults>(fight.gameObject);
            var data = new SerializedObject(results);
            Reference(data, "fight", fight); Reference(data, "planner", planner); Reference(data, "beatClock", clock);
            data.ApplyModifiedProperties();
            var panel = fight.GetComponent<FightVictoryPanel>() ?? Undo.AddComponent<FightVictoryPanel>(fight.gameObject);
            data = new SerializedObject(panel); Reference(data, "results", results); data.ApplyModifiedProperties();
            Validate(fight);
            ValidateDefeat();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
        }
        private static void ValidateDefeat()
        {
            var root = new GameObject("Defeat Results Validation") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var results = root.AddComponent<FightStageResults>();
                Call(results, "Begin");
                Set(results, "<TotalWaves>k__BackingField", 3);
                Call(results, "OnWaveCleared", 1);
                Call(results, "Advance", 12f, 1f, false);
                var panel = root.AddComponent<FightVictoryPanel>();
                Set(panel, "results", results);
                Set(panel, "showVictoryPanel", false);
                Set(panel, "showDefeatPanel", false);
                Call(panel, "OnEnable");
                Call(results, "OnDefeat");
                Check(!panel.IsVisible && results.Completed && !results.Won && results.ClearedWaves == 1, "Defeat state/count or visibility switch failed");
                Set(panel, "showDefeatPanel", true);
                Call(panel, "Show");
                Call(results, "OnVictory");
                Call(results, "Advance", 50f, 1f, false);
                var texts = root.GetComponentsInChildren<Text>();
                Check(panel.IsVisible && texts.Any(t => t.text == "DEFEAT") && texts.Any(t => t.text == "WAVES CLEARED   1 / 3"), "Defeat panel missing or partial waves overwritten");
                ValidateTitle(texts.Single(t => t.text == "DEFEAT"));
                Check(!results.Won && results.ElapsedSeconds == 12 && root.GetComponentsInChildren<Canvas>().Length == 1, "Defeat changed after completion or duplicated UI");
                Check(root.GetComponentInChildren<Button>() != null, "Defeat restart button missing");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Validate(FightCombatController fight)
        {
            var root = new GameObject("Stage Results Validation") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var results = root.AddComponent<FightStageResults>();
                Set(results, "fight", fight);
                Call(results, "Advance", 3f, 1f, false);
                Check(results.ElapsedSeconds == 0, "Preparation counted toward stage time");
                Call(results, "Begin");
                Set(results, "<SongLengthSeconds>k__BackingField", 10f);
                Call(results, "Advance", 7f, 1f, false);
                Call(results, "Advance", 100f, 1f, true);
                Check(results.ElapsedSeconds == 7, "Pause/calibration advanced stage time");
                Call(results, "Advance", 2f, 2f, false);
                Check(results.ElapsedSeconds == 9 && results.SongProgressSeconds == 11 && results.OvertimeSeconds == 1, "Pitch-aware song overtime is incorrect");
                var panel = root.AddComponent<FightVictoryPanel>();
                Set(panel, "results", results);
                Call(panel, "OnEnable");
                int events = 0; results.ResultReady += () => events++;
                Call(results, "Finish", true);
                Call(results, "Finish", true);
                Call(results, "Advance", 100f, 1f, false);
                Check(events == 1 && results.ElapsedSeconds == 9 && results.Completed && panel.IsVisible, "Completion did not freeze stats/show UI exactly once");
                var canvas = root.GetComponentInChildren<Canvas>();
                Check(canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.sortingOrder == 200, "Results are not independent player UI");
                var texts = root.GetComponentsInChildren<Text>();
                Check(texts.Any(t => t.text == "VICTORY") && texts.Any(t => t.text.StartsWith("OVERTIME")), "Victory statistics missing");
                ValidateTitle(texts.Single(t => t.text == "VICTORY"));
                foreach (var text in texts) Check(text.rectTransform.anchorMin == new Vector2(.5f, .5f), "Result label is not centered");
                Call(panel, "OnDisable");
                Check(!panel.IsVisible, "Disabled panel stayed visible");
                Set(results, "<SongLengthSeconds>k__BackingField", 0f);
                Check(results.OvertimeSeconds == 0, "Unknown song length was reported as overtime");
                // All temporary objects are removed before saving the user's scene.
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void ValidateTitle(Text title)
        {
            Canvas.ForceUpdateCanvases();
            Check(title.rectTransform.rect.height >= title.preferredHeight, "Title line height exceeds its rect and is truncated");
            var settings = title.GetGenerationSettings(title.rectTransform.rect.size);
            var generator = new TextGenerator();
            generator.Populate(title.text, settings);
            Check(generator.vertexCount > 4, "Title has text but generated no visible glyphs");
            var before = settings;
            before.generationExtents = new Vector2(960, 64);
            var oldGenerator = new TextGenerator();
            oldGenerator.Populate(title.text, before);
            Debug.Log($"RESULT_TITLE_GEOMETRY: {title.text}, old64px={oldGenerator.vertexCount}, fitted={generator.vertexCount}, height={title.rectTransform.rect.height}, preferred={title.preferredHeight}");
        }
    }
}
