using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.RhythmDemoEditor
{
    [CustomEditor(typeof(FmodRhythmJudge))]
    [CanEditMultipleObjects]
    public sealed class FmodRhythmJudgeEditor : Editor
    {
        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("個人跟拍校正（共用 JSON）", EditorStyles.boldLabel);
            DrawSavedProfile("鍵盤保存值（ms）", "Keyboard");
            DrawSavedProfile("手把保存值（ms）", "Gamepad");
            EditorGUILayout.SelectableLabel(RhythmCalibrationStore.FilePath, EditorStyles.textField, GUILayout.Height(36));
            if (GUILayout.Button("開啟校正檔案位置")) EditorUtility.RevealInFinder(RhythmCalibrationStore.FilePath);
            if (!string.IsNullOrEmpty(RhythmCalibrationStore.LastError))
                EditorGUILayout.HelpBox(RhythmCalibrationStore.LastError, MessageType.Warning);
            EditorGUILayout.HelpBox("正值 = 習慣晚按，負值 = 習慣早按。SAVE 會保存至本機，停止 Play Mode 後仍保留；不寫入場景或 Prefab。未保存的 PREVIEW 不會改動上方保存值。", MessageType.Info);

            if (targets.Length != 1)
                return;

            var judge = (FmodRhythmJudge)target;
            if (!EditorApplication.isPlaying || !judge.PersonalCalibrationEnabled)
            {
                EditorGUILayout.HelpBox("進入 FightScene3 的 Play Mode 後，下方會顯示目前裝置與實際套用值。", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("目前實際套用（即時）", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("輸入裝置", judge.InputProfile);
                EditorGUILayout.FloatField("個人補償（ms）", judge.PersonalDelayMs);
                EditorGUILayout.FloatField("音樂／UI 基礎 Offset（ms）", judge.VisualOffsetMs);
                EditorGUILayout.FloatField("有效判定 Offset（ms）", judge.JudgementOffsetMs);
            }
            EditorGUILayout.HelpBox("有效判定 Offset = 基礎 Offset − 個人補償。試用期間的個人補償可能與保存值不同；音樂／UI 不隨個人補償移動。", MessageType.None);
        }

        private static void DrawSavedProfile(string label, string profile)
        {
            string key = "FightTiming.v1." + profile;
            using (new EditorGUI.DisabledScope(true))
            {
                if (RhythmCalibrationStore.HasSaved(profile))
                    EditorGUILayout.FloatField(new GUIContent(label, "Legacy PlayerPrefs key: " + key), RhythmCalibrationStore.GetDelay(profile));
                else
                    EditorGUILayout.TextField(new GUIContent(label, "PlayerPrefs key: " + key), "尚未保存（預設 0 ms）");
            }
        }
    }
}
