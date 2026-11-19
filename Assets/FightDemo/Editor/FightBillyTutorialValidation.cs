using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace RhythmHunter.FightDemo.Editor
{
    [InitializeOnLoad]
    static class FightBillyTutorialValidation
    {
        const string Request = "Temp/BillyTutorialPlay.request", Original = "BillyTutorial.OriginalStage";
        static double deadline, pageAt, captureAt;
        static long lastInput = long.MinValue, skipBeat = long.MinValue, refreshStart;
        static int lastStep = -1;
        static bool skipTested, expiredTested, reacquired, reloading;
        static FightBillyTutorialValidation()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || SessionState.GetInt(Original, -1) < 0) return;
                var planner = UnityEngine.Object.FindFirstObjectByType<FightLevelPlanner>();
                if (planner) { var so = new SerializedObject(planner); so.FindProperty("stageIndex").intValue = SessionState.GetInt(Original, 0); so.ApplyModifiedPropertiesWithoutUndo(); }
                SessionState.EraseInt(Original);
            };
        }
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (!EditorApplication.isPlaying)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    var planner = UnityEngine.Object.FindFirstObjectByType<FightLevelPlanner>();
                    Check(planner && planner.Chapter.Stages.Count >= 4, "Open Chapter 1");
                    SessionState.SetInt(Original, planner.StageIndex);
                    var so = new SerializedObject(planner); so.FindProperty("stageIndex").intValue = 2; so.ApplyModifiedPropertiesWithoutUndo();
                    EditorApplication.isPlaying = true; return;
                }
                Application.runInBackground = true;
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 120;
                Check(EditorApplication.timeSinceStartup < deadline, "Timeout at " + lastStep);
                var fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                if (!fight) return;
                var level = fight.GetComponent<FightLevelPlanner>();
                var tutorial = fight.GetComponent<FightTutorialController>();
                if (reloading)
                {
                    if (level.StageIndex != 3 || tutorial == null || tutorial.StepIndex < 0) return;
                    Check(fight.RosterManager.ActiveHeroes.Count == 3, "Next lesson lost full party");
                    Finish("PASS: Billy five-step lesson; consecutive shots only; duplicate and missed beat do not complete shot; expired mark reapplication does not count as refresh; 3 shots and 3 refreshes; free practice; next lesson loads full team."); return;
                }
                if (!tutorial || tutorial.StepIndex < 0 || fight.WaveTransitionActive) return;
                Check(fight.RosterManager.ActiveHeroes.Count == 1, "Billy lesson not solo");
                if (tutorial.Completed)
                {
                    Check(skipTested && expiredTested && reacquired && !fight.BattleEnded, "Incomplete coverage");
                    if (captureAt == 0) { ScreenCapture.CaptureScreenshot("Temp/BillyTutorialComplete.png"); captureAt = EditorApplication.timeSinceStartup + 1; return; }
                    if (EditorApplication.timeSinceStartup < captureAt) return;
                    Check(tutorial.TryEnterNextTutorial(), "Next tutorial request rejected"); reloading = true; return;
                }
                if (lastStep != tutorial.StepIndex)
                {
                    lastStep = tutorial.StepIndex; pageAt = EditorApplication.timeSinceStartup + .7;
                    if (lastStep == 3) refreshStart = fight.NormalBattleBeatCount;
                }
                if (tutorial.Reading) { if (EditorApplication.timeSinceStartup >= pageAt) tutorial.AdvanceDialogue(); return; }
                if (lastStep == 3 && expiredTested && !reacquired && fight.RosterManager.ActiveEnemies.First().HasGoldAimed)
                { Check(tutorial.Progress == 0, "Reapplying an expired mark incorrectly counted as refresh"); reacquired = true; }
                var clock = fight.BeatClock;
                if (!clock.TryGetTimelinePositionMs(out int ms)) return;
                long beat = clock.LatestBeat.GlobalBeat;
                if (lastInput == beat || Math.Abs(ms - clock.LatestBeat.TimelinePositionMs) > 35) return;
                lastInput = beat;
                if (lastStep == 1 && !skipTested)
                {
                    if (skipBeat == long.MinValue)
                    {
                        fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Front);
                        fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Front);
                        Check(tutorial.Progress == 0, "Preparation/duplicate counted as shot"); skipBeat = beat + 1; return;
                    }
                    if (beat <= skipBeat) return;
                    Check(tutorial.Progress == 0, "Missed beat completed a shot"); skipTested = true;
                }
                if (lastStep == 3 && !expiredTested)
                {
                    if (fight.NormalBattleBeatCount < refreshStart + 10) return;
                    Check(!fight.RosterManager.ActiveEnemies.First().HasGoldAimed && tutorial.Progress == 0, "Expected mark expiry");
                    expiredTested = true;
                }
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Front);
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        static void Finish(string result)
        {
            File.WriteAllText("Temp/BillyTutorialPlay.result", result); File.Delete(Request);
            deadline = captureAt = 0; lastStep = -1; lastInput = skipBeat = long.MinValue;
            skipTested = expiredTested = reacquired = reloading = false;
            EditorApplication.isPlaying = false;
        }
    }
}
