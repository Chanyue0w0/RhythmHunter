using System.Linq;
using RhythmHunter.FightDemo;
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
            var chart = (FightBossRhythmChart)target;
            if (!chart.Validate(out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.HelpBox("數字為抵達拍，從 Normal_End 起算（包含轉場）。同一拍只能有一個方向。Travel Beats 為飛行提前量，Animation Beats 為出招演出長度，不會修改角色 Prefab。", MessageType.Info);
        }
    }
    public sealed class FightBossRhythmChartWindow : EditorWindow
    {
        private FightBossRhythmChart chart;
        private Vector2 scroll;
        private int length = 114;
        public static void Open(FightBossRhythmChart asset)
        { var window = GetWindow<FightBossRhythmChartWindow>("Boss 譜面"); window.chart = asset; window.Show(); }
        [MenuItem("Rhythm Hunter/Boss Rhythm Chart")]
        private static void OpenMenu() => Open(Selection.activeObject as FightBossRhythmChart);
        private void OnGUI()
        {
            chart = (FightBossRhythmChart)EditorGUILayout.ObjectField("譜面", chart, typeof(FightBossRhythmChart), false);
            if (!chart) return;
            length = Mathf.Clamp(EditorGUILayout.IntField("顯示拍數", length), 16, 1024);
            EditorGUILayout.HelpBox("點格子新增／移動該拍的斬擊，再點同一格刪除。上到下：左／中／右。每 4 拍一小節；抵達時間由 FMOD 節拍決定。", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            Rect area = GUILayoutUtility.GetRect(length * 28 + 50, 150);
            for (int beat = 0; beat < length; beat++)
            {
                float x = area.x + 50 + beat * 28;
                GUI.Label(new Rect(x, area.y, 28, 22), beat.ToString());
                for (int lane = 0; lane < 3; lane++)
                {
                    var rect = new Rect(x, area.y + 25 + lane * 36, 26, 32);
                    bool occupied = chart.notes.Any(n => n.arrivalBeat == beat && (int)n.lane == lane);
                    EditorGUI.DrawRect(rect, occupied ? new Color(.85f, .22f, .1f) : beat % 4 == 0 ? new Color(.3f,.3f,.3f) : new Color(.18f,.18f,.18f));
                    if (GUI.Button(rect, occupied ? "●" : "", GUIStyle.none))
                    {
                        Undo.RecordObject(chart, "Edit Boss rhythm note");
                        chart.notes.RemoveAll(n => n.arrivalBeat == beat);
                        if (!occupied) chart.notes.Add(new FightBossRhythmChart.Note { arrivalBeat = beat, lane = (FightBossRhythmChart.Lane)lane });
                        chart.notes = chart.notes.OrderBy(n => n.arrivalBeat).ToList(); EditorUtility.SetDirty(chart);
                    }
                }
            }
            string[] labels = { "左 X", "中 Y", "右 B" };
            for (int i = 0; i < 3; i++) GUI.Label(new Rect(area.x, area.y + 29 + i * 36, 48, 30), labels[i]);
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("儲存譜面")) AssetDatabase.SaveAssetIfDirty(chart);
        }
    }
}
