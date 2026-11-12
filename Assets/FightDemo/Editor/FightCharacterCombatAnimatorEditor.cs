using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [CustomEditor(typeof(FightCharacterCombatAnimator))]
    public sealed class FightCharacterCombatAnimatorEditor : Editor
    {
        public bool EmbeddedPreview { get; set; }
        private readonly bool[] expanded = { true, true, false, false, false, false, false, false, false, false, false, true };

        public override void OnInspectorGUI()
        {
            foreach (var inspected in targets) if (inspected == null) return;
            serializedObject.Update();
            if (!EmbeddedPreview && GUILayout.Button("開啟角色動畫預覽／編輯視窗"))
                WeaponAttachmentEditorWindow.OpenCharacter((FightCharacterCombatAnimator)target);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            Field("targetRenderer");
            Field("beatSource");
            Field("fallbackBpm");

            DrawSequence(8, 7, "Idle / Opening Idle — 普通待機（開場與戰鬥）");
            DrawSequence(11, 11, "Charged Idle — 滿充能待機（僅第 3 階）");
            DrawSequence(9, 8, "Drink — 喝酒");

            EditorGUILayout.PropertyField(serializedObject.FindProperty("useDrunkIdle"),
                new GUIContent("啟用酒醉 Idle", "開場結束後使用附加酒醉待機；關閉時繼續普通 Idle。"));
            expanded[0] = EditorGUILayout.Foldout(expanded[0], "附加 Idle — 酒醉／舊角色預設待機", true);
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
            DrawSequence(10, 9, "Aim — 射手舉槍瞄準（準備拍）");
            DrawSequence(2, 4, "TeamSkill — 隊伍技能");
            DrawSequence(3, 5, "GetHurt — 受傷");
            DrawSequence(4, 6, "Die — 死亡");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("其他動作／舊流程", EditorStyles.boldLabel);
            DrawSequence(5, 3, "Guard — 防禦");
            DrawSequence(6, 2, "Heavy Attack — 重擊");
            DrawSequence(7, 1, "Light Attack — 舊版輕擊");
            EditorGUILayout.HelpBox("Team Skill 在第一張勾選『生成攻擊特效』的圖片結算能力；附加特效不會觸發傷害。Aim 只負責準備演出；Normal 播放射擊。", MessageType.Info);
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
                    sequence.FindPropertyRelative("attachmentFrameIds").ClearArray();
                    sequence.FindPropertyRelative("frameEffects").ClearArray();
                    sequence.FindPropertyRelative("frameBeatDurations").ClearArray();
                    sequence.FindPropertyRelative("useFrameBeatDurations").boolValue = true;
                    sequence.FindPropertyRelative("framesPerSecond").floatValue = 16;
                    sequence.FindPropertyRelative("warningFrame").intValue = -1;
                    sequence.FindPropertyRelative("attackEffectFrame").intValue = -1;
                    sequence.FindPropertyRelative("damageFrame").intValue = -1;
                }
            }
            if (sequence != null)
            {
                EditorGUILayout.PropertyField(sequence.FindPropertyRelative("warningEffect"), new GUIContent("Warning 特效與音效（每次勾選共用）"), true);
                var warningData = sequence.FindPropertyRelative("warningEffect");
                var warningRows = sequence.FindPropertyRelative("frameEffects");
                bool hasWarning = sequence.FindPropertyRelative("warningFrame").intValue >= 0;
                for (int row = 0; row < warningRows.arraySize; row++)
                    hasWarning |= warningRows.GetArrayElementAtIndex(row).FindPropertyRelative("playWarning").boolValue;
                if (hasWarning && warningData.FindPropertyRelative("prefab").objectReferenceValue == null)
                    EditorGUILayout.HelpBox("已勾選 Warning，但尚未指定共用 Prefab；正式警告特效與音效不會播放。請展開上方 Warning 設定。", MessageType.Warning);
                else if (hasWarning && string.IsNullOrWhiteSpace(warningData.FindPropertyRelative("soundEventPath").stringValue))
                    EditorGUILayout.HelpBox("Warning 特效已指定，但 Sound Event Path 留空，因此不會播放警告音效。", MessageType.Info);
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
            var ids = frames.serializedObject.FindProperty(frames.propertyPath.Substring(0, frames.propertyPath.Length - "frames".Length) + "attachmentFrameIds");
            bool hasIds = ids != null && ids.arraySize > 0;
            if (hasIds)
            {
                int previous = ids.arraySize;
                ids.arraySize = frames.arraySize;
                for (int i = previous; i < ids.arraySize; i++) ids.GetArrayElementAtIndex(i).stringValue = System.Guid.NewGuid().ToString("N");
            }
            SerializedProperty effectsByFrame = sequence?.FindPropertyRelative("frameEffects");
            if (effectsByFrame != null)
            {
                while (effectsByFrame.arraySize < frames.arraySize)
                {
                    int index = effectsByFrame.arraySize;
                    effectsByFrame.InsertArrayElementAtIndex(index);
                    effectsByFrame.GetArrayElementAtIndex(index).FindPropertyRelative("effects").ClearArray();
                    effectsByFrame.GetArrayElementAtIndex(index).FindPropertyRelative("playAttackEffect").boolValue = false;
                    effectsByFrame.GetArrayElementAtIndex(index).FindPropertyRelative("playWarning").boolValue = false;
                }
                effectsByFrame.arraySize = frames.arraySize;
            }
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
                        var legacyWarning = sequence.FindPropertyRelative("warningFrame");
                        var frameWarning = effectsByFrame.GetArrayElementAtIndex(i).FindPropertyRelative("playWarning");
                        bool warningEnabled = legacyWarning.intValue == i || frameWarning.boolValue;
                        bool warningValue = EditorGUILayout.ToggleLeft("進入圖片時生成 Warning（特效＋音效，可多選）", warningEnabled);
                        if (warningValue != warningEnabled)
                        {
                            if (legacyWarning.intValue == i) legacyWarning.intValue = -1;
                            frameWarning.boolValue = warningValue;
                        }
                        var legacyEffect = sequence.FindPropertyRelative("attackEffectFrame");
                        var frameEffect = effectsByFrame.GetArrayElementAtIndex(i).FindPropertyRelative("playAttackEffect");
                        bool wasEnabled = legacyEffect.intValue == i || frameEffect.boolValue;
                        bool enabled = EditorGUILayout.ToggleLeft("進入圖片時生成攻擊特效", wasEnabled);
                        if (enabled != wasEnabled)
                        {
                            if (legacyEffect.intValue == i) legacyEffect.intValue = -1;
                            frameEffect.boolValue = enabled;
                        }
                        DrawEffects(effectsByFrame.GetArrayElementAtIndex(i).FindPropertyRelative("effects"));
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
                    if (hasIds) ids.DeleteArrayElementAtIndex(i);
                    durations.DeleteArrayElementAtIndex(i);
                    effectsByFrame?.DeleteArrayElementAtIndex(i);
                    RemapEvents(sequence, i, -1);
                    break;
                }
                if (move != 0)
                {
                    frames.MoveArrayElement(i, i + move);
                    if (hasIds) ids.MoveArrayElement(i, i + move);
                    durations.MoveArrayElement(i, i + move);
                    effectsByFrame?.MoveArrayElement(i, i + move);
                    RemapEvents(sequence, i, i + move);
                    break;
                }
            }
            if (GUILayout.Button("新增圖片"))
            {
                int index = frames.arraySize;
                frames.InsertArrayElementAtIndex(index);
                frames.GetArrayElementAtIndex(index).objectReferenceValue = null;
                if (hasIds)
                {
                    ids.arraySize = frames.arraySize;
                    ids.GetArrayElementAtIndex(index).stringValue = System.Guid.NewGuid().ToString("N");
                }
                if (durations.arraySize <= index) durations.InsertArrayElementAtIndex(index);
                durations.GetArrayElementAtIndex(index).floatValue = 0.5f;
                if (effectsByFrame != null)
                {
                    effectsByFrame.InsertArrayElementAtIndex(index);
                    effectsByFrame.GetArrayElementAtIndex(index).FindPropertyRelative("effects").ClearArray();
                    effectsByFrame.GetArrayElementAtIndex(index).FindPropertyRelative("playAttackEffect").boolValue = false;
                    effectsByFrame.GetArrayElementAtIndex(index).FindPropertyRelative("playWarning").boolValue = false;
                }
            }
            if (useBeats.boolValue)
            {
                float total = 0;
                for (int i = 0; i < frames.arraySize; i++)
                    total += Mathf.Max(0.01f, durations.GetArrayElementAtIndex(i).floatValue);
                EditorGUILayout.LabelField("單次正向總長", $"{total:0.###} 拍");
            }
            if (sequence != null)
                EditorGUILayout.HelpBox("特效清單與『生成攻擊特效』勾選獨立運作：清單生成附加特效，勾選則生成角色原本設定的普通攻擊／技能特效。不要在清單再放同一命中特效，除非需要播放兩次。傷害由戰鬥系統結算。圖片排序與刪除會同步處理清單。", MessageType.None);
        }

        private static void DrawEffects(SerializedProperty effects)
        {
            effects.isExpanded = EditorGUILayout.Foldout(effects.isExpanded, $"生成特效（{effects.arraySize}）", true);
            if (!effects.isExpanded) return;
            for (int i = 0; i < effects.arraySize; i++)
            {
                var entry = effects.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("prefab"), new GUIContent("特效 Prefab"));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("soundEventPath"), new GUIContent("同步音效（FMOD 路徑，留空關閉）"));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("soundVolume"), new GUIContent("音效音量"));
                    var location = entry.FindPropertyRelative("location");
                    location.enumValueIndex = EditorGUILayout.Popup("生成位置", location.enumValueIndex, new[] { "自身掛點", "本次攻擊目標掛點" });
                    if (location.enumValueIndex == 0)
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("selfAnchor"), new GUIContent("自訂自身掛點（選填）"));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("positionOffset"), new GUIContent("位置偏移（掛點座標）"));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("rotationOffset"), new GUIContent("旋轉偏移"));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("followAnchor"), new GUIContent("跟隨掛點"));
                    var limitLifetime = entry.FindPropertyRelative("limitLifetime");
                    EditorGUILayout.PropertyField(limitLifetime, new GUIContent("限制存活時間"));
                    if (limitLifetime.boolValue)
                    {
                        var lifetime = entry.FindPropertyRelative("lifetimeSeconds");
                        lifetime.floatValue = Mathf.Max(0.01f, EditorGUILayout.FloatField("存活秒數", lifetime.floatValue));
                    }
                    if (GUILayout.Button("刪除此特效")) { effects.DeleteArrayElementAtIndex(i); break; }
                }
            }
            if (GUILayout.Button("新增特效"))
            {
                int index = effects.arraySize;
                effects.InsertArrayElementAtIndex(index);
                var entry = effects.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("prefab").objectReferenceValue = null;
                entry.FindPropertyRelative("soundEventPath").stringValue = string.Empty;
                entry.FindPropertyRelative("soundVolume").floatValue = 1f;
                entry.FindPropertyRelative("location").enumValueIndex = 0;
                entry.FindPropertyRelative("selfAnchor").objectReferenceValue = null;
                entry.FindPropertyRelative("positionOffset").vector3Value = Vector3.zero;
                entry.FindPropertyRelative("rotationOffset").vector3Value = Vector3.zero;
                entry.FindPropertyRelative("followAnchor").boolValue = false;
                entry.FindPropertyRelative("limitLifetime").boolValue = false;
                entry.FindPropertyRelative("lifetimeSeconds").floatValue = 1.5f;
            }
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
