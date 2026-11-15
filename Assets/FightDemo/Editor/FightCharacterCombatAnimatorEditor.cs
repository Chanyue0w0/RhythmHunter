using System;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [CustomPropertyDrawer(typeof(FightCharacterDefinition.EnemyAbility))]
    public sealed class EnemyAbilityAnimationDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => (EditorGUIUtility.singleLineHeight + 2) * 7;
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(position, label, EditorStyles.boldLabel); position.y += position.height + 2;
            EditorGUI.PropertyField(position, property.FindPropertyRelative("name")); position.y += position.height + 2;
            var component = property.serializedObject.targetObject as Component;
            var animator = component != null ? component.GetComponentInChildren<FightCharacterCombatAnimator>(true) : null;
            var legacy = property.FindPropertyRelative("animation"); var handle = property.FindPropertyRelative("animationSourceId");
            if (animator == null) EditorGUI.PropertyField(position, legacy);
            else
            {
                var ids = new System.Collections.Generic.List<int>(); var names = new System.Collections.Generic.List<string>();
                int current = handle.intValue >= 0 ? handle.intValue : legacy.intValue;
                foreach (var sequence in animator.Sequences)
                    if (sequence != null) { ids.Add((int)sequence.Animation); names.Add(sequence.Name); }
                int index = ids.IndexOf(current);
                if (index < 0) { index = ids.Count; ids.Add(current); names.Add("缺少動畫（引用 " + current + "，請重新選擇）"); }
                EditorGUI.BeginChangeCheck();
                int choice = EditorGUI.Popup(position, "播放動畫", index, names.ToArray());
                if (EditorGUI.EndChangeCheck()) { handle.intValue = ids[choice]; if (ids[choice] < 1000) legacy.intValue = ids[choice]; }
            }
            position.y += position.height + 2;
            EditorGUI.PropertyField(position, property.FindPropertyRelative("damagePerHit"));
            position.y += position.height + 2;
            EditorGUI.PropertyField(position, property.FindPropertyRelative("selectionWeight"), new GUIContent("抽選權重"));
            position.y += position.height + 2;
            EditorGUI.PropertyField(position, property.FindPropertyRelative("healOnEffectFrame"), new GUIContent("特效拍點回血量"));
            position.y += position.height + 2;
            EditorGUI.PropertyField(position, property.FindPropertyRelative("healingEffectPrefab"), new GUIContent("觸發回血的附加特效"));
            EditorGUI.EndProperty();
        }
    }

    [CustomEditor(typeof(FightCharacterCombatAnimator))]
    public sealed class FightCharacterCombatAnimatorEditor : Editor
    {
        public bool EmbeddedPreview { get; set; }
        public event Action AnimationListChanged;
        private string newName = "New Animation";
        private bool idleExpanded;

        public override void OnInspectorGUI()
        {
            if (target == null) return;
            serializedObject.Update();
            if (!EmbeddedPreview && GUILayout.Button("開啟角色動畫預覽／編輯視窗"))
                WeaponAttachmentEditorWindow.OpenCharacter((FightCharacterCombatAnimator)target);
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            Field("targetRenderer"); Field("beatSource"); Field("fallbackBpm");
            Field("useDrunkIdle");
            idleExpanded = EditorGUILayout.Foldout(idleExpanded, "基礎 Idle（沿用舊待機資料）", true);
            if (idleExpanded)
            {
                Field("idleAnimationName");
                DrawFrames(serializedObject.FindProperty("frames"), serializedObject.FindProperty("useIdleFrameBeatDurations"), serializedObject.FindProperty("idleFrameBeatDurations"));
                if (!serializedObject.FindProperty("useIdleFrameBeatDurations").boolValue) Field("cyclesPerBeat");
                Field("pingPong"); Field("phaseOffset");
            }
            EditorGUILayout.HelpBox("動畫名稱可自由修改；程式用途與武器影格 ID 不隨改名或排序改變。新增自訂動畫可由 Boss 能力池選用，或使用 Play(名稱) 呼叫。", MessageType.Info);
            var list = serializedObject.FindProperty("sequences");
            for (int i = 0; i < list.arraySize; i++)
            {
                var sequence = list.GetArrayElementAtIndex(i);
                int custom = sequence.FindPropertyRelative("customSourceId").intValue;
                int type = sequence.FindPropertyRelative("animation").intValue;
                string name = sequence.FindPropertyRelative("animationName").stringValue;
                if (string.IsNullOrWhiteSpace(name)) name = ((FightCharacterCombatAnimator.CombatAnimation)type).ToString();
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    sequence.isExpanded = EditorGUILayout.Foldout(sequence.isExpanded, name, true);
                    if (!sequence.isExpanded) continue;
                    EditorGUI.BeginChangeCheck();
                    string edited = EditorGUILayout.DelayedTextField("動畫名稱", name);
                    if (EditorGUI.EndChangeCheck())
                    {
                        bool duplicate = false;
                        for (int j = 0; j < list.arraySize; j++)
                        {
                            if (i == j) continue;
                            var other = list.GetArrayElementAtIndex(j);
                            string otherName = other.FindPropertyRelative("animationName").stringValue;
                            if (string.IsNullOrWhiteSpace(otherName)) otherName = ((FightCharacterCombatAnimator.CombatAnimation)other.FindPropertyRelative("animation").intValue).ToString();
                            duplicate |= string.Equals(otherName, edited.Trim(), StringComparison.Ordinal);
                        }
                        if (string.IsNullOrWhiteSpace(edited) || duplicate) EditorUtility.DisplayDialog("無法改名", "名稱不可空白或與此角色其他動畫重複。", "確定");
                        else sequence.FindPropertyRelative("animationName").stringValue = edited.Trim();
                    }
                    EditorGUILayout.LabelField("程式用途（改名不影響）", custom >= 1000 ? "自訂／由能力指定" : ((FightCharacterCombatAnimator.CombatAnimation)type).ToString());
                    EditorGUILayout.PropertyField(sequence.FindPropertyRelative("warningEffect"), new GUIContent("Warning 特效與音效（每次勾選共用）"), true);
                    DrawFrames(sequence.FindPropertyRelative("frames"), sequence.FindPropertyRelative("useFrameBeatDurations"), sequence.FindPropertyRelative("frameBeatDurations"), sequence);
                    if (!sequence.FindPropertyRelative("useFrameBeatDurations").boolValue) EditorGUILayout.PropertyField(sequence.FindPropertyRelative("framesPerSecond"));
                    int move = 0; bool remove;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(i == 0)) if (GUILayout.Button("動畫上移")) move = -1;
                        using (new EditorGUI.DisabledScope(i == list.arraySize - 1)) if (GUILayout.Button("動畫下移")) move = 1;
                        remove = GUILayout.Button("刪除此動畫");
                    }
                    if (remove && EditorUtility.DisplayDialog("刪除動畫", $"刪除「{name}」及其影格／事件設定？引用它的能力不會自動改用其他動畫。可使用 Undo 還原。", "刪除", "取消"))
                    { list.DeleteArrayElementAtIndex(i); break; }
                    if (move != 0) { list.MoveArrayElement(i, i + move); break; }
                }
            }
            newName = EditorGUILayout.TextField("新動畫名稱", newName);
            if (GUILayout.Button("新增自訂動畫"))
            {
                serializedObject.ApplyModifiedProperties();
                var animator = (FightCharacterCombatAnimator)target;
                if (string.IsNullOrWhiteSpace(newName) || animator.FindSequence(newName) != null)
                    EditorUtility.DisplayDialog("無法新增", "名稱不可空白或重複。", "確定");
                else
                {
                    Undo.RecordObject(animator, "Add character animation");
                    animator.AddCustomSequence(newName);
                    EditorUtility.SetDirty(animator);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                    AnimationListChanged?.Invoke();
                    GUI.changed = true;
                }
                serializedObject.Update();
            }
            if (GUILayout.Button("新增缺少的既有用途動畫…"))
            {
                serializedObject.ApplyModifiedProperties();
                var animator = (FightCharacterCombatAnimator)target;
                var menu = new GenericMenu();
                foreach (FightCharacterCombatAnimator.CombatAnimation type in Enum.GetValues(typeof(FightCharacterCombatAnimator.CombatAnimation)))
                {
                    if (animator.FindSequence(type) != null) continue;
                    var captured = type;
                    menu.AddItem(new GUIContent(type.ToString()), false, () =>
                    {
                        Undo.RecordObject(animator, "Add built-in animation");
                        var sequence = new FightCharacterCombatAnimator.Sequence();
                        sequence.Configure(captured, null, 16, -1, -1, -1); sequence.ConfigureFrameBeats(null);
                        animator.AddBuiltInSequence(sequence);
                        EditorUtility.SetDirty(animator); PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                        AnimationListChanged?.Invoke();
                    });
                }
                menu.ShowAsContext();
            }
            serializedObject.ApplyModifiedProperties();
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
