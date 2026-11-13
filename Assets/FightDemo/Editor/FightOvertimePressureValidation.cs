using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightOvertimePressureValidation
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
        static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
        static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, Flags).Invoke(o, a);
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        static FightOvertimePressureValidation() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/OvertimePressureValidation.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(request); } catch (IOException) { return; }
            try { Validate(); File.WriteAllText("Temp/OvertimePressureValidation.result", "PASS: three goblins force overtime from disabled pressure/rest, no countdown/reduction/reward, MP x2, recovery 2/1/0 at 0/20/40 beats, queued cooldown reduction, no same-beat re-entry or overlapping attack at zero recovery, next eligible beat attacks, reset."); }
            catch (Exception e) { File.WriteAllText("Temp/OvertimePressureValidation.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Diagnostics/Validate Overtime Pressure")]
        public static void Validate()
        {
            foreach (string kind in new[] { "Normal", "Killer", "Giant" })
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                FightStageDefinition stage = null;
                try
                {
                    var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                    var fight = all.OfType<FightCombatController>().Single();
                    foreach (var sound in all.OfType<FightAudioPlayer>()) Set(sound, "soundEnabled", false);
                    stage = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<FightStageDefinition>("Assets/FightDemo/Levels/Stage01.asset"));
                    Set(stage, "useNormalEndDeadline", true); Set(stage, "overtimeOnlyOnFinalCombatWave", true);
                    Set(stage, "overtimeAccelerationIntervalBeats", 20); Set(stage, "overtimeRecoveryReductionBeats", 1); Set(stage, "overtimeMinimumRecoveryBeats", 0);
                    var enemyPrefab = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_" + kind + ".prefab");
                    Set(stage, "waves", new List<FightStageDefinition.Wave> { new() { front = enemyPrefab, pressure = new FightEnemyPressureSettings { enablePressurePhase = false, recoveryBeats = 2 } } });
                    Check(fight.RosterManager.TryConfigureStage(stage, out string error), error);
                    fight.RosterManager.SpawnConfiguredRoster();
                    Call(fight, "RebuildRosterAndResetCombat");
                    Set(fight, "beatClock", null); Set(fight, "waveEntrance", null);
                    Check(fight.TryBeginBattle(), "Battle start failed");
                    Set(fight, "pressureRestUntil", 100L); Set(fight, "teamSkillGauge", 3); Set(fight, "stageDeadlineMs", 1000);
                    Call(fight, "EvaluateStageDeadline", 1000);
                    Check(fight.StageOvertimeActive && fight.PressurePhaseActive && fight.PressurePhaseEnabled && !fight.PressureRestActive, kind + " did not force pressure");
                    Check(fight.OvertimeElapsedBeats == 0 && fight.PressureRecoveryBeats == 2 && fight.TeamSkillActivationBlocked, "Initial overtime state wrong");
                    var enemy = fight.RosterManager.ActiveEnemies[0];
                    Call(fight, "StartAuthoredEnemyTurns", 0L);
                    int hitCount = fight.ScheduledEnemyHitCount;
                    Check(hitCount > 0 && enemy.CombatAnimator.IsPlaying, kind + " did not attack immediately");
                    object cadence = ((IDictionary)Get(fight, "enemyCadences"))[enemy];
                    var readyField = cadence.GetType().GetField("pressureReady");
                    double initialReady = (double)readyField.GetValue(cadence);
                    float remaining = fight.PressureRemainingExact;
                    for (int beat = 1; beat <= 40; beat++)
                    {
                        Set(fight, "<NormalBattleBeatCount>k__BackingField", (long)beat);
                        Call(fight, "AdvanceWavePressureBeat");
                        if (beat == 19) Check(fight.PressureRecoveryBeats == 2, "Accelerated early");
                        if (beat == 20) Check(fight.PressureRecoveryBeats == 1 && (double)readyField.GetValue(cadence) == initialReady - 1, "Did not reduce queued recovery at beat 20");
                    }
                    Check(fight.PressureRecoveryBeats == 0 && fight.OvertimeAccelerationSteps == 2, "Recovery did not reach zero");
                    Call(fight, "RecordPressureDamage", enemy, 100f);
                    for (int i = 0; i < 100; i++) Call(fight, "RecordPressureBlock");
                    Call(fight, "EndPressurePhase");
                    Check(fight.PressureRemainingExact == remaining && fight.PressureBreakRevision == 0 && !fight.PressureRestActive && fight.TeamSkillGauge == 3, "Overtime reduced/ended/rewarded MP");
                    var input = new FmodRhythmJudge.Result(FmodRhythmJudge.Grade.Perfect, 0, 1000, 1000, new FmodBeatClock.NearestBeat(1000, 251, 1, 1000, 0), false, "Overtime");
                    Call(fight, "GainTeamSkill", input);
                    Check(fight.TeamSkillGauge == 5, "Overtime MP must be x2");
                    // Mature cooldown cannot restart a still-running animation.
                    Call(fight, "StartAuthoredEnemyTurns", 40L);
                    Check(fight.ScheduledEnemyHitCount == hitCount, "Overlapping attack at zero recovery");
                    ((IList)Get(fight, "enemyHits")).Clear(); enemy.CombatAnimator.InterruptPlayback();
                    Call(fight, "StartAuthoredEnemyTurns", 40L);
                    Check(fight.ScheduledEnemyHitCount == 0, "Repeated same-beat call restarted attack");
                    Set(fight, "<NormalBattleBeatCount>k__BackingField", 41L);
                    Call(fight, "StartAuthoredEnemyTurns", 41L);
                    Check(fight.ScheduledEnemyHitCount > 0 && enemy.CombatAnimator.IsPlaying, "Did not attack next eligible beat");
                    Call(fight, "ResetStageDeadline"); Call(fight, "ResetWavePressure");
                    Check(!fight.StageOvertimeActive && !fight.PressurePhaseActive && fight.OvertimeElapsedBeats == 0, "Overtime leaked after reset");
                }
                finally { if (stage != null) UnityEngine.Object.DestroyImmediate(stage); EditorSceneManager.ClosePreviewScene(scene); }
            }
        }
    }
}
