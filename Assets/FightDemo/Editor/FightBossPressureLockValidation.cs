using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FMOD.Studio;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightBossPressureLockValidation
    {
        const string Request = "Temp/BossPressureLock.request";
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        static double deadline, waitUntil;
        static int step;
        static FightUnitSlot boss;
        static FightBossPressureLockValidation() => EditorApplication.update += Tick;
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlaying)
            { if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true; return; }
            try
            {
                Application.runInBackground = true;
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 120;
                Check(EditorApplication.timeSinceStartup < deadline, "Timeout step " + step);
                var fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                if (!fight || fight.AwaitingBattleStart || fight.EnemyActionsPaused) return;
                boss = fight.RosterManager.ActiveEnemies.FirstOrDefault(x => x && x.BossPhaseOneProtected);
                if (!boss || boss.IsTransitionInvulnerable) return;
                var clock = fight.BeatClock;
                if (step == 0)
                {
                    Set(fight, "endBattleOnZeroHp", false);
                    boss.RestoreFullHealth();
                    fight.InterruptEnemyAttack(boss);
                    Set(fight, "pressureRemaining", 0d);
                    Set(fight, "pressureRestUntil", fight.WaveCombatBeatCount + 2);
                    Call(boss, "RefreshBossRestPresentation");
                    var tired = boss.CombatAnimator.FindSequence(boss.CharacterDefinition.TiredIdleAnimationName);
                    Check(tired != null && boss.CombatAnimator.DisplayedFrame.Source == (int)tired.Animation, "Uncleared Boss rest did not show Tired");
                    Set(fight, "pressureRestUntil", 0L); Call(boss, "RefreshBossRestPresentation");
                    Check(boss.CombatAnimator.DisplayedFrame.Source != (int)tired.Animation, "Uncleared Boss did not leave Tired after rest");
                    Set(fight, "pressureRemaining", 12d); Set(fight, "pressureDamageProgress", 0f);
                    Set(boss, "currentHp", FightCombatUnits.BossHpFloor + 1);
                    foreach (var ability in boss.CharacterDefinition.EnemyAbilities) ability.selectionWeight = ability.name == "Strike" ? 1 : 0;
                    Check((bool)Call(fight, "TryStartAuthoredEnemyAttack", boss), "Could not start Pressure attack");
                    int hits = fight.ScheduledEnemyHitCount;
                    var animation = boss.CombatAnimator.ActiveAnimation;
                    Check(boss.TakeDamage(1) == 1 && boss.CurrentHp == FightCombatUnits.BossHpFloor && boss.BossPhaseOneTired, "HP floor not reached");
                    Check(boss.BossFinishingCurrentPressure && !boss.BossPhaseOneActionsStopped && fight.PressureRemainingExact == 12, "Lock interrupted Pressure");
                    Check(boss.CombatAnimator.IsPlaying && boss.CombatAnimator.ActiveAnimation == animation && fight.ScheduledEnemyHitCount == hits, "Lock interrupted active attack");
                    boss.TakeDamage(1);
                    Check(boss.CombatAnimator.ActiveAnimation == animation && boss.Heal(30) == 0, "Locked boss reacted or healed");
                    Check(!boss.PlayCombatAnimation(FightCharacterCombatAnimator.CombatAnimation.Hit, null, null, null), "Locked boss accepted Hit");
                    fight.InterruptEnemyAttack(boss);
                    Check((bool)Call(fight, "TryStartAuthoredEnemyAttack", boss), "Locked boss cannot continue this Pressure's attacks");
                    Call(fight, "UpdateBossMusicStage");
                    Check(!fight.BossPhaseTwoActive && clock.OvertimeMusicEnabled, "Finite Pressure disarmed/transitioned music early");
                    clock.TryGetMarkerPosition("Normal_End", out int end);
                    var music = (EventInstance)typeof(FmodBeatClock).GetField("musicInstance", Flags).GetValue(clock);
                    music.setTimelinePosition(end - 150); music.getSystem(out var system); system.flushCommands();
                    waitUntil = EditorApplication.timeSinceStartup + 1.1; step = 1;
                }
                else if (step == 1 && EditorApplication.timeSinceStartup >= waitUntil)
                {
                    Check(fight.PressurePhaseActive && !fight.BossPhaseTwoActive && !fight.StageOvertimeActive, "Music endpoint cut active finite Pressure");
                    Call(fight, "EndPressurePhase"); Call(boss, "RefreshBossRestPresentation");
                    Check(fight.PressureRestActive && boss.BossPhaseOneActionsStopped && !boss.BossFinishingCurrentPressure, "Pressure did not finish into rest");
                    Check(!boss.CombatAnimator.IsPlaying && fight.ScheduledEnemyHitCount == 0, "Rest retained pending attack");
                    Check(boss.CombatAnimator.DisplayedFrame.Source == (int)boss.CombatAnimator.FindSequence(boss.CharacterDefinition.TiredIdleAnimationName).Animation, "Locked rest did not show Tired");
                    boss.TakeDamage(1);
                    Check(!boss.CombatAnimator.IsPlaying && !(bool)Call(fight, "TryStartAuthoredEnemyAttack", boss), "Locked rest restarted Hit or attack");
                    Check(fight.PressureNextStartBeat == -1, "Locked boss scheduled another Pressure");
                    boss.RestoreFullHealth(); Set(fight, "stageOvertimeActive", true); Set(boss, "currentHp", FightCombatUnits.BossHpFloor + 1);
                    boss.TakeDamage(1);
                    Check(!boss.BossFinishingCurrentPressure && boss.BossPhaseOneActionsStopped, "Overtime incorrectly waits for endless Pressure");
                    Call(fight, "UpdateBossMusicStage"); step = 2;
                }
                else if (step == 2 && fight.BossPhaseTwoActive)
                {
                    File.WriteAllText("Temp/BossPressureLock.result", "PASS: finite Pressure survives HP lock and native music endpoint; attacks continue; no healing/Hit on lock; rest uses Tired before and after lock; no new Pressure after success; overtime lock immediately enters phase two.");
                    Finish();
                }
            }
            catch (Exception e) { File.WriteAllText("Temp/BossPressureLock.result", "FAIL: " + e); Finish(); }
        }
        static void Finish()
        { File.Delete(Request); deadline = waitUntil = 0; step = 0; boss = null; EditorApplication.isPlaying = false; }
    }
}
