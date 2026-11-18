using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using FMODUnity;

namespace RhythmHunter.FightDemoEditor
{
    public sealed partial class FightBossRhythmChartWindow
    {
        [SerializeField] private float pixelsPerBeat = 60;
        [SerializeField] private double waveformOffset;
        [SerializeField] private float spacingBeats = .5f;
        private string waveformPath, waveformStatus;
        private bool waveformPending;
        private float[] peaks;
        private double audioSeconds;
        private readonly HashSet<int> selection = new();
        private Dictionary<int, double> dragStart;
        private float dragX;
        private int dragControl, dragUndo;
        private double rangeStart, rangeEnd = 4;
        private void DisposeWaveform()
        { waveformPending = false; }
        private void LoadWaveform()
        {
            DisposeWaveform(); peaks = null;
            try
            {
                var evt = EventManager.EventFromPath(eventPath);
                string root = Path.GetFullPath("ProjectFMOD");
                string xmlPath = Directory.GetFiles(root, "{" + evt.Guid.ToString().Trim('{','}') + "}.xml", SearchOption.AllDirectories).Single();
                var sounds = XDocument.Load(xmlPath).Descendants("object").Where(x => (string)x.Attribute("class") == "SingleSound").ToArray();
                if (sounds.Length != 1) throw new Exception("多音軌事件請手動選擇參考音檔，並確認波形對齊偏移。");
                var sound = sounds[0];
                string audioId = sound.Elements("relationship").First(x => (string)x.Attribute("name") == "audioFile").Element("destination").Value;
                var audioXml = XDocument.Load(Directory.GetFiles(root, audioId + ".xml", SearchOption.AllDirectories).Single());
                string relative = audioXml.Descendants("property").First(x => (string)x.Attribute("name") == "assetPath").Element("value").Value;
                var project = Directory.GetParent(Path.GetDirectoryName(xmlPath)).Parent.FullName;
                waveformPath = Path.Combine(project, "Assets", relative);
                string offset = sound.Elements("property").FirstOrDefault(x => (string)x.Attribute("name") == "timelockedOffset")?.Element("value")?.Value;
                waveformOffset = double.TryParse(offset, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;
                BeginWaveform();
            }
            catch (Exception e) { waveformStatus = e.Message; }
        }
        private void BeginWaveform()
        {
            DisposeWaveform(); peaks = null;
            if (!File.Exists(waveformPath)) { waveformStatus = "找不到音檔：" + waveformPath; return; }
            waveformPending = true; waveformStatus = "正在由 FMOD 解碼波形…";
        }
        private void PollWaveform()
        {
            if (!waveformPending) return;
            waveformPending = false;
            FMOD.Sound sound = default;
            try
            {
                EditorUtils.System.getCoreSystem(out var core);
                var result = core.createSound(waveformPath, FMOD.MODE.OPENONLY | FMOD.MODE.CREATESTREAM, out sound);
                if (result != FMOD.RESULT.OK) throw new Exception(result.ToString());
                sound.getFormat(out _, out var format, out int channels, out int bits);
                sound.getDefaults(out float frequency, out _);
                sound.getLength(out uint length, FMOD.TIMEUNIT.PCMBYTES);
                if (bits != 16 && format != FMOD.SOUND_FORMAT.PCMFLOAT) throw new Exception("Unsupported PCM: " + format);
                int bytesPerSample = bits / 8;
                audioSeconds = length / (double)(bytesPerSample * channels) / frequency;
                peaks = new float[(int)Math.Ceiling(audioSeconds * 500)];
                var buffer = new byte[65536 - 65536 % (bytesPerSample * channels)];
                long samples = 0;
                while (true)
                {
                    result = sound.readData(buffer, out uint read);
                    for (int i = 0; i + bytesPerSample <= read; i += bytesPerSample)
                    {
                        float value = format == FMOD.SOUND_FORMAT.PCMFLOAT ? BitConverter.ToSingle(buffer,i) : BitConverter.ToInt16(buffer,i) / 32768f;
                        int bin = (int)(samples++ / (double)channels / frequency * 500);
                        if (bin < peaks.Length) peaks[bin] = Mathf.Max(peaks[bin], Mathf.Abs(value));
                    }
                    if (result == FMOD.RESULT.ERR_FILE_EOF || read == 0) break;
                    if (result != FMOD.RESULT.OK) throw new Exception(result.ToString());
                }
                waveformStatus = $"{Path.GetFileName(waveformPath)} · {frequency}Hz · {channels}ch · {audioSeconds:F3}s · 2ms 峰值";
            }
            catch (Exception e) { waveformStatus = e.Message; peaks = null; }
            finally { if (sound.hasHandle()) sound.release(); }
            Repaint();
        }
        private double PxToSeconds(float px) => px / pixelsPerBeat * MsPerBeat / 1000;
        private float SecondsToPx(double seconds) => (float)(seconds * 1000 / MsPerBeat * pixelsPerBeat);
        private void WriteTime(int index, double seconds)
        {
            var n = chart.notes[index];
            if (n.useFreeTime) n.timeSeconds = seconds - n.offsetMs / 1000d;
            else n.offsetMs = (float)(seconds * 1000 - n.arrivalBeat * MsPerBeat);
            // A free-time point with a large positive offset may need a new nonnegative base.
            if (n.useFreeTime && n.timeSeconds < 0) { n.timeSeconds = seconds; n.offsetMs = 0; }
            chart.notes[index] = n;
        }
        private void TranslateSelection(Dictionary<int, double> originals, double delta)
        {
            if (originals.Count == 0) return;
            delta = Math.Max(-originals.Values.Min(), Math.Min(duration - originals.Values.Max(), delta));
            foreach (var pair in originals) WriteTime(pair.Key, pair.Value + delta);
            EditorUtility.SetDirty(chart);
        }
        private void SpaceSelection()
        {
            var indices = selection.Where(i => i >= 0 && i < chart.notes.Count).OrderBy(i => Seconds(chart.notes[i])).ToArray();
            if (indices.Length < 2) return;
            double first = Seconds(chart.notes[indices[0]]), spacing = spacingBeats * MsPerBeat / 1000;
            if (first + spacing * (indices.Length - 1) > duration) { message = "等距排列將超出歌曲結束；請縮小間距或先平移整組。"; return; }
            Undo.RecordObject(chart, "Space selected rhythm notes");
            for (int i = 0; i < indices.Length; i++) WriteTime(indices[i], first + spacing * i);
            EditorUtility.SetDirty(chart);
        }
        private void DrawAdvancedTools()
        {
            pixelsPerBeat = EditorGUILayout.Slider("時間軸縮放（像素／拍）", pixelsPerBeat, 24, 320);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("重新讀取波形")) LoadWaveform();
            if (GUILayout.Button("手動選音檔"))
            { var file = EditorUtility.OpenFilePanel("參考音檔", "ProjectFMOD", ""); if (!string.IsNullOrEmpty(file)) { waveformPath = file; BeginWaveform(); } }
            EditorGUILayout.EndHorizontal();
            waveformOffset = EditorGUILayout.DoubleField("波形來源偏移（秒）", waveformOffset);
            EditorGUILayout.LabelField(waveformStatus ?? "讀取歌曲後載入原始音檔波形", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.BeginHorizontal();
            rangeStart = EditorGUILayout.DoubleField("起拍", rangeStart);
            rangeEnd = EditorGUILayout.DoubleField("迄拍", rangeEnd);
            if (GUILayout.Button("選取此拍段"))
            {
                selection.Clear();
                for (int i = 0; i < chart.notes.Count; i++)
                { double beat = Seconds(chart.notes[i]) * 1000 / MsPerBeat; if (beat >= rangeStart && beat <= rangeEnd) selection.Add(i); }
                selected = selection.Count > 0 ? selection.First() : -1;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"已選 {selection.Count} 點", GUILayout.Width(85));
            spacingBeats = Mathf.Max(.001f, EditorGUILayout.FloatField("間距（拍）", spacingBeats));
            using (new EditorGUI.DisabledScope(selection.Count < 2))
                if (GUILayout.Button("套用等距")) SpaceSelection();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("拖曳音符調整抵達時間；Ctrl／Shift 點選可複選，拖曳整組保留原間隔。吸附只對齊拖曳主點，其餘保持距離。波形與拍數尺點擊可跳轉試聽；Ctrl+Z 還原整次拖曳／排列。", MessageType.Info);
        }
        private void DrawWaveform(Rect area)
        {
            EditorGUI.DrawRect(new Rect(area.x + 50, area.y + 24, area.width - 50, 76), new Color(.07f,.1f,.13f));
            if (peaks == null) return;
            int left = Math.Max(0, (int)scroll.x - 50), right = Math.Min((int)area.width - 50, left + (int)position.width + 80);
            for (int px = left; px < right; px++)
            {
                double seconds = startMs / 1000d + PxToSeconds(px) + waveformOffset;
                int a = (int)Math.Floor(seconds * 500), b = (int)Math.Ceiling((seconds + PxToSeconds(1)) * 500);
                float value = 0;
                for (int i = Math.Max(0,a); i <= Math.Min(peaks.Length-1,b); i++) value = Mathf.Max(value,peaks[i]);
                EditorGUI.DrawRect(new Rect(area.x + 50 + px, area.y + 62 - value * 36, 1, Mathf.Max(1,value * 72)), new Color(.25f,.8f,.75f));
            }
        }
    }
}
