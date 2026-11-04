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
    public static class FightTeamSkillBurstValidation
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static object Get(object obj, string field) => obj.GetType().GetField(field, Flags).GetValue(obj);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
        private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);

        [MenuItem("Rhythm Hunter/Validate Team Skill Burst")]
        public static void Run()
        {
            int checks = 0;
            void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
            var window = new FightTeamSkillBeatWindow();
            window.Begin(10, 16);
            Check(!window.TryAccept(10, 9.99, .2), "Queued input cannot cast before the burst.");
            Check(!window.TryAccept(10.25, 10.25, .2), "Off-beat does not cast.");
            Check(window.TryAccept(10.5, 10.5, .2), "Off-beat does not consume the next valid half beat.");
            Check(!window.TryAccept(10.5, 10.5, .2), "Repeated input cannot double-cast.");
            window.Begin(10, 16);
            for (int i = 0; i < 32; i++)
                Check(window.TryAccept(10 + i * .5, 10 + i * .5, .2), "Every one of 32 half-beat slots accepts once.");
            Check(!window.TryAccept(26, 26, .2) && !window.IsOpen(26), "Exact end restores normal mode.");
            window.Begin(100, 16);
            Check(window.TryAccept(100, 100.1, .2), "New activation clears prior consumed slots; calibrated time is used.");

            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Main_FightScene.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                Set(fight.RosterManager, "requireBattlePreparation", false);
                Set(fight, "showEnemyDamageNumbers", false); Set(fight, "showStatusIcons", false);
                var heroes = new[] { "LongswordWarrior", "Mage", "Skeleton" }.Select(n =>
                    AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>($"Assets/FightDemo/Prefabs/ArtBattle/{n}.prefab")).ToArray();
                var enemy = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Killer.prefab");
                fight.RosterManager.SetRoster(heroes, new[] { enemy, enemy, enemy });
                Call(fight, "RebuildRosterAndResetCombat");
                Set(fight.BeatClock, "hasAnchor", true);
                Set(fight.BeatClock, "latestBeat", new FmodBeatClock.BeatSnapshot(0, 1, 1, 0, 120, 4, 4));
                Set(Get(fight, "rhythmJudge"), "perfectWindowMs", 100f);
                var heroSlots = fight.RosterManager.ActiveHeroes.ToArray();
                var animators = heroSlots.Select(hero => hero.CombatAnimator).ToArray();
                foreach (var hero in heroSlots)
                {
                    var sequence = hero.CombatAnimator.FindSequence(FightCharacterCombatAnimator.CombatAnimation.Skill);
                    double length = Enumerable.Range(0, sequence.Frames.Count).Sum(i => (double)sequence.GetFrameBeats(i));
                    Check(sequence.UseFrameBeatDurations && length <= 1.00001, "All three Prefab skill sequences fit within one beat.");
                    Set(hero, "combatAnimator", null); // Deterministic fallback resolution; authored timing checked above.
                }
                foreach (var target in fight.RosterManager.ActiveEnemies) { Set(target, "maxHp", 100f); target.RestoreFullHealth(); }
                Set(fight, "latestCombatBeat", 0L); Set(fight, "teamSkillGauge", fight.TeamSkillGaugeMax);
                Check(fight.TryStartTeamSkill() && fight.TeamSkillGauge == 0 && fight.EnemyActionsPaused, "Start consumes mana and freezes enemies.");
                var order = new System.Collections.Generic.List<FightUnitSlot>();
                fight.TeamSkillStepResolved += order.Add;
                void Press(double beat) => Call(fight, "SubmitTeamSkillBurstAt", beat, beat);
                Press(1.25); Check(order.Count == 0, "Wrong timing has no skill effect.");
                Press(1.5); Press(1.5); Press(2); Press(2.5);
                Check(order.Count == 9 && order[0] == fight.FrontHero.UnitSlot && order[1] == fight.SecondHero.UnitSlot && order[2] == fight.ThirdHero.UnitSlot,
                    "Each successful input casts all three heroes; duplicate input casts none.");
                Check(fight.NormalBattleBeatCount == 0 && fight.TeamSkillGauge == 0, "Burst casts do not spend status beats or award MP.");
                Call(fight, "UpdateTeamSkillBurstAt", 16.9);
                Check(fight.TeamSkillRunning, "Mode remains reserved until its full duration ends.");
                Call(fight, "UpdateTeamSkillBurstAt", 100000d);
                Check(!fight.TeamSkillRunning && !fight.EnemyActionsPaused, "After duration and projectiles complete, enemies resume.");
                Call(fight, "ResetTeamSkill");
                Check(!fight.TeamSkillRunning && !fight.EnemyActionsPaused, "Restart clears burst and pause state.");
                for (int i = 0; i < heroSlots.Length; i++) Set(heroSlots[i], "combatAnimator", animators[i]);
                Set(fight, "teamSkillGauge", fight.TeamSkillGaugeMax);
                Check(fight.TryStartTeamSkill(), "A second activation starts cleanly.");
                order.Clear();
                Press(1); Press(1.5); Press(1.5);
                Call(fight, "AdvanceBurstFrames", 3d);
                Check(order.Count == 6 && heroSlots.All(hero => order.Count(resolved => resolved == hero) == 2),
                    "Two rapid inputs preserve two authored impact events for every hero; duplicate adds none.");
                Call(fight, "ResetTeamSkill");
                Call(fight, "AdvanceBurstFrames", 100d);
                Check(order.Count == 6 && ((System.Collections.IList)Get(fight, "burstFrameEvents")).Count == 0,
                    "Restart cancels outstanding cast events without synthesizing hits.");
                foreach (var hero in heroSlots) Set(hero, "combatAnimator", null);
                var enemies = fight.RosterManager.ActiveEnemies.OrderByDescending(enemySlot => enemySlot.SlotIndex).ToArray();
                foreach (var target in enemies) target.RestoreFullHealth();
                Set(fight, "teamSkillGauge", fight.TeamSkillGaugeMax);
                Check(fight.TryStartTeamSkill(), "Overkill test starts a fresh burst.");
                int deaths = 0;
                foreach (var target in enemies) target.HealthChanged += (_, hp, __) => { if (hp == 0) deaths++; };
                enemies[0].TakeDamage(200);
                Check((FightUnitSlot)Call(fight, "FindBurstSingleTarget") == enemies[1], "Single target advances past a defeated front enemy.");
                enemies[1].TakeDamage(200); enemies[2].TakeDamage(200);
                Check(deaths == 3 && (FightUnitSlot)Call(fight, "FindBurstSingleTarget") == enemies[2], "All defeated: retain the last victim for single-target hits.");
                Check(((FightUnitSlot[])Call(fight, "GetTeamSkillEffectTargets", fight.ThirdHero.UnitSlot)).Length == 3,
                    "Area casts retain all enemies present at burst entry.");
                Set(fight, "showEnemyDamageNumbers", true);
                int numbers = fight.ActiveDamageNumberCount;
                enemies[2].TakeDamage(2);
                Check(fight.ActiveDamageNumberCount > numbers && enemies[2].CurrentHp == 0 && deaths == 3,
                    "Overkill displays damage without negative HP or repeated death notifications.");
                Check(enemies[2].AttachSlime(1, 4, fight.SecondHero.UnitSlot), "Slime remains usable on retained targets.");
                enemies[2].TakeDamage(1);
                Check(enemies[2].SlimeDetonationPending, "Direct overkill can still trigger attached slime.");
                enemies[2].AdvanceFragmentDetonations(.5f);
                Check(deaths == 3, "Overkill explosion does not award another kill.");
                Call(fight, "CheckWaveCompletion");
                Check(!fight.AwaitingNextWave && fight.TeamSkillRunning, "Defeating all enemies does not interrupt burst input.");
                order.Clear(); Press(1);
                Check(order.Count == 3, "All three heroes can still cast after every enemy reaches zero HP.");
                Call(fight, "UpdateTeamSkillBurstAt", 100000d);
                Check(!fight.TeamSkillRunning && !fight.IsBurstRetainedTarget(enemies[2]) && Call(fight, "FindBurstSingleTarget") == null,
                    "Completion releases overkill targets without reviving enemies.");
                int after = fight.ActiveDamageNumberCount;
                enemies[2].TakeDamage(2);
                Check(fight.ActiveDamageNumberCount == after && deaths == 3, "Dead enemies stop accepting damage after burst.");
                string message = $"PASS: {checks} Team Skill Burst checks.";
                File.WriteAllText("Temp/FightTeamSkillBurstValidation.result", message);
                Debug.Log(message);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
