using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [CustomEditor(typeof(FightCombatController)), CanEditMultipleObjects]
    public sealed class FightCombatControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Enemy Pressure / 敵人攻勢計時", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("攻勢期間：完整攻擊動畫結束後休息 Recovery Beats，再於整拍出招。請在 Stage > Waves > Pressure 設定；重開戰鬥後生效。", MessageType.Info);
            if (Application.isPlaying && targets.Length == 1)
            {
                var fight = (FightCombatController)target;
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.IntField("Wave", fight.CurrentWave);
                    EditorGUILayout.LongField("Wave Combat Beat", fight.WaveCombatBeatCount);
                    EditorGUILayout.TextField("Phase", fight.BattleEnded ? "ENDED" : fight.AwaitingBattleStart || fight.BattleEntranceRunning ? "ENTRY" :
                        fight.AwaitingNextWave ? "WAVE TRANSITION" : !fight.PressurePhaseEnabled ? "DISABLED" : fight.PressurePhaseActive ? "PRESSURE" : fight.PressureRestActive ? "REST" : fight.PressureWarningActive ? "WARNING" : "NORMAL");
                    if (fight.StageOvertimeActive)
                    {
                        EditorGUILayout.TextField("Remaining Beats", "UNLIMITED / 加時");
                        EditorGUILayout.LongField("Overtime Elapsed Beats", fight.OvertimeElapsedBeats);
                        EditorGUILayout.IntField("Acceleration Steps", fight.OvertimeAccelerationSteps);
                    }
                    else EditorGUILayout.LongField("Remaining Beats", fight.PressurePhaseRemainingBeats);
                    EditorGUILayout.LongField("Next Start Beat (-1 = none)", fight.PressureNextStartBeat);
                    EditorGUILayout.IntField("Recovery Beats", fight.PressureRecoveryBeats);
                    EditorGUILayout.Toggle("Clock Frozen", fight.IsPaused || fight.TimingCalibrationActive || fight.TeamSkillRunning ||
                        fight.EnemyActionsPaused || fight.WaveTransitionActive || fight.AwaitingBattleStart || fight.BattleEnded);
                }
            }
            else EditorGUILayout.HelpBox("Play 後顯示有效拍數、攻勢剩餘拍數與凍結狀態。現有未設定的 Wave 預設關閉。", MessageType.None);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
