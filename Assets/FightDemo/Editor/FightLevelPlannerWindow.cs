using UnityEditor;
using UnityEngine;
using RhythmHunter.FightDemo;

namespace RhythmHunter.FightDemoEditor
{
    public sealed class FightLevelPlannerWindow : EditorWindow
    {
        private FightChapterDefinition chapter;
        private FightStageDefinition stage;
        private Vector2 scroll;
        [MenuItem("Rhythm Hunter/Level Planner")]
        public static void Open() => GetWindow<FightLevelPlannerWindow>("Level Planner");
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Chapter → Stage（一場戰鬥／一首歌）→ Wave（前、中、後衛敵人）。場景 FightLevelPlanner 選擇 Chapter 與 Stage Index；玩家隊伍仍由 FightRosterManager 設定。", MessageType.Info);
            chapter = (FightChapterDefinition)EditorGUILayout.ObjectField("Chapter", chapter, typeof(FightChapterDefinition), false);
            if (chapter == null && Selection.activeObject is FightChapterDefinition selected) chapter = selected;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Chapter")) Create<FightChapterDefinition>("Chapter");
                if (GUILayout.Button("New Stage")) Create<FightStageDefinition>("Stage");
                if (GUILayout.Button("New Environment")) Create<FightEnvironmentDefinition>("Environment");
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (chapter != null)
            {
                Draw(chapter);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Stages（點選編輯）", EditorStyles.boldLabel);
                for (int i = 0; i < chapter.Stages.Count; i++)
                {
                    var item = chapter.Stages[i];
                    var environment = item != null && item.Environment != null ? item.Environment : chapter.DefaultEnvironment;
                    string tags = item != null ? $"{(item.Tutorial ? " [Tutorial]" : "")}{(item.BossStage ? " [Boss]" : "")} [{(environment != null ? environment.DisplayName : "Cave（預設）")}]" : "";
                    if (GUILayout.Button($"{i + 1}. {(item != null ? item.DisplayName : "未指定 Stage")}{tags}")) stage = item;
                }
            }
            stage = (FightStageDefinition)EditorGUILayout.ObjectField("Edit Stage", stage, typeof(FightStageDefinition), false);
            if (stage == null && Selection.activeObject is FightStageDefinition selectedStage) stage = selectedStage;
            if (stage != null)
            {
                Draw(stage);
                if (!stage.Validate(out var error)) EditorGUILayout.HelpBox(error, MessageType.Error);
                EditorGUILayout.HelpBox("Tutorial 僅標記教學關。Environment 優先使用 Stage 設定，留空則繼承 Chapter 的 Default Environment，最後才回退 Cave。Art2 場景已提供 Cave 與 Forest 的 2.5D 配置；Chapter01 預設 Forest。", MessageType.Info);
                if (stage.Tutorial && stage.BossStage)
                    EditorGUILayout.HelpBox("此關同時標記 Tutorial 與 Boss；目前仍會執行原有 Boss 流程。", MessageType.Warning);
                EditorGUILayout.HelpBox("Wave 的 Front／Middle／Back 是敵人位置，可留空但整波不能全空。拖入 ArtBattle 的敵人 Prefab；音樂、加時與 Pressure 依 Stage／Wave 設定。", MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Save Assets")) AssetDatabase.SaveAssets();
        }
        private static void Draw(Object asset)
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                var data = new SerializedObject(asset);
                data.Update();
                var property = data.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.name != "m_Script") EditorGUILayout.PropertyField(property, true);
                }
                data.ApplyModifiedProperties();
            }
        }
        private static void Create<T>(string label) where T : ScriptableObject
        {
            string path = EditorUtility.SaveFilePanelInProject("Create " + label, label, "asset", "Select an asset location.", "Assets/FightDemo");
            if (string.IsNullOrEmpty(path)) return;
            var asset = CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssets(); Selection.activeObject = asset;
        }
    }
}
