using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace RhythmHunter.FightDemo.Editor
{
    [InitializeOnLoad]
    static class FightMageTutorialValidation
    {
        const string Request = "Temp/MageTutorialPlay.request";
        const string Original = "MageTest.OriginalStage";
        static double deadline, pageAt, finishedAt;
        static long inputBeat = long.MinValue;
        static int step = -1, slimeHits;
        static bool checkedState, subscribed;
        static FightCombatController fight;
        static FightMageTutorialValidation()
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
        static object Call(string name, params object[] args) => typeof(FightCombatController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fight, args);
        static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (!EditorApplication.isPlaying)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    var planner = UnityEngine.Object.FindFirstObjectByType<FightLevelPlanner>();
                    Assert(planner && planner.Chapter.Stages.Count >= 2, "Open Chapter 1 in Art2.");
                    SessionState.SetInt(Original, planner.StageIndex);
                    var so = new SerializedObject(planner); so.FindProperty("stageIndex").intValue = 1; so.ApplyModifiedPropertiesWithoutUndo();
                    EditorApplication.isPlaying = true; return;
                }
                Application.runInBackground = true;
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 100;
                Assert(EditorApplication.timeSinceStartup < deadline, "Timeout, step " + step);
                fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                var tutorial = fight ? fight.GetComponent<FightTutorialController>() : null;
                if (!tutorial || tutorial.StepIndex < 0 || fight.WaveTransitionActive) return;
                if (!subscribed) { fight.MageSlimeAttached += OnSlime; subscribed = true; }
                if (!checkedState)
                {
                    Assert(tutorial.Reading && fight.ScheduledEnemyHitCount == 0, "Enemy attacked before practice");
                    var hero = fight.GetHeroForCommand(FightInputRouter.HeroCommand.Front).UnitSlot;
                    var enemy = fight.RosterManager.ActiveEnemies.First();
                    Assert(hero.CharacterDefinition.EnableUnusedGuardSlime, "Mage ability disabled");
                    var lessonField = typeof(FightTutorialController).GetField("lesson", BindingFlags.Instance | BindingFlags.NonPublic);
                    var savedLesson = lessonField.GetValue(tutorial);
                    try
                    {
                        lessonField.SetValue(tutorial, null);
                        long beat = fight.BeatClock.LatestBeat.GlobalBeat;
                        Call("TrackMageGuard", hero, beat);
                        Call("RecordMageGuardBlock", beat);
                        var guards = (System.Collections.IList)typeof(FightCombatController).GetField("mageGuards", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fight);
                        Assert(guards.Count == 1 && (bool)guards[0].GetType().GetField("blocked").GetValue(guards[0]), "Block did not arm the matching cast");
                        Assert((long)guards[0].GetType().GetField("fireBeat").GetValue(guards[0]) == beat + 1, "Retaliation not scheduled on next beat");
                        Call("RecordMageGuardBlock", beat);
                        Assert(guards.Count == 1, "Multi-hit block duplicated retaliation");
                        Call("ShiftMageGuards", 16L);
                        Assert((long)guards[0].GetType().GetField("end").GetValue(guards[0]) == beat + 17, "Fever pause lost guard duration");
                        Call("ClearMageAbility");
                    }
                    finally { lessonField.SetValue(tutorial, savedLesson); }
                    Call("ApplyMageSlime", hero, enemy);
                    Call("ApplyMageSlime", hero, enemy);
                    fight.RefreshStatusIcons(enemy);
                    Assert(fight.HasMageWeaken(enemy), "Missing weaken status");
                    enemy.ApplyFragile(); enemy.ApplyBleeding(1, 8); enemy.ApplyAimed(hero, true);
                    fight.RefreshStatusIcons(enemy);
                    var iconList = (System.Collections.IEnumerable)typeof(FightCombatController).GetField("activeStatusIcons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fight);
                    var icons = iconList.Cast<object>().Where(x => (FightUnitSlot)x.GetType().GetProperty("Target", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(x) == enemy).ToArray();
                    Assert(icons.Any(x => (int)x.GetType().GetProperty("Kind", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(x) == 5), "Weaken icon not assigned/spawned");
                    float Prop(object icon, string name) => (float)icon.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(icon);
                    for (int i = 0; i < icons.Length; i++)
                        for (int j = i + 1; j < icons.Length; j++)
                            if (Mathf.Abs(Prop(icons[i], "Row") - Prop(icons[j], "Row")) < .001f)
                                Assert(Mathf.Abs(Prop(icons[i], "Lane") - Prop(icons[j], "Lane")) >= (Prop(icons[i], "LayoutWidth") + Prop(icons[j], "LayoutWidth")) * .5f, "Status icons overlap at peak pulse size");
                    enemy.ClearFragile(); enemy.ClearTimedStatuses();
                    Assert(fight.GetSlimeReducedDamage(enemy, 3) == 2 && fight.GetSlimeReducedDamage(enemy, 1) == 0, "Reduction stacks or floor wrong");
                    var countProperty = typeof(FightCombatController).GetProperty("NormalBattleBeatCount");
                    long originalBeat = fight.NormalBattleBeatCount;
                    countProperty.SetValue(fight, originalBeat + 8);
                    Assert(fight.GetSlimeReducedDamage(enemy, 3) == 3, "Reduction did not expire at eight ordinary beats");
                    countProperty.SetValue(fight, originalBeat);
                    Call("ClearMageAbility");
                    fight.ApplyPartyDamage(99999, fight.BeatClock.LatestBeat.GlobalBeat);
                    Assert(!fight.BattleEnded && fight.PartyHp > 0, "Tutorial player died");
                    slimeHits = 0; checkedState = true;
                }
                if (tutorial.Completed)
                {
                    Assert(slimeHits >= 3 && fight.BlockedAttackCount >= 3, "Objectives not met");
                    Assert(!fight.BattleEnded && fight.TutorialPreventsDeath, "Practice protection lost");
                    if (finishedAt == 0) { ScreenCapture.CaptureScreenshot("Temp/MageTutorialComplete.png"); finishedAt = EditorApplication.timeSinceStartup + 1; return; }
                    if (EditorApplication.timeSinceStartup < finishedAt) return;
                    Finish("PASS: mage lesson; 3 actual blocks and 3 successful-guard slime contacts; next-beat scheduling; multi-hit deduplication; guard pause shift; weaken icon and non-overlapping rows; refresh without stacking; 8-beat expiry; lethal-damage protection; free practice retained."); return;
                }
                if (step != tutorial.StepIndex) { step = tutorial.StepIndex; pageAt = EditorApplication.timeSinceStartup + .8; }
                if (tutorial.Reading)
                {
                    Assert(fight.ScheduledEnemyHitCount == 0, "Reading retained enemy attacks");
                    if (EditorApplication.timeSinceStartup >= pageAt) tutorial.AdvanceDialogue();
                    return;
                }
                var clock = fight.BeatClock;
                if (!clock.TryGetTimelinePositionMs(out int ms) || clock.LatestBeat.GlobalBeat == inputBeat) return;
                if (Math.Abs(ms - clock.LatestBeat.TimelinePositionMs) > 35) return;
                inputBeat = clock.LatestBeat.GlobalBeat;
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Front);
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        static void OnSlime(FightUnitSlot target) => slimeHits++;
        static void Finish(string result)
        {
            if (fight) fight.MageSlimeAttached -= OnSlime;
            File.WriteAllText("Temp/MageTutorialPlay.result", result); File.Delete(Request);
            deadline = pageAt = finishedAt = 0; step = -1; checkedState = subscribed = false; slimeHits = 0;
            EditorApplication.isPlaying = false;
        }
    }
}
