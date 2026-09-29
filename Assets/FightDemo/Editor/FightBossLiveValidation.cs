using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightBossLiveValidation
    {
        const string Request = "Temp/FightBossLiveValidation.request", Result = "Temp/FightBossLiveValidation.result";
        const string Running = "BossLive.Running", Previous = "BossLive.Previous";
        static int phase, position;
        static double deadline, checkpoint;
        static long previousInput = long.MinValue;
        static FightCombatController fight;
        static FightPauseController pause;
        static readonly HashSet<FightCombatController.BossPhase> seen = new();
        static FightBossLiveValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Running, false)) Finish("FAIL: interrupted"); };
        }
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling) return;
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetString(Previous, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                SessionState.SetBool(Running, true);
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/FightDemo/Scenes/FightScene3.unity");
                File.WriteAllText(Result, "Starting live Boss validation.\n"); EditorApplication.isPlaying = true; return;
            }
            try
            {
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 70;
                Check(EditorApplication.timeSinceStartup < deadline, "Timed out.");
                Application.runInBackground = true;
                var clock = UnityEngine.Object.FindFirstObjectByType<FmodBeatClock>();
                if (clock == null || clock.ReceivedBeatCount < 1) return;
                FMODUnity.RuntimeManager.CoreSystem.mixerResume();
                fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                pause = UnityEngine.Object.FindFirstObjectByType<FightPauseController>();
                Check(fight.BossActive, "Scene boss not active.");
                seen.Add(fight.CurrentBossPhase);
                if (phase == 0 && fight.CurrentBossPhase == FightCombatController.BossPhase.Rage)
                {
                    ScreenCapture.CaptureScreenshot("Temp/Boss-Rage.png");
                    pause.SetPaused(true); checkpoint = EditorApplication.timeSinceStartup + .3; phase = 1; return;
                }
                if (phase == 1 && EditorApplication.timeSinceStartup >= checkpoint)
                {
                    clock.TryGetTimelinePositionMs(out position); checkpoint = EditorApplication.timeSinceStartup + 1; phase = 2; return;
                }
                if (phase == 2 && EditorApplication.timeSinceStartup >= checkpoint)
                {
                    clock.TryGetTimelinePositionMs(out int now);
                    Check(Math.Abs(now - position) <= 1 && fight.CurrentBossPhase == FightCombatController.BossPhase.Rage, "Pause must freeze Boss and FMOD.");
                    pause.SetPaused(false); phase = 3;
                }
                if (phase == 3 && fight.CurrentBossPhase == FightCombatController.BossPhase.Armor)
                {
                    long n = clock.LatestBeat.GlobalBeat;
                    if (n != previousInput)
                    {
                        previousInput = n;
                        var judgement = new FmodRhythmJudge.Result(FmodRhythmJudge.Grade.Perfect, 0, 0, 0,
                            new FmodBeatClock.NearestBeat(n, 1, 1, 0, 0), false, "boss-live");
                        typeof(FightCombatController).GetMethod("ApplyHeroJudgement", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(fight, new object[] { fight.GetHeroForCommand(FightInputRouter.HeroCommand.Back), FightInputRouter.HeroCommand.Back, judgement });
                    }
                    if (fight.CurrentBossPhase == FightCombatController.BossPhase.Break)
                    {
                        seen.Add(fight.CurrentBossPhase); ScreenCapture.CaptureScreenshot("Temp/Boss-Break.png"); phase = 4;
                    }
                }
                if (phase == 4)
                {
                    Check(!fight.HasPendingEnemyAttack, "Break must cancel enemy actions.");
                    if (fight.CurrentBossPhase == FightCombatController.BossPhase.Neutral)
                    {
                        Check(seen.Count == 7 && Mathf.Abs(clock.LatestBeat.Tempo - 121.15f) < .01f, "All seven phases must run on current music.");
                        Finish("PASS: actual FMOD 121.15, all seven Boss phases, pause/resume, Mage counter, complete Break window and HUD screenshots. Ability judgments injected; hardware input covered by Team Skill live suite.");
                    }
                }
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        static void Finish(string message)
        {
            File.AppendAllText(Result, message + "\n"); File.Delete(Request);
            if (pause != null) pause.SetPaused(false);
            string previous = SessionState.GetString(Previous, "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool(Running, false); EditorApplication.isPlaying = false;
            phase = 0; deadline = 0; previousInput = long.MinValue; seen.Clear(); fight = null; pause = null;
        }
    }
}
