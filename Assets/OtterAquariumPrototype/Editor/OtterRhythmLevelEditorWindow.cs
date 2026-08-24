using System.Collections.Generic;
using System.IO;
using RhythmHunter.OtterAquariumPrototype;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    public sealed class OtterRhythmLevelEditorWindow : EditorWindow
    {
        private const string DefaultDataFolder = "Assets/OtterAquariumPrototype/Data";

        private OtterRhythmLevelData level;
        private Vector2 scroll;
        private bool showSettings = true;
        private bool showQuickTemplate = true;
        private bool showComposer = true;
        private bool showPhrases = true;
        private bool showExchange = true;

        private string levelId;
        private string displayName;
        private string notes;
        private string musicEventPath;
        private float musicStartDelaySeconds = 1f;
        private float chartOffsetMs;
        private float bpm = 100f;
        private int beatsPerBar = 4;
        private int totalBars = 24;
        private int ppq = OtterRhythmLevelData.DefaultPpq;
        private float perfectWindowMs = 70f;
        private float goodWindowMs = 140f;
        private float judgementOffsetMs = 30f;
        private string cueSfx;
        private string hitSfx;
        private string missSfx;
        private string successSfx;

        private OtterRhythmPresetLibrary.ProgressionTemplate template = OtterRhythmPresetLibrary.ProgressionTemplate.Beginner;
        private int templatePhraseCount = 8;

        private int selectedPhrase = -1;
        private int phraseStartBar = 3;
        private string phraseLabel = "新的節奏組";
        private bool phraseAdaptive;
        private int standardPreset;
        private int assistPreset;
        private int challengePreset = 5;
        private bool useCustomSteps;
        private bool[] customSteps = new bool[16];

        private readonly List<string> validationErrors = new();
        private readonly List<string> validationWarnings = new();

        [MenuItem("Rhythm Hunter/Otter Aquarium/Open Rhythm Level Editor")]
        public static void Open()
        {
            GetWindow<OtterRhythmLevelEditorWindow>("海獺節奏關卡");
        }

        private void OnEnable()
        {
            minSize = new Vector2(720f, 620f);
            if (level == null)
            {
                level = AssetDatabase.LoadAssetAtPath<OtterRhythmLevelData>(OtterShellBeatLabSceneBuilder.LevelDataPath);
                if (level != null)
                    LoadSettingsFromLevel();
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is OtterRhythmLevelData selected && selected != level)
            {
                level = selected;
                LoadSettingsFromLevel();
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            if (level == null)
            {
                EditorGUILayout.HelpBox("請選擇或建立一個 OtterRhythmLevelData 關卡資產。", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawValidationSummary();
            DrawSettings();
            DrawQuickTemplate();
            DrawComposer();
            DrawPhraseList();
            DrawExchange();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("海獺節奏關卡編輯器", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "不需要先懂樂理：可直接選中文節奏預設，或點擊 16 格步進器。每組 Phrase 會先由螃蟹示範一小節，再由玩家重複一小節。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            OtterRhythmLevelData selected = (OtterRhythmLevelData)EditorGUILayout.ObjectField(
                new GUIContent("目前關卡", "可拖入任何 OtterRhythmLevelData 資產"),
                level,
                typeof(OtterRhythmLevelData),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                level = selected;
                selectedPhrase = -1;
                if (level != null)
                    LoadSettingsFromLevel();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("建立新關卡", GUILayout.Height(28f)))
                CreateNewLevel();
            GUI.enabled = level != null;
            if (GUILayout.Button("複製目前關卡", GUILayout.Height(28f)))
                DuplicateLevel();
            if (GUILayout.Button("套用到測試 Scene", GUILayout.Height(28f)))
                ApplyToTestScene();
            if (GUILayout.Button("在 Project 定位", GUILayout.Height(28f)))
                EditorGUIUtility.PingObject(level);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6f);
        }

        private void DrawValidationSummary()
        {
            OtterRhythmLevelExchange.Validate(level, validationErrors, validationWarnings);
            if (validationErrors.Count == 0)
                EditorGUILayout.HelpBox($"資料有效｜{level.Phrases.Count} 組 Phrase｜{level.TotalBars} 小節｜{level.AuthoredBpm:0.##} BPM", MessageType.Info);
            else
                EditorGUILayout.HelpBox(string.Join("\n", validationErrors), MessageType.Error);

            if (validationWarnings.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", validationWarnings), MessageType.Warning);
        }

        private void DrawSettings()
        {
            showSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showSettings, "1. 關卡與音樂設定");
            if (showSettings)
            {
                EditorGUI.indentLevel++;
                levelId = EditorGUILayout.TextField(new GUIContent("Level ID", "供程式、JSON 與音樂表格辨識，建議只用英文、數字與連字號。"), levelId);
                displayName = EditorGUILayout.TextField("顯示名稱", displayName);
                EditorGUILayout.LabelField("製作備註");
                notes = EditorGUILayout.TextArea(notes, GUILayout.MinHeight(48f));

                EditorGUILayout.Space(5f);
                musicEventPath = EditorGUILayout.TextField("FMOD Music Event", musicEventPath);
                musicStartDelaySeconds = EditorGUILayout.FloatField(new GUIContent("播放前等待（秒）", "進入 Play Mode 後等待多久才啟動音樂。"), musicStartDelaySeconds);
                chartOffsetMs = EditorGUILayout.FloatField(new GUIContent("Chart Offset（ms）", "正值讓譜面整體變晚，負值讓譜面整體變早。"), chartOffsetMs);
                bpm = EditorGUILayout.FloatField("BPM", bpm);
                beatsPerBar = EditorGUILayout.IntSlider("每小節拍數", beatsPerBar, 2, 7);
                totalBars = EditorGUILayout.IntField("總小節數", totalBars);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.IntField(new GUIContent("PPQ", "每一拍切成多少 tick；固定 480 方便與 DAW/MIDI 對接。"), ppq);

                EditorGUILayout.Space(5f);
                perfectWindowMs = EditorGUILayout.FloatField("Perfect ±ms", perfectWindowMs);
                goodWindowMs = EditorGUILayout.FloatField("Good ±ms", goodWindowMs);
                judgementOffsetMs = EditorGUILayout.FloatField("輸入校正 Offset（ms）", judgementOffsetMs);

                EditorGUILayout.Space(5f);
                EditorGUILayout.LabelField("可選 FMOD SFX（可留白）", EditorStyles.boldLabel);
                cueSfx = EditorGUILayout.TextField("Cue", cueSfx);
                hitSfx = EditorGUILayout.TextField("Hit", hitSfx);
                missSfx = EditorGUILayout.TextField("Miss", missSfx);
                successSfx = EditorGUILayout.TextField("Success", successSfx);

                if (GUILayout.Button("儲存上述設定", GUILayout.Height(26f)))
                    SaveSettingsToLevel();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4f);
        }

        private void DrawQuickTemplate()
        {
            showQuickTemplate = EditorGUILayout.BeginFoldoutHeaderGroup(showQuickTemplate, "2. 快速產生關卡流程");
            if (showQuickTemplate)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(
                    "入門：從單拍與強拍逐步增加變化。\n流行律動：每拍、2/4 反拍與常見 Groove。\n切分進階：休止、半拍與 3-3-2。最後兩組自動加入適性分支。",
                    MessageType.None);
                template = (OtterRhythmPresetLibrary.ProgressionTemplate)EditorGUILayout.EnumPopup("流程範本", template);
                templatePhraseCount = EditorGUILayout.IntSlider("Phrase 數量", templatePhraseCount, 2, 16);
                if (GUILayout.Button("用範本取代目前所有 Phrase", GUILayout.Height(28f))
                    && EditorUtility.DisplayDialog("取代節奏流程？", "目前所有 Phrase 會被新範本取代。", "取代", "取消"))
                {
                    Undo.RecordObject(level, "Apply Rhythm Progression Template");
                    List<OtterRhythmLevelData.Phrase> phrases = OtterRhythmPresetLibrary.CreateProgression(
                        template,
                        templatePhraseCount,
                        level.Ppq,
                        level.BeatsPerBar);
                    level.ConfigureMusic(
                        level.MusicEventPath,
                        level.MusicStartDelaySeconds,
                        level.ChartOffsetMs,
                        level.AuthoredBpm,
                        level.BeatsPerBar,
                        2 + templatePhraseCount * 2,
                        level.Ppq);
                    level.ReplacePhrases(phrases);
                    MarkLevelDirty();
                    LoadSettingsFromLevel();
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4f);
        }

        private void DrawComposer()
        {
            showComposer = EditorGUILayout.BeginFoldoutHeaderGroup(showComposer, "3. Phrase 節奏編排");
            if (showComposer)
            {
                EditorGUI.indentLevel++;
                phraseStartBar = EditorGUILayout.IntField(new GUIContent("示範起始小節", "下一小節會自動成為玩家回應小節。"), phraseStartBar);
                phraseLabel = EditorGUILayout.TextField("提示名稱", phraseLabel);
                phraseAdaptive = EditorGUILayout.Toggle(new GUIContent("啟用適性分支", "依近期命中率選 Assist／Standard／Challenge。"), phraseAdaptive);

                standardPreset = EditorGUILayout.Popup("Standard 預設", standardPreset, OtterRhythmPresetLibrary.DisplayNames);
                OtterRhythmPresetLibrary.Preset selectedPreset = OtterRhythmPresetLibrary.Get(standardPreset);
                EditorGUILayout.HelpBox(selectedPreset.TheoryHint, MessageType.None);

                EditorGUILayout.BeginHorizontal();
                useCustomSteps = EditorGUILayout.ToggleLeft("使用下方自訂 16 分音符步進器", useCustomSteps);
                if (GUILayout.Button("載入 Standard 預設到步進器", GUILayout.Width(220f)))
                {
                    customSteps = OtterRhythmPresetLibrary.PatternToSteps(
                        selectedPreset.CreatePattern(level.Ppq, level.BeatsPerBar),
                        level.Ppq,
                        level.BeatsPerBar);
                    useCustomSteps = true;
                }
                EditorGUILayout.EndHorizontal();

                EnsureStepArray();
                DrawStepGrid();

                if (phraseAdaptive)
                {
                    assistPreset = EditorGUILayout.Popup("Assist 預設", assistPreset, OtterRhythmPresetLibrary.DisplayNames);
                    challengePreset = EditorGUILayout.Popup("Challenge 預設", challengePreset, OtterRhythmPresetLibrary.DisplayNames);
                    EditorGUILayout.HelpBox(
                        "適性只替換節奏內容，不改 Perfect／Good 判定窗，因此測試數據仍可公平比較。",
                        MessageType.Info);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(selectedPhrase >= 0 ? "更新選取的 Phrase" : "新增 Phrase", GUILayout.Height(28f)))
                    CommitPhrase();
                if (selectedPhrase >= 0 && GUILayout.Button("取消編輯", GUILayout.Height(28f)))
                    ResetComposer();
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4f);
        }

        private void DrawStepGrid()
        {
            EditorGUILayout.LabelField("每拍四格：1 / e / & / a", EditorStyles.miniBoldLabel);
            for (int beat = 0; beat < level.BeatsPerBar; beat++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"第 {beat + 1} 拍", GUILayout.Width(58f));
                for (int subdivision = 0; subdivision < 4; subdivision++)
                {
                    int step = beat * 4 + subdivision;
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = customSteps[step]
                        ? new Color(0.25f, 0.9f, 1f, 1f)
                        : new Color(0.5f, 0.55f, 0.58f, 1f);
                    customSteps[step] = GUILayout.Toggle(
                        customSteps[step],
                        OtterRhythmPresetLibrary.StepLabel(step),
                        "Button",
                        GUILayout.Width(46f));
                    GUI.backgroundColor = previous;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawPhraseList()
        {
            showPhrases = EditorGUILayout.BeginFoldoutHeaderGroup(showPhrases, $"4. 關卡 Phrase 清單（{level.Phrases.Count}）");
            if (showPhrases)
            {
                for (int i = 0; i < level.Phrases.Count; i++)
                {
                    OtterRhythmLevelData.Phrase phrase = level.Phrases[i];
                    EditorGUILayout.BeginVertical(i == selectedPhrase ? "SelectionRect" : "box");
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"#{i + 1:00}  小節 {phrase.StartBar}→{phrase.StartBar + 1}", EditorStyles.boldLabel, GUILayout.Width(140f));
                    GUILayout.Label(phrase.Label, GUILayout.MinWidth(150f));
                    GUILayout.Label(phrase.Adaptive ? "適性" : "固定", GUILayout.Width(38f));
                    GUILayout.Label($"Standard: {phrase.StandardPattern?.Id}", EditorStyles.miniLabel, GUILayout.MinWidth(150f));
                    if (GUILayout.Button("編輯", GUILayout.Width(52f)))
                        EditPhrase(i);
                    if (GUILayout.Button("刪除", GUILayout.Width(52f))
                        && EditorUtility.DisplayDialog("刪除 Phrase？", $"刪除第 {i + 1} 組節奏？", "刪除", "取消"))
                    {
                        Undo.RecordObject(level, "Remove Rhythm Phrase");
                        level.RemovePhraseAt(i);
                        MarkLevelDirty();
                        ResetComposer();
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                    DrawCompactPattern("節奏", phrase.StandardPattern);
                    if (phrase.Adaptive)
                    {
                        DrawCompactPattern("Assist", phrase.AssistPattern);
                        DrawCompactPattern("Challenge", phrase.ChallengePattern);
                    }
                    EditorGUILayout.EndVertical();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4f);
        }

        private void DrawCompactPattern(string label, OtterRhythmLevelData.Pattern pattern)
        {
            bool[] steps = OtterRhythmPresetLibrary.PatternToSteps(pattern, level.Ppq, level.BeatsPerBar);
            System.Text.StringBuilder text = new();
            for (int i = 0; i < steps.Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                    text.Append(" | ");
                text.Append(steps[i] ? "● " : "· ");
            }
            EditorGUILayout.LabelField(label, text.ToString(), EditorStyles.miniLabel);
        }

        private void DrawExchange()
        {
            showExchange = EditorGUILayout.BeginFoldoutHeaderGroup(showExchange, "5. 與音樂製作交換資料");
            if (showExchange)
            {
                EditorGUILayout.HelpBox(
                    "JSON 可完整往返關卡設定；CSV 會展開每個 Cue／Response 的小節、拍點、PPQ tick、absolute beat 與 timeline seconds，可交給 DAW／試算表使用。",
                    MessageType.Info);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("匯出完整 JSON", GUILayout.Height(28f)))
                    ExportJson();
                if (GUILayout.Button("從 JSON 覆蓋目前關卡", GUILayout.Height(28f)))
                    ImportJson();
                if (GUILayout.Button("匯出音樂製作 CSV", GUILayout.Height(28f)))
                    ExportCsv();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(10f);
        }

        private void SaveSettingsToLevel()
        {
            Undo.RecordObject(level, "Edit Rhythm Level Settings");
            level.ConfigureAuthoring(levelId, displayName, notes);
            level.ConfigureMusic(
                musicEventPath,
                musicStartDelaySeconds,
                chartOffsetMs,
                bpm,
                beatsPerBar,
                totalBars,
                ppq);
            level.ConfigureJudgement(perfectWindowMs, goodWindowMs, judgementOffsetMs);
            level.ConfigureOptionalSfx(cueSfx, hitSfx, missSfx, successSfx);
            MarkLevelDirty();
            EnsureStepArray();
        }

        private void LoadSettingsFromLevel()
        {
            if (level == null)
                return;
            if (level.EnsureAuthoringDefaults())
                MarkLevelDirty();
            levelId = level.LevelId;
            displayName = level.DisplayName;
            notes = level.AuthoringNotes;
            musicEventPath = level.MusicEventPath;
            musicStartDelaySeconds = level.MusicStartDelaySeconds;
            chartOffsetMs = level.ChartOffsetMs;
            bpm = level.AuthoredBpm;
            beatsPerBar = level.BeatsPerBar;
            totalBars = level.TotalBars;
            ppq = level.Ppq;
            perfectWindowMs = level.PerfectWindowMs;
            goodWindowMs = level.GoodWindowMs;
            judgementOffsetMs = level.JudgementOffsetMs;
            cueSfx = level.CueSoundEventPath;
            hitSfx = level.HitSoundEventPath;
            missSfx = level.MissSoundEventPath;
            successSfx = level.SuccessSoundEventPath;
            EnsureStepArray();
        }

        private void CommitPhrase()
        {
            OtterRhythmLevelData.Pattern standard = useCustomSteps
                ? OtterRhythmPresetLibrary.StepsToPattern("custom-grid", customSteps, level.Ppq)
                : OtterRhythmPresetLibrary.Get(standardPreset).CreatePattern(level.Ppq, level.BeatsPerBar);
            if (standard.HitTicks.Count == 0)
            {
                EditorUtility.DisplayDialog("節奏不可為空", "請至少點亮一個步進器格子。", "好");
                return;
            }

            OtterRhythmLevelData.Pattern assist = phraseAdaptive
                ? OtterRhythmPresetLibrary.Get(assistPreset).CreatePattern(level.Ppq, level.BeatsPerBar)
                : standard;
            OtterRhythmLevelData.Pattern challenge = phraseAdaptive
                ? OtterRhythmPresetLibrary.Get(challengePreset).CreatePattern(level.Ppq, level.BeatsPerBar)
                : standard;
            OtterRhythmLevelData.Phrase phrase = new(
                Mathf.Max(1, phraseStartBar),
                phraseLabel,
                phraseAdaptive,
                assist,
                standard,
                challenge);

            Undo.RecordObject(level, selectedPhrase >= 0 ? "Update Rhythm Phrase" : "Add Rhythm Phrase");
            if (selectedPhrase >= 0)
                level.ReplacePhrase(selectedPhrase, phrase);
            else
                level.AddPhrase(phrase);
            MarkLevelDirty();
            ResetComposer();
        }

        private void EditPhrase(int index)
        {
            selectedPhrase = index;
            OtterRhythmLevelData.Phrase phrase = level.Phrases[index];
            phraseStartBar = phrase.StartBar;
            phraseLabel = phrase.Label;
            phraseAdaptive = phrase.Adaptive;
            standardPreset = OtterRhythmPresetLibrary.FindIndex(phrase.StandardPattern?.Id);
            assistPreset = OtterRhythmPresetLibrary.FindIndex(phrase.AssistPattern?.Id);
            challengePreset = OtterRhythmPresetLibrary.FindIndex(phrase.ChallengePattern?.Id);
            customSteps = OtterRhythmPresetLibrary.PatternToSteps(phrase.StandardPattern, level.Ppq, level.BeatsPerBar);
            useCustomSteps = standardPreset == 0 && phrase.StandardPattern?.Id != OtterRhythmPresetLibrary.Get(0).Id;
            showComposer = true;
        }

        private void ResetComposer()
        {
            selectedPhrase = -1;
            phraseStartBar = level != null && level.Phrases.Count > 0
                ? level.Phrases[level.Phrases.Count - 1].StartBar + 2
                : 3;
            phraseLabel = "新的節奏組";
            phraseAdaptive = false;
            useCustomSteps = false;
            customSteps = new bool[Mathf.Max(1, level != null ? level.BeatsPerBar : 4) * 4];
        }

        private void EnsureStepArray()
        {
            int length = Mathf.Max(1, beatsPerBar) * 4;
            if (customSteps != null && customSteps.Length == length)
                return;
            bool[] resized = new bool[length];
            if (customSteps != null)
            {
                for (int i = 0; i < Mathf.Min(customSteps.Length, resized.Length); i++)
                    resized[i] = customSteps[i];
            }
            customSteps = resized;
        }

        private void CreateNewLevel()
        {
            EnsureDataFolder();
            string path = EditorUtility.SaveFilePanelInProject(
                "建立海獺節奏關卡",
                "NewOtterRhythmLevel",
                "asset",
                "選擇關卡資產儲存位置",
                DefaultDataFolder);
            if (string.IsNullOrWhiteSpace(path))
                return;
            OtterRhythmLevelData created = CreateInstance<OtterRhythmLevelData>();
            created.ConfigurePrototypeDefaults();
            created.ConfigureAuthoring(Path.GetFileNameWithoutExtension(path), Path.GetFileNameWithoutExtension(path), string.Empty);
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            level = created;
            Selection.activeObject = created;
            LoadSettingsFromLevel();
        }

        private void DuplicateLevel()
        {
            EnsureDataFolder();
            string sourcePath = AssetDatabase.GetAssetPath(level);
            string path = EditorUtility.SaveFilePanelInProject(
                "複製海獺節奏關卡",
                $"{level.name}_Copy",
                "asset",
                "選擇複製資產儲存位置",
                Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? DefaultDataFolder);
            if (string.IsNullOrWhiteSpace(path))
                return;
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            if (!AssetDatabase.CopyAsset(sourcePath, path))
            {
                EditorUtility.DisplayDialog("複製失敗", path, "好");
                return;
            }
            AssetDatabase.SaveAssets();
            level = AssetDatabase.LoadAssetAtPath<OtterRhythmLevelData>(path);
            Selection.activeObject = level;
            LoadSettingsFromLevel();
        }

        private void ApplyToTestScene()
        {
            SaveSettingsToLevel();
            OtterRhythmLevelExchange.Validate(level, validationErrors, validationWarnings);
            if (validationErrors.Count > 0)
            {
                EditorUtility.DisplayDialog("關卡資料有錯誤", string.Join("\n", validationErrors), "好");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.OpenScene(OtterShellBeatLabSceneBuilder.ScenePath, OpenSceneMode.Single);
            OtterRhythmLevelRunner runner = FindInScene<OtterRhythmLevelRunner>(scene);
            FmodBeatClock clock = FindInScene<FmodBeatClock>(scene);
            if (runner == null || clock == null)
            {
                EditorUtility.DisplayDialog("測試 Scene 不完整", "找不到 Level Runner 或 FMOD Beat Clock，請先重建 Scene。", "好");
                return;
            }
            runner.Configure(clock, level);
            clock.Configure(level.MusicEventPath, level.MusicStartDelaySeconds, true);
            EditorUtility.SetDirty(runner);
            EditorUtility.SetDirty(clock);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(OtterShellBeatLabSceneBuilder.ScenePath);
            Debug.Log($"[OtterRhythmLevelEditor] Applied '{level.DisplayName}' to {OtterShellBeatLabSceneBuilder.ScenePath}");
        }

        private void ExportJson()
        {
            string path = EditorUtility.SaveFilePanel("匯出節奏關卡 JSON", string.Empty, $"{level.LevelId}.json", "json");
            if (!string.IsNullOrWhiteSpace(path))
                OtterRhythmLevelExchange.ExportJson(level, path);
        }

        private void ImportJson()
        {
            string path = EditorUtility.OpenFilePanel("匯入節奏關卡 JSON", string.Empty, "json");
            if (string.IsNullOrWhiteSpace(path))
                return;
            if (!EditorUtility.DisplayDialog("覆蓋目前關卡？", $"JSON 將覆蓋 {level.name} 的所有設定與 Phrase。", "匯入", "取消"))
                return;
            if (!OtterRhythmLevelExchange.TryImportJson(path, level, out string error))
                EditorUtility.DisplayDialog("JSON 匯入失敗", error, "好");
            else
                LoadSettingsFromLevel();
        }

        private void ExportCsv()
        {
            string path = EditorUtility.SaveFilePanel("匯出音樂製作 CSV", string.Empty, $"{level.LevelId}_timing.csv", "csv");
            if (!string.IsNullOrWhiteSpace(path))
                OtterRhythmLevelExchange.ExportProducerCsv(level, path);
        }

        private void MarkLevelDirty()
        {
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureDataFolder()
        {
            if (!AssetDatabase.IsValidFolder(DefaultDataFolder))
                AssetDatabase.CreateFolder("Assets/OtterAquariumPrototype", "Data");
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }
            return null;
        }
    }

    [CustomEditor(typeof(OtterRhythmLevelData))]
    internal sealed class OtterRhythmLevelDataInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            OtterRhythmLevelData level = (OtterRhythmLevelData)target;
            EditorGUILayout.LabelField(level.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{level.AuthoredBpm:0.##} BPM  •  {level.BeatsPerBar}/4  •  {level.TotalBars} bars  •  {level.Phrases.Count} phrases");
            EditorGUILayout.HelpBox("使用專用編輯器可取得中文預設、16 格步進器、適性分支及 JSON／CSV 功能。", MessageType.Info);
            if (GUILayout.Button("開啟海獺節奏關卡編輯器", GUILayout.Height(30f)))
            {
                Selection.activeObject = level;
                OtterRhythmLevelEditorWindow.Open();
            }
        }
    }
}
