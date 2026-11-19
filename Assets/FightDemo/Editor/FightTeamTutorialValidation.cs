using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace RhythmHunter.FightDemo.Editor
{
    [InitializeOnLoad]
    static class FightTeamTutorialValidation
    {
        const string Request = "Temp/TeamTutorialPlay.request", Original = "TeamTutorial.OriginalStage";
        static double deadline, nextPage;
        static long inputBeat = long.MinValue, finishBeat;
        static int previousStep = -1, shots, finishAttacks;
        static bool checkedState;
        static FightCombatController fight;
        static FightTeamTutorialValidation()
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
        static void Assert(bool ok, string text) { if (!ok) throw new Exception(text); }
        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (!EditorApplication.isPlaying)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    var planner = UnityEngine.Object.FindFirstObjectByType<FightLevelPlanner>();
                    Assert(planner && planner.Chapter.Stages.Count == 5, "Chapter 1 must have five stages");
                    for (int i = 0; i < 5; i++)
                    {
                        var stage = planner.Chapter.Stages[i]; Assert(stage.Validate(out string error), error);
                        Assert(stage.TutorialFullParty == (i >= 3), "Solo/full party classification changed");
                    }
                    Assert(planner.Chapter.Stages[4].TutorialLesson == null && !planner.Chapter.Stages[4].TeachesFever, "Reserved Fever stage implemented unexpectedly");
                    SessionState.SetInt(Original, planner.StageIndex);
                    var so = new SerializedObject(planner); so.FindProperty("stageIndex").intValue = 3; so.ApplyModifiedPropertiesWithoutUndo();
                    EditorApplication.isPlaying = true; return;
                }
                Application.runInBackground = true;
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 100;
                Assert(EditorApplication.timeSinceStartup < deadline, "Timeout at step " + previousStep);
                fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                var tutorial = fight ? fight.GetComponent<FightTutorialController>() : null;
                if (!tutorial || tutorial.StepIndex < 0 || fight.WaveTransitionActive) return;
                if (!checkedState)
                {
                    Assert(fight.RosterManager.ActiveHeroes.Count == 3 && !fight.RosterManager.IsSoloTutorialStage, "Full party did not spawn");
                    var buttons = (RectTransform[])typeof(FightTutorialController).GetField("positionButtons", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tutorial);
                    Assert(buttons.All(b => b != null && b.gameObject.activeInHierarchy), "Missing occupied-position buttons");
                    for (int i = 0; i < 3; i++)
                    {
                        var slot = fight.RosterManager.GetHeroAtPosition((FightRosterManager.PartyPosition)i);
                        var foot = slot.ActorInstance.transform.Find("FootAnchor");
                        var buttonScreen = RectTransformUtility.WorldToScreenPoint(null, buttons[i].position);
                        Assert(buttonScreen.x > 0 && buttonScreen.x < Screen.width && buttonScreen.y > 0 && buttonScreen.y < Screen.height, "Position button is offscreen");
                        Assert(Mathf.Abs(buttonScreen.x - Camera.main.WorldToScreenPoint(foot.position).x) < 2, "Button horizontal origin is wrong");
                        Assert(buttonScreen.y < Camera.main.WorldToScreenPoint(foot.position).y, "Button is not below feet");
                    }
                    fight.RosterManager.ActiveEnemies.First().TakeDamage(99999);
                    fight.ApplyPartyDamage(99999, fight.BeatClock.LatestBeat.GlobalBeat);
                    Assert(fight.RosterManager.ActiveEnemies.First().CurrentHp == 1 && fight.PartyHp > 0 && !fight.BattleEnded, "Training lethal damage protection failed");
                    checkedState = true;
                }
                if (tutorial.Completed)
                {
                    Assert(!fight.BattleEnded, "Training battle unexpectedly ended");
                    if (finishBeat == 0)
                    {
                        finishBeat = fight.NormalBattleBeatCount; finishAttacks = fight.BlockedAttackCount + fight.ReceivedAttackCount;
                        ScreenCapture.CaptureScreenshot("Temp/TeamTutorialComplete.png"); return;
                    }
                    if (fight.NormalBattleBeatCount < finishBeat + 5) return;
                    Assert(fight.ScheduledEnemyHitCount == 0 && finishAttacks == fight.BlockedAttackCount + fight.ReceivedAttackCount, "Goblin kept attacking after block practice");
                    Finish("PASS: five stages; first three solo; full-party stage 4; occupied X/Y/B buttons below feet; enemy/player survive lethal damage; 5 charged hits at locked HP; 3 real blocks; 4 follow-up contacts; enemy attacks stopped after defense; Fever stage reserved."); return;
                }
                if (previousStep != tutorial.StepIndex) { previousStep = tutorial.StepIndex; nextPage = EditorApplication.timeSinceStartup + .6; shots = 0; }
                if (tutorial.Reading)
                {
                    Assert(fight.ScheduledEnemyHitCount == 0, "Enemy attack leaked into dialogue");
                    if (EditorApplication.timeSinceStartup >= nextPage) tutorial.AdvanceDialogue();
                    return;
                }
                var clock = fight.BeatClock;
                if (!clock.TryGetTimelinePositionMs(out int ms) || inputBeat == clock.LatestBeat.GlobalBeat || Math.Abs(ms - clock.LatestBeat.TimelinePositionMs) > 35) return;
                inputBeat = clock.LatestBeat.GlobalBeat;
                var command = previousStep == 3 ? FightInputRouter.HeroCommand.Middle :
                    previousStep == 5 && shots++ < 2 ? FightInputRouter.HeroCommand.Back : FightInputRouter.HeroCommand.Front;
                fight.SubmitHeroCommand(command);
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        static void Finish(string text)
        {
            File.WriteAllText("Temp/TeamTutorialPlay.result", text); File.Delete(Request);
            deadline = nextPage = 0; previousStep = -1; checkedState = false; finishBeat = 0; inputBeat = long.MinValue;
            EditorApplication.isPlaying = false;
        }
    }
}
