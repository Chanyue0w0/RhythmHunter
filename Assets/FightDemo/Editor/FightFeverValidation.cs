using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using RhythmHunter.FightDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightFeverValidation
    {
        const string Request = "Temp/FeverPlay.request";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static int step, frames;
        static double deadline, nextPress, phaseAt;
        static long frozenBeat;
        static float hp;
        static bool pauseChecked;
        static double pausedElapsed;
        static FightFeverValidation() => EditorApplication.update += Poll;
        static void Check(bool valid, string message) { if (!valid) throw new Exception(message); }
        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (!EditorApplication.isPlaying)
                {
                    if (deadline != 0) { Finish("FAIL: Play Mode ended before validation completed."); return; }
                    if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true;
                    return;
                }
                Application.runInBackground = true;
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 100;
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Fever validation timeout at step " + step);
                var fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                var fever = fight ? fight.GetComponent<FightFeverController>() : null;
                if (!fever || fight.BattleEntranceRunning) return;
                if (fight.AwaitingBattleStart) { fight.TryBeginBattle(); return; }
                if (fight.WaveTransitionActive) return;
                if (step == 0)
                {
                    if (fever.CurrentPhase != FightFeverController.Phase.Introduction) return;
                    if (++frames < 20) return;
                    Check(fight.TeamSkillGaugeExact == fight.TeamSkillGaugeMax, "Tutorial must start full");
                    ScreenCapture.CaptureScreenshot("Temp/Fever-introduction.png");
                    step++; phaseAt = EditorApplication.timeSinceStartup; return;
                }
                if (EditorApplication.timeSinceStartup - phaseAt < .3) return;
                if (step == 1)
                {
                    if (fever.CurrentPhase == FightFeverController.Phase.Introduction)
                    { fight.SubmitHeroCommand(FightInputRouter.HeroCommand.TeamSkill); phaseAt = EditorApplication.timeSinceStartup; return; }
                    fight.SubmitHeroCommand(FightInputRouter.HeroCommand.TeamSkill);
                    if (fever.CurrentPhase != FightFeverController.Phase.Flight) return;
                    step++; phaseAt = EditorApplication.timeSinceStartup; return;
                }
                if (step == 2)
                {
                    if (fever.CurrentPhase != FightFeverController.Phase.Charging) return;
                    Check(fever.Progress == 0, "Activation must not add charge");
                    Check(fight.TeamSkillGauge == 0, "Mana must be spent");
                    frozenBeat = fight.NormalBattleBeatCount; hp = fight.PartyHp;
                    step++; phaseAt = EditorApplication.timeSinceStartup; return;
                }
                if (step == 3)
                {
                    if (fever.Active) Check(fight.NormalBattleBeatCount == frozenBeat && fight.PartyHp == hp, "Combat must remain frozen");
                    if (!pauseChecked && fever.CurrentPhase == FightFeverController.Phase.Charging)
                    {
                        var model = (FightFeverCharge)typeof(FightFeverController).GetField("charge", Flags).GetValue(fever);
                        if (!fight.IsPaused)
                        {
                            pausedElapsed = model.Elapsed; fight.GetComponent<FightPauseController>().SetPaused(true);
                            phaseAt = EditorApplication.timeSinceStartup; return;
                        }
                        if (EditorApplication.timeSinceStartup - phaseAt < 1) return;
                        fight.SubmitHeroCommand(FightInputRouter.HeroCommand.TeamSkill);
                        Check(Math.Abs(model.Elapsed - pausedElapsed) < .001 && fever.Progress == 0, "Pause must freeze clock/input");
                        fight.GetComponent<FightPauseController>().SetPaused(false); pauseChecked = true;
                    }
                    if (fever.CurrentPhase == FightFeverController.Phase.Idle)
                    {
                        Check(!fight.FeverBattleSuspended && fight.TeamSkillGauge == 0, "Failure cleanup");
                        typeof(FightCombatController).GetMethod("SetFeverMana", Flags).Invoke(fight, new object[] { true });
                        step++; return;
                    }
                    return;
                }
                if (step == 4)
                {
                    fight.SubmitHeroCommand(FightInputRouter.HeroCommand.TeamSkill);
                    if (fever.CurrentPhase == FightFeverController.Phase.Flight) step++;
                    return;
                }
                if (step == 5)
                {
                    if (fever.CurrentPhase == FightFeverController.Phase.Charging && EditorApplication.timeSinceStartup >= nextPress)
                    {
                        fight.SubmitHeroCommand(FightInputRouter.HeroCommand.TeamSkill); nextPress = EditorApplication.timeSinceStartup + .11;
                        if (fever.Progress > .5f && fever.Progress < .6f) ScreenCapture.CaptureScreenshot("Temp/Fever-charging.png");
                    }
                    if (fever.CurrentPhase == FightFeverController.Phase.Released)
                    {
                        Check(fever.Progress == 1, "Success logical fill");
                        step++; phaseAt = EditorApplication.timeSinceStartup;
                    }
                    return;
                }
                if (step == 6)
                {
                    if (EditorApplication.timeSinceStartup - phaseAt < 1.4) return;
                    ScreenCapture.CaptureScreenshot("Temp/Fever-success.png");
                    Check(!fever.TutorialCompleted, "Success must not complete tutorial before reading");
                    step++; phaseAt = EditorApplication.timeSinceStartup; return;
                }
                if (step == 7)
                {
                    fight.SubmitHeroCommand(FightInputRouter.HeroCommand.TeamSkill);
                    if (fever.CurrentPhase == FightFeverController.Phase.Explanation)
                    { ScreenCapture.CaptureScreenshot("Temp/Fever-explanation.png"); step++; phaseAt = EditorApplication.timeSinceStartup; }
                    return;
                }
                if (step == 8)
                {
                    fight.SubmitHeroCommand(FightInputRouter.HeroCommand.TeamSkill);
                    Check(fever.TutorialCompleted && !fight.FeverBattleSuspended && fight.TeamSkillGauge == 0, "Read confirmation cleanup");
                    step++; phaseAt = EditorApplication.timeSinceStartup; return;
                }
                Check(GameObject.Find("Fever Isolated Render Stage") == null, "Render stage leaked");
                Finish("PASS: full mana; two introduction pages; activation excluded; 16-beat timeout; combat frozen; zero mana after failure; successful retry; charged hero effects; explicit completion reading; render-stage cleanup. Screenshots saved.");
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        static void Finish(string result)
        {
            File.WriteAllText("Temp/FeverPlay.result", result); File.Delete(Request);
            step = frames = 0; deadline = nextPress = phaseAt = 0; pauseChecked = false;
            EditorApplication.isPlaying = false;
        }
    }
}
