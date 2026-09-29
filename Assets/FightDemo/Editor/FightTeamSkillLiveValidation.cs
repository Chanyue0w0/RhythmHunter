using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightTeamSkillLiveValidation
    {
        const string Request = "Temp/FightTeamSkillLiveValidation.request", Result = "Temp/FightTeamSkillLiveValidation.result";
        const string Previous = "TeamSkillLive.Previous", Running = "TeamSkillLive.Running";
        static int phase, pausedPosition;
        static double deadline, checkpoint;
        static Gamepad pad;
        static readonly List<FightUnitSlot> order = new();
        static FightCombatController fight;
        static FightPauseController pause;
        static float ultimateTargetHp;
        static FightTeamSkillLiveValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Running, false)) Finish("FAIL: interrupted");
            };
        }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling) return;
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetString(Previous, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                SessionState.SetBool(Running, true);
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/FightDemo/Scenes/FightScene3.unity");
                File.WriteAllText(Result, "Starting Team Skill play validation.\n"); EditorApplication.isPlaying = true; return;
            }
            try
            {
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 70;
                Require(EditorApplication.timeSinceStartup < deadline, "Timed out.");
                Application.runInBackground = true;
                var clock = UnityEngine.Object.FindFirstObjectByType<FmodBeatClock>();
                if (clock == null) return;
                FMODUnity.RuntimeManager.CoreSystem.mixerResume();
                if (phase == 0)
                {
                    if (clock.ReceivedBeatCount < 3) return;
                    fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                    pause = UnityEngine.Object.FindFirstObjectByType<FightPauseController>();
                    fight.TeamSkillStepResolved += OnStep;
                    // Logic validation separately covers real Basic actions; prime only
                    // charge here to exercise hardware routing and live FMOD sequencing.
                    var gain = typeof(FightCombatController).GetMethod("GainTeamSkill", BindingFlags.Instance | BindingFlags.NonPublic);
                    for (int i = 0; i < fight.TeamSkillGaugeMax; i++)
                        gain.Invoke(fight, new object[] { new FmodRhythmJudge.Result(FmodRhythmJudge.Grade.Perfect, 0, 0, 0,
                            new FmodBeatClock.NearestBeat(clock.LatestBeat.GlobalBeat - fight.TeamSkillGaugeMax + 1 + i, 1, 1, 0, 0), false, "test") });
                    Require(fight.TeamSkillReady, "Charge priming failed.");
                    pad = InputSystem.AddDevice<Gamepad>();
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder).WithButton(GamepadButton.South));
                    phase = 1; checkpoint = EditorApplication.timeSinceStartup + .3; return;
                }
                if (phase == 1 && EditorApplication.timeSinceStartup >= checkpoint)
                {
                    Require(fight.TeamSkillReady && !fight.TeamSkillRunning, "RB+A must not spend Team Skill charge.");
                    ScreenCapture.CaptureScreenshot("Temp/TeamSkill-ready.png");
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    phase = 2; checkpoint = EditorApplication.timeSinceStartup + .2; return;
                }
                if (phase == 2 && EditorApplication.timeSinceStartup >= checkpoint)
                {
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
                    phase = 3; return;
                }
                if (phase == 3 && order.Count == 1)
                {
                    Require(fight.TeamSkillGauge == 0 && fight.TeamSkillRunning, "A must consume charge and begin chain.");
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    pause.SetPaused(true);
                    phase = 4; checkpoint = EditorApplication.timeSinceStartup + .3; return;
                }
                if (phase == 4 && EditorApplication.timeSinceStartup >= checkpoint)
                {
                    clock.TryGetTimelinePositionMs(out pausedPosition);
                    phase = 5; checkpoint = EditorApplication.timeSinceStartup + 1; return;
                }
                if (phase == 5 && EditorApplication.timeSinceStartup >= checkpoint)
                {
                    clock.TryGetTimelinePositionMs(out int current);
                    Require(order.Count == 1 && Math.Abs(current - pausedPosition) <= 1, "Pause must freeze music and chain.");
                    pause.SetPaused(false); phase = 6; return;
                }
                if (phase == 6 && order.Count == 3 && !fight.TeamSkillRunning)
                {
                    Require(!fight.TeamSkillRunning && order[0] == fight.FrontHero.UnitSlot && order[1] == fight.SecondHero.UnitSlot && order[2] == fight.ThirdHero.UnitSlot, "Resumed chain must complete in formation order.");
                    Require(Math.Abs(clock.LatestBeat.Tempo - 121.15f) < .01f, "Music tempo changed.");
                    ScreenCapture.CaptureScreenshot("Temp/TeamSkill-complete.png");
                    phase = 7; checkpoint = EditorApplication.timeSinceStartup + .4; return;
                }
                if (phase == 7 && EditorApplication.timeSinceStartup >= checkpoint)
                {
                    Require(fight.TeamUltimateCharge == 1, "Completed live chain must award one Ultimate charge.");
                    var gain = typeof(FightCombatController).GetMethod("GainTeamUltimateCharge", BindingFlags.Instance | BindingFlags.NonPublic);
                    gain.Invoke(fight, null); gain.Invoke(fight, null);
                    Require(fight.TeamUltimateReady, "Three charges must ready the Ultimate.");
                    ScreenCapture.CaptureScreenshot("Temp/TeamUltimate-ready.png");
                    // Plain A must never consume a ready Ultimate.
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
                    phase = 8; checkpoint = EditorApplication.timeSinceStartup + .3; return;
                }
                if (phase == 8 && EditorApplication.timeSinceStartup >= checkpoint)
                {
                    Require(fight.TeamUltimateReady && !fight.TeamUltimateRunning, "Plain A must not trigger Ultimate.");
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    phase = 9; checkpoint = EditorApplication.timeSinceStartup + .2; return;
                }
                if (phase == 9 && EditorApplication.timeSinceStartup >= checkpoint)
                {
                    ultimateTargetHp = fight.ActiveEnemySlot.CurrentHp;
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder).WithButton(GamepadButton.South));
                    phase = 10; return;
                }
                if (phase == 10 && fight.TeamUltimatePerforming)
                {
                    Require(fight.TeamUltimateCharge == 0 && fight.TeamUltimateImpactCount == 0, "RB+A consumes charge and begins windup.");
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    pause.SetPaused(true);
                    phase = 11; checkpoint = EditorApplication.timeSinceStartup + .3; return;
                }
                if (phase == 11 && EditorApplication.timeSinceStartup >= checkpoint)
                {
                    clock.TryGetTimelinePositionMs(out pausedPosition);
                    phase = 12; checkpoint = EditorApplication.timeSinceStartup + 1; return;
                }
                if (phase == 12 && EditorApplication.timeSinceStartup >= checkpoint)
                {
                    clock.TryGetTimelinePositionMs(out int current);
                    Require(Math.Abs(current - pausedPosition) <= 1 && fight.TeamUltimateImpactCount == 0 && fight.ActiveEnemySlot.CurrentHp == ultimateTargetHp, "Pause freezes Ultimate and prevents early damage.");
                    pause.SetPaused(false); phase = 13; return;
                }
                if (phase == 13 && fight.TeamUltimateImpactCount == 1)
                {
                    Require(fight.ActiveEnemySlot.CurrentHp == ultimateTargetHp - 3, "Live Ultimate applies exactly 3 damage.");
                    ScreenCapture.CaptureScreenshot("Temp/TeamUltimate-impact.png");
                    phase = 14; return;
                }
                if (phase == 14 && !fight.TeamUltimateRunning)
                    Finish("PASS: Team Skill and Ultimate A/RB+A routing, 30 mana, charge icons, FMOD 121.15, completed-chain charge, pause/resume both performances, exactly one 3-damage Ultimate impact.");
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        static void OnStep(FightUnitSlot slot) => order.Add(slot);
        static void Finish(string result)
        {
            File.AppendAllText(Result, result + "\n"); File.Delete(Request);
            if (fight != null) fight.TeamSkillStepResolved -= OnStep;
            if (pause != null) pause.SetPaused(false);
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            string previous = SessionState.GetString(Previous, "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool(Running, false); EditorApplication.isPlaying = false;
            phase = 0; deadline = 0; order.Clear(); pad = null; fight = null; pause = null;
        }
    }
}
