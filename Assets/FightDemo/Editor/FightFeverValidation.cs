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
        static bool sawMage, magePauseChecked, mageFinished;
        static long mageResumeBeat;
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
                    bool checkResume = File.ReadAllText(Request).Trim() == "resume";
                    if (checkResume && mageFinished && fever.CurrentPhase != FightFeverController.Phase.Performing)
                    {
                        long elapsed = fight.NormalBattleBeatCount - mageResumeBeat;
                        if (elapsed < 8)
                        {
                            Check(targets.All(t => fight.HasMageWeaken(t) && fight.MageWeakenBeatsRemaining(t) == 8 - elapsed), "Post-Fever weaken countdown mismatch");
                            return;
                        }
                        Check(!fight.FeverBattleSuspended && targets.All(t => !fight.HasMageWeaken(t)), "Weaken must expire after eight actual normal battle beats");
                        Check(GameObject.Find("Fever Isolated Render Stage") == null, "Mage render stage leaked after resume");
                        Finish("PASS: warrior/mage damage, 20 penetrating shots, pause, and actual post-Fever eight-beat weaken countdown/expiry. Screenshots saved."); return;
                    }
                    if (fever.CurrentPhase == FightFeverController.Phase.Performing)
                    {
                        if (fever.PerformingHero.CharacterDefinition.EnableUnusedGuardSlime)
                        {
                            sawMage = true;
                            if (fever.PerformanceElapsedBeats < fever.MageFireStartBeat)
                                Check(fever.MageShotsFired == 0, "Mage fired before the three-beat aura delay");
                            if (!magePauseChecked && fever.MageShotsFired >= 3)
                            {
                                if (!fight.IsPaused)
                                { pausedElapsed = fever.PerformanceElapsedBeats; fight.GetComponent<FightPauseController>().SetPaused(true); phaseAt = EditorApplication.timeSinceStartup; return; }
                                if (EditorApplication.timeSinceStartup - phaseAt < 1) return;
                                Check(Math.Abs(pausedElapsed - fever.PerformanceElapsedBeats) < .001, "Mage volley advanced during pause");
                                fight.GetComponent<FightPauseController>().SetPaused(false); magePauseChecked = true;
                            }
                            foreach (var target in targets)
                                if (target.CurrentHp > 0 && fight.HasMageWeaken(target))
                                {
                                    Check(fight.MageWeakenBeatsRemaining(target) == 8, "Mage debuff must not count down during Fever");
                                    Check(fight.GetSlimeReducedDamage(target, 10) < 10, "Mage weaken must already be effective during Fever");
                                }
                            if (fever.PerformanceElapsedBeats > fever.MageFireStartBeat + 1 && fever.PerformanceElapsedBeats < fever.MageFireStartBeat + 3)
                                ScreenCapture.CaptureScreenshot("Temp/Fever-mage-volley.png");
                            if (fever.PerformanceElapsedBeats >= 9.5 && !mageFinished)
                            {
                                Check(fever.MageShotsFired == 20 && fever.MageHitCount == 60, "Expected twenty piercing volleys, each hitting all three enemies");
                                Check(targets.Select((t, i) => Mathf.Abs(t.CurrentHp - (lethal && i == 0 ? 0 : 30)) < .001f).All(x => x), "Mage must deal exactly 20 damage per enemy after warrior's 50");
                                Check(GameObject.Find("MagicAura(Clone)") == null && GameObject.Find("EnergyStrike(Clone)") == null, "Mage projectiles or aura remained after firing");
                                // Advance only the ordinary battle clock to verify the post-Fever duration contract.
                                var beatField = typeof(FightCombatController).GetField("<NormalBattleBeatCount>k__BackingField", Flags);
                                long beat = fight.NormalBattleBeatCount;
                                try
                                {
                                    beatField.SetValue(fight, beat + 7);
                                    Check(targets.Where(t => t.CurrentHp > 0).All(t => fight.HasMageWeaken(t) && fight.MageWeakenBeatsRemaining(t) == 1), "Weaken must last seven resumed beats");
                                    beatField.SetValue(fight, beat + 8);
                                    Check(targets.All(t => !fight.HasMageWeaken(t)), "Weaken must expire after eight resumed beats");
                                }
                                finally { beatField.SetValue(fight, beat); }
                                if (checkResume)
                                {
                                    typeof(FightFeverController).GetField("tutorial", Flags).SetValue(fever, false);
                                    mageResumeBeat = beat;
                                }
                                mageFinished = true; ScreenCapture.CaptureScreenshot("Temp/Fever-mage-idle.png");
                            }
                            return;
                        }
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
                    Check(sawSlash && sawIdle && sawMage && mageFinished, "Warrior or mage phases were skipped");
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
                Finish("PASS: QTE failure/retry; warrior 50 damage; mage three-beat delay, twenty penetrating shots, 20 damage per enemy; mage pause, weaken active during Fever and eight resumed beats duration; aura/projectile cleanup; tutorial victory and next stage. Screenshots saved.");
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        static void Finish(string result)
        {
            sawMage = magePauseChecked = mageFinished = false;
            File.WriteAllText("Temp/FeverPlay.result", result); File.Delete(Request);
            step = frames = 0; deadline = nextPress = phaseAt = 0; pauseChecked = false;
            targets = null; sawSlash = sawIdle = performancePauseChecked = lethal = false; lethalEvents = 0;
            EditorApplication.isPlaying = false;
        }
    }
}
