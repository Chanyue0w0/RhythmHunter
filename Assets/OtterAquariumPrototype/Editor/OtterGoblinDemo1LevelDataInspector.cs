using System.Collections.Generic;
using RhythmHunter.OtterAquariumPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    [CustomEditor(typeof(OtterGoblinDemo1LevelData))]
    internal sealed class OtterGoblinDemo1LevelDataInspector : Editor
    {
        private const int Ppq = OtterGoblinDemo1LevelData.DefaultPpq;
        private const int StepsPerBeat = 4;
        private const int StepTicks = Ppq / StepsPerBeat;
        private const float LaneLabelWidth = 92f;
        private const float CellWidth = 19f;

        private static readonly Color WarningColor = new(1f, 0.67f, 0.25f, 1f);
        private static readonly Color AxeColor = new(0.92f, 0.25f, 0.2f, 1f);
        private static readonly Color CatchColor = new(0.25f, 0.9f, 1f, 1f);

        private SerializedProperty levelId;
        private SerializedProperty displayName;
        private SerializedProperty authoringNotes;
        private SerializedProperty musicEventPath;
        private SerializedProperty musicStartDelaySeconds;
        private SerializedProperty musicVolume;
        private SerializedProperty musicGridOffsetMs;
        private SerializedProperty authoredBpm;
        private SerializedProperty beatsPerBar;
        private SerializedProperty totalBars;
        private SerializedProperty ppq;
        private SerializedProperty perfectWindowMs;
        private SerializedProperty goodWindowMs;
        private SerializedProperty judgementOffsetMs;
        private SerializedProperty extraInputStunBeats;
        private SerializedProperty warningSoundEventPath;
        private SerializedProperty attackSoundEventPath;
        private SerializedProperty blockSoundEventPath;
        private SerializedProperty perfectSoundEventPath;
        private SerializedProperty goodSoundEventPath;
        private SerializedProperty missSoundEventPath;
        private SerializedProperty phrases;

        private bool showLevel = true;
        private bool showCombat = true;
        private bool showAudio = true;
        private bool showPhrases = true;
        private int selectedPhrase;

        private void OnEnable()
        {
            levelId = serializedObject.FindProperty("levelId");
            displayName = serializedObject.FindProperty("displayName");
            authoringNotes = serializedObject.FindProperty("authoringNotes");
            musicEventPath = serializedObject.FindProperty("musicEventPath");
            musicStartDelaySeconds = serializedObject.FindProperty("musicStartDelaySeconds");
            musicVolume = serializedObject.FindProperty("musicVolume");
            musicGridOffsetMs = serializedObject.FindProperty("musicGridOffsetMs");
            authoredBpm = serializedObject.FindProperty("authoredBpm");
            beatsPerBar = serializedObject.FindProperty("beatsPerBar");
            totalBars = serializedObject.FindProperty("totalBars");
            ppq = serializedObject.FindProperty("ppq");
            perfectWindowMs = serializedObject.FindProperty("perfectWindowMs");
            goodWindowMs = serializedObject.FindProperty("goodWindowMs");
            judgementOffsetMs = serializedObject.FindProperty("judgementOffsetMs");
            extraInputStunBeats = serializedObject.FindProperty("extraInputStunBeats");
            warningSoundEventPath = serializedObject.FindProperty("warningSoundEventPath");
            attackSoundEventPath = serializedObject.FindProperty("attackSoundEventPath");
            blockSoundEventPath = serializedObject.FindProperty("blockSoundEventPath");
            perfectSoundEventPath = serializedObject.FindProperty("perfectSoundEventPath");
            goodSoundEventPath = serializedObject.FindProperty("goodSoundEventPath");
            missSoundEventPath = serializedObject.FindProperty("missSoundEventPath");
            phrases = serializedObject.FindProperty("phrases");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawToolbar();
            DrawLevelSettings();
            DrawCombatSettings();
            DrawAudioSettings();
            DrawPhraseEditor();

            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed)
                EditorUtility.SetDirty(target);

            OtterGoblinDemo1LevelData level = (OtterGoblinDemo1LevelData)target;
            if (level.Validate(out string error))
                EditorGUILayout.HelpBox($"資料有效｜{level.Phrases.Count} 組 Phrase｜{level.TotalBars} 小節｜{level.AuthoredBpm:0.##} BPM", MessageType.Info);
            else
                EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("套用並開啟共用 Scene", GUILayout.Height(26f)))
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
                OtterGoblinDemo1SceneBuilder.ApplyLevelToSharedScene(
                    (OtterGoblinDemo1LevelData)target);
            }
            if (GUILayout.Button("儲存資產", GUILayout.Height(26f)))
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "三軌時間軸每格為十六分音符（120 tick）。提示 X 與接取 X' 可點擊微調；投擲 _ 由攻擊類型固定，每個接取拍可另外指定投擲物 Prefab。按「套用並開啟共用 Scene」會一起更換歌曲、BPM、校準與完整譜面。",
                MessageType.Info);
        }

        private void DrawLevelSettings()
        {
            showLevel = EditorGUILayout.BeginFoldoutHeaderGroup(showLevel, "1. 關卡與音樂");
            if (showLevel)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(levelId, new GUIContent("Level ID"));
                EditorGUILayout.PropertyField(displayName, new GUIContent("顯示名稱"));
                EditorGUILayout.PropertyField(authoringNotes, new GUIContent("製作備註"));
                EditorGUILayout.Space(3f);
                EditorGUILayout.PropertyField(musicEventPath, new GUIContent("FMOD Music Event"));
                EditorGUILayout.PropertyField(musicStartDelaySeconds, new GUIContent("播放前等待（秒）"));
                EditorGUILayout.PropertyField(musicVolume, new GUIContent("音樂音量"));
                EditorGUILayout.PropertyField(musicGridOffsetMs, new GUIContent("Chart Offset（ms）"));
                EditorGUILayout.PropertyField(authoredBpm, new GUIContent("BPM"));
                EditorGUILayout.PropertyField(beatsPerBar, new GUIContent("每小節拍數"));
                EditorGUILayout.PropertyField(totalBars, new GUIContent("總小節數"));
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(ppq, new GUIContent("PPQ"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawCombatSettings()
        {
            showCombat = EditorGUILayout.BeginFoldoutHeaderGroup(showCombat, "2. 戰鬥與判定");
            if (showCombat)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(perfectWindowMs, new GUIContent("Perfect ±ms"));
                EditorGUILayout.PropertyField(goodWindowMs, new GUIContent("Good ±ms"));
                EditorGUILayout.PropertyField(judgementOffsetMs, new GUIContent("輸入校正 Offset（ms）"));
                EditorGUILayout.PropertyField(extraInputStunBeats, new GUIContent("亂按硬直（拍）"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawAudioSettings()
        {
            showAudio = EditorGUILayout.BeginFoldoutHeaderGroup(showAudio, "3. FMOD 音效");
            if (showAudio)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(warningSoundEventPath, new GUIContent("提示 X"));
                EditorGUILayout.PropertyField(attackSoundEventPath, new GUIContent("投斧 _"));
                EditorGUILayout.PropertyField(blockSoundEventPath, new GUIContent("成功格擋"));
                EditorGUILayout.PropertyField(perfectSoundEventPath, new GUIContent("Perfect（可留白）"));
                EditorGUILayout.PropertyField(goodSoundEventPath, new GUIContent("Good（可留白）"));
                EditorGUILayout.PropertyField(missSoundEventPath, new GUIContent("Miss／亂按"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawPhraseEditor()
        {
            showPhrases = EditorGUILayout.BeginFoldoutHeaderGroup(showPhrases, "4. 戰鬥 Phrase 清單與步進器");
            if (!showPhrases)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            selectedPhrase = Mathf.Clamp(selectedPhrase, 0, Mathf.Max(0, phrases.arraySize - 1));
            for (int i = 0; i < phrases.arraySize; i++)
            {
                SerializedProperty phrase = phrases.GetArrayElementAtIndex(i);
                int bar = phrase.FindPropertyRelative("startBar").intValue;
                int offsetTicks = phrase.FindPropertyRelative("startOffsetTicks").intValue;
                int kind = phrase.FindPropertyRelative("kind").enumValueIndex;
                float beat = offsetTicks / (float)Ppq + 1f;
                string text = $"#{i + 1:00}  Bar {bar:00} Beat {beat:0.##}  {KindLabel((OtterGoblinDemo1LevelData.AttackKind)kind)}";
                if (GUILayout.Toggle(selectedPhrase == i, text, EditorStyles.miniButton))
                    selectedPhrase = i;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("新增 Phrase"))
                AddPhrase();
            GUI.enabled = phrases.arraySize > 0;
            if (GUILayout.Button("刪除選取"))
                DeleteSelectedPhrase();
            if (GUILayout.Button("依 Bar 排序"))
                SortPhrases();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (phrases.arraySize > 0)
                DrawSelectedPhrase(phrases.GetArrayElementAtIndex(selectedPhrase));
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawSelectedPhrase(SerializedProperty phrase)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"編輯 Phrase #{selectedPhrase + 1:00}", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(phrase.FindPropertyRelative("startBar"), new GUIContent("開始小節"));
            SerializedProperty startOffset = phrase.FindPropertyRelative("startOffsetTicks");
            int barSteps = Mathf.Max(1, beatsPerBar.intValue * StepsPerBeat);
            int startStep = Mathf.Clamp(Mathf.RoundToInt(startOffset.intValue / (float)StepTicks), 0, barSteps - 1);
            startStep = EditorGUILayout.IntSlider(
                new GUIContent("小節內起點（十六分格）", "0 是第 1 拍；4 是第 2 拍；可讓前後 Phrase 無縫銜接。"),
                startStep,
                0,
                barSteps - 1);
            startOffset.intValue = startStep * StepTicks;
            EditorGUILayout.LabelField("實際起點", $"Beat {startStep / (float)StepsPerBeat + 1f:0.##}  •  +{startOffset.intValue} tick");
            EditorGUILayout.PropertyField(phrase.FindPropertyRelative("label"), new GUIContent("顯示名稱"));

            SerializedProperty kindProperty = phrase.FindPropertyRelative("kind");
            OtterGoblinDemo1LevelData.AttackKind oldKind =
                (OtterGoblinDemo1LevelData.AttackKind)kindProperty.enumValueIndex;
            OtterGoblinDemo1LevelData.AttackKind newKind =
                (OtterGoblinDemo1LevelData.AttackKind)EditorGUILayout.EnumPopup("攻擊類型", oldKind);
            if (newKind != oldKind)
                ApplyPreset(phrase, newKind);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("恢復此類型預設", GUILayout.Width(150f)))
                ApplyPreset(phrase, newKind);
            EditorGUILayout.EndHorizontal();
            DrawFixedPatternPreset(phrase, newKind);

            SerializedProperty warningLength = phrase.FindPropertyRelative("warningLengthBeats");
            SerializedProperty attackLength = phrase.FindPropertyRelative("attackLengthBeats");
            SerializedProperty warningTicks = phrase.FindPropertyRelative("warningPattern").FindPropertyRelative("hitTicks");
            SerializedProperty responseTicks = phrase.FindPropertyRelative("pattern").FindPropertyRelative("hitTicks");
            SerializedProperty projectilePrefabs = phrase.FindPropertyRelative("projectilePrefabs");
            int expectedCount = ExpectedCount(newKind);
            EnsureProjectileSlotCount(projectilePrefabs, expectedCount);
            int totalBeats = 2 + attackLength.intValue;

            EditorGUILayout.Space(4f);
            DrawBeatRuler(totalBeats);
            DrawEditableLane(
                "提示 X",
                warningTicks,
                totalBeats,
                0,
                warningLength.intValue * Ppq - StepTicks,
                "X",
                WarningColor,
                expectedCount);
            DrawProjectileLane(newKind, totalBeats);
            DrawEditableLane(
                "接取 X'",
                responseTicks,
                totalBeats,
                2 * Ppq,
                attackLength.intValue * Ppq,
                "X'",
                CatchColor,
                expectedCount);
            DrawProjectilePrefabSlots(projectilePrefabs, responseTicks, expectedCount);

            EditorGUILayout.HelpBox(
                "點亮或關閉格點後，兩條可編輯軌都必須維持此類型規定的數量。每個接取拍可指定不同投擲物 Prefab；空白時使用 BeatProjectiles/axe。旋轉設定保存在 Prefab 的 RhythmTimelineProjectile 元件。",
                MessageType.None);
            EditorGUILayout.EndVertical();
        }

        private static void DrawBeatRuler(int totalBeats)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("拍號", GUILayout.Width(LaneLabelWidth));
            int totalSteps = totalBeats * StepsPerBeat;
            for (int step = 0; step <= totalSteps; step++)
            {
                string label = step % StepsPerBeat == 0 ? (step / StepsPerBeat + 1).ToString() : string.Empty;
                GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(CellWidth));
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEditableLane(
            string laneName,
            SerializedProperty ticks,
            int totalBeats,
            int displayOffsetTicks,
            int maxRelativeTick,
            string symbol,
            Color activeColor,
            int expectedCount)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"{laneName} {ticks.arraySize}/{expectedCount}", GUILayout.Width(LaneLabelWidth));
            int totalSteps = totalBeats * StepsPerBeat;
            for (int step = 0; step <= totalSteps; step++)
            {
                int absoluteTick = step * StepTicks;
                int relativeTick = absoluteTick - displayOffsetTicks;
                bool editable = relativeTick >= 0 && relativeTick <= maxRelativeTick;
                bool active = ContainsTick(ticks, relativeTick);
                Color previous = GUI.backgroundColor;
                if (active)
                    GUI.backgroundColor = activeColor;
                using (new EditorGUI.DisabledScope(!editable))
                {
                    if (GUILayout.Button(active ? symbol : "·", EditorStyles.miniButton, GUILayout.Width(CellWidth)))
                        ToggleTick(ticks, relativeTick);
                }
                GUI.backgroundColor = previous;
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawProjectileLane(OtterGoblinDemo1LevelData.AttackKind kind, int totalBeats)
        {
            HashSet<int> axeTicks = new(GetAxeTicks(kind));
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("投擲 _（固定）", GUILayout.Width(LaneLabelWidth));
            int totalSteps = totalBeats * StepsPerBeat;
            for (int step = 0; step <= totalSteps; step++)
            {
                int tick = step * StepTicks;
                bool active = axeTicks.Contains(tick);
                Color previous = GUI.backgroundColor;
                if (active)
                    GUI.backgroundColor = AxeColor;
                using (new EditorGUI.DisabledScope(true))
                    GUILayout.Button(active ? "_" : "·", EditorStyles.miniButton, GUILayout.Width(CellWidth));
                GUI.backgroundColor = previous;
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawProjectilePrefabSlots(
            SerializedProperty projectilePrefabs,
            SerializedProperty responseTicks,
            int expectedCount)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("每個接取拍的投擲物", EditorStyles.boldLabel);
            for (int i = 0; i < expectedCount; i++)
            {
                SerializedProperty prefabProperty = projectilePrefabs.GetArrayElementAtIndex(i);
                int responseTick = i < responseTicks.arraySize
                    ? responseTicks.GetArrayElementAtIndex(i).intValue
                    : 0;
                float catchBeat = 3f + responseTick / (float)Ppq;
                GameObject current = prefabProperty.objectReferenceValue as GameObject;
                GameObject selected = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent($"#{i + 1:00}  接取 Beat {catchBeat:0.##}", "空白會使用 BeatProjectiles/axe 預設投擲物。"),
                    current,
                    typeof(GameObject),
                    false);
                if (selected != current)
                    prefabProperty.objectReferenceValue = selected;
                if (selected != null && selected.GetComponent<RhythmTimelineProjectile>() == null)
                {
                    EditorGUILayout.HelpBox(
                        $"{selected.name} 缺少 RhythmTimelineProjectile，請選擇 BeatProjectiles 資料夾內的 Prefab。",
                        MessageType.Error);
                }
            }
        }

        private static void EnsureProjectileSlotCount(SerializedProperty projectilePrefabs, int expectedCount)
        {
            if (projectilePrefabs != null && projectilePrefabs.arraySize != expectedCount)
                projectilePrefabs.arraySize = expectedCount;
        }

        private void AddPhrase()
        {
            int index = phrases.arraySize;
            int startBar = index == 0
                ? 5
                : phrases.GetArrayElementAtIndex(index - 1).FindPropertyRelative("startBar").intValue + 2;
            phrases.InsertArrayElementAtIndex(index);
            SerializedProperty phrase = phrases.GetArrayElementAtIndex(index);
            phrase.FindPropertyRelative("startBar").intValue = startBar;
            phrase.FindPropertyRelative("startOffsetTicks").intValue = 0;
            ApplyPreset(phrase, OtterGoblinDemo1LevelData.AttackKind.Single);
            selectedPhrase = index;
        }

        private void DeleteSelectedPhrase()
        {
            if (phrases.arraySize == 0)
                return;
            phrases.DeleteArrayElementAtIndex(selectedPhrase);
            selectedPhrase = Mathf.Clamp(selectedPhrase, 0, Mathf.Max(0, phrases.arraySize - 1));
        }

        private void SortPhrases()
        {
            for (int i = 0; i < phrases.arraySize - 1; i++)
            {
                for (int j = i + 1; j < phrases.arraySize; j++)
                {
                    SerializedProperty leftPhrase = phrases.GetArrayElementAtIndex(i);
                    SerializedProperty rightPhrase = phrases.GetArrayElementAtIndex(j);
                    long left = (long)leftPhrase.FindPropertyRelative("startBar").intValue * 100000
                        + leftPhrase.FindPropertyRelative("startOffsetTicks").intValue;
                    long right = (long)rightPhrase.FindPropertyRelative("startBar").intValue * 100000
                        + rightPhrase.FindPropertyRelative("startOffsetTicks").intValue;
                    if (right < left)
                        phrases.MoveArrayElement(j, i);
                }
            }
            selectedPhrase = Mathf.Clamp(selectedPhrase, 0, Mathf.Max(0, phrases.arraySize - 1));
        }

        private static void ApplyPreset(
            SerializedProperty phrase,
            OtterGoblinDemo1LevelData.AttackKind kind)
        {
            phrase.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            phrase.FindPropertyRelative("responseDelayBeats").intValue = 1;
            SerializedProperty warning = phrase.FindPropertyRelative("warningPattern");
            SerializedProperty response = phrase.FindPropertyRelative("pattern");

            switch (kind)
            {
                case OtterGoblinDemo1LevelData.AttackKind.Single:
                    phrase.FindPropertyRelative("label").stringValue = "SINGLE • X _";
                    phrase.FindPropertyRelative("warningLengthBeats").intValue = 1;
                    phrase.FindPropertyRelative("attackLengthBeats").intValue = 1;
                    SetPattern(warning, "single-cue", 0);
                    SetPattern(response, "single-response", 0);
                    break;

                case OtterGoblinDemo1LevelData.AttackKind.Triple:
                    phrase.FindPropertyRelative("label").stringValue = "SPECIAL • X X X _";
                    phrase.FindPropertyRelative("warningLengthBeats").intValue = 2;
                    phrase.FindPropertyRelative("attackLengthBeats").intValue = 2;
                    SetPattern(warning, "triple-cue", 0, 240, 480);
                    SetPattern(response, "triple-response", 0, 480, 960);
                    break;

                case OtterGoblinDemo1LevelData.AttackKind.DoubleSingle:
                    phrase.FindPropertyRelative("label").stringValue = "COMBO • X _ ×2";
                    phrase.FindPropertyRelative("warningLengthBeats").intValue = 3;
                    phrase.FindPropertyRelative("attackLengthBeats").intValue = 3;
                    SetPattern(warning, "double-single-cue", 0, 960);
                    SetPattern(response, "double-single-response", 0, 960);
                    break;

                default:
                    phrase.FindPropertyRelative("label").stringValue = "COMBO • TRIPLE → X _";
                    phrase.FindPropertyRelative("warningLengthBeats").intValue = 5;
                    phrase.FindPropertyRelative("attackLengthBeats").intValue = 5;
                    SetPattern(warning, "triple-single-cue", 0, 240, 480, 1920);
                    SetPattern(response, "triple-single-response", 0, 480, 960, 1920);
                    break;
            }
            EnsureProjectileSlotCount(
                phrase.FindPropertyRelative("projectilePrefabs"),
                ExpectedCount(kind));
        }

        private static void DrawFixedPatternPreset(
            SerializedProperty phrase,
            OtterGoblinDemo1LevelData.AttackKind kind)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("固定節奏", GUILayout.Width(70f));
            EditorGUILayout.HelpBox(
                "短拍與三連拍不使用節奏變體；請以新增、排序及銜接 Phrase 調整密度。",
                MessageType.Info);
            if (GUILayout.Button("還原固定型", GUILayout.Width(90f)))
                ApplyPreset(phrase, kind);
            EditorGUILayout.EndHorizontal();
        }

        private static void SetPattern(SerializedProperty pattern, string id, params int[] ticks)
        {
            pattern.FindPropertyRelative("id").stringValue = id;
            SerializedProperty array = pattern.FindPropertyRelative("hitTicks");
            array.arraySize = ticks.Length;
            for (int i = 0; i < ticks.Length; i++)
                array.GetArrayElementAtIndex(i).intValue = ticks[i];
        }

        private static bool ContainsTick(SerializedProperty ticks, int tick)
        {
            if (tick < 0)
                return false;
            for (int i = 0; i < ticks.arraySize; i++)
            {
                if (ticks.GetArrayElementAtIndex(i).intValue == tick)
                    return true;
            }
            return false;
        }

        private static void ToggleTick(SerializedProperty ticks, int tick)
        {
            List<int> values = new(ticks.arraySize + 1);
            bool removed = false;
            for (int i = 0; i < ticks.arraySize; i++)
            {
                int value = ticks.GetArrayElementAtIndex(i).intValue;
                if (value == tick)
                    removed = true;
                else
                    values.Add(value);
            }
            if (!removed)
                values.Add(tick);
            values.Sort();
            ticks.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                ticks.GetArrayElementAtIndex(i).intValue = values[i];
        }

        private static int ExpectedCount(OtterGoblinDemo1LevelData.AttackKind kind)
        {
            return kind switch
            {
                OtterGoblinDemo1LevelData.AttackKind.Single => 1,
                OtterGoblinDemo1LevelData.AttackKind.Triple => 3,
                OtterGoblinDemo1LevelData.AttackKind.DoubleSingle => 2,
                _ => 4
            };
        }

        private static int[] GetAxeTicks(OtterGoblinDemo1LevelData.AttackKind kind)
        {
            return kind switch
            {
                OtterGoblinDemo1LevelData.AttackKind.Single => new[] { Ppq },
                OtterGoblinDemo1LevelData.AttackKind.Triple => new[] { Ppq * 3 / 2 },
                OtterGoblinDemo1LevelData.AttackKind.DoubleSingle => new[] { Ppq, Ppq * 3 },
                _ => new[] { Ppq * 3 / 2, Ppq * 5 }
            };
        }

        private static string KindLabel(OtterGoblinDemo1LevelData.AttackKind kind)
        {
            return kind switch
            {
                OtterGoblinDemo1LevelData.AttackKind.Single => "單斧 X _",
                OtterGoblinDemo1LevelData.AttackKind.Triple => "三連斧",
                OtterGoblinDemo1LevelData.AttackKind.DoubleSingle => "雙單斧",
                _ => "三連接單斧"
            };
        }
    }
}
