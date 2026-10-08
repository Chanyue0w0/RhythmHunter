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
    public static class FightPartyLifecycleValidation
    {
        const string Request = "Temp/FightPartyLifecycleValidation.request", Result = "Temp/FightPartyLifecycleValidation.result";
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static int checks;
        static FightPartyLifecycleValidation() => EditorApplication.update += Tick;
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request); Run();
        }
        static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        static void Set(object o, string n, object v) => o.GetType().GetField(n, Private).SetValue(o, v);
        static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, Private).GetValue(o);
        static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, Private).Invoke(o, a);
        static void Quiet(FightPartySkillValidation.Fixture f) { foreach (var enemy in f.Enemies) Set(enemy, "attackIntervalBeats", 10000); }
        static void Resolve(FightCombatController fight, int beat) => Call(fight, "ResolveEnemyCombatWork", 1000 + beat * 500 + 140);
        static void KillWave(FightPartySkillValidation.Fixture f) { foreach (var enemy in f.Fight.RosterManager.ActiveEnemies) enemy.TakeDamage(1000); }

        [MenuItem("Rhythm Hunter/Validate Party Checkpoint 4 - Lifecycle")]
        public static void Run()
        {
            checks = 0;
            try
            {
                using var f = new FightPartySkillValidation.Fixture(new[] { 0, 1, 2 });
                var fight = f.Fight; var source = f.Enemies[0]; Quiet(f);
                Check(fight.ScheduleEnemyHits(source, new[] { 1, 2, 3 }, 1), "Telegraphed multi-hit action schedules.");
                Set(source, "attackIntervalBeats", 1);
                f.Beat(1);
                Check(!fight.HasPendingEnemyAttack, "Authored sequence owns the turn; legacy automatic attack does not overlap it.");
                Quiet(f); Resolve(fight, 1);
                Check(fight.PartyArmor == 2 && fight.ScheduledEnemyHitCount == 2, $"First attack segment resolves once: armor={fight.PartyArmor}, hits={fight.ScheduledEnemyHitCount}, normal={fight.NormalBattleBeatCount}, offset={Field<RhythmHunter.RhythmDemo.FmodRhythmJudge>(fight, "rhythmJudge").JudgementOffsetMs}.");
                Check(fight.TryInterruptEnemyAttack(source) && fight.ScheduledEnemyHitCount == 0, "Interruption cancels all unexecuted segments.");
                Check(!fight.TryInterruptEnemyAttack(source), "Recovery/idle is not a successful interruption.");
                f.Beat(2); Resolve(fight, 2); Check(fight.PartyArmor == 2, "Cancelled segment cannot deal damage.");

                f.Reset(); Quiet(f);
                fight.ScheduleEnemyHits(source, new[] { 3 }, 2);
                Check(fight.TryInterruptEnemyAttack(source), "Telegraphed charge can be interrupted before impact.");
                fight.ScheduleEnemyHits(source, new[] { 1 }, 1, false);
                Check(!fight.TryInterruptEnemyAttack(source) && fight.ScheduledEnemyHitCount == 1, "Uninterruptible action stays scheduled.");
                f.Beat(1); Resolve(fight, 1); Check(fight.PartyArmor == 2, "Uninterruptible hit resolves normally.");

                f.Reset(); Quiet(f);
                fight.ScheduleEnemyHits(source, new[] { 2 }, 1, true, true);
                fight.ScheduleEnemyHits(source, new[] { 3 }, 1);
                Check(fight.TryInterruptEnemyAttack(source) && fight.ScheduledEnemyHitCount == 1, "Interrupting emitter preserves launched projectile.");
                int normalEvents = 0; fight.NormalBattleBeat += _ => normalEvents++;
                f.Beat(1); f.StartChain();
                for (int beat = 2; beat <= 7; beat++) { f.Beat(beat); Resolve(fight, beat); }
                Check(normalEvents == 1 && fight.NormalBattleBeatCount == 1 && fight.ScheduledEnemyHitCount == 1, "Performance is beat zero for normal-combat durations and projectile progress.");
                f.Beat(8);
                Check(normalEvents == 2 && fight.NormalBattleBeatCount == 2, "First resumed ordinary beat advances durations once.");
                Call(fight, "ResolveEnemyCombatWork", 5000 + 100);
                Check(fight.PartyArmor == 3, "Delayed impact waits for complete late input window.");
                Resolve(fight, 8); Check(fight.PartyArmor == 2 && fight.ScheduledEnemyHitCount == 0, "Projectile resumes and lands once.");
                f.Beat(8); Resolve(fight, 8); Check(normalEvents == 2 && fight.PartyArmor == 2, "Duplicate beat does not advance or repeat impact.");

                f.Reset(); Quiet(f);
                fight.ScheduleEnemyHits(source, new[] { 1, 1 }, 1);
                f.Beat(1);
                Field<System.Collections.Generic.HashSet<long>>(fight, "guardedBeats").Add(1);
                Resolve(fight, 1);
                Check(fight.PartyArmor == 3 && fight.BlockedAttackCount == 2, "One-beat guard blocks every simultaneous eligible impact.");

                f.Reset(); Quiet(f);
                fight.ScheduleEnemyHits(source, new[] { 1 }, 1, true, true); f.Beat(1);
                Field<System.Collections.Generic.HashSet<long>>(fight, "guardedBeats").Add(1);
                f.StartChain();
                for (int beat = 2; beat <= 8; beat++) f.Beat(beat);
                Resolve(fight, 8);
                Check(fight.PartyArmor == 3 && fight.BlockedAttackCount == 1,
                    "Active guard and an already-due projectile retain their relationship through performance.");

                f.Reset(); Quiet(f);
                var effectObject = new GameObject("EnemyProjectileTest");
                var effect = effectObject.AddComponent<FightAttackEffect>();
                var owner = effectObject.AddComponent<FightBattleEffect>(); owner.Configure(fight, true, 0);
                effect.Play(Vector3.right, 2);
                f.StartChain(); Call(effect, "Update");
                Check(owner.Frozen && Field<float>(effect, "elapsed") == 0, "Enemy visual travel freezes with Team Skill.");
                for (int beat = 1; beat <= 7; beat++) f.Beat(beat);
                Check(!owner.Frozen, "Enemy visual resumes after chain.");
                Call(fight, "OnDisable"); Check(effectObject == null && fight.ScheduledEnemyHitCount == 0, "Disable removes effects and delayed work.");

                f.Reset(); Quiet(f);
                source.CharacterDefinition.BossPattern.enabled = true;
                Set(fight, "bossActor", source);
                Call(fight, "EnterBossPhase", FightCombatController.BossPhase.Armor, 0L);
                float guardedDamage = (float)Call(fight, "ModifyBossDamage", source, 2f);
                Check(guardedDamage < 2, "Existing fixed armor window mitigates hero damage.");
                f.StartChain(); f.Beat(1);
                Call(fight, "EnterBossPhase", FightCombatController.BossPhase.Break, 1L);
                int breakDuration = fight.BossBeatsRemaining;
                for (int beat = 2; beat <= 7; beat++) f.Beat(beat);
                Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Break && fight.BossBeatsRemaining == breakDuration,
                    "Break applied during performance retains its entire duration at resume (beat zero).");
                f.Beat(8);
                Check(fight.BossBeatsRemaining == breakDuration - 1, "Existing timed vulnerability begins counting on ordinary battle beats.");

                f.Reset(); Quiet(f);
                var next = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Killer.prefab");
                Set(fight.RosterManager, "additionalWaves", new[] { new FightRosterManager.EnemyWave { enemies = new[] { next, next, next } } });
                var heroes = fight.RosterManager.ActiveHeroes.ToArray();
                Set(fight, "teamSkillGauge", 27); fight.ApplyPartyDamage(4, 0);
                Set(fight, "aimedShotActor", f.Gunner); Set(fight, "aimedShotProgress", 2);
                fight.ScheduleEnemyHits(source, new[] { 2 }, 1, true, true);
                KillWave(f); f.Beat(1);
                Check(fight.CurrentWave == 2 && fight.PartyHp == 3 && fight.PartyArmor == 3 && fight.TeamSkillGauge == 27, "Wave restores DEF, preserves HP and MP.");
                Check(fight.RosterManager.ActiveHeroes.SequenceEqual(heroes) && fight.RosterManager.FormationLocked &&
                    !fight.RosterManager.TrySwapHeroes(FightRosterManager.PartyPosition.Front, FightRosterManager.PartyPosition.Back), "Wave never respawns/unlocks the player formation.");
                Check(fight.GetAimedShotProgress(f.Gunner) == 0 && fight.ScheduledEnemyHitCount == 0, "Wave clears streak and old projectiles.");
                KillWave(f); f.Beat(2);
                Check(fight.BattleEnded && fight.BattleWon && !fight.TryStartTeamSkill(), "Last wave ends in victory and blocks further actions.");

                f.Reset(); Quiet(f); f.StartChain(); f.Beat(1); KillWave(f);
                f.Beat(2);
                Check(fight.AwaitingNextWave && fight.CurrentWave == 1, "Wave waits while old Team Skill finishes.");
                for (int beat = 3; beat <= 7; beat++) f.Beat(beat);
                Check(fight.CurrentWave == 2 && fight.RosterManager.ActiveEnemies.All(e => e.CurrentHp == e.MaxHp), "Remaining skills never hit freshly spawned wave.");

                f.Reset(); Quiet(f);
                Set(fight, "endBattleOnZeroHp", false);
                fight.ApplyPartyDamage(99, 0); f.Beat(1);
                Check(fight.PartyHp == 0 && !fight.BattleEnded, "Testing toggle keeps 0 HP playable through damage and beat checks.");
                f.GunInput(2); f.GunInput(3); f.GunInput(4);
                Check(fight.TeamSkillGauge == 3 && f.Enemies[0].CurrentHp == 99, "At 0 HP, valid inputs still gain MP and complete a three-beat shot.");
                f.StartChain();
                Check(fight.TeamSkillRunning, "At 0 HP, Team Skill is still available in testing mode.");
                Set(fight, "endBattleOnZeroHp", true); f.Beat(5);
                Check(fight.BattleEnded && !fight.TeamSkillRunning, "Re-enabling defeat at 0 HP ends the battle on the next check.");

                f.Reset(); Quiet(f); int defeats = 0; fight.BattleLost += () => defeats++;
                fight.ScheduleEnemyHits(source, new[] { 2 }, 1, true, true); f.StartChain();
                fight.ApplyPartyDamage(99, 0); f.Beat(1); fight.ApplyPartyDamage(99, 1);
                Check(fight.BattleEnded && !fight.BattleWon && defeats == 1 && !fight.TeamSkillRunning && !fight.EnemyActionsPaused && fight.ScheduledEnemyHitCount == 0,
                    "Defeat fires once and clears chain, pause and pending work.");
                int mana = fight.TeamSkillGauge; fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Front);
                Check(fight.TeamSkillGauge == mana && !fight.TryStartTeamUltimate(), "No input/Ultimate after defeat.");
                File.WriteAllText(Result, $"PASS: {checks} checks. Action cancellation, delayed impacts, pause and late-window ordering, multi-hit guard, normal-beat clock, effect cleanup, waves/resources/formation, no cross-wave skills, victory and defeat. Deterministic; live FMOD/art remain checkpoint 5.\n");
            }
            catch (Exception exception) { File.WriteAllText(Result, "FAIL: " + exception); Debug.LogException(exception); }
        }
    }
}
