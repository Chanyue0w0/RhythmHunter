using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightPartyBasicValidation
    {
        private const string Request = "Temp/FightPartyBasicValidation.request";
        private const string Result = "Temp/FightPartyBasicValidation.result";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int checks;
        static FightPartyBasicValidation() => EditorApplication.update += Tick;
        private static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            Run();
        }

        [MenuItem("Rhythm Hunter/Validate Party Checkpoint 2 - Basic Abilities")]
        public static void Run()
        {
            checks = 0;
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/FightScene3.unity");
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var fight = components.OfType<FightCombatController>().Single();
                var roster = fight.RosterManager;
                var clock = Field<FmodBeatClock>(fight, "beatClock");
                var judge = Field<FmodRhythmJudge>(fight, "rhythmJudge");
                var neutral = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Killer.prefab");
                Call(fight, "SubscribeDependencies");
                roster.SetRoster(roster.HeroPrefabs.ToArray(), new[] { neutral, neutral, neutral });
                Check(fight.TryBeginBattle(), "Begin the prepared battle.");
                var shooter = fight.ThirdHero.UnitSlot;
                var mage = fight.SecondHero.UnitSlot;
                const FightInputRouter.HeroCommand Gun = FightInputRouter.HeroCommand.Back;
                const FightInputRouter.HeroCommand Guard = FightInputRouter.HeroCommand.Middle;
                const FightInputRouter.HeroCommand Sword = FightInputRouter.HeroCommand.Front;
                double interval = 500;
                void Reset(float bpm = 120, float calibration = 0)
                {
                    interval = 60000d / bpm;
                    Set(clock, "hasAnchor", true);
                    Set(clock, "latestBeat", new FmodBeatClock.BeatSnapshot(0, 1, 1, 1000, bpm, 4, 4));
                    judge.Configure(clock, 120, calibration);
                    judge.ResetDuplicateTracking();
                    Call(fight, "RebuildRosterAndResetCombat");
                    foreach (var enemy in roster.ActiveEnemies)
                    {
                        enemy.CharacterDefinition.BossPattern.enabled = false;
                        Set(enemy, "maxHp", 100f);
                        enemy.RestoreFullHealth();
                    }
                }
                FmodRhythmJudge.Result Input(FightInputRouter.HeroCommand command, int beat, int delta = 0)
                {
                    int raw = (int)Math.Round(1000 + beat * interval + delta - judge.JudgementOffsetMs);
                    var result = (FmodRhythmJudge.Result)Call(judge, "JudgeTimelinePosition", raw);
                    Call(fight, "ApplyHeroJudgement", fight.GetHeroForCommand(command), command, result);
                    return result;
                }
                void Queue(int beat) => Call(fight, "QueueEnemyAttack", new FmodBeatClock.BeatSnapshot(beat, beat / 4 + 1, beat % 4 + 1,
                    (int)Math.Round(1000 + beat * interval), (float)(60000d / interval), 4, 4));
                void Resolve() => Call(fight, "ResolvePendingEnemyAttack", Field<int>(fight, "pendingAttackRosterVersion"),
                    Field<long>(fight, "pendingEnemyActionId"), Field<FightUnitSlot>(fight, "pendingEnemyAttacker"));

                Reset();
                var target = fight.ActiveEnemySlot;
                Input(Sword, 1);
                Check(target.CurrentHp == 99 && fight.TeamSkillGauge == 1, "Sword resolves 1 HP immediately and grants 1 MP.");
                Input(Sword, 1);
                Check(target.CurrentHp == 99 && fight.TeamSkillGauge == 1, "Duplicate sword cannot damage or grant MP.");

                foreach (int delta in new[] { -120, 0, 120 })
                {
                    Reset();
                    Queue(3);
                    Check(Input(Guard, 3, delta).Judgement == FmodRhythmJudge.Grade.Perfect, "Guard accepts full window.");
                    Resolve();
                    Queue(3); Resolve();
                    Queue(3); Resolve();
                    Check(fight.BlockedAttackCount == 3 && fight.PartyArmor == 3 && fight.PartyHp == 4, "One guard blocks three distinct hits on the same beat.");
                    Check(fight.TeamSkillGauge == 1, "Multiple blocks do not generate extra MP.");
                    Resolve();
                    Check(fight.BlockedAttackCount == 3, "A damage callback cannot be applied twice.");
                    Queue(4); Resolve();
                    Check(fight.PartyArmor == 2, "Guard does not leak into the next beat.");
                }
                Reset();
                Input(Guard, 3, 121); Queue(3); Resolve();
                Check(fight.PartyArmor == 2 && fight.TeamSkillGauge == 0, "Miss gives neither guard nor MP.");
                Reset();
                Queue(3); Input(Guard, 4, -120); Resolve();
                Check(fight.PartyArmor == 2, "Early next-beat guard cannot block an earlier attack.");
                Queue(4); Resolve();
                Check(fight.BlockedAttackCount == 1, "Guard protects its judged beat.");
                Reset();
                Input(Guard, 3, 120); Input(Guard, 4, -120);
                Queue(3); Resolve(); Queue(4); Resolve(); Queue(4); Resolve();
                Check(fight.BlockedAttackCount == 3, "Adjacent guards coexist and do not consume one another.");
                Set(mage.CharacterDefinition, "basicGuardBeats", 2);
                Reset(); Input(Guard, 3); Queue(3); Resolve(); Queue(4); Resolve(); Queue(5); Resolve();
                Check(fight.BlockedAttackCount == 2 && fight.PartyArmor == 2, "Authored guard duration is used.");
                Set(mage.CharacterDefinition, "basicGuardBeats", 1);

                Reset(); target = fight.ActiveEnemySlot;
                Input(Gun, 1);
                Check(fight.GetAimedShotProgress(shooter) == 1 && target.CurrentHp == 100 && fight.TeamSkillGauge == 1, "First input prepares only.");
                Input(Gun, 1); Input(Gun, 1);
                Check(fight.GetAimedShotProgress(shooter) == 1 && target.CurrentHp == 100 && fight.TeamSkillGauge == 1, "Same-beat spam cannot progress or charge.");
                Input(Gun, 2);
                Check(fight.GetAimedShotProgress(shooter) == 2 && target.CurrentHp == 100 && fight.TeamSkillGauge == 2, "Second input aims only.");
                Input(Gun, 3);
                Check(fight.GetAimedShotProgress(shooter) == 0 && target.CurrentHp == 99 && fight.TeamSkillGauge == 3, "Third input fires exactly once and resets.");
                Input(Gun, 3); Input(Gun, 4); Input(Gun, 5);
                Check(target.CurrentHp == 99 && fight.GetAimedShotProgress(shooter) == 2 && fight.TeamSkillGauge == 5, "Next shot requires a new sequence.");
                Input(Gun, 6);
                Check(target.CurrentHp == 98 && fight.TeamSkillGauge == 6, "Second three-beat cycle.");

                Reset(); Input(Gun, 1); Input(Gun, 2, 121);
                Check(fight.GetAimedShotProgress(shooter) == 0 && fight.TeamSkillGauge == 1, "Wrong timing resets preparation without refunding MP.");
                Input(Gun, 3);
                Check(fight.GetAimedShotProgress(shooter) == 1, "After miss, preparation starts over.");
                Reset(); Input(Gun, 1); Input(Gun, 3);
                Check(fight.GetAimedShotProgress(shooter) == 1 && fight.TeamSkillGauge == 2, "Skipped beat cannot preserve loading.");
                Reset(); Input(Gun, 1); Input(Sword, 2); Input(Gun, 2);
                Check(fight.GetAimedShotProgress(shooter) == 0 && fight.TeamSkillGauge == 2, "Another hero uses the shared next beat; duplicate gun input cannot reclaim it.");

                // Real judge and FMOD anchor math, including the full late window.
                foreach (float bpm in new[] { 60f, 120f, 150f })
                foreach (float calibration in new[] { -90f, 0f, 90f })
                {
                    Reset(bpm, calibration);
                    Input(Gun, 1, -120);
                    double lateEdge = 1000 + 2 * interval + 120 - judge.JudgementOffsetMs;
                    Call(fight, "ExpireAimedShotWindow", lateEdge);
                    Check(fight.GetAimedShotProgress(shooter) == 1, "Expected beat remains available through the late edge.");
                    Check(Input(Gun, 2, 120).Judgement == FmodRhythmJudge.Grade.Perfect, "Late-edge input accepted with BPM/calibration.");
                    Check(fight.GetAimedShotProgress(shooter) == 2, "Late-edge advances to aim.");
                    Input(Gun, 3, -120);
                    Check(fight.ActiveEnemySlot.CurrentHp == 99, "Early/late sequence still fires once.");
                    Reset(bpm, calibration); Input(Gun, 1);
                    Call(fight, "ExpireAimedShotWindow", lateEdge + 13);
                    Check(fight.GetAimedShotProgress(shooter) == 0 && fight.TeamSkillGauge == 1, "Window expiration clears progress but retains MP.");
                }

                Reset(); Input(Gun, 1); Input(Gun, 2);
                target = fight.ActiveEnemySlot; target.TakeDamage(100);
                Input(Gun, 3);
                Check(roster.ActiveEnemies.Count(e => e.CurrentHp == 99) == 1, "Shot selects the currently living front target.");
                Reset(); Input(Gun, 1);
                foreach (var enemy in roster.ActiveEnemies) enemy.TakeDamage(100);
                Call(fight, "UpdateBasicAbilityProgress");
                Check(fight.GetAimedShotProgress(shooter) == 0, "No targets clears progress.");
                Input(Gun, 2);
                Check(fight.GetAimedShotProgress(shooter) == 0 && fight.TeamSkillGauge == 2, "Accepted input MP does not depend on hitting a target.");

                Reset(); Input(Gun, 1);
                Call(fight, "BeginEnemyPause");
                Set(fight, "nextTeamSkillBeat", long.MaxValue - 1);
                Call(fight, "ExpireAimedShotWindow", 100000d);
                int mp = fight.TeamSkillGauge;
                fight.SubmitHeroCommand(Gun);
                Check(fight.GetAimedShotProgress(shooter) == 1 && fight.TeamSkillGauge == mp, "Team Skill freezes preparation and blocks Basic input.");
                Set(fight, "latestCombatBeat", 30L);
                Call(fight, "EndEnemyPause", true);
                Set(fight, "nextTeamSkillBeat", long.MaxValue);
                Input(Gun, 30); Input(Gun, 31);
                Check(fight.ActiveEnemySlot.CurrentHp == 99 && fight.TeamSkillGauge == 3, "Preparation continues on the first resumed normal beat.");

                Reset(); Input(Gun, 1); fight.SetTimingCalibrationActive(true);
                Check(fight.GetAimedShotProgress(shooter) == 0, "Calibration clears preparation.");
                fight.SetTimingCalibrationActive(false);
                Reset(); Input(Gun, 1); Set(fight, "battleEnded", true); Call(fight, "UpdateBasicAbilityProgress");
                Check(fight.GetAimedShotProgress(shooter) == 0, "Battle end clears preparation.");
                Reset(); Input(Gun, 1); Call(fight, "OnDisable");
                Check(fight.GetAimedShotProgress(shooter) == 0, "Disable clears preparation.");
                Set(shooter.CharacterDefinition, "consecutiveInputBeats", 2);
                Reset(); Input(Gun, 1); Input(Gun, 2);
                Check(fight.ActiveEnemySlot.CurrentHp == 99 && fight.GetAimedShotProgress(shooter) == 0, "Required input count comes from prefab data.");
                Set(shooter.CharacterDefinition, "consecutiveInputBeats", 3);

                Reset();
                for (int beat = 1; beat <= 65; beat++) Input(Guard, beat);
                Check(fight.TeamSkillGauge == 60, "No-hit guard grants MP and cap is 60.");
                Input(Guard, 65);
                Check(fight.TeamSkillGauge == 60, "Duplicate MP never exceeds cap.");

                Reset(); Input(Gun, 1);
                var presenter = components.OfType<FightScenePresenter>().Single();
                var health = Field<Text>(presenter, "healthText");
                var hud = fight.gameObject.AddComponent<FightDefenseHud>();
                hud.Configure(fight, health.canvas, health.font);
                Check(Field<Text>(hud, "basicText").text.Contains("1/3  PREPARING"), "HUD shows preparation.");
                Input(Gun, 2); hud.Refresh();
                Check(Field<Text>(hud, "basicText").text.Contains("2/3  AIMING"), "HUD shows aiming.");
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    typeof(FightScene3BeatValidation).GetMethod("CaptureHud", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { health.canvas, "Temp/FightPartyBasic-preview.png" });
                UnityEngine.Object.DestroyImmediate(hud);
                File.WriteAllText(Result, $"PASS: {checks} checks. Immediate sword; multi-hit beat guard and boundaries; three consecutive inputs; duplicate/miss/skip/reset; calibrated 60/120/150 BPM timing; MP input accounting/cap; target reacquisition; Team Skill freeze/resume; lifecycle resets; authored parameters; progress HUD.\nInterruption/bonus, new Team Skills, waves and final art remain later checkpoints. Live FMOD/device timing is not covered by this deterministic suite.\n");
                Debug.Log("FIGHT_PARTY_BASIC_VALIDATION_PASS");
            }
            catch (Exception exception) { File.WriteAllText(Result, "FAIL: " + exception); Debug.LogException(exception); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        private static T Field<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static void Check(bool valid, string message) { checks++; if (!valid) throw new InvalidOperationException(message); }
    }
}
