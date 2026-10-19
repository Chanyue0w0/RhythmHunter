using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [CustomEditor(typeof(FightCharacterCombatAnimator))]
    public sealed class FightCharacterCombatAnimatorEditor : Editor
    {
        private readonly bool[] expanded = { true, true, false, false, false, false, false, false, false, false };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            Field("targetRenderer");
            Field("beatSource");
            Field("fallbackBpm");

            DrawSequence(8, 7, "Opening Idle — 開場清醒待機");
            DrawSequence(9, 8, "Drink — 喝酒");

            expanded[0] = EditorGUILayout.Foldout(expanded[0], "Idle — 主要待機（酒醉）", true);
            if (expanded[0])
            {
                EditorGUI.indentLevel++;
                DrawFrames(serializedObject.FindProperty("frames"),
                    serializedObject.FindProperty("useIdleFrameBeatDurations"),
                    serializedObject.FindProperty("idleFrameBeatDurations"));
                if (!serializedObject.FindProperty("useIdleFrameBeatDurations").boolValue) Field("cyclesPerBeat");
                Field("pingPong");
                Field("phaseOffset");
                EditorGUI.indentLevel--;
            }

            DrawSequence(1, 0, "Normal — 普通能力");
            DrawSequence(2, 4, "TeamSkill — 隊伍技能");
            DrawSequence(3, 5, "GetHurt — 受傷");
            DrawSequence(4, 6, "Die — 死亡");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("其他動作／舊流程", EditorStyles.boldLabel);
            DrawSequence(5, 3, "Guard — 防禦");
            DrawSequence(6, 2, "Heavy Attack — 重擊");
            DrawSequence(7, 1, "Light Attack — 舊版輕擊");
            EditorGUILayout.HelpBox("圖片拍數控制演出；Team Skill 總拍數與傷害時機仍由戰鬥系統設定。分類不會新增死亡等戰鬥觸發流程。", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSequence(int foldout, int type, string title)
        {
            expanded[foldout] = EditorGUILayout.Foldout(expanded[foldout], title, true);
            if (!expanded[foldout]) return;
            EditorGUI.indentLevel++;
            SerializedProperty list = serializedObject.FindProperty("sequences");
            SerializedProperty sequence = null;
            int matches = 0;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty item = list.GetArrayElementAtIndex(i);
                if (item.FindPropertyRelative("animation").intValue != type) continue;
                matches++;
                if (sequence == null) sequence = item;
            }
            if (matches > 1)
                EditorGUILayout.HelpBox("此分類有重複序列，播放使用第一份。可在 Debug Inspector 檢查舊資料。", MessageType.Warning);
            if (sequence == null)
            {
                if (GUILayout.Button("新增此類動畫"))
                {
                    int index = list.arraySize;
                    list.InsertArrayElementAtIndex(index);
                    sequence = list.GetArrayElementAtIndex(index);
                    sequence.FindPropertyRelative("animation").intValue = type;
                    sequence.FindPropertyRelative("frames").ClearArray();
                    sequence.FindPropertyRelative("frameBeatDurations").ClearArray();
                    sequence.FindPropertyRelative("useFrameBeatDurations").boolValue = true;
                    sequence.FindPropertyRelative("framesPerSecond").floatValue = 16;
                    sequence.FindPropertyRelative("warningFrame").intValue = -1;
                    sequence.FindPropertyRelative("attackEffectFrame").intValue = -1;
                    sequence.FindPropertyRelative("damageFrame").intValue = 0;
                }
            }
            if (sequence != null)
            {
                DrawFrames(sequence.FindPropertyRelative("frames"), sequence.FindPropertyRelative("useFrameBeatDurations"),
                    sequence.FindPropertyRelative("frameBeatDurations"), type == 7 || type == 8 ? null : sequence);
                if (!sequence.FindPropertyRelative("useFrameBeatDurations").boolValue)
                    EditorGUILayout.PropertyField(sequence.FindPropertyRelative("framesPerSecond"));
            }
            EditorGUI.indentLevel--;
        }

        private static void DrawFrames(SerializedProperty frames, SerializedProperty useBeats, SerializedProperty durations,
            SerializedProperty sequence = null)
        {
            EditorGUILayout.PropertyField(useBeats, new GUIContent("逐張設定拍數"));
            while (durations.arraySize < frames.arraySize)
            {
                int index = durations.arraySize;
                durations.InsertArrayElementAtIndex(index);
                durations.GetArrayElementAtIndex(index).floatValue = 0.5f;
            }
            for (int i = 0; i < frames.arraySize; i++)
            {
                int move = 0;
                bool remove;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"圖片 {i + 1}", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(frames.GetArrayElementAtIndex(i), new GUIContent("Sprite"));
                    SerializedProperty duration = durations.GetArrayElementAtIndex(i);
                    if (useBeats.boolValue)
                    {
                        EditorGUI.BeginChangeCheck();
                        float value = EditorGUILayout.FloatField("持續拍數", duration.floatValue);
                        if (EditorGUI.EndChangeCheck())
                            duration.floatValue = float.IsNaN(value) || float.IsInfinity(value) ? 0.5f : Mathf.Max(0.01f, value);
                    }
                    else EditorGUILayout.LabelField("持續時間", "使用此動畫的 FPS／循環速度");
                    if (sequence != null)
                    {
                        FrameEvent(sequence, "warningFrame", "進入圖片時生成 Warning", i);
                        FrameEvent(sequence, "attackEffectFrame", "進入圖片時生成攻擊特效", i);
                        FrameEvent(sequence, "damageFrame", "進入圖片時發出傷害事件（舊流程）", i);
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(i == 0))
                            if (GUILayout.Button("上移")) move = -1;
                        using (new EditorGUI.DisabledScope(i == frames.arraySize - 1))
                            if (GUILayout.Button("下移")) move = 1;
                        remove = GUILayout.Button("刪除圖片");
                    }
                }
                if (remove)
                {
                    frames.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    frames.DeleteArrayElementAtIndex(i);
                    durations.DeleteArrayElementAtIndex(i);
                    RemapEvents(sequence, i, -1);
                    break;
                }
                if (move != 0)
                {
                    frames.MoveArrayElement(i, i + move);
                    durations.MoveArrayElement(i, i + move);
                    RemapEvents(sequence, i, i + move);
                    break;
                }
            }
            if (GUILayout.Button("新增圖片"))
            {
                int index = frames.arraySize;
                frames.InsertArrayElementAtIndex(index);
                frames.GetArrayElementAtIndex(index).objectReferenceValue = null;
                if (durations.arraySize <= index) durations.InsertArrayElementAtIndex(index);
                durations.GetArrayElementAtIndex(index).floatValue = 0.5f;
            }
            if (useBeats.boolValue)
            {
                float total = 0;
                for (int i = 0; i < frames.arraySize; i++)
                    total += Mathf.Max(0.01f, durations.GetArrayElementAtIndex(i).floatValue);
                EditorGUILayout.LabelField("單次正向總長", $"{total:0.###} 拍");
            }
            if (sequence != null)
                EditorGUILayout.HelpBox("每種事件每套動畫各觸發一次；勾選另一張會移動該事件。上移／下移會一起移動圖片、拍數與事件。", MessageType.None);
        }

        private static void FrameEvent(SerializedProperty sequence, string name, string label, int index)
        {
            SerializedProperty property = sequence.FindPropertyRelative(name);
            bool enabled = property.intValue == index;
            bool value = EditorGUILayout.ToggleLeft(label, enabled);
            if (value != enabled) property.intValue = value ? index : -1;
        }

        private static void RemapEvents(SerializedProperty sequence, int from, int to)
        {
            if (sequence == null) return;
            foreach (string name in new[] { "warningFrame", "attackEffectFrame", "damageFrame" })
            {
                SerializedProperty property = sequence.FindPropertyRelative(name);
                int index = property.intValue;
                if (to < 0) property.intValue = index == from ? -1 : index > from ? index - 1 : index;
                else if (index == from) property.intValue = to;
                else if (index == to) property.intValue = from;
            }
        }

        private void Field(string name) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), true);
    }
}
