using System;
using System.Collections.Generic;
using System.Linq;
using FMODUnity;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [CustomEditor(typeof(FightBossRhythmChart))]
    public sealed class FightBossRhythmChartEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("開啟三路譜面編輯器")) FightBossRhythmChartWindow.Open((FightBossRhythmChart)target);
            DrawDefaultInspector();
            if (!((FightBossRhythmChart)target).Validate(out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.HelpBox("Arrival Beat + Offset Ms：基準拍加正負毫秒。Use Free Time：改用從 Normal_End 起算的 Time Seconds，再加 Offset Ms。舊資料偏移預設 0。", MessageType.Info);
        }
    }
    public sealed partial class FightBossRhythmChartWindow : EditorWindow
    {
        [SerializeField] private FightBossRhythmChart chart;
        [SerializeField] private string eventPath;
        [SerializeField] private bool snap = true;
        [SerializeField] private float recordingOffsetMs;
        private Vector2 scroll;
        private FMOD.Studio.EventInstance preview;
        private double tempo, cursor, duration;
        private int startMs, selected = -1;
        private bool recording, paused, previewStarted;
        private readonly HashSet<KeyCode> heldKeys = new();
        private string message;
        private double MsPerBeat => 60000 / Math.Max(1, tempo);
        public static void Open(FightBossRhythmChart asset)
        { var w = GetWindow<FightBossRhythmChartWindow>("Boss 譜面"); w.chart = asset; w.Show(); }
        [MenuItem("Rhythm Hunter/Boss Rhythm Chart")]
        private static void OpenMenu() => Open(Selection.activeObject as FightBossRhythmChart);
        private void OnEnable()
        {
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            Undo.undoRedoPerformed += Repaint;
        }
        private void OnDisable()
        {
            Stop(); DisposeWaveform(); EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            Undo.undoRedoPerformed -= Repaint;
        }
        private void PlayModeChanged(PlayModeStateChange _) => Stop();
        private void Tick()
        {
            PollWaveform();
            if (!preview.isValid()) return;
            preview.getPlaybackState(out var state);
            if (!previewStarted)
            {
                if (state != FMOD.Studio.PLAYBACK_STATE.PLAYING) return;
                previewStarted = true;
            }
            if (preview.getTimelinePosition(out int raw) == FMOD.RESULT.OK) cursor = Math.Max(0, (raw - startMs) / 1000d);
            preview.getPaused(out paused);
            UpdateAutoAudition();
            if (cursor >= duration || state == FMOD.Studio.PLAYBACK_STATE.STOPPED) Stop();
            Repaint();
        }
        private void Stop()
        { ClearAuditionSounds(); if (preview.isValid()) EditorUtils.PreviewStop(preview); preview.clearHandle(); recording = paused = previewStarted = false; heldKeys.Clear(); }
        private void OnLostFocus() => heldKeys.Clear();
        private void LoadSong()
        {
            Stop(); message = null;
            var clock = FindFirstObjectByType<FmodBeatClock>();
            if (string.IsNullOrWhiteSpace(eventPath) && clock) eventPath = clock.MusicEventPath;
            try
            {
                var markers = FightOpeningTempoImporter.ReadMarkers(eventPath);
                var start = markers.First(m => m.name == "Normal_End");
                var end = markers.First(m => m.name == "Boss_End");
                tempo = FightOpeningTempoImporter.ReadTempo(eventPath);
                if (tempo <= 0 || end.positionMs <= start.positionMs) throw new Exception("Invalid tempo or section markers.");
                startMs = start.positionMs; duration = (end.positionMs - startMs) / 1000d; cursor = 0;
                LoadWaveform();
            }
            catch (Exception e) { tempo = 0; message = "讀取音樂失敗：" + e.Message; }
        }
        private void Play()
        {
            if (EditorApplication.isPlaying || tempo <= 0) return;
            if (preview.isValid()) { EditorUtils.PreviewPause(preview); return; }
            // Decode the reference waveform before starting music; decoding can block the editor for seconds.
            PollWaveform();
            EditorUtils.LoadPreviewBanks();
            var evt = EventManager.EventFromPath(eventPath);
            if (!evt) { message = "FMOD Event 不存在，請檢查 Bank／Event 路徑。"; return; }
            preview = EditorUtils.PreviewEvent(evt, new Dictionary<string, float> { { "Overtime", 0 } }, 1, (float)(startMs / 1000d + cursor));
        }
        private double Seconds(FightBossRhythmChart.Note n) => (n.useFreeTime ? n.timeSeconds : n.arrivalBeat * MsPerBeat / 1000) + n.offsetMs / 1000d;
        private void Add(int lane, double seconds)
        {
            var n = new FightBossRhythmChart.Note { lane = (FightBossRhythmChart.Lane)lane };
            seconds = Math.Max(0, seconds);
            n.arrivalBeat = Mathf.RoundToInt((float)(seconds * 1000 / MsPerBeat));
            n.useFreeTime = !snap; n.timeSeconds = seconds;
            Undo.RecordObject(chart, "Add rhythm note"); chart.notes.Add(n); selected = chart.notes.Count - 1; EditorUtility.SetDirty(chart);
        }
        private void Record(int lane)
        {
            // Read native playback position at the actual key event, not the last GUI repaint.
            if (autoAudition || !preview.isValid() || paused || preview.getTimelinePosition(out int raw) != FMOD.RESULT.OK) return;
            Add(lane, (raw - startMs + recordingOffsetMs) / 1000d);
        }
        private Vector2 windowScroll;
        private void OnGUI()
        {
            windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
            try { DrawContents(); }
            finally { EditorGUILayout.EndScrollView(); }
        }
        private void DrawContents()
        {
            var next = (FightBossRhythmChart)EditorGUILayout.ObjectField("譜面", chart, typeof(FightBossRhythmChart), false);
            if (next != chart) { Stop(); chart = next; selected = -1; selection.Clear(); }
            if (!chart) return;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                string path = EditorGUILayout.TextField("FMOD Event", eventPath);
                if (path != eventPath) { Stop(); eventPath = path; tempo = 0; }
                if (GUILayout.Button("讀取場景歌曲／更新 FMOD 拍速與段落")) LoadSong();
                if (tempo > 0)
                {
                    EditorGUILayout.LabelField($"FMOD {tempo:F2} BPM · Normal_End {startMs / 1000d:F3}s · 二階段 {duration:F3}s");
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(preview.isValid() && !paused ? "暫停" : "播放／繼續")) Play();
                    if (GUILayout.Button("停止")) Stop();
                    recording = GUILayout.Toggle(recording, "錄點 X／Y／B", "Button");
                    EditorGUILayout.EndHorizontal();
                    double nextCursor = EditorGUILayout.Slider("試聽位置（秒）", (float)cursor, 0, (float)duration);
                    if (Math.Abs(nextCursor - cursor) > .001)
                    { SeekPreview(nextCursor); }
                }
            }
            if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("試聽錄點請在非 Play 模式使用，避免與戰鬥音樂重疊。修改後下次進入二階段套用。", MessageType.Info);
            snap = EditorGUILayout.Toggle("新增／錄點吸附整拍", snap);
            recordingOffsetMs = EditorGUILayout.FloatField("錄點補償（ms，負值提前）", recordingOffsetMs);
            EditorGUILayout.HelpBox("先讀取歌曲。雙擊軌道加點；單擊音符選取，下方微調。取消吸附後可自由錄點。錄點時讓此視窗保持焦點，按 X／Y／B；每次按下記錄一點。", MessageType.Info);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Warning);
            if (tempo <= 0) return;
            var e = Event.current;
            if (e.type == EventType.KeyUp) heldKeys.Remove(e.keyCode);
            if (recording && !EditorApplication.isPlaying && e.type == EventType.KeyDown && !EditorGUIUtility.editingTextField)
            {
                int lane = e.keyCode == KeyCode.X ? 0 : e.keyCode == KeyCode.Y ? 1 : e.keyCode == KeyCode.B ? 2 : -1;
                if (lane >= 0) { if (heldKeys.Add(e.keyCode)) Record(lane); e.Use(); }
            }
            selection.RemoveWhere(i => i < 0 || i >= chart.notes.Count);
            DrawAudioCalibration(); DrawAdvancedTools(); DrawTimeline(); DrawSelected();
            double window = 120;
            var fight = FindFirstObjectByType<FightCombatController>();
            if (fight && fight.BossRhythmJudge) window = fight.BossRhythmJudge.PerfectWindowMs;
            bool valid = chart.ValidateTiming(MsPerBeat, 0, 0, window, out string error);
            if (!valid) EditorGUILayout.HelpBox(error, MessageType.Warning);
            var arrivals = chart.notes.Select(Seconds).OrderBy(t => t).ToArray();
            if (arrivals.Zip(arrivals.Skip(1), (a, b) => (b - a) * 1000 < window * 2).Any(overlap => overlap))
                EditorGUILayout.HelpBox("密集段落：判定窗口重疊是允許的，不會停止出招。每次按鍵只判定同方向最近的一顆；同距離優先較早音符。", MessageType.Info);
            if (chart.notes.Any(n => Seconds(n) >= duration)) EditorGUILayout.HelpBox("有音符超過 Boss_End，歌曲結束前不會完成。請往前移動。", MessageType.Warning);
            if (GUILayout.Button("儲存譜面")) AssetDatabase.SaveAssetIfDirty(chart);
        }
        private void DrawTimeline()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(260));
            float width = SecondsToPx(duration) + 60;
            var area = GUILayoutUtility.GetRect(width, 230, GUILayout.ExpandWidth(false));
            DrawWaveform(area);
            for (int beat = 0; beat * MsPerBeat / 1000 <= duration; beat++)
            {
                float x = area.x + 50 + beat * pixelsPerBeat;
                GUI.Label(new Rect(x, area.y, pixelsPerBeat, 20), $"{beat}");
                if (pixelsPerBeat >= 60) GUI.Label(new Rect(x, area.y + 100, pixelsPerBeat, 20), $"{beat*MsPerBeat/1000:F2}s", EditorStyles.miniLabel);
                EditorGUI.DrawRect(new Rect(x, area.y + 24, 1, 204), beat % 4 == 0 ? Color.gray : new Color(.25f,.25f,.25f,.4f));
            }
            string[] labels = { "左 X", "中 Y", "右 B" };
            for (int i = 0; i < 3; i++) GUI.Label(new Rect(area.x, area.y + 124 + i * 36, 48, 30), labels[i]);
            var e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            for (int i = 0; i < chart.notes.Count; i++)
            {
                var n = chart.notes[i]; float x = area.x + 50 + SecondsToPx(Seconds(n));
                var rect = new Rect(x - 7, area.y + 124 + (int)n.lane * 36, 14, 26);
                EditorGUI.DrawRect(rect, selection.Contains(i) || i == selected ? Color.yellow : n.useFreeTime ? Color.cyan : new Color(.95f,.3f,.15f));
                GUI.Label(rect,new GUIContent("",$"#{i+1} · {Seconds(n):F3}s · {Seconds(n)*1000/MsPerBeat:F3}拍 · {n.offsetMs:+0.0;-0.0;0}ms"));
                if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                {
                    if (e.control || e.shift) { if (!selection.Add(i)) selection.Remove(i); selected = selection.Contains(i) ? i : -1; e.Use(); break; }
                    if (!selection.Contains(i)) { selection.Clear(); selection.Add(i); }
                    selected = i;
                    dragStart = selection.ToDictionary(k => k, k => Seconds(chart.notes[k])); dragX = e.mousePosition.x;
                    Undo.IncrementCurrentGroup(); dragUndo = Undo.GetCurrentGroup(); Undo.RegisterCompleteObjectUndo(chart,"Drag rhythm notes");
                    GUIUtility.hotControl = dragControl = control; e.Use(); break;
                }
            }
            if (dragStart != null && GUIUtility.hotControl == dragControl)
            {
                if (e.type == EventType.MouseDrag)
                {
                    double delta = PxToSeconds(e.mousePosition.x - dragX);
                    if (snap && selected >= 0)
                    { double target = dragStart[selected] + delta; delta = Math.Round(target * 1000 / MsPerBeat) * MsPerBeat / 1000 - dragStart[selected]; }
                    TranslateSelection(dragStart, delta); e.Use(); Repaint();
                }
                else if (e.type == EventType.MouseUp)
                { GUIUtility.hotControl = 0; dragStart = null; Undo.CollapseUndoOperations(dragUndo); e.Use(); }
                else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                { TranslateSelection(dragStart,0); GUIUtility.hotControl=0; dragStart=null; e.Use(); }
            }
            EditorGUI.DrawRect(new Rect(area.x + 50 + SecondsToPx(cursor), area.y + 22, 2, 206), Color.green);
            if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition) && e.mousePosition.x >= area.x + 50)
            {
                double seconds = Math.Clamp(PxToSeconds(e.mousePosition.x - area.x - 50),0,duration);
                if (e.mousePosition.y < area.y + 120)
                { SeekPreview(seconds); e.Use(); }
                else if (e.clickCount == 2)
                { int lane = Mathf.Clamp((int)((e.mousePosition.y-area.y-124)/36),0,2); Add(lane,seconds); selection.Clear(); selection.Add(selected); e.Use(); }
            }
            EditorGUILayout.EndScrollView();
        }
        private void DrawSelected()
        {
            if (selected < 0 || selected >= chart.notes.Count) return;
            var n = chart.notes[selected]; EditorGUILayout.LabelField($"選取音符 #{selected + 1} · 抵達 {Seconds(n):F3}s");
            EditorGUI.BeginChangeCheck();
            n.lane = (FightBossRhythmChart.Lane)EditorGUILayout.EnumPopup("方向", n.lane);
            bool free = EditorGUILayout.Toggle("自由時間模式", n.useFreeTime);
            if (free != n.useFreeTime)
            {
                double seconds = Seconds(n);
                n.useFreeTime = free;
                if (free) { n.timeSeconds = seconds; n.offsetMs = 0; }
                else { n.arrivalBeat = Mathf.RoundToInt((float)(seconds * 1000 / MsPerBeat)); n.offsetMs = (float)(seconds * 1000 - n.arrivalBeat * MsPerBeat); }
            }
            if (n.useFreeTime) n.timeSeconds = Math.Max(0, EditorGUILayout.DoubleField("二階段秒數", n.timeSeconds));
            else n.arrivalBeat = Math.Max(0, EditorGUILayout.IntField("基準拍", n.arrivalBeat));
            n.offsetMs = EditorGUILayout.FloatField("抵達偏移（ms）", n.offsetMs);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(chart, "Adjust note timing"); chart.notes[selected] = n; EditorUtility.SetDirty(chart); }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("提前 10ms")) Shift(-10);
            if (GUILayout.Button("延後 10ms")) Shift(10);
            if (GUILayout.Button("刪除此點")) { Undo.RecordObject(chart, "Remove note"); chart.notes.RemoveAt(selected); selected = -1; selection.Clear(); EditorUtility.SetDirty(chart); }
            EditorGUILayout.EndHorizontal();
        }
        private void Shift(float ms)
        { Undo.RecordObject(chart, "Shift note"); var n = chart.notes[selected]; n.offsetMs += ms; chart.notes[selected] = n; EditorUtility.SetDirty(chart); }
    }
}
