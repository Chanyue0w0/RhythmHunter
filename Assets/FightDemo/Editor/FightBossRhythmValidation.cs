using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightBossRhythmValidation
    {
        const string Request = "Temp/BossRhythmValidation.request";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static double deadline;
        static int step;
        static string mode, progress;
        static FightBossRhythmChart chart;
        static HashSet<int> submitted = new();
        static bool captured, pauseChecked;
        static double pauseUntil;
        static double pausedBeat;
        static float initialArmor, initialHp;
        static FightBossRhythmValidation() => EditorApplication.update += Tick;
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlaying)
            { if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true; return; }
            try
            {
                Application.runInBackground = true;
                if (deadline == 0) { deadline = EditorApplication.timeSinceStartup + 160; mode = File.ReadAllText(Request).Trim(); }
                Check(EditorApplication.timeSinceStartup < deadline, "Timeout: " + progress);
                var fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                if (!fight || fight.AwaitingBattleStart) return;
                var battle = fight.BossRhythm;
                Check(battle, "Boss rhythm component missing");
                progress = $"{mode} step={step}, beat={battle.CurrentBeat:F2}, hits={battle.Hits}, missed={battle.MissedNotes}, ended={fight.BattleEnded}, HP={fight.PartyHp}, chart={battle.ChartError}";
                File.WriteAllText("Temp/BossRhythmValidation.progress", progress);
                if (step == 0)
                {
                    chart = AssetDatabase.LoadAssetAtPath<FightBossRhythmChart>("Assets/FightDemo/Levels/GoblinKing_PhaseTwo_Chart.asset");
                    Check(chart && chart.Validate(out _), "Chart invalid");
                    Check(chart.notes.Select(n => n.arrivalBeat).Distinct().Count() == chart.notes.Count, "Simultaneous notes");
                    var duplicate = UnityEngine.Object.Instantiate(chart); duplicate.notes.Add(duplicate.notes[0]);
                    Check(!duplicate.Validate(out _), "Duplicate beat allowed"); UnityEngine.Object.Destroy(duplicate);
                    Set(fight, "endBattleOnZeroHp", true);
                    fight.GetComponent<FightBossStageDebug>().RequestPhaseTwoSkip();
                    step = 1;
                }
                else if (step == 1 && fight.GetComponent<FightBossPhaseTwoTransition>().Ready)
                {
                    Check(string.IsNullOrEmpty(battle.ChartError), battle.ChartError);
                    Check(battle.SkippedTransitionNotes == 0, "Default chart started before transition completed");
                    Check(battle.CurrentBeat < chart.notes[0].arrivalBeat - chart.travelBeats, "No reaction time for first note");
                    var presenter = UnityEngine.Object.FindFirstObjectByType<FightScenePresenter>();
                    var timeline = (RectTransform)typeof(FightScenePresenter).GetField("timelineRoot", Flags).GetValue(presenter);
                    Check(timeline && !timeline.gameObject.activeSelf, "Gem/BeatUI not hidden");
                    var bar = (UnityEngine.UI.Image)typeof(FightScenePresenter).GetField("stageCountdownFill", Flags).GetValue(presenter);
                    Check(bar && bar.color.r > .9f && bar.color.g < .3f, "Boss timer is not red");
                    if (mode == "lose") { Set(fight, "partyArmor", 0f); Set(fight, "partyHp", 1f); }
                    initialArmor = fight.PartyArmor; initialHp = fight.PartyHp;
                    step = 2;
                }
                else if (step == 2)
                {
                    if (!pauseChecked && battle.CurrentBeat > 12 && battle.CurrentBeat < 15)
                    {
                        if (pauseUntil == 0) { fight.BeatClock.SetPaused(true); pauseUntil = EditorApplication.timeSinceStartup + .6; }
                        if (EditorApplication.timeSinceStartup < pauseUntil - .3) pausedBeat = battle.CurrentBeat;
                        if (EditorApplication.timeSinceStartup >= pauseUntil)
                        { Check(Math.Abs(battle.CurrentBeat - pausedBeat) < .01, "Chart advanced during pause"); fight.BeatClock.SetPaused(false); pauseChecked = true; }
                        return;
                    }
                    if (mode != "lose" && battle.Running)
                    {
                        // First note deliberately missed; every later note uses the production command/judge route.
                        foreach (var note in chart.notes.Skip(1))
                            if (!submitted.Contains(note.arrivalBeat) && battle.CurrentBeat >= note.arrivalBeat - .14 && battle.CurrentBeat < note.arrivalBeat + .15)
                            { submitted.Add(note.arrivalBeat); fight.SubmitHeroCommand((FightInputRouter.HeroCommand)note.lane); }
                        if (battle.CurrentBeat >= 14 && battle.CurrentBeat < 14.2 && !submitted.Contains(-1))
                        {
                            submitted.Add(-1); fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Middle);
                            Check(battle.BadInputs == 1, "Empty timing did not apply Miss");
                            fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Back);
                            Check(battle.BadInputs == 1, "Miss lock did not suppress repeated input");
                        }
                    }
                    if (!captured && battle.CurrentBeat > 22 && battle.CurrentBeat < 23)
                    { ScreenCapture.CaptureScreenshot("Temp/BossRhythmGameplay.png"); captured = true; }
                    if (!fight.BattleEnded) return;
                    var results = UnityEngine.Object.FindFirstObjectByType<FightStageResults>();
                    Check(results.Completed, "Results did not complete");
                    if (mode == "lose")
                    { Check(!fight.BattleWon && fight.PartyHp == 0 && battle.MissedNotes == 1, "Miss did not cause existing defeat sequence"); }
                    else
                    {
                        Check(fight.BattleWon, "Survival did not win");
                        Check(battle.MissedNotes == 1 && battle.Hits == chart.notes.Count - 1, "Wrong hit/miss totals: " + progress);
                        Check(Math.Abs(fight.PartyArmor - (initialArmor - 1)) < .001 && fight.PartyHp == initialHp, "Miss was not exactly one armor-first damage");
                        Check(fight.RosterManager.ActiveEnemies.All(x => !x || x.ActorInstance == null), "Boss not removed after outro");
                        Check(battle.CreatedVisuals <= 16, "Visual pool grew unexpectedly");
                    }
                    File.WriteAllText("Temp/BossRhythmValidation_" + mode + ".result", "PASS: " + progress + "; actual native FMOD chart, calibrated input route, unique arrivals, pause, hidden gem/BeatUI, red timer, shared damage, pooled effects, result panel.");
                    Finish();
                }
            }
            catch (Exception e) { File.WriteAllText("Temp/BossRhythmValidation_" + mode + ".result", "FAIL: " + e); Finish(); }
        }
        static void Finish()
        {
            File.Delete(Request); step = 0; deadline = pauseUntil = 0; captured = pauseChecked = false; submitted.Clear();
            EditorApplication.isPlaying = false;
        }
    }
}
