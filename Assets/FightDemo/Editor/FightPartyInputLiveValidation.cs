using System;
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
    public static class FightPartyInputLiveValidation
    {
        const string Request = "Temp/FightPartyInputLiveValidation.request", Result = "Temp/FightPartyInputLiveValidation.result";
        const string Running = "PartyInputLive.Running", Previous = "PartyInputLive.Previous";
        static FightCombatController fight;
        static FmodBeatClock clock;
        static FmodRhythmJudge judge;
        static Keyboard keyboard;
        static Gamepad pad;
        static bool originalBackground, sent, received;
        static int step;
        static long targetBeat;
        static double deadline;
        static float initialEnemyHp;
        static readonly int[] Positions = { 0, 1, 2, 2, 2, 0, 1, 2, 2, 2 };
        static readonly double[] Offsets = { -60, 60, -60, 60, -60, 60, -60, 60, -60, 60 };
        static FightPartyInputLiveValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Running, false)) Finish("FAIL: interrupted");
            };
        }
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling) return;
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetString(Previous, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                SessionState.SetBool(Running, true);
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/FightDemo/Scenes/FightScene3.unity");
                File.WriteAllText(Result, "Starting live keyboard/gamepad timing validation.\n");
                EditorApplication.isPlaying = true; return;
            }
            try
            {
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 45;
                Check(EditorApplication.timeSinceStartup < deadline, "Timed out waiting for live music/input.");
                if (fight == null)
                {
                    clock = UnityEngine.Object.FindFirstObjectByType<FmodBeatClock>();
                    if (clock == null || clock.ReceivedBeatCount < 3) return;
                    fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                    judge = UnityEngine.Object.FindFirstObjectByType<FmodRhythmJudge>();
                    originalBackground = Application.runInBackground; Application.runInBackground = true;
                    Check(fight.TryBeginBattle(), "Explicit start required.");
                    foreach (var enemy in fight.RosterManager.ActiveEnemies)
                    {
                        enemy.CharacterDefinition.BossPattern.enabled = false;
                        Set(enemy, "maxHp", 100f); Set(enemy, "attackIntervalBeats", 10000); enemy.RestoreFullHealth();
                    }
                    initialEnemyHp = fight.ActiveEnemySlot.CurrentHp;
                    keyboard = InputSystem.AddDevice<Keyboard>(); pad = InputSystem.AddDevice<Gamepad>();
                    fight.HeroCalled += OnInput;
                    targetBeat = clock.LatestBeat.GlobalBeat + 2;
                }
                FMODUnity.RuntimeManager.CoreSystem.mixerResume();
                if (step >= Positions.Length)
                {
                    Check(fight.TeamSkillGauge == 10 && fight.ActiveEnemySlot.CurrentHp == initialEnemyHp - 4,
                        "Ten successful inputs grant 10 MP and two swords plus two three-beat shots deal four damage.");
                    Check(fight.GetAimedShotProgress(fight.ThirdHero.UnitSlot) == 0, "Both shooter cycles finish and reset.");
                    ScreenCapture.CaptureScreenshot("Temp/PartyInput-live.png");
                    Finish("PASS: real FMOD clock, simulated keyboard Q/W/E and gamepad X/Y/B, early/late inputs around +/-60 ms, two three-beat shots, 10 MP independent of hit count, expected damage. Saved calibration unchanged; physical-device latency and human audio alignment not verified.");
                    return;
                }
                if (received)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(pad, new GamepadState());
                    received = sent = false; step++; targetBeat++; return;
                }
                if (sent) return;
                if (!clock.TryGetTimelinePositionMs(out int ms)) return;
                double targetMs = clock.LatestBeat.TimelinePositionMs + (targetBeat - clock.LatestBeat.GlobalBeat) * clock.MillisecondsPerBeat;
                double delta = ms - targetMs;
                if (delta < Offsets[step]) return;
                Check(delta < Offsets[step] + 60, "Editor stalled past the intended input window.");
                judge.SetInputProfile(step < 5 ? "Keyboard" : "Gamepad"); judge.SetPersonalDelay(0, false);
                sent = true;
                int position = Positions[step];
                if (step < 5) InputSystem.QueueStateEvent(keyboard, new KeyboardState(position == 0 ? Key.Q : position == 1 ? Key.W : Key.E));
                else InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(position == 0 ? GamepadButton.West : position == 1 ? GamepadButton.North : GamepadButton.East));
            }
            catch (Exception exception) { Finish("FAIL: " + exception); }
        }
        static void OnInput(FightCombatController.HeroCallResult call)
        {
            if (!sent || received) return;
            try
            {
                Check((int)call.Command == Positions[step], "Input maps to the expected formation slot.");
                Check(call.RhythmResult.Judgement == FmodRhythmJudge.Grade.Perfect && !call.RhythmResult.DuplicateBeat,
                    $"Live input rejected at step {step}: {call.RhythmResult.DeltaMs:0.0} ms.");
                Check(call.RhythmResult.NearestBeat.GlobalBeat == targetBeat, "Early/late input belongs to the intended beat.");
                File.AppendAllText(Result, $"{(step < 5 ? "Keyboard" : "Gamepad")} slot {Positions[step]} beat {targetBeat}: {call.RhythmResult.DeltaMs:0.0} ms\n");
                received = true;
            }
            catch (Exception exception) { Finish("FAIL: " + exception); }
        }
        static void Finish(string result)
        {
            File.AppendAllText(Result, result + "\n"); File.Delete(Request);
            if (fight != null) { fight.HeroCalled -= OnInput; Application.runInBackground = originalBackground; }
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            string previous = SessionState.GetString(Previous, "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool(Running, false); EditorApplication.isPlaying = false;
            fight = null; keyboard = null; pad = null; step = 0; deadline = 0; sent = received = false;
        }
    }
}
