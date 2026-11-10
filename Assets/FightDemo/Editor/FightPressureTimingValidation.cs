using System;
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
    public static class FightPressureTimingValidation
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
        static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static FightPressureTimingValidation() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string path = "Temp/PressureTimingValidation.request";
            if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(path); } catch (IOException) { return; }
            try { Run(); }
            catch (Exception e) { File.WriteAllText("Temp/PressureTimingValidation.result", "FAIL: " + e); Debug.LogException(e); }
        }

        [MenuItem("Rhythm Hunter/Levels/Validate Pressure Timing (Checkpoint 1)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before running the preview-scene validation.");
            ValidateWindows();
            ValidateBattle();
            const string result = "PASS: 19/20/39/40/59/60 boundaries; one-shot/repeat/disabled/invalid data; stage snapshot; ordinary clock deduplication; preparation/entry/pause/calibration/TeamSkill freeze; stun does not freeze group; real Wave transition/reset; victory/defeat/restart.";
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/PressureTimingValidation.result", result);
            Debug.Log(result);
        }

        public static void RunBatch()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch (Exception e)
            {
                File.WriteAllText("Temp/PressureTimingValidation.result", "FAIL: " + e);
                Debug.LogException(e); EditorApplication.Exit(1);
            }
        }

        static void ValidateWindows()
        {
            var p = new FightEnemyPressureSettings { enablePressurePhase = true };
            Check(p.Validate(out _), "Default pressure configuration invalid");
            foreach (long beat in new long[] { 0, 1, 19, 40, 59, 80, 99 })
                Check(!p.IsActiveAt(beat), "Pressure active outside window at " + beat);
            Check(p.RemainingAt(20) == 20 && p.RemainingAt(39) == 1 && p.RemainingAt(60) == 20 && p.RemainingAt(100) == 20, "Boundary length/repeat drift");
            Check(p.NextStartAt(0) == 20 && p.NextStartAt(40) == 60, "Next start wrong");
            p.repeat = false;
            Check(p.IsActiveAt(20) && !p.IsActiveAt(60) && p.NextStartAt(40) == -1, "One shot repeated");
            p.enablePressurePhase = false;
            Check(!p.IsActiveAt(20) && p.NextStartAt(0) == -1, "Disabled window active");
            p.enablePressurePhase = p.repeat = true; p.repeatEveryBeats = 19;
            Check(!p.Validate(out _) && !p.IsActiveAt(20), "Overlapping intervals accepted");
            p.repeatEveryBeats = 0;
            Check(!p.Validate(out _) && p.RemainingAt(100) == 0, "Invalid repeat causes divide by zero");
            p.repeatEveryBeats = 40; p.startBeat = 0;
            Check(!p.Validate(out _), "Invalid start accepted");
        }

        static void ValidateBattle()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            FightStageDefinition stage = null;
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                var roster = fight.RosterManager;
                var clock = fight.GetComponent<FmodBeatClock>();
                var entrance = fight.GetComponent<FightWaveEntrance>();
                stage = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<FightStageDefinition>("Assets/FightDemo/Levels/Stage01.asset"));
                var waves = (List<FightStageDefinition.Wave>)typeof(FightStageDefinition).GetField("waves", Flags).GetValue(stage);
                waves[0].pressure = new FightEnemyPressureSettings { enablePressurePhase = true };
                waves[1].pressure = new FightEnemyPressureSettings { enablePressurePhase = false };
                Check(roster.TryConfigureStage(stage, out var error), error);
                waves[0].pressure.startBeat = 5;
                var copy = roster.GetWavePressureSettings(0); copy.startBeat = 3;
                Check(roster.GetWavePressureSettings(0).startBeat == 20, "Live assets/caller mutated active stage snapshot");
                roster.SpawnConfiguredRoster();
                Call(fight, "RebuildRosterAndResetCombat");
                Set(fight, "beatClock", null); Set(fight, "waveEntrance", null);
                long global = 0;
                void Beat() => Call(fight, "AdvanceNormalBattleBeat", new FmodBeatClock.BeatSnapshot(++global, 1, 1, (int)global * 500, 120, 4, 4));
                Beat(); Check(fight.WaveCombatBeatCount == 0, "Preparation consumed beat");
                Check(fight.TryBeginBattle(), "Could not begin test battle");
                for (int i = 0; i < 19; i++) Beat();
                Check(fight.WaveCombatBeatCount == 19 && !fight.PressurePhaseActive, "Started early");
                Beat(); Check(fight.PressurePhaseActive && fight.PressurePhaseRemainingBeats == 20, "Beat 20 missing");
                Call(fight, "AdvanceNormalBattleBeat", new FmodBeatClock.BeatSnapshot(global, 1, 1, (int)global * 500, 120, 4, 4));
                Check(fight.WaveCombatBeatCount == 20, "Duplicate beat advanced clock");
                Set(fight, "nextTeamSkillBeat", 0L); Beat();
                Check(fight.WaveCombatBeatCount == 20 && fight.PressurePhaseActive, "TeamSkill consumed pressure time");
                Set(fight, "nextTeamSkillBeat", long.MaxValue);
                Call(fight, "BeginEnemyPause"); Beat(); Call(fight, "EndEnemyPause", true);
                Check(fight.WaveCombatBeatCount == 20, "Enemy pause consumed beat");
                Set(fight, "<TimingCalibrationActive>k__BackingField", true); Beat();
                Set(fight, "<TimingCalibrationActive>k__BackingField", false);
                Set(fight, "beatClock", clock); Set(clock, "paused", true); Beat();
                Set(clock, "paused", false); Set(fight, "beatClock", null);
                Set(fight, "waveEntrance", entrance); Set(entrance, "running", true); Beat();
                Set(entrance, "running", false); Set(fight, "waveEntrance", null);
                Check(fight.WaveCombatBeatCount == 20, "Calibration/pause/entry consumed beat");
                roster.ActiveEnemies[0].ApplyStun(4);
                Beat(); Check(fight.WaveCombatBeatCount == 21, "Individual stun froze group pressure");
                while (fight.WaveCombatBeatCount < 39) Beat();
                Check(fight.PressurePhaseRemainingBeats == 1, "Last active beat incorrect");
                Beat(); Check(!fight.PressurePhaseActive && fight.PressureNextStartBeat == 60, "Beat 40 did not end");
                while (fight.WaveCombatBeatCount < 60) Beat();
                Check(fight.PressurePhaseActive && fight.PressurePhaseRemainingBeats == 20, "Second window did not start");
                foreach (var enemy in roster.ActiveEnemies) enemy.TakeDamage(1000);
                Call(fight, "CheckWaveCompletion");
                Check(fight.AwaitingNextWave && !fight.PressurePhaseActive, "Wave clear retained pressure");
                Beat(); Check(fight.WaveCombatBeatCount == 60, "Inter-wave wait consumed beat");
                Set(fight, "latestCombatBeat", global + 1); Call(fight, "AdvanceWaveBoundary");
                Set(fight, "latestCombatBeat", global + 2); Call(fight, "AdvanceWaveBoundary");
                Check(fight.CurrentWave == 2 && fight.WaveCombatBeatCount == 0 && !fight.PressurePhaseEnabled, "Wave change did not reset/apply second configuration");
                Beat(); Check(fight.WaveCombatBeatCount == 1, "Wave 2 did not begin its own count");
                Call(fight, "FinishBattle", true); Beat();
                Check(!fight.PressurePhaseActive && fight.WaveCombatBeatCount == 1, "Victory clock still running");
                Call(fight, "ResetBattleStateForRoster");
                Check(fight.CurrentWave == 1 && fight.WaveCombatBeatCount == 0 && fight.PressurePhaseEnabled, "Restart/reset not cleared");
                Beat(); Call(fight, "FinishBattle", false); Beat();
                Check(fight.WaveCombatBeatCount == 1 && !fight.PressurePhaseActive, "Defeat clock still running");
            }
            finally
            {
                if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
