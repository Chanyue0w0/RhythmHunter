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
    internal static class FightBossMusicStageValidation
    {
        const string Request = "Temp/BossMusicStageValidation.request", Result = "Temp/BossMusicStageValidation.result";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static double deadline;
        static int step, phaseEntries;
        static string mode;
        static FightUnitSlot boss;
        static float hp;
        static string progress;
        static FightBossMusicStageValidation() => EditorApplication.update += Tick;
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void SeekForTest(FmodBeatClock clock, int position)
        {
            var instance = (EventInstance)typeof(FmodBeatClock).GetField("musicInstance", Flags).GetValue(clock);
            Check(instance.setTimelinePosition(position) == FMOD.RESULT.OK, "Test seek failed");
            instance.getSystem(out var system); system.flushCommands();
        }
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlaying)
            { if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true; return; }
            try
            {
                Application.runInBackground = true;
                if (deadline == 0) { deadline = EditorApplication.timeSinceStartup + 100; mode = File.ReadAllText(Request).Trim(); }
                Check(EditorApplication.timeSinceStartup < deadline, "Boss music stage timed out: " + progress);
                var fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                var clock = UnityEngine.Object.FindFirstObjectByType<FmodBeatClock>();
                if (fight)
                {
                    // Isolate tests from user preview flags; never save these runtime-only changes.
                    var debug = fight.GetComponent<FightBossStageDebug>();
                    if (debug)
                    {
                        typeof(FightBossStageDebug).GetField("completePhaseOneOnStart", Flags).SetValue(debug, false);
                        typeof(FightBossStageDebug).GetField("skipToPhaseTwoOnStart", Flags).SetValue(debug, false);
                    }
                }
                if (fight && clock)
                {
                    clock.TryGetRawTimelinePositionMs(out int rawNow);
                    progress = $"step={step}, raw={rawNow}, phase={fight.CurrentBossMusicPhase}, paused={fight.IsPaused}, playing={clock.IsPlaying}, entering={fight.AwaitingBattleStart}, seek={clock.BossSectionJumpInProgress}, loops={clock.CompletedMusicLoops}";
                    File.WriteAllText("Temp/BossMusicStageValidation.progress", progress);
                }
                if (!fight || !clock || fight.AwaitingBattleStart) return;
                Check(fight.IsBossMusicStage && !fight.StageDeadlineEnabled, "Boss used ordinary deadline");
                Check(clock.TryGetMarkerPosition("Normal_End", out int normalEnd) && clock.TryGetMarkerPosition("Boss_End", out _), "Markers missing");
                if (step == 0)
                {
                    boss = fight.RosterManager.ActiveEnemies.First(x => x.BossPhaseOneProtected);
                    hp = fight.PartyHp;
                    fight.BossPhaseTwoStarted += () => phaseEntries++;
                    if (mode == "early")
                    {
                        Check(boss.DebugCompleteBossPhaseOne(), "Could not complete phase one");
                        typeof(FightCombatController).GetMethod("UpdateBossMusicStage", Flags).Invoke(fight, null);
                        Check(!fight.BossPhaseTwoActive && !clock.OvertimeMusicEnabled, "Early clear must wait, with loop disabled");
                        SeekForTest(clock, normalEnd - 600);
                    }
                    else if (mode == "debug") fight.GetComponent<FightBossStageDebug>().RequestPhaseTwoSkip();
                    else { Check(clock.OvertimeMusicEnabled, "Boss loop not prearmed"); SeekForTest(clock, normalEnd - 600); }
                    step = mode == "early" || mode == "debug" ? 2 : 1;
                }
                else if (step == 1 && fight.StageOvertimeActive)
                {
                    Check(fight.PressurePhaseActive && !fight.BossPhaseTwoActive, "Boss overtime missing endless Pressure");
                    Check(boss.DebugCompleteBossPhaseOne(), "Overtime clear failed");
                    step = 2;
                }
                else if (step == 2 && fight.BossPhaseTwoActive && !clock.BossSectionJumpInProgress)
                {
                    Check(!fight.StageOvertimeActive && !fight.PressureVisualsActive, "Pressure leaked into phase two");
                    Check(boss.CurrentHp == 1 && boss.BossPhaseOneTired, "Boss lost protection during music preview");
                    Check(!fight.HasPendingEnemyAttack && fight.ScheduledEnemyHitCount == 0, "Phase-one hits survived transition");
                    Check(fight.TeamSkillActivationBlocked && phaseEntries == 1, "Phase transition duplicate or Fever unlocked");
                    Check(clock.TryGetRawTimelinePositionMs(out int raw) && raw >= normalEnd, "Music did not enter phase two");
                    if (mode != "overtime") Check(Math.Abs(fight.PartyHp - hp) < .001f, "Transition changed party HP");
                    clock.TryGetMarkerPosition("Boss_End", out int end);
                    SeekForTest(clock, end - 250);
                    step = 3;
                }
                else if (step == 3 && fight.CurrentBossMusicPhase == FightCombatController.BossMusicPhase.PhaseTwoMusicEnded)
                {
                    Check(phaseEntries == 1 && fight.BossPhaseTwoSecondsRemaining == 0, "End state/countdown wrong");
                    File.WriteAllText(Result, "PASS: " + mode + " actual Play scene; Boss deadline isolation, native music transition, Pressure cleanup, pending attack cleanup, HP floor, one transition, Boss_End signal. Phase-two gameplay intentionally pending.");
                    Finish();
                }
            }
            catch (Exception e) { File.WriteAllText(Result, "FAIL: " + e); Finish(); }
        }
        static void Finish()
        {
            File.Delete(Request); deadline = 0; step = phaseEntries = 0; boss = null;
            EditorApplication.isPlaying = false;
        }
    }
}
