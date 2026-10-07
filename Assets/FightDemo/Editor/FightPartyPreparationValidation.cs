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
    /// <summary>Runs in an isolated preview scene; never saves or replaces the user's open scene.</summary>
    [InitializeOnLoad]
    public static class FightPartyPreparationValidation
    {
        private const string Request = "Temp/FightPartyPreparationValidation.request";
        private const string Result = "Temp/FightPartyPreparationValidation.result";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int assertions;

        static FightPartyPreparationValidation() => EditorApplication.update += Tick;

        private static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            File.Delete(Request);
            Run();
        }

        [MenuItem("Rhythm Hunter/Validate Party Checkpoint 1 - Preparation")]
        public static void Run()
        {
            assertions = 0;
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/FightScene3.unity");
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var fight = components.OfType<FightCombatController>().Single();
                var roster = fight.RosterManager;
                Call(fight, "SubscribeDependencies");
                roster.SpawnConfiguredRoster();
                // Preview scenes do not execute Awake. Exercise the same runtime event
                // subscription, so swaps must refresh combat data without test-side rebuilds.
                Check(fight.AwaitingBattleStart, "Scene must require preparation.");
                var originals = roster.HeroPrefabs.ToArray();
                var enemies = roster.EnemyPrefabs.ToArray();
                Check(originals.Select(p => p.CharacterId).SequenceEqual(new[] { "LongswordWarrior", "EnergyMage", "SkeletonGunner" }), "Initial formation.");
                Check(originals.Select(p => p.MaxHp).SequenceEqual(new[] { 2f, 1f, 1f }), "Base HP.");
                Check(originals.Select(p => p.Defense).SequenceEqual(new[] { 2, 1, 1 }), "Base DEF.");
                Check(originals.All(p => p.TeamSkillManaCost == 20), "Each character contributes 20 MP.");
                Check(originals.All(p => p.BasicAbilityType != FightCharacterDefinition.AbilityBehavior.HealParty &&
                    p.SkillType != FightCharacterDefinition.AbilityBehavior.HealParty), "No legacy hero healing.");

                // Exercise every permutation using only the public swap API.
                int[][] orders = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
                    new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
                foreach (var order in orders)
                {
                    for (int position = 0; position < 3; position++)
                    {
                        int from = Array.IndexOf(roster.HeroPrefabs.ToArray(), originals[order[position]]);
                        if (from != position)
                            Check(roster.TrySwapHeroes((FightRosterManager.PartyPosition)from, (FightRosterManager.PartyPosition)position), "Swap accepted before start.");
                    }
                    Check(fight.MaxPartyHp == 4 && fight.PartyHp == 4, "One shared full HP pool.");
                    Check(fight.MaxPartyArmor == (order[0] == 0 ? 3 : 1), "Frontline-only armor with one passive bonus.");
                    Check(fight.PartyArmor == fight.MaxPartyArmor, "Preparation armor recalculated.");
                    Check(fight.TeamSkillGaugeMax == 60 && fight.TeamSkillGauge == 0, "MP capacity is contribution sum, not starting MP.");
                    for (int position = 0; position < 3; position++)
                    {
                        var slot = fight.GetHeroForCommand((FightInputRouter.HeroCommand)position).UnitSlot;
                        Check(slot == roster.GetHeroAtPosition((FightRosterManager.PartyPosition)position), "Input resolves authored slot.");
                        Check(slot.CharacterDefinition.CharacterId == originals[order[position]].CharacterId, "Identity follows the swap.");
                        Check(slot.NormalAbilityBehavior == originals[order[position]].BasicAbilityType, "Ability follows character.");
                        Check(slot.NormalAbilityPower == originals[order[position]].BasicAbilityPower, "Power follows character.");
                    }
                }
                roster.SetRoster(originals, enemies);
                for (int repeat = 0; repeat < 20; repeat++)
                {
                    Check(roster.TrySwapHeroes(FightRosterManager.PartyPosition.Front, FightRosterManager.PartyPosition.Middle), "Repeated swap.");
                    Check(fight.MaxPartyArmor == (repeat % 2 == 0 ? 1 : 3), "Passive cannot accumulate.");
                }
                Check(!roster.TrySwapHeroes((FightRosterManager.PartyPosition)(-1), FightRosterManager.PartyPosition.Front), "Invalid position rejected.");
                Check(!roster.TrySwapHeroes(FightRosterManager.PartyPosition.Front, FightRosterManager.PartyPosition.Front), "Self swap rejected.");
                var heroHp = roster.ActiveHeroes.Select(h => h.CurrentHp).ToArray();
                Check(fight.ApplyPartyDamage(99, 0) == 0 && fight.PartyHp == 4, "No damage during preparation.");
                for (int beat = 0; beat < 16; beat++)
                    Call(fight, "OnBeat", new FmodBeatClock.BeatSnapshot(beat, beat / 4 + 1, beat % 4 + 1, beat * 500, 120, 4, 4));
                Check(!fight.HasPendingEnemyAttack && !fight.BossActive, "No enemy behavior advances during preparation.");
                int inputEvents = 0;
                fight.HeroCalled += _ => inputEvents++;
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Front);
                Check(inputEvents == 0 && fight.TeamSkillGauge == 0, "Basic input is disabled before start.");
                Check(!fight.TryStartTeamSkill() && !fight.TryStartTeamUltimate(), "No skills before start.");

                // Verify UI values and idempotent construction before pressing its real button.
                var presenter = components.OfType<FightScenePresenter>().Single();
                var health = Field<Text>(presenter, "healthText");
                var panel = fight.gameObject.AddComponent<FightFormationPanel>();
                panel.Configure(fight, health.canvas, health.font);
                var root = Field<RectTransform>(panel, "panel");
                panel.Configure(fight, health.canvas, health.font);
                Check(Field<RectTransform>(panel, "panel") == root, "Formation UI is not duplicated.");
                var buttons = Field<Button[]>(panel, "buttons");
                buttons[0].onClick.Invoke();
                Check(fight.MaxPartyArmor == 1 && Field<Text[]>(panel, "positions")[0].text.Contains("ENERGY MAGE"), "Swap button refreshes UI and armor.");
                buttons[0].onClick.Invoke();
                Check(Field<Text>(panel, "summary").text.Contains("HP 4   DEF 3   MP 0/60"), "Shared resource preview.");
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    typeof(FightScene3BeatValidation).GetMethod("CaptureHud", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { health.canvas, "Temp/FightPartyPreparation-preview.png" });
                buttons[3].onClick.Invoke();
                Check(!fight.AwaitingBattleStart && roster.FormationLocked, "Start locks formation.");
                Check(!root.gameObject.activeSelf && Field<Text>(panel, "lockedLabel").gameObject.activeSelf, "Battle shows lock status.");
                Check(!fight.TryBeginBattle(), "Start cannot reset a running battle.");
                var actors = roster.ActiveHeroes.Select(h => h.CharacterDefinition).ToArray();
                var currentEnemy = roster.ActiveEnemies.First().CharacterDefinition;
                Check(!roster.TrySwapHeroes(FightRosterManager.PartyPosition.Front, FightRosterManager.PartyPosition.Back), "In-battle swap blocked.");
                roster.SetRoster(originals.Reverse().ToArray(), enemies);
                roster.SetRoster(originals.Reverse().ToArray(), enemies, false);
                roster.SpawnConfiguredRoster();
                Check(actors.SequenceEqual(roster.ActiveHeroes.Select(h => h.CharacterDefinition)), "SetRoster and respawn cannot bypass lock.");
                Check(roster.HeroPrefabs.SequenceEqual(originals), "Locked roster data remains unchanged.");
                Check(roster.ActiveEnemies.First().CharacterDefinition == currentEnemy, "Blocked respawn cannot replace enemies.");
                Check(fight.ApplyPartyDamage(2, 20) == 2 && fight.PartyArmor == 1 && fight.PartyHp == 4, "Armor absorbs damage first.");
                Check(fight.ApplyPartyDamage(2, 21) == 2 && fight.PartyArmor == 0 && fight.PartyHp == 3, "Armor overflow reaches shared HP.");
                fight.AdvanceArmorRecovery(10000);
                Check(fight.PartyArmor == 0 && fight.PartyHp == 3, "No natural armor recovery or healing.");
                Check(heroHp.SequenceEqual(roster.ActiveHeroes.Select(h => h.CurrentHp)), "No independent hero HP was spent.");
                // The future encouragement input must not pass through Basic judgement.
                typeof(FightCombatController).GetField("nextTeamSkillBeat", Private).SetValue(fight, 10001L);
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Front);
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Middle);
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Back);
                Check(inputEvents == 0 && fight.TeamSkillGauge == 0, "Team Skill blocks all Basic inputs before judgement.");
                Call(fight, "ResetTeamSkill");
                UnityEngine.Object.DestroyImmediate(panel);
                Call(fight, "UnsubscribeDependencies");
                File.WriteAllText(Result, $"PASS: {assertions} assertions. Six formations, 20 repeated frontline swaps, 4 shared HP, DEF 3/1/1, 0/60 MP, position/ability bindings, preparation damage/input/enemy gates, UI buttons and construction, battle lock and API bypass attempts, shared armor overflow, no natural recovery, Team Skill Basic-input gate.\nNot verified: live FMOD/gamepad, new abilities, waves, terminal battle lifecycle, final character art.\n");
                Debug.Log("FIGHT_PARTY_PREPARATION_VALIDATION_PASS");
            }
            catch (Exception exception)
            {
                File.WriteAllText(Result, "FAIL: " + exception);
                Debug.LogException(exception);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        private static void Check(bool value, string message)
        {
            assertions++;
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
