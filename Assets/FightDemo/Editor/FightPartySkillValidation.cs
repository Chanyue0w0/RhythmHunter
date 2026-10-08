using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightPartySkillValidation
    {
        const string Request = "Temp/FightPartySkillValidation.request", Result = "Temp/FightPartySkillValidation.result";
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static int checks;
        static FightPartySkillValidation() => EditorApplication.update += Tick;
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request); Run();
        }

        [MenuItem("Rhythm Hunter/Validate Party Checkpoint 3 - Skills and Fragile")]
        public static void Run()
        {
            checks = 0;
            try
            {
                using (var f = new Fixture(new[] { 0, 1, 2 }))
                {
                    var fight = f.Fight;
                    var front = f.Enemies[0];
                    var middle = f.Enemies[1];
                    var back = f.Enemies[2];
                    Check(front.ApplyFragile() && front.ApplyFragile(), "Fragile can be refreshed.");
                    Check(front.TakeDamage(0) == 0 && front.IsFragile, "Zero damage preserves Fragile.");
                    Check(front.TakeDamage(-1) == 0 && front.IsFragile, "Rejected negative damage preserves Fragile.");
                    Check(front.TakeDamage(1) == 2 && !front.IsFragile, "Repeated application does not stack.");
                    Check(front.TakeDamage(1) == 1 && front.CurrentHp == 97, "Only the first effective damage event doubles.");
                    Check(!f.Warrior.ApplyFragile(), "Fragile is enemy-only.");

                    f.Reset(); front.ApplyFragile(); f.Skill(f.Warrior);
                    Check(front.CurrentHp == 96 && front.IsFragile, "Warrior consumes old Fragile then applies a new one.");
                    f.Reset(); Set(front, "currentHp", 1f); f.Skill(f.Warrior);
                    Check(front.CurrentHp == 0 && !front.IsFragile && !middle.IsFragile, "Killing slash does not transfer Fragile.");
                    Check(!front.ApplyFragile(), "Dead enemies reject status application.");

                    f.Reset(); middle.ApplyFragile(); back.ApplyFragile(); f.Skill(f.Mage);
                    Check(front.CurrentHp == 100 && middle.CurrentHp == 92 && back.CurrentHp == 100, "Mage selects the foremost Fragile enemy, including behind front.");
                    Check(!middle.IsFragile && back.IsFragile, "Focused cast consumes only the chosen target's status.");
                    f.Reset(); f.Skill(f.Mage);
                    Check(f.Enemies.All(e => e.CurrentHp == 98), "Mage without Fragile deals 2 to all.");
                    f.Reset(); middle.ApplyFragile(); Set(middle, "currentHp", 1f); f.Skill(f.Mage);
                    Check(front.CurrentHp == 100 && middle.CurrentHp == 0 && back.CurrentHp == 100, "Focused lethal hit never falls back to area damage.");

                    f.Reset(); back.ApplyFragile(); f.Skill(f.Gunner);
                    Check(front.CurrentHp == 96 && middle.CurrentHp == 100 && back.CurrentHp == 100 && back.IsFragile, "Rear Fragile does not enable piercing.");
                    f.Reset(); front.ApplyFragile(); middle.ApplyFragile(); f.Skill(f.Gunner);
                    Check(front.CurrentHp == 96 && middle.CurrentHp == 96 && back.CurrentHp == 98, "Piercing checks Fragile independently on each target.");
                    Check(!front.IsFragile && !middle.IsFragile, "Each pierced Fragile status is consumed.");
                    f.Reset(); front.ApplyFragile(); Set(front, "currentHp", 1f); f.Skill(f.Gunner);
                    Check(front.CurrentHp == 0 && middle.CurrentHp == 98 && back.CurrentHp == 98, "Piercing mode survives front death during resolution.");

                    f.Reset(); front.CharacterDefinition.BossPattern.enabled = true;
                    front.CharacterDefinition.BossPattern.armorReduction = 1;
                    Call(fight, "AdvanceBossState", 0L);
                    Call(fight, "EnterBossPhase", FightCombatController.BossPhase.Armor, 0L);
                    front.ApplyFragile();
                    Call(fight, "ApplyHeroDamage", Field<int>(fight, "rosterVersion"), f.Warrior, front, .5f);
                    Check(front.CurrentHp == 100 && front.IsFragile, "Fully mitigated damage does not consume Fragile.");
                    Call(fight, "ApplyHeroDamage", Field<int>(fight, "rosterVersion"), f.Warrior, front, 2f);
                    Check(front.CurrentHp == 98 && !front.IsFragile, "Existing mitigation resolves before Fragile doubles effective damage.");

                    f.Reset(); Check(!fight.TryInterruptEnemyAttack(front), "Idle cannot be successfully interrupted.");
                    f.Queue(3); Check(fight.TryInterruptEnemyAttack(front) && !fight.HasPendingEnemyAttack, "Pending enemy attack reports successful cancellation.");
                    Check(!fight.TryInterruptEnemyAttack(front), "A cancelled action cannot grant a second success.");
                    f.Reset(); Set(front.CharacterDefinition, "attackInterruptible", false); f.Queue(3);
                    Check(!fight.TryInterruptEnemyAttack(front) && fight.HasPendingEnemyAttack, "Uninterruptible action remains pending.");
                    f.Resolve(); Check(!fight.TryInterruptEnemyAttack(front), "Recovery after resolution is not interruptible.");
                    Set(front.CharacterDefinition, "attackInterruptible", true);

                    f.Reset(); f.GunInput(1); f.GunInput(2); f.Queue(3); front.ApplyFragile();
                    int hits = 0;
                    Action<FightUnitSlot, float, float> onHit = (_, hp, max) => hits++;
                    front.HealthChanged += onHit;
                    f.GunInput(3);
                    front.HealthChanged -= onHit;
                    Check(front.CurrentHp == 96 && hits == 1 && !front.IsFragile, "Successful interrupt adds 1 to the same bullet, then Fragile doubles 2 to 4 exactly once.");
                    Check(!fight.HasPendingEnemyAttack && fight.TeamSkillGauge == 3, "Interrupt cancels pending hit without extra MP.");
                    f.Resolve(); Check(fight.PartyArmor == 3, "Cancelled pending callback cannot deal damage.");
                    f.Reset(); f.GunInput(1); f.GunInput(2); f.GunInput(3);
                    Check(front.CurrentHp == 99, "Idle target takes only 1 base damage.");
                    f.Reset(); Set(front.CharacterDefinition, "attackInterruptible", false);
                    f.GunInput(1); f.GunInput(2); f.Queue(3); front.ApplyFragile(); f.GunInput(3);
                    Check(front.CurrentHp == 98 && fight.HasPendingEnemyAttack, "Uninterruptible target gets no bonus, but still consumes Fragile normally.");
                    Set(front.CharacterDefinition, "attackInterruptible", true);
                    f.Reset(); f.Queue(3); f.Skill(f.Gunner);
                    Check(fight.HasPendingEnemyAttack && front.CurrentHp == 96, "Gunner Team Skill does not inherit the Basic interrupt.");

                    f.Reset(); f.Queue(3); Call(fight, "BeginEnemyPause");
                    Check(fight.TryInterruptEnemyAttack(front) && fight.EnemyActionsPaused, "Interruption never lifts Team Skill's enemy pause.");
                    f.Reset(); front.CharacterDefinition.BossPattern.enabled = true;
                    Call(fight, "AdvanceBossState", 0L);
                    foreach (var phase in new[] { FightCombatController.BossPhase.Charge, FightCombatController.BossPhase.Rage })
                    {
                        Call(fight, "EnterBossPhase", phase, 0L);
                        Check(fight.TryInterruptEnemyAttack(front), "Active windup / sequence is interruptible.");
                        Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Neutral && !fight.IsEnemyAttackBeat(1), "Remaining hits of the interrupted sequence are discarded.");
                    }

                    f.Reset(); Set(front, "currentHp", 1f);
                    f.StartChain(); for (int beat = 1; beat <= 7; beat++) f.Beat(beat);
                    Check(front.CurrentHp == 0 && middle.CurrentHp == 94 && back.CurrentHp == 98, "Later casters retarget living enemies after a kill.");
                    f.Reset(); foreach (var enemy in f.Enemies) enemy.TakeDamage(100);
                    fight.ApplyPartyDamage(4, 0); f.StartChain(); for (int beat = 1; beat <= 4; beat++) f.Beat(beat);
                    Check(fight.PartyArmor == 3 && fight.PartyHp == 3, "Mage restores armor even when no targets remain; never heals HP.");
                    f.Beat(5); f.Beat(6); f.Beat(7);
                    Check(!fight.TeamSkillRunning && !fight.EnemyActionsPaused, "Empty battlefield does not strand the chain.");

                    f.Reset(); front.ApplyFragile(); Call(fight, "OnDisable");
                    Check(!front.IsFragile && !fight.TeamSkillRunning, "Teardown clears enemy statuses and chain state.");
                    f.Reset();
                    Action<FightUnitSlot, float, float> rebuild = (_, hp, max) => Call(fight, "RebuildRosterAndResetCombat");
                    front.HealthChanged += rebuild;
                    f.StartChain(); f.Beat(1); f.Beat(2);
                    front.HealthChanged -= rebuild;
                    Check(!fight.TeamSkillRunning && !front.IsFragile, "Roster invalidation during damage cannot resurrect a cancelled chain.");

                    f.Reset(); f.StartChain(); f.Beat(1); f.Beat(2);
                    var presenter = f.Components.OfType<FightScenePresenter>().Single();
                    var health = Field<Text>(presenter, "healthText");
                    var hud = fight.gameObject.AddComponent<FightDefenseHud>();
                    hud.Configure(fight, health.canvas, health.font);
                    Check(Field<Text[]>(hud, "enemyLabels").Any(t => t.text.Contains("◆ x2")), "Enemy HUD shows the Fragile marker.");
                    Call(presenter, "UpdateTeamSkillHud");
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        typeof(FightScene3BeatValidation).GetMethod("CaptureHud", BindingFlags.Static | BindingFlags.NonPublic)
                            .Invoke(null, new object[] { health.canvas, "Temp/FightPartySkill-preview.png" });
                    f.Beat(3); f.Beat(4); hud.Refresh();
                    Check(Field<Text[]>(hud, "enemyLabels").All(t => !t.text.Contains("◆ x2")), "Consumed Fragile disappears from the HUD.");
                    UnityEngine.Object.DestroyImmediate(hud);
                }
                ValidateChain(new[] { 0, 1, 2 }, new[] { 86f, 100f, 100f });
                ValidateChain(new[] { 0, 2, 1 }, new[] { 92f, 96f, 96f });
                ValidateChain(new[] { 1, 0, 2 }, new[] { 92f, 96f, 96f });
                File.WriteAllText(Result, $"PASS: {checks} checks. Fragile once/nonstacking/zero/lethal/mitigation; skill modes and retargeting; shared armor-only passive; same-bullet interruption bonus; idle/recovery/uninterruptible; pending and test-boss sequence cancellation; actual sequential chains 14/0/0 and 8/4/4; no-target/teardown/roster invalidation; status HUD.\nFull Wave flow, combat projectiles, future enemy behaviors and live FMOD/device input remain checkpoint 4/5.\n");
                Debug.Log("FIGHT_PARTY_SKILL_VALIDATION_PASS");
            }
            catch (Exception exception) { File.WriteAllText(Result, "FAIL: " + exception); Debug.LogException(exception); }
        }

        static void ValidateChain(int[] order, float[] hp)
        {
            using var f = new Fixture(order);
            f.Fight.ApplyPartyDamage(f.Fight.MaxPartyArmor + 1, 0);
            float beforeHp = f.Fight.PartyHp;
            int steps = 0;
            f.Fight.TeamSkillStepResolved += actor =>
            {
                Check(actor == f.Fight.GetHeroForCommand((FightInputRouter.HeroCommand)steps).UnitSlot, "Each actual chain step follows formation.");
                if (actor == f.Mage) Check(f.Fight.PartyArmor == f.Fight.MaxPartyArmor, "Mage passive runs after its own resolution, in any position.");
                steps++;
            };
            f.StartChain();
            Check(f.Fight.TeamSkillGauge == 0 && f.Fight.EnemyActionsPaused, "Start consumes 60 MP and pauses enemies.");
            foreach (int start in new[] { 1, 3, 5 })
            {
                var before = f.Enemies.Select(e => e.CurrentHp).ToArray();
                int beforeSteps = steps;
                f.Beat(start); f.Beat(start);
                Check(f.Enemies.Select(e => e.CurrentHp).SequenceEqual(before) && steps == beforeSteps,
                    "Preparation beat does not deal damage or resolve a passive.");
                Check(f.Fight.TeamSkillBeatsRemaining == 2 && f.Fight.EnemyActionsPaused,
                    "Each hero reserves two beats while enemies remain paused.");
                f.Beat(start + 1); f.Beat(start + 1);
                Check(steps == beforeSteps + 1 && f.Fight.TeamSkillBeatsRemaining == 1,
                    "Second beat resolves exactly once and retains the final performance beat.");
            }
            Check(f.Enemies.Select(e => e.CurrentHp).SequenceEqual(hp), "Formation damage totals.");
            Check(steps == 3 && f.Fight.TeamSkillGauge == 0, "No duplicate cast or MP from skill effects.");
            Check(f.Fight.PartyHp == beforeHp && f.Fight.PartyArmor == f.Fight.MaxPartyArmor, "No HP healing; shared armor ends full.");
            Check(f.Fight.TeamSkillRunning, "Last caster retains its full performance duration.");
            f.Beat(7);
            Check(!f.Fight.TeamSkillRunning && !f.Fight.EnemyActionsPaused && f.Fight.TeamUltimateCharge == 1, "Chain finishes and preserves the existing Ultimate charge.");
        }

        sealed class Fixture : IDisposable
        {
            readonly Scene scene;
            readonly FmodBeatClock clock;
            readonly FmodRhythmJudge judge;
            public readonly MonoBehaviour[] Components;
            public readonly FightCombatController Fight;
            public readonly FightUnitSlot[] Enemies;
            public FightUnitSlot Warrior => Hero("LongswordWarrior");
            public FightUnitSlot Mage => Hero("EnergyMage");
            public FightUnitSlot Gunner => Hero("SkeletonGunner");
            FightUnitSlot Hero(string id) => Fight.RosterManager.ActiveHeroes.Single(h => h.CharacterDefinition.CharacterId == id);
            public Fixture(int[] order)
            {
                scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/FightScene3.unity");
                Components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                Fight = Components.OfType<FightCombatController>().Single();
                clock = Field<FmodBeatClock>(Fight, "beatClock"); judge = Field<FmodRhythmJudge>(Fight, "rhythmJudge");
                var heroes = Fight.RosterManager.HeroPrefabs.ToArray();
                var enemy = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Killer.prefab");
                Call(Fight, "SubscribeDependencies");
                Fight.RosterManager.SetRoster(order.Select(i => heroes[i]).ToArray(), new[] { enemy, enemy, enemy });
                Enemies = Fight.RosterManager.ActiveEnemies.OrderByDescending(e => e.SlotIndex).ToArray();
                Check(Fight.TryBeginBattle(), "Prepared battle starts.");
                Reset();
            }
            public void Reset()
            {
                Set(clock, "hasAnchor", true); Set(clock, "latestBeat", Snapshot(0));
                judge.Configure(clock, 120, 0); judge.ResetDuplicateTracking();
                Call(Fight, "RebuildRosterAndResetCombat");
                foreach (var enemy in Enemies) { enemy.CharacterDefinition.BossPattern.enabled = false; Set(enemy, "maxHp", 100f); enemy.RestoreFullHealth(); }
            }
            public void Skill(FightUnitSlot actor) => Call(Fight, "PerformConfiguredAbility",
                new[] { Fight.FrontHero, Fight.SecondHero, Fight.ThirdHero }.Single(h => h.UnitSlot == actor),
                FightCombatController.ActionType.Skill, actor.SkillBehavior, actor.SkillPower, 0L);
            public void GunInput(int beat)
            {
                var command = (FightInputRouter.HeroCommand)Gunner.SlotIndex;
                var result = (FmodRhythmJudge.Result)Call(judge, "JudgeTimelinePosition", 1000 + beat * 500);
                Call(Fight, "ApplyHeroJudgement", Fight.GetHeroForCommand(command), command, result);
            }
            public void Queue(int beat) => Call(Fight, "QueueEnemyAttack", Snapshot(beat));
            public void Resolve() => Call(Fight, "ResolvePendingEnemyAttack", Field<int>(Fight, "pendingAttackRosterVersion"),
                Field<long>(Fight, "pendingEnemyActionId"), Field<FightUnitSlot>(Fight, "pendingEnemyAttacker"));
            public void Beat(int beat) { Set(clock, "latestBeat", Snapshot(beat)); Call(Fight, "OnBeat", Snapshot(beat)); }
            public void StartChain() { Set(Fight, "teamSkillGauge", 60); Check(Fight.TryStartTeamSkill(), "Team Skill starts with 60 MP."); }
            public void Dispose() => EditorSceneManager.ClosePreviewScene(scene);
            static FmodBeatClock.BeatSnapshot Snapshot(int beat) => new(beat, beat / 4 + 1, beat % 4 + 1, 1000 + beat * 500, 120, 4, 4);
        }
        static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, Private).GetValue(obj);
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
        static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
        static void Check(bool valid, string message) { checks++; if (!valid) throw new InvalidOperationException(message); }
    }
}
