using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemo.Editor
{
    [InitializeOnLoad]
    static class FightGuidedTutorialSetup
    {
        static FightGuidedTutorialSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/GuidedTutorialSetup.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            bool revise = File.ReadAllText(request).Trim() == "revise";
            File.Delete(request);
            try { Build(revise); File.WriteAllText("Temp/GuidedTutorialSetup.result", "PASS: lesson attached, dummy lift-only motion and ground clearance saved."); }
            catch (Exception e) { File.WriteAllText("Temp/GuidedTutorialSetup.result", "FAIL: " + e); }
        }
        static void Build(bool revise)
        {
            const string path = "Assets/FightDemo/Levels/Tutorial_Alex.asset";
            var lesson = AssetDatabase.LoadAssetAtPath<FightTutorialLesson>(path);
            if (!lesson || revise)
            {
                bool create = !lesson;
                if (create) lesson = ScriptableObject.CreateInstance<FightTutorialLesson>();
                lesson.steps.Clear();
                string Mark(string s) => "<b><color=#FFD879>" + s + "</color></b>";
                void Page(string title, string text, bool charge = false) => lesson.steps.Add(new() { title = title, text = text, allowSwordCharge = charge });
                void Goal(string title, string text, FightTutorialLesson.Goal goal, int required, bool charge = false) => lesson.steps.Add(new() { title = title, text = text, goal = goal, required = required, allowSwordCharge = charge });
                Page("歡迎來到《對拍勇者》", "本教學使用 " + Mark("Xbox 搖桿") + "。\n跟著音樂節奏，呼喚勇者一起戰鬥吧！");
                Page("先學會對拍", "當拍點往中間寶石接近時，\n對拍按下 " + Mark("X") + "，讓勇者跳起來！");
                Goal("對拍呼喚勇者", "對拍按 " + Mark("X") + "。", FightTutorialLesson.Goal.OnBeatCalls, 8);
                Page("做得好！", "這隻角色是「" + Mark("戰士-艾力克斯") + "」\n\n對拍按下 " + Mark("X") + "，揮劍攻擊" + Mark("最前方的敵人") + "！");
                Goal("普通攻擊命中", "對拍按 " + Mark("X") + "，攻擊假人。", FightTutorialLesson.Goal.NormalHits, 4);
                Page("戰士-艾力克斯", "<b>技能： 節拍共振</b>\n\n攻擊可以" + Mark("充能") + "，滿能量時斬擊更強，並造成" + Mark("追加傷害") + "！", true);
                Goal("共振攻擊命中", "持續對拍攻擊，使出強化斬擊。", FightTutorialLesson.Goal.ChargedHits, 8, true);
                Goal("訓練完成！", "自由練習，或按 " + Mark("RB") + " 進入下一關教學。", FightTutorialLesson.Goal.FreePractice, 1, true);
                if (create) AssetDatabase.CreateAsset(lesson, path);
                else EditorUtility.SetDirty(lesson);
            }
            var stage = AssetDatabase.LoadAssetAtPath<FightStageDefinition>("Assets/FightDemo/Levels/Chapter01_Stage01_Tutorial.asset");
            var stageData = new SerializedObject(stage); stageData.FindProperty("tutorialLesson").objectReferenceValue = lesson;
            stageData.ApplyModifiedPropertiesWithoutUndo();
            var planner = UnityEngine.Object.FindFirstObjectByType<FightLevelPlanner>();
            if (!planner) throw new Exception("Open Art2_FightScene first.");
            if (!planner.GetComponent<FightTutorialController>()) planner.gameObject.AddComponent<FightTutorialController>();
            var uiData = new SerializedObject(planner.GetComponent<FightTutorialController>());
            uiData.FindProperty("objectivePosition").vector2Value = new Vector2(0, 210);
            uiData.ApplyModifiedPropertiesWithoutUndo();
            const string dummyPath = "Assets/FightDemo/Prefabs/ArtBattle/TrainingDummy.prefab";
            var dummy = PrefabUtility.LoadPrefabContents(dummyPath);
            try
            {
                var fields = new SerializedObject(dummy.GetComponent<FightCharacterDefinition>());
                fields.FindProperty("ignoreFloorOcclusion").boolValue = false; fields.ApplyModifiedPropertiesWithoutUndo();
                dummy.transform.Find("Visual").localPosition = new Vector3(0, .025f, 0);
                var motion = new SerializedObject(dummy.GetComponent<SceneBeatMotionManager>());
                motion.FindProperty("targets").GetArrayElementAtIndex(0).FindPropertyRelative("positiveOnly").boolValue = true;
                motion.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(dummy, dummyPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(dummy); }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(planner.gameObject.scene); EditorSceneManager.SaveScene(planner.gameObject.scene);
        }
    }
}
