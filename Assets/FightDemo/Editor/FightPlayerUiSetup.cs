using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmHunter.FightDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightPlayerUiSetup
    {
        static FightPlayerUiSetup() => EditorApplication.update += Requested;
        static void Requested()
        {
            const string path = "Temp/PlayerUiSetup.request";
            if (!File.Exists(path) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            string mode;
            try { mode = File.ReadAllText(path).Trim(); File.Delete(path); } catch (IOException) { return; }
            try
            {
                if (mode == "hide-damage-numbers")
                {
                    var scene = SceneManager.GetActiveScene();
                    if (!scene.path.EndsWith("Art2_FightScene.unity")) throw new InvalidOperationException("Open Art2 first.");
                    var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                    Undo.RecordObject(fight, "Hide damage numbers");
                    var data = new SerializedObject(fight);
                    data.FindProperty("showEnemyDamageNumbers").boolValue = false;
                    data.ApplyModifiedProperties();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(fight);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
                    File.WriteAllText("Temp/PlayerUiSetup.result", "PASS: damage numbers disabled; Art2 saved; controller=" + fight.name);
                    return;
                }
                if (mode == "hide-timeline-bar")
                {
                    var scene = SceneManager.GetActiveScene();
                    if (!scene.path.EndsWith("Art2_FightScene.unity")) throw new InvalidOperationException("Open Art2 first.");
                    var presenter = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightScenePresenter>(true)).Single();
                    Undo.RecordObject(presenter, "Hide timeline bar artwork");
                    var data = new SerializedObject(presenter);
                    data.FindProperty("showTimelineBar").boolValue = false;
                    data.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
                    File.WriteAllText("Temp/PlayerUiSetup.result", "PASS: timeline bar hidden; Art2 saved.");
                    return;
                }
                bool hideWorldInfo = mode == "hide-world-info";
                if (hideWorldInfo) HideWorldInformation(); else Configure();
                File.WriteAllText("Temp/PlayerUiSetup.result", hideWorldInfo ? "PASS: Art2 world labels and health visuals hidden; enemy health UI disabled; scene saved." : "PASS: live Art2 scene references assigned and saved.");
            }
            catch (Exception ex) { File.WriteAllText("Temp/PlayerUiSetup.result", "FAIL: " + ex); Debug.LogException(ex); }
        }
        [MenuItem("Rhythm Hunter/Art2/Hide World Unit Information")]
        public static void HideWorldInformation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/FightDemo/Scenes/Art2_FightScene.unity") throw new InvalidOperationException("Open Art2_FightScene first.");
            var slots = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightUnitSlot>(true)).ToArray();
            foreach (var slot in slots)
                foreach (Transform child in slot.transform)
                    if (child.GetComponent<TextMesh>() || child.name == "HealthFill" || child.name == "HealthBackground")
                    {
                        Undo.RecordObject(child.gameObject, "Hide world unit information");
                        child.gameObject.SetActive(false);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                    }
            var presenter = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightScenePresenter>(true)).Single();
            Undo.RecordObject(presenter, "Hide enemy health UI");
            var data = new SerializedObject(presenter);
            data.FindProperty("showEnemyHealthUi").boolValue = false;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(presenter);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
            var report = new System.Text.StringBuilder();
            foreach (var slot in slots)
                foreach (Transform child in slot.transform)
                    if (child.GetComponent<TextMesh>() || child.name == "HealthFill" || child.name == "HealthBackground")
                    {
                        if (child.gameObject.activeSelf) throw new InvalidOperationException("Label is still visible: " + child.name);
                        report.AppendLine(slot.name + "/" + child.name);
                    }
            File.WriteAllText("Temp/WorldInfoHidden.txt", report.ToString());
        }
        [MenuItem("Rhythm Hunter/Art2/Configure Player UI Artwork")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/FightDemo/Scenes/Art2_FightScene.unity") throw new InvalidOperationException("Open Art2_FightScene first.");
            var presenter = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightScenePresenter>(true)).Single();
            var canvas = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Single(c => c.name == "FightHudCanvas");
            var data = new SerializedObject(presenter);
            Undo.RecordObject(presenter, "Configure player UI artwork");
            const string folder = "Assets/FightDemo/Arts/UI/戰鬥場景/節拍器/";
            Sprite Load(string name)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(folder + name + ".png");
                if (sprite == null) throw new InvalidOperationException("Missing sprite " + name);
                return sprite;
            }
            data.FindProperty("playerUiCanvas").objectReferenceValue = canvas;
            data.FindProperty("partyResourceHudPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FightPartyResourceHud>("Assets/FightDemo/Prefabs/UI/PartyResourceHud.prefab");
            data.FindProperty("timelineBarSprite").objectReferenceValue = Load("bar(5 ver)");
            data.FindProperty("timelinePointSprite").objectReferenceValue = Load("blue/Beat bar(blue)");
            data.FindProperty("stoneEmptySprite").objectReferenceValue = Load("blue/stone_1(blue)");
            data.FindProperty("stoneFillSprite").objectReferenceValue = Load("blue/stone_2(blue)");
            var frames = data.FindProperty("stoneReadyFrames"); frames.arraySize = 5;
            for (int i = 0; i < 5; i++) frames.GetArrayElementAtIndex(i).objectReferenceValue = Load("blue/stone_4(blue)_" + (i + 1));
            data.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
        }
    }
}
