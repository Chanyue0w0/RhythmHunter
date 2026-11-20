using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
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
        static FightUnitSlot[] targets;
        static bool sawSlash, sawIdle, performancePauseChecked, lethal;
        static int lethalEvents;
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
                if (deadline == 0) { deadline = EditorApplication.timeSinceStartup + 100; lethal = File.ReadAllText(Request).Trim() == "lethal"; }
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
                        targets = UnityEngine.Object.FindObjectsByType<FightUnitSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                            .Where(s => s.Team == FightUnitSlot.UnitTeam.Enemy).Take(3).ToArray();
                        Check(targets.Length == 3, "Need three enemy slots for area-damage test");
                        var prefab = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>(File.ReadAllText(Request).Trim() == "dummy" ? "Assets/FightDemo/Prefabs/ArtBattle/TrainingDummy.prefab" : "Assets/FightDemo/Prefabs/ArtBattle/Goblin_Normal.prefab");
                        foreach (var target in targets)
                        {
                            target.SpawnCharacter(prefab, fight);
                            var data = new SerializedObject(target);
                            typeof(FightUnitSlot).GetField("currentHp", Flags).SetValue(target, 100f);
                            data.FindProperty("maxHp").floatValue = 100; data.ApplyModifiedPropertiesWithoutUndo();
                        }
                        var enemies = (List<FightUnitSlot>)typeof(FightCombatController).GetField("fightScene2Enemies", Flags).GetValue(fight);
                        enemies.Clear(); enemies.AddRange(targets);
                        if (lethal)
                        {
                            typeof(FightUnitSlot).GetField("currentHp", Flags).SetValue(targets[0], 2f);
                            targets[0].EnemyDefeated += (_, __) => lethalEvents++;
                        }
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
                    if (fever.CurrentPhase == FightFeverController.Phase.Performing)
                    {
                        Check(fight.GetSwordChargeStage(fever.PerformingHero) == 3, "Weapon must stay fully charged");
                        if (fever.PerformanceElapsedBeats < 2) Check(targets.Select((t, i) => t.CurrentHp == (lethal && i == 0 ? 2 : 100)).All(x => x), "Intro must not deal damage");
                        if (!performancePauseChecked && fever.PerformanceElapsedBeats > 3)
                        {
                            if (!fight.IsPaused)
                            {
                                pausedElapsed = fever.PerformanceElapsedBeats; fight.GetComponent<FightPauseController>().SetPaused(true);
                                phaseAt = EditorApplication.timeSinceStartup; return;
                            }
                            if (EditorApplication.timeSinceStartup - phaseAt < 1) return;
                            Check(Math.Abs(pausedElapsed - fever.PerformanceElapsedBeats) < .001, "Performance pause advanced the timeline");
                            fight.GetComponent<FightPauseController>().SetPaused(false); performancePauseChecked = true;
                        }
                        if (fever.PerformanceElapsedBeats > 4 && fever.PerformanceElapsedBeats < 7)
                        {
                            sawSlash = true;
                            Check(fever.PrimarySlashCount > 0 && fever.DelayedSlashCount > 0, "Missing slash ticks");
                            Check(fight.GetComponent<FightFeverAfterimagePool>().ActiveCount > 0, "Missing pooled echoes");
                            ScreenCapture.CaptureScreenshot("Temp/Fever-warrior-slashes.png");
                        }
                        if (fever.PerformanceElapsedBeats >= 8 && !sawIdle)
                        {
                            sawIdle = true;
                            Check(fever.PrimarySlashCount == 20 && fever.DelayedSlashCount == 20, "Expected twenty main and twenty delayed hits");
                            Check(targets.Select((t, i) => Mathf.Abs(t.CurrentHp - (lethal && i == 0 ? 0 : 50)) < .001f).All(x => x), "Each enemy must lose exactly 50 HP (capped at zero)");
                            if (lethal) Check(targets[0].ActorInstance && fight.IsBurstRetainedTarget(targets[0]) && lethalEvents == 1, "Lethal enemy must remain until performance ends, with one defeat event");
                            Check(fever.PerformingHero.CombatAnimator.ActiveAnimation == FightCharacterCombatAnimator.CombatAnimation.OpeningIdle ||
                                fever.PerformingHero.CombatAnimator.FindSequence("Idle")?.Animation == fever.PerformingHero.CombatAnimator.ActiveAnimation, "Last two beats must be Idle");
                            Check(fight.GetComponent<FightFeverAfterimagePool>().ActiveCount == 0, "Echoes must return by Idle");
                            ScreenCapture.CaptureScreenshot("Temp/Fever-warrior-idle.png");
                        }
                        return;
                    }
                    if (!fever.TutorialCompleted) return;
                    Check(sawSlash && sawIdle, "Warrior phases were skipped");
                    Check(fight.BattleEnded && fight.BattleWon, "Fever success must finish the tutorial battle");
                    Check(fight.GetComponent<FightLevelPlanner>().TryFindNextStage(out _, out _), "Tutorial must offer next battle");
                    var pool = fight.GetComponent<FightFeverAfterimagePool>(); int allocated = pool.AllocatedCount;
                    foreach (var target in targets) pool.Strike(target);
                    Check(pool.AllocatedCount == allocated, "Echo pool must reuse existing renderers"); pool.ReleaseAll();
                    step = 8; phaseAt = EditorApplication.timeSinceStartup; return;
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
                    Check(fever.TutorialCompleted && !fight.FeverBattleSuspended && fight.TeamSkillGauge == 0, "Automatic tutorial completion cleanup");
                    step++; phaseAt = EditorApplication.timeSinceStartup; return;
                }
                Check(GameObject.Find("Fever Isolated Render Stage") == null, "Render stage leaked");
                Finish("PASS: 16-beat QTE failure/retry and pause; warrior fully charged; 10 Multi-Slash loops, 20 main + 20 delayed hits, exactly 50 HP per enemy across three targets; last two beats Idle; pooled echoes return and reuse; unimplemented heroes skipped; automatic tutorial victory, next stage available and cleanup. Screenshots saved.");
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        static void Finish(string result)
        {
            File.WriteAllText("Temp/FeverPlay.result", result); File.Delete(Request);
            step = frames = 0; deadline = nextPress = phaseAt = 0; pauseChecked = false;
            targets = null; sawSlash = sawIdle = performancePauseChecked = lethal = false; lethalEvents = 0;
            EditorApplication.isPlaying = false;
        }
    }
}
