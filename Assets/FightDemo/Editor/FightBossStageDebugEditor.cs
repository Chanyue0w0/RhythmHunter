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
            EditorGUILayout.HelpBox("可測試一階段成功等待 Normal_End，或直接跳至二階段音樂。此項先完成音樂與階段切換；二階段走位、譜面與輸入將於後續項目接入。",MessageType.Info);
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
