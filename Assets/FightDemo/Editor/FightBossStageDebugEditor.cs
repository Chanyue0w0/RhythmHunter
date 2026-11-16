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
            EditorGUILayout.HelpBox("目前完成第一項：可快速設為 1HP／疲憊。二階段音樂及轉場尚未接入；跳轉按鈕先保留請求，不會假裝已進入二階段。",MessageType.Info);
            using(new EditorGUI.DisabledScope(!debug.CanTest))
            {
                if(GUILayout.Button("測試：完成一階段（1HP／疲憊）"))debug.CompletePhaseOne();
                if(GUILayout.Button("測試：請求直接進入二階段"))debug.RequestPhaseTwoSkip();
            }
            EditorGUILayout.LabelField("二階段跳轉請求",debug.PhaseTwoSkipRequested ? "等待二階段控制器接手" : "無");
        }
    }
}
