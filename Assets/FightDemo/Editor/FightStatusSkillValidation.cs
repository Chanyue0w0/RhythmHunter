using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    public static class FightStatusSkillValidation
    {
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
        static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);
        static int checks;
        static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
        static FmodBeatClock.BeatSnapshot Beat(int n) => new(n, n / 4 + 1, n % 4 + 1, 1000 + n * 500, 120, 4, 4);

        [MenuItem("Rhythm Hunter/Validate Mage and Skeleton Status Skills")]
        public static void Run()
        {
            checks = 0;
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Main_FightScene.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                Set(fight.RosterManager, "requireBattlePreparation", false);
                Set(fight, "showEnemyDamageNumbers", false);
                var heroes = new[] { "LongswordWarrior", "Mage", "Skeleton" }.Select(name =>
                    AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>($"Assets/FightDemo/Prefabs/ArtBattle/{name}.prefab")).ToArray();
                var enemyData = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Killer.prefab");
                fight.RosterManager.SetRoster(heroes, new[] { enemyData, enemyData, enemyData });
                Call(fight, "RebuildRosterAndResetCombat");
                var warrior = fight.FrontHero.UnitSlot;
                var mage = fight.SecondHero.UnitSlot;
                var gunner = fight.ThirdHero.UnitSlot;
                var enemies = fight.RosterManager.ActiveEnemies.OrderByDescending(e => e.SlotIndex).ToArray();
                var front = enemies[0];
                // Isolate deterministic combat rules from sprite playback and FMOD audio.
                foreach (var hero in fight.RosterManager.ActiveHeroes) Set(hero, "combatAnimator", null);
                void Reset()
                {
                    Call(fight, "ResetBattleStateForRoster");
                    Set(fight, "latestCombatBeat", 0L);
                    foreach (var e in enemies)
                    {
                        e.CharacterDefinition.BossPattern.enabled = false;
                        Set(e.CharacterDefinition, "attackIntervalBeats", 1000);
                        Set(e, "attackIntervalBeats", 1000);
                        Set(e, "maxHp", 100f); e.RestoreFullHealth();
                    }
                }
                void Skill(FightUnitSlot actor)
                {
                    Set(fight, "resolvingTeamSkillFrame", true);
                    try
                    {
                        Call(fight, "PerformConfiguredAbility", new[] { fight.FrontHero, fight.SecondHero, fight.ThirdHero }.Single(h => h.UnitSlot == actor),
                            FightCombatController.ActionType.Skill, actor.SkillBehavior, actor.SkillPower, 0L);
                    }
                    finally { Set(fight, "resolvingTeamSkillFrame", false); }
                }
                void Tick(int n) => Call(fight, "OnBeat", Beat(n));

                Reset();
                Check(fight.MaxPartyArmor == 2 && fight.MaxPartyHp == 4, "Passives disabled: warrior front supplies 2 DEF and team has 4 HP.");
                fight.ApplyPartyDamage(1, 0);
                Skill(mage);
                Check(enemies.All(e => e.HasSlimeFragment && e.CurrentHp == 100), "Slime attaches to all three without damage.");
                Call(fight, "ApplyAbilityArmorRecovery", mage.CharacterDefinition, true);
                Check(fight.PartyArmor == 1, "Mage passive no longer restores armor.");
                Skill(gunner);
                Check(enemies.All(e => e.CurrentHp == 99 && e.SlimeDetonationPending && !e.HasSlimeFragment), "Direct hit arms, but does not immediately detonate, the fragment.");
                foreach (var e in enemies) e.AdvanceFragmentDetonations(.49f);
                Check(enemies.All(e => e.CurrentHp == 99), "Explosion cannot deal damage before half a beat.");
                foreach (var e in enemies) e.AdvanceFragmentDetonations(.01f);
                Check(enemies.All(e => e.CurrentHp == 98 && e.IsStunned && !e.HasSlimeFragment), "Bomb direct damage detonates each fragment once.");
                foreach (var e in enemies) e.AdvanceFragmentDetonations(1f);
                Check(enemies.All(e => e.CurrentHp == 98 && !e.SlimeDetonationPending), "Expired queue does not repeat explosions.");
                front.TakeDamage(1);
                Check(front.CurrentHp == 97, "Consumed fragment cannot explode again.");

                Reset(); Skill(mage); front.ApplyBleeding(.5f, 4); front.ApplyFragile();
                Tick(1);
                Check(front.CurrentHp == 99 && !front.IsFragile && front.HasSlimeFragment, "Bleed consumes Fragile but does not trigger slime.");
                Tick(2); Tick(3); Tick(4);
                Check(front.CurrentHp == 97.5f && front.BleedBeatsRemaining == 0, "Bleeding ticks exactly four times.");
                Check(front.HasSlimeFragment, "Fourth ordinary interval retains attachment.");
                Tick(5);
                Check(!front.HasSlimeFragment && front.CurrentHp == 97.5f, "Attachment expires without damage after four full intervals.");
                Reset(); front.ApplyBleeding(.5f, 4); Tick(1); front.ApplyBleeding(.5f, 4); Tick(2);
                Check(front.CurrentHp == 99 && front.BleedBeatsRemaining == 3, "Bleed refreshes duration without stacking ticks.");

                Reset();
                Call(fight, "PerformAimedShot", gunner, 1f, 1L);
                Call(fight, "PerformAimedShot", gunner, 1f, 1L);
                Check(front.CurrentHp == 100 && fight.GetAimedShotProgress(gunner) == 1, "Same-beat repeats cannot fire.");
                Call(fight, "PerformAimedShot", gunner, 1f, 3L);
                Check(front.CurrentHp == 100 && fight.GetAimedShotProgress(gunner) == 1, "A skipped beat resets aiming.");
                Check(fight.ScheduleEnemyHits(front, new[] { 5 }, 1), "Schedule target's windup.");
                Call(fight, "PerformAimedShot", gunner, 1f, 4L);
                Check(front.CurrentHp == 99 && front.BleedBeatsRemaining == 4 && fight.GetAimedShotProgress(gunner) == 0,
                    "Two consecutive inputs deal 1 damage and apply bleeding.");
                Check(fight.ScheduledEnemyHitCount == 1, "Basic shot no longer interrupts the target.");
                Call(fight, "PerformAimedShot", gunner, 1f, 5L);
                Call(fight, "TrackAimedShotJudgement", gunner, new FmodRhythmJudge.Result(
                    FmodRhythmJudge.Grade.Miss, 200, 0, 0, default, false, "test miss"));
                Check(fight.GetAimedShotProgress(gunner) == 0, "Miss resets aiming progress.");

                Reset(); Skill(mage); front.TakeDamage(0);
                Check(front.HasSlimeFragment, "Rejected zero damage does not detonate a fragment.");
                front.TakeDamage(1, FightUnitSlot.DamageKind.Secondary);
                Check(front.HasSlimeFragment, "Secondary damage does not chain-detonate fragments.");
                front.TakeDamage(200);
                Check(!front.HasSlimeFragment && front.BleedBeatsRemaining == 0 && !front.IsStunned, "Death clears statuses.");

                Reset();
                fight.ScheduleEnemyHits(front, new[] { 2, 3 }, 1, false);
                fight.ScheduleEnemyHits(front, new[] { 2 }, 1, true, true);
                Skill(gunner);
                Check(fight.ScheduledEnemyHitCount == 1 && front.IsStunned, "Stun cancels remaining melee hits, retains launched projectile.");
                Check(!fight.ScheduleEnemyHits(front, new[] { 1 }, 1), "Stunned enemy cannot start new attacks.");
                for (int i = 1; i <= 4; i++) { Tick(i); Check(front.IsStunned, "Stun protects each of four ordinary intervals."); }
                Tick(5); Check(!front.IsStunned, "Stun ends before the fifth interval.");

                Reset(); front.CharacterDefinition.BossPattern.enabled = true;
                fight.ScheduleEnemyHits(front, new[] { 2 }, 1);
                Skill(gunner);
                Check(front.CurrentHp == 99 && !front.IsStunned && fight.ScheduledEnemyHitCount == 1,
                    "Super-armored boss takes bomb damage without interruption or stun.");

                Reset(); Set(fight, "teamSkillGauge", 60);
                Check(fight.TryStartTeamSkill(), "Full MP starts the chain.");
                for (int i = 1; i <= 6; i++) Tick(i);
                Check(fight.NormalBattleBeatCount == 0 && enemies.All(e => e.IsStunned), "Team Skill does not spend status beats.");
                Check(front.CurrentHp == 96 && enemies.Skip(1).All(e => e.CurrentHp == 99), "Team Skill direct damage resolves before delayed explosions.");
                foreach (var e in enemies) e.AdvanceFragmentDetonations(.5f);
                Check(fight.TeamSkillRunning && fight.NormalBattleBeatCount == 0, "Explosion delay advances during Team Skill without spending status beats.");
                Check(front.CurrentHp == 95 && enemies.Skip(1).All(e => e.CurrentHp == 98),
                    "Warrior then slime then bomb: 2 + fragile bomb 2 + explosion 1; others 2.");
                Tick(7);
                Check(!fight.TeamSkillRunning && fight.NormalBattleBeatCount == 1 && front.StunBeatsRemaining == 4,
                    "First resumed ordinary beat starts duration one.");
                Call(fight, "OnDisable");
                Check(enemies.All(e => !e.IsStunned && !e.HasSlimeFragment && !e.SlimeDetonationPending && e.BleedBeatsRemaining == 0), "Teardown clears all timed statuses and explosions.");
                Reset(); Skill(mage); front.TakeDamage(1); front.TakeDamage(1);
                front.AdvanceFragmentDetonations(.5f);
                Check(front.CurrentHp == 97 && !front.SlimeDetonationPending, "Two direct hits during delay still produce just one explosion.");
                Skill(mage); front.TakeDamage(1); front.ClearTimedStatuses(); front.AdvanceFragmentDetonations(1);
                Check(front.CurrentHp == 96, "Clearing statuses cancels pending explosion damage.");
                Skill(mage); front.TakeDamage(1); front.TakeDamage(200); front.AdvanceFragmentDetonations(1);
                Check(front.CurrentHp == 0 && !front.SlimeDetonationPending, "Target death cancels its queued explosion without retargeting.");
                string result = $"PASS: {checks} Mage/Skeleton status skill checks.";
                File.WriteAllText("Temp/FightStatusSkillValidation.result", result);
                Debug.Log(result);
            }
            catch (Exception ex)
            {
                File.WriteAllText("Temp/FightStatusSkillValidation.result", "FAIL: " + ex);
                throw;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
