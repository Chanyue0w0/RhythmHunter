using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightWaveTransitionValidation
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static FightWaveTransitionValidation() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/WaveTransitionValidation.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(request); } catch (IOException) { return; }
            try { Run(); } catch (Exception e) { File.WriteAllText("Temp/WaveTransitionValidation.result", "FAIL: " + e); Debug.LogException(e); }
        }
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
        static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Run()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                Set(fight, "enableSuccessfulInputSound", false);
                var roster = fight.RosterManager;
                roster.SpawnConfiguredRoster();
                Call(fight, "RebuildRosterAndResetCombat");
                Set(roster, "<BattleStarted>k__BackingField", true);
                Set(fight, "<AwaitingNextWave>k__BackingField", true);
                var enemy = roster.ActiveEnemies.First();
                var immune = enemy.ActorInstance.AddComponent<FightTransitionInvulnerabilityVisual>();
                immune.Initialize(enemy, null, "IMMUNE", Color.cyan, .12f, .25f);
                var label = enemy.ActorInstance.GetComponentsInChildren<TextMesh>(true).Single(t => t.name == "TransitionInvulnerabilityLabel");
                Check(!label.gameObject.activeSelf, "Immune label appeared merely from entry");
                enemy.TakeDamage(1);
                Check(label.gameObject.activeSelf, "Blocked damage did not show immune label");
                Set(immune, "remaining", 0f); Call(immune, "Refresh");
                Check(!label.gameObject.activeSelf, "Immune label did not expire");
                var warrior = fight.RosterManager.ActiveHeroes.First();
                Call(fight, "PlayAnimatedHeroAction", warrior, enemy, FightCombatController.ActionType.LightAttack, 1f);
                Check(label.gameObject.activeSelf, "Transition attack zero-damage path missed immune feedback");
                float hp = enemy.CurrentHp;
                Set(enemy, "fragile", true);
                foreach (FightUnitSlot.DamageKind kind in Enum.GetValues(typeof(FightUnitSlot.DamageKind)))
                    Check(enemy.TakeDamage(1, kind) == 0 && enemy.CurrentHp == hp && enemy.IsFragile, "Transition damage consumed HP/fragile: " + kind);
                var mage = fight.GetHeroForCommand(FightInputRouter.HeroCommand.Middle);
                Set(fight, "partyHp", 1f); Set(fight, "partyArmor", 0f);
                Call(fight, "HealPartyHealth", mage.UnitSlot, 1f);
                Check(fight.PartyHp == 2, "Healing blocked during transition");
                Check(fight.RecoverPartyArmor(FightCharacterDefinition.ArmorRestoreMode.Full) > 0, "Armor recovery blocked");
                var accepted = new FmodRhythmJudge.Result(FmodRhythmJudge.Grade.Perfect, 0, 1000, 1000,
                    new FmodBeatClock.NearestBeat(100, 26, 1, 1000, 0), false, "Validation");
                int mana = fight.TeamSkillGauge;
                Call(fight, "ApplyHeroJudgement", mage, FightInputRouter.HeroCommand.Middle, accepted);
                Check(fight.TeamSkillGauge == Mathf.Min(mana + 1, fight.TeamSkillGaugeMax), "Ordinary input did not grant one MP");
                var guards = (System.Collections.Generic.HashSet<long>)typeof(FightCombatController).GetField("guardedBeats", Private).GetValue(fight);
                Check(guards.Contains(100), "Guard did not arm during transition");
                mana = fight.TeamSkillGauge;
                int feedback = 0; fight.HeroCalled += _ => feedback++;
                Call(fight, "ApplyTransitionRhythmJudgement", accepted);
                Check(feedback == 1 && fight.TeamSkillGauge == mana && !fight.TeamSkillRunning, "A activated skill, changed MP, or missed feedback");
                var shooter = fight.ThirdHero.UnitSlot;
                Call(fight, "PlayAnimatedHeroAction", shooter, enemy, FightCombatController.ActionType.LightAttack, 1f);
                Set(fight, "<AwaitingNextWave>k__BackingField", false);
                var fire = (Action)typeof(FightCharacterCombatAnimator).GetField("attackEffectCallback", Private).GetValue(shooter.CombatAnimator);
                fire?.Invoke();
                var projectile = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightBeatProjectile>(true)).FirstOrDefault();
                Check(projectile != null, "Transition shot did not spawn projectile");
                var impact = (Action)typeof(FightBeatProjectile).GetField("impact", Private).GetValue(projectile);
                impact.Invoke();
                Check(enemy.CurrentHp == hp && enemy.IsFragile && enemy.BleedBeatsRemaining == 0, "Delayed transition projectile damaged after immunity ended");
                Check(enemy.TakeDamage(.5f) > 0 && !enemy.IsFragile, "Damage did not resume after transition");
                ValidateDeath(fight, roster);
                ValidateWaveWait(fight, roster);
                File.WriteAllText("Temp/WaveTransitionValidation.result", "PASS: transition input/immunity and death regression; two unique subsequent beats before next wave; one armor refill/event per wave; MP/HP preserved; heroes retained; final wave victory without extra spawn.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void ValidateWaveWait(FightCombatController fight, FightRosterManager roster)
        {
            Set(roster, "<BattleStarted>k__BackingField", false);
            roster.SpawnConfiguredRoster();
            Call(fight, "RebuildRosterAndResetCombat");
            Set(roster, "stageWaves", new[] { roster.EnemyPrefabs.ToArray(), roster.EnemyPrefabs.ToArray() });
            Set(roster, "<BattleStarted>k__BackingField", true);
            Set(fight, "latestCombatBeat", 100L);
            Set(fight, "partyArmor", 0f);
            Set(fight, "teamSkillGauge", 17);
            Set(fight, "waveTransitionWaitBeats", 2);
            var heroes = roster.ActiveHeroes.Select(h => h.ActorInstance).ToArray();
            float hp = fight.PartyHp;
            int clears = 0, starts = 0;
            fight.WaveCleared += _ => clears++;
            fight.WaveStarted += _ => starts++;
            foreach (var enemy in roster.ActiveEnemies) enemy.TakeDamage(1000);
            Call(fight, "CheckWaveCompletion");
            Check(fight.AwaitingNextWave && fight.WaveWaitBeatsRemaining == 2 && clears == 1, "Wave clear did not start two-beat wait once");
            Check(fight.PartyArmor == fight.MaxPartyArmor && fight.TeamSkillGauge == 17 && fight.PartyHp == hp, "Wave recovery changed HP/MP or missed full armor");
            Call(fight, "AdvanceWaveBoundary");
            Check(fight.CurrentWave == 1 && fight.WaveWaitBeatsRemaining == 2, "Clear beat consumed wait");
            Set(fight, "latestCombatBeat", 101L);
            Call(fight, "AdvanceWaveBoundary");
            Call(fight, "AdvanceWaveBoundary");
            Check(fight.CurrentWave == 1 && fight.WaveWaitBeatsRemaining == 1 && clears == 1, "Duplicate beat consumed gap or repeated refill");
            Set(fight, "latestCombatBeat", 102L);
            Call(fight, "AdvanceWaveBoundary");
            Check(fight.CurrentWave == 2 && !fight.AwaitingNextWave && starts == 1 && fight.TeamSkillGauge == 17, "Second beat did not spawn next wave or MP changed");
            Check(roster.ActiveHeroes.Select(h => h.ActorInstance).SequenceEqual(heroes), "Wave transition respawned heroes");
            foreach (var enemy in roster.ActiveEnemies) enemy.TakeDamage(1000);
            Call(fight, "AdvanceWaveBoundary");
            Check(fight.BattleWon && fight.BattleEnded && starts == 1 && clears == 2, "Final wave failed victory or spawned extra wave");
        }

        private static void ValidateDeath(FightCombatController fight, FightRosterManager roster)
        {
            var enemies = roster.ActiveEnemies.Where(e => e.HasCharacter).ToArray();
            Check(enemies.Length >= 2, "Death validation needs two enemies");
            var first = enemies[0]; var second = enemies[1];
            var positions = enemies.Select(e => e.transform.position).ToArray();
            int deaths = 0;
            first.EnemyDefeated += (_, _) => deaths++;
            first.TakeDamage(1000);
            Check(first.ActorInstance != null, "Actor removed before on-hit callbacks could finish");
            Call(first, "RemoveDefeatedEnemyPresentation");
            first.TakeDamage(1);
            Call(first, "RemoveDefeatedEnemyPresentation");
            Check(deaths == 1 && first.ActorInstance == null && first.CurrentHp == 0 && first.HasCharacter, "Direct death cleanup/event failed");
            Set(fight, "nextTeamSkillBeat", 100L);
            Call(fight, "BeginBurstTargets");
            int burstDeaths = 0;
            second.EnemyDefeated += (_, _) => burstDeaths++;
            second.TakeDamage(1000, FightUnitSlot.DamageKind.DamageOverTime);
            Call(second, "RemoveDefeatedEnemyPresentation");
            Check(second.ActorInstance != null && fight.IsBurstRetainedTarget(second), "Burst corpse disappeared early");
            second.TakeDamage(1);
            Check(burstDeaths == 1, "Overkill repeated defeat event");
            Set(fight, "nextTeamSkillBeat", long.MaxValue);
            Call(fight, "ClearBurstTargets");
            Call(second, "RemoveDefeatedEnemyPresentation");
            Check(second.ActorInstance == null, "Burst corpse survived skill completion");
            for (int i = 0; i < enemies.Length; i++)
                Check(enemies[i].transform.position == positions[i], "Death moved logical formation slots");
            var hero = roster.ActiveHeroes.First();
            var actor = hero.ActorInstance;
            Set(hero, "currentHp", 0f);
            Call(hero, "RemoveDefeatedEnemyPresentation");
            Check(hero.ActorInstance == actor, "Enemy cleanup removed hero");
            Set(roster, "<BattleStarted>k__BackingField", false);
            roster.SpawnConfiguredRoster();
            Check(roster.ActiveEnemies.Where(e => e.HasCharacter).All(e => e.ActorInstance != null && e.CurrentHp > 0), "Respawn failed after death removal");
            first = roster.ActiveEnemies.First(e => e.HasCharacter);
            first.TakeDamage(1000, FightUnitSlot.DamageKind.DamageOverTime);
            Call(first, "RemoveDefeatedEnemyPresentation");
            Check(first.ActorInstance == null, "Ordinary bleed death failed to remove actor");
        }
    }
}
