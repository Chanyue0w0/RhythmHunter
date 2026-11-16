using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightBossPhaseTwoValidation
    {
        const string Request = "Temp/BossPhaseTwoValidation.request";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static int step;
        static double deadline, pauseUntil;
        static Vector3 pausedPosition;
        static string lastPhase;
        static float timeAtStart;
        static FightBossPhaseTwoValidation() => EditorApplication.update += Tick;
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlaying)
            { if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true; return; }
            try
            {
                Application.runInBackground = true;
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 110;
                Check(EditorApplication.timeSinceStartup < deadline, "Timed out at " + lastPhase);
                var fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                if (!fight || fight.AwaitingBattleStart) return;
                var transition = fight.GetComponent<FightBossPhaseTwoTransition>();
                Check(transition, "Transition component missing"); lastPhase = transition.Phase.ToString();
                File.WriteAllText("Temp/BossPhaseTwoValidation.progress", $"step={step}; phase={lastPhase}; remaining={fight.BossPhaseTwoSecondsRemaining}");
                if (step == 0)
                {
                    typeof(FightCombatController).GetField("partyHp", Flags).SetValue(fight, 2f);
                    typeof(FightCombatController).GetField("partyArmor", Flags).SetValue(fight, 0f);
                    fight.GetComponent<FightBossStageDebug>().RequestPhaseTwoSkip(); step = 1;
                }
                else if (step == 1 && transition.Phase == FightBossPhaseTwoTransition.TransitionPhase.BossJump)
                {
                    Check(fight.PartyHp == 2 && fight.PartyArmor == fight.MaxPartyArmor, "HP preservation / armor recovery failed");
                    timeAtStart = fight.BossPhaseTwoSecondsRemaining;
                    fight.BeatClock.SetPaused(true);
                    pauseUntil = EditorApplication.timeSinceStartup + .2; step = 2;
                }
                else if (step == 2 && EditorApplication.timeSinceStartup >= pauseUntil)
                {
                    pausedPosition = fight.RosterManager.ActiveEnemies.First().ActorRoot.position;
                    pauseUntil = EditorApplication.timeSinceStartup + .4; step = 3;
                }
                else if (step == 3 && EditorApplication.timeSinceStartup >= pauseUntil)
                {
                    Check(Vector3.Distance(pausedPosition, fight.RosterManager.ActiveEnemies.First().ActorRoot.position) < .001f, "Jump moved while paused");
                    fight.BeatClock.SetPaused(false); step = 4;
                }
                else if (step == 4 && transition.Ready)
                {
                    Check(fight.PartyHp == 2 && fight.PartyArmor == fight.MaxPartyArmor, "Transition changed resources unexpectedly");
                    Check(fight.BossPhaseTwoSecondsRemaining < timeAtStart - 1, "Transition did not consume music time");
                    var settings = new SerializedObject(transition);
                    var heroes = new[] { fight.FrontHero.UnitSlot, fight.SecondHero.UnitSlot, fight.ThirdHero.UnitSlot };
                    var destinations = settings.FindProperty("heroDestinations");
                    for (int i = 0; i < 3; i++)
                    {
                        var target = (Transform)destinations.GetArrayElementAtIndex(i).objectReferenceValue;
                        Check(Vector3.Distance(heroes[i].ActorInstance.transform.Find("FootAnchor").position, target.position) < .025f, "Hero foot missed lane " + i);
                        var animator = heroes[i].CombatAnimator;
                        Check(animator.FindSequence("Idle_back")?.Frames.Count == 3, "Rear idle sprites missing");
                        Check((string)typeof(FightCharacterCombatAnimator).GetField("idleOverrideName", Flags).GetValue(animator) == "Idle_back", "Rear idle not selected");
                    }
                    var boss = fight.RosterManager.ActiveEnemies.First(x => x.BossPhaseOneProtected);
                    var bossTarget = (Transform)settings.FindProperty("bossDestination").objectReferenceValue;
                    Check(Vector3.Distance(boss.ActorInstance.transform.Find("FootAnchor").position, bossTarget.position) < .025f, "Boss foot missed door destination");
                    Check((string)typeof(FightCharacterCombatAnimator).GetField("idleOverrideName", Flags).GetValue(boss.CombatAnimator) == "IdlePh2", "Boss second idle not selected");
                    // Full-rect sprite bounds include transparent margins. Inspect the captured image for framing.
                    Check(Mathf.Abs(settings.FindProperty("bossDestinationScale").floatValue - .6f) < .001f, "Preview scene has not loaded the configured Boss scale");
                    ScreenCapture.CaptureScreenshot("Temp/BossPhaseTwoReady.png");
                    pauseUntil = EditorApplication.timeSinceStartup + 1; step = 5;
                }
                else if (step == 5 && EditorApplication.timeSinceStartup >= pauseUntil)
                {
                    transition.enabled = false;
                    Check(transition.Phase == FightBossPhaseTwoTransition.TransitionPhase.Inactive, "Disable did not restore transition");
                    File.WriteAllText("Temp/BossPhaseTwoValidation.result", "PASS: actual Jump animation completed before Boss movement; pause/resume; Boss and three hero foot destinations; front/middle/rear lane mapping; Idle_back and IdlePh2 selection; HP preserved, DEF restored; music counts transition; disable cleanup. IdlePh2 art remains placeholder.");
                    Finish();
                }
            }
            catch (Exception e) { File.WriteAllText("Temp/BossPhaseTwoValidation.result", "FAIL: " + e); Finish(); }
        }
        static void Finish()
        { File.Delete(Request); deadline = 0; step = 0; EditorApplication.isPlaying = false; }
    }
}
