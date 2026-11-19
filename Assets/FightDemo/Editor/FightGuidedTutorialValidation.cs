using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using RhythmHunter.RhythmDemo;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RhythmHunter.FightDemo.Editor
{
    [InitializeOnLoad]
    static class FightGuidedTutorialValidation
    {
        const string Request = "Temp/GuidedTutorialPlay.request";
        static double deadline, nextPage, finishAt;
        static int lastStep = -1, primaryHits, chargedHits;
        static long lastInput = long.MinValue;
        static float minimumFoot = float.MaxValue;
        static bool pauseTested, practiceCaptured, subscribed, reloading;
        static int ordinaryCalls;
        static FightCombatController fight;
        static Gamepad testPad;
        static float hpAtComplete;
        static int readingPosition;
        static FightGuidedTutorialValidation() => EditorApplication.update += Poll;
        static void OnHit(FightUnitSlot slot, bool charged, int issuedFrame) { primaryHits++; if (charged) chargedHits++; }
        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (!EditorApplication.isPlaying) { if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true; return; }
                Application.runInBackground = true;
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 120;
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout at step " + lastStep);
                fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                var tutorial = fight ? fight.GetComponent<FightTutorialController>() : null;
                if (reloading)
                {
                    if (!fight || fight.GetComponent<FightPauseController>()?.MenuReady != true) return;
                    var planner = fight.GetComponent<FightLevelPlanner>();
                    if (planner.StageIndex != 1) return;
                    if (!tutorial.Active || planner.ActiveStage.TutorialLesson == null || Time.timeScale != 1) throw new Exception("Reload failed to begin the Mage lesson or retained pause");
                    Finish($"PASS: 8 steps; reading music continues; 8 rhythm-only calls/no damage; 4 ordinary hits/no charge; 8 empowered hits; free combat after completion; real RB input reloads Mage; min dummy foot Y={minimumFoot:F5}; screenshots saved."); return;
                }
                if (!tutorial || tutorial.StepIndex < 0) return;
                if (!subscribed) { fight.SwordPrimaryHit += OnHit; subscribed = true; }
                if (tutorial.Completed)
                {
                    if (fight.BattleEnded) throw new Exception("Tutorial ended instead of free practice");
                    if (minimumFoot < .005f) throw new Exception("Dummy intersected ground: " + minimumFoot);
                    if (Time.timeScale != 1) throw new Exception("Completion left time paused");
                    if (finishAt == 0)
                    {
                        if (chargedHits != 8) throw new Exception("Expected 8 charged primary hits, got " + chargedHits);
                        hpAtComplete = fight.RosterManager.ActiveEnemies.First().CurrentHp;
                        ScreenCapture.CaptureScreenshot("Temp/TutorialComplete.png");
                        finishAt = EditorApplication.timeSinceStartup + 2; return;
                    }
                    if (EditorApplication.timeSinceStartup < finishAt) { SendOnBeat(); return; }
                    if (fight.RosterManager.ActiveEnemies.First().CurrentHp >= hpAtComplete) throw new Exception("Free practice attacks did not damage target");
                    fight.SwordPrimaryHit -= OnHit; subscribed = false;
                    reloading = true;
                    testPad = InputSystem.AddDevice<Gamepad>();
                    InputSystem.QueueStateEvent(testPad, new GamepadState().WithButton(GamepadButton.RightShoulder));
                    return;
                }
                var dummy = fight.RosterManager.ActiveEnemies.FirstOrDefault();
                if (dummy && dummy.ActorInstance && !fight.WaveEntranceRunning)
                {
                    var visual = dummy.ActorInstance.transform.Find("Visual");
                    // Source image's first opaque bottom pixel is row 121 from bottom; PPU=409.6.
                    float y = visual.TransformPoint(new Vector3(0, (121f - .12f * 1024f) / 409.6f, 0)).y;
                    minimumFoot = Mathf.Min(minimumFoot, y);
                }
                if (tutorial.StepIndex != lastStep)
                {
                    lastStep = tutorial.StepIndex; nextPage = EditorApplication.timeSinceStartup + .7;
                    if (lastStep == 5 && ordinaryCalls < 4) throw new Exception("Old in-flight attack completed new objective");
                    if (lastStep == 3 && (primaryHits != 0 || dummy.CurrentHp != 99999)) throw new Exception("Rhythm-only practice caused damage");
                    fight.BeatClock.TryGetTimelinePositionMs(out readingPosition);
                    if (lastStep == 0) ScreenCapture.CaptureScreenshot("Temp/TutorialDialogue.png");
                }
                var hero = fight.GetHeroForCommand(FightInputRouter.HeroCommand.Front).UnitSlot;
                if (lastStep < 5 && fight.GetSwordChargeStage(hero) != 1) throw new Exception("Early tutorial charged the sword");
                if (tutorial.Reading)
                {
                    int beforeHits = primaryHits;
                    fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Front);
                    if (primaryHits != beforeHits || fight.IsPaused) throw new Exception("Reading caused damage or paused music");
                    var pause = fight.GetComponent<FightPauseController>();
                    if (!pauseTested)
                    {
                        pause.SetPaused(true); fight.SubmitHeroCommand(FightInputRouter.HeroCommand.TeamSkill);
                        if (tutorial.StepIndex != lastStep) throw new Exception("Pause menu allowed page advance");
                        pause.SetPaused(false);
                        if (fight.IsPaused || Time.timeScale != 1) throw new Exception("Menu resume did not resume music");
                        pauseTested = true;
                    }
                    if (EditorApplication.timeSinceStartup >= nextPage)
                    {
                        fight.BeatClock.TryGetTimelinePositionMs(out int now);
                        if (now <= readingPosition + 100) throw new Exception("Music did not advance while reading");
                        fight.SubmitHeroCommand(FightInputRouter.HeroCommand.TeamSkill);
                    }
                    return;
                }
                int beforeStep = tutorial.StepIndex;
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.TeamSkill);
                if (beforeStep != tutorial.StepIndex || fight.TeamSkillRunning) throw new Exception("A skipped objective or activated Fever");
                if (!practiceCaptured && lastStep == 2 && EditorApplication.timeSinceStartup > nextPage)
                { ScreenCapture.CaptureScreenshot("Temp/TutorialPractice.png"); practiceCaptured = true; }
                SendOnBeat();
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        static void SendOnBeat()
        {
                var judge = fight.GetComponent<FmodRhythmJudge>();
                if (!judge.TryGetEvaluatedBeat(0, out double beat)) return;
                long nearest = (long)Math.Round(beat);
                if (nearest != lastInput && Math.Abs(beat - nearest) < .055)
                {
                    lastInput = nearest;
                    if (lastStep == 4) ordinaryCalls++;
                    fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Front);
                }
        }
        static void Finish(string result)
        {
            if (fight) fight.SwordPrimaryHit -= OnHit;
            if (testPad != null) { InputSystem.RemoveDevice(testPad); testPad = null; }
            File.WriteAllText("Temp/GuidedTutorialPlay.result", result); File.Delete(Request);
            deadline = nextPage = finishAt = 0; lastStep = -1; lastInput = long.MinValue;
            primaryHits = chargedHits = ordinaryCalls = 0; minimumFoot = float.MaxValue; subscribed = pauseTested = practiceCaptured = reloading = false;
            EditorApplication.isPlaying = false;
        }
    }
}
