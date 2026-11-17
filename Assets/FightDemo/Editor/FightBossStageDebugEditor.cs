using UnityEditor;
using UnityEngine;
using RhythmHunter.FightDemo;

namespace RhythmHunter.FightDemoEditor
{
    [CustomEditor(typeof(FightBossStageDebug))]
    public sealed class FightBossStageDebugEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var debug=(FightBossStageDebug)target;
            EditorGUILayout.HelpBox("可測試一階段成功等待 Normal_End，或直接跳至二階段音樂。跳轉後會執行 Boss 與勇者走位，再開始三路譜面；轉場時間包含在二階段音樂內。",MessageType.Info);
            using(new EditorGUI.DisabledScope(!debug.CanTest))
            {
                if(GUILayout.Button("測試：完成一階段（1HP／疲憊）"))debug.CompletePhaseOne();
                if(GUILayout.Button("測試：請求直接進入二階段"))debug.RequestPhaseTwoSkip();
            }
            EditorGUILayout.LabelField("二階段跳轉請求",debug.PhaseTwoSkipRequested ? "等待二階段控制器接手" : "無");
            EditorGUILayout.LabelField("目前音樂階段", debug.MusicPhase);
        }
    }
}
