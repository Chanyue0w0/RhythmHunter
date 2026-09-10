using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    /// <summary>
    /// Batch-mode smoke test for FightScene2 front-hero input, ally auto-attacks,
    /// mana gain, skill configuration, beat guard timing, prefab data, and nullable rosters.
    /// </summary>
    [InitializeOnLoad]
    public static class FightScene2PlayModeValidation
    {
        private const double TimeoutSeconds = 20.0;
        private const string Prefix = "FightScene2.Validation.";
        private const string ActiveKey = Prefix + "Active";
        private const string StartedAtKey = Prefix + "StartedAt";
        private const string LightAttemptedKey = Prefix + "LightAttempted";
        private const string HeavyAttemptedKey = Prefix + "HeavyAttempted";
        private const string GuardAttemptedKey = Prefix + "GuardAttempted";
        private const string InitialEnemyHpKey = Prefix + "InitialEnemyHp";
        private const string PassedKey = Prefix + "Passed";
        private const string FailureKey = Prefix + "Failure";

        static FightScene2PlayModeValidation()
        {
            if (SessionState.GetBool(ActiveKey, false))
                RegisterCallbacks();
        }

        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FightSceneBuilder.Scene2Path) == null)
            {
                Debug.LogError("FIGHT_SCENE2_SMOKE_TEST_FAIL: FightScene2.unity is missing.");
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(FightSceneBuilder.Scene2Path);
            FightUnitSlot[] slots = Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None);
            if (slots.Length != 6)
            {
                Debug.LogError($"FIGHT_SCENE2_SMOKE_TEST_FAIL: Expected six unit slots, found {slots.Length}.");
                EditorApplication.Exit(1);
                return;
            }

            foreach (FightUnitSlot slot in slots)
            {
                if (slot.ActorRoot != null &&
                    slot.ActorRoot.GetComponentsInChildren<BeatSyncedIdleAnimator>(true).Length > 0)
                {
                    Debug.LogError(
                        $"FIGHT_SCENE2_SMOKE_TEST_FAIL: {slot.name} still contains a scene-authored character.");
                    EditorApplication.Exit(1);
                    return;
                }
            }

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetFloat(StartedAtKey, 0f);
            SessionState.SetBool(LightAttemptedKey, false);
            SessionState.SetBool(HeavyAttemptedKey, false);
            SessionState.SetBool(GuardAttemptedKey, false);
            SessionState.SetInt(InitialEnemyHpKey, 0);
            SessionState.SetBool(PassedKey, false);
            SessionState.SetString(FailureKey, "FightScene2 validation timed out.");
            RegisterCallbacks();
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                return;
            }

            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            CleanupCallbacks();
            bool passed = SessionState.GetBool(PassedKey, false);
            string failure = SessionState.GetString(FailureKey, "Unknown FightScene2 validation failure.");
            ClearSessionState();

            if (passed)
            {
                Debug.Log("FIGHT_SCENE2_SMOKE_TEST_PASS");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"FIGHT_SCENE2_SMOKE_TEST_FAIL: {failure}");
                EditorApplication.Exit(1);
            }
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedAtKey, 0f);
            FmodBeatClock clock = Object.FindFirstObjectByType<FmodBeatClock>();
            FightInputRouter input = Object.FindFirstObjectByType<FightInputRouter>();
            FightCombatController fight = Object.FindFirstObjectByType<FightCombatController>();
            FightRosterManager roster = Object.FindFirstObjectByType<FightRosterManager>();

            if (clock != null && !string.IsNullOrEmpty(clock.LastError))
            {
                FailAndExit(clock.LastError);
                return;
            }

            if (input != null && !input.IsConfigured)
            {
                FailAndExit("FightInputRouter is not configured.");
                return;
            }

            if (fight != null && !fight.UsesFrontHeroControls)
            {
                FailAndExit("FightScene2 front-hero mode did not activate.");
                return;
            }

            if (fight != null && roster == null)
            {
                FailAndExit("FightRosterManager is missing.");
                return;
            }

            if (roster != null &&
                (roster.ActiveHeroes.Count < 1 || roster.ActiveHeroes.Count > 3 ||
                 roster.ActiveEnemies.Count < 1 || roster.ActiveEnemies.Count > 3 ||
                 !HasValidPrefabData(roster)))
            {
                FailAndExit(
                    $"Default prefab roster is invalid. Heroes={roster.ActiveHeroes.Count}, Enemies={roster.ActiveEnemies.Count}.");
                return;
            }

            if (fight != null && SessionState.GetInt(InitialEnemyHpKey, 0) == 0)
                SessionState.SetInt(InitialEnemyHpKey, TotalEnemyHp());

            if (clock != null && fight != null && clock.ReceivedBeatCount >= 1 &&
                !SessionState.GetBool(LightAttemptedKey, false) &&
                clock.LatestBeat.GlobalBeat % fight.EnemyAttackIntervalBeats != 0 &&
                clock.TryGetBeatPhase(out float lightPhase) && lightPhase < 0.08f)
            {
                SessionState.SetBool(LightAttemptedKey, true);
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Tank);
            }

            if (clock != null && fight != null && clock.ReceivedBeatCount >= 2 &&
                SessionState.GetBool(LightAttemptedKey, false) &&
                !SessionState.GetBool(HeavyAttemptedKey, false) &&
                clock.LatestBeat.GlobalBeat % fight.EnemyAttackIntervalBeats != 0 &&
                clock.TryGetBeatPhase(out float heavyPhase) && heavyPhase < 0.08f)
            {
                int attacksBefore = fight.FrontHero.UnitSlot?.NormalAttackPlayCount ?? 0;
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Support);
                int attacksAfter = fight.FrontHero.UnitSlot?.NormalAttackPlayCount ?? 0;
                SessionState.SetBool(HeavyAttemptedKey, attacksAfter > attacksBefore);
            }

            if (clock != null && fight != null && clock.ReceivedBeatCount >= 4 &&
                !SessionState.GetBool(GuardAttemptedKey, false) &&
                clock.LatestBeat.GlobalBeat % fight.EnemyAttackIntervalBeats == 0 &&
                clock.TryGetBeatPhase(out float guardPhase) && guardPhase < 0.08f)
            {
                SessionState.SetBool(GuardAttemptedKey, true);
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Damage);
            }

            if (clock != null && fight != null &&
                SessionState.GetBool(LightAttemptedKey, false) &&
                SessionState.GetBool(HeavyAttemptedKey, false) &&
                SessionState.GetBool(GuardAttemptedKey, false) && fight.BlockedAttackCount >= 1 &&
                fight.SecondHero.UnitSlot != null &&
                fight.SecondHero.UnitSlot.NormalAttackPlayCount >= 5)
            {
                int initialEnemyHp = SessionState.GetInt(InitialEnemyHpKey, 0);
                int currentEnemyHp = TotalEnemyHp();
                int frontMana = fight.FrontHero.CurrentMana;
                int frontAttacks = fight.FrontHero.UnitSlot?.NormalAttackPlayCount ?? 0;
                int bardAttacks = fight.SecondHero.UnitSlot?.NormalAttackPlayCount ?? 0;
                int bardSkills = fight.SecondHero.SkillActivationCount;
                int mageAttacks = fight.ThirdHero.UnitSlot?.NormalAttackPlayCount ?? 0;
                bool combatFlowPassed = fight.FrontHero.UnitSlot != null &&
                                        fight.SecondHero.UnitSlot != null &&
                                        fight.ThirdHero.UnitSlot != null &&
                                        !fight.HealthSystemEnabled &&
                                        !fight.BattleEnded &&
                                        frontMana >= 1 &&
                                        fight.FrontHero.UnitSlot.LightAttackPlayCount >= 1 &&
                                        fight.FrontHero.UnitSlot.HeavyAttackPlayCount >= 1 &&
                                        fight.FrontHero.UnitSlot.GuardPlayCount >= 1 &&
                                        bardAttacks >= 5 &&
                                        bardSkills >= 1 &&
                                        fight.SecondHero.UnitSlot.SkillAttackPlayCount >= 1 &&
                                        mageAttacks >= 2 &&
                                        currentEnemyHp == initialEnemyHp &&
                                        clock.IsPlaying;
                FightCharacterDefinition[] singleEnemyRoster =
                {
                    roster.EnemyPrefabs[0],
                    null,
                    null
                };
                roster.SetRoster(roster.HeroPrefabs, singleEnemyRoster);
                bool nullableRosterPassed = roster.ActiveHeroes.Count == 3 &&
                                            roster.ActiveEnemies.Count == 1 &&
                                            fight.ActiveEnemySlot != null &&
                                            fight.ActiveEnemySlot.HasCharacter;
                bool passed = combatFlowPassed && nullableRosterPassed;

                SessionState.SetBool(PassedKey, passed);
                SessionState.SetString(
                    FailureKey,
                    passed
                        ? string.Empty
                        : $"Invalid flow. Mana={frontMana}, FrontAttacks={frontAttacks}, " +
                          $"BardAttacks={bardAttacks}, BardSkills={bardSkills}, MageAttacks={mageAttacks}, " +
                          $"EnemyHP={currentEnemyHp}/{initialEnemyHp}, Blocks={fight.BlockedAttackCount}, " +
                          $"SingleEnemyRoster={nullableRosterPassed}, " +
                          $"HealthEnabled={fight.HealthSystemEnabled}, BattleEnded={fight.BattleEnded}, " +
                          $"ClockPlaying={clock.IsPlaying}.");
                EditorApplication.ExitPlaymode();
                return;
            }

            if (elapsed >= TimeoutSeconds)
                EditorApplication.ExitPlaymode();
        }

        private static bool HasValidPrefabData(FightRosterManager roster)
        {
            foreach (FightUnitSlot slot in roster.ActiveHeroes)
            {
                if (!HasValidPrefabData(slot))
                    return false;
            }

            foreach (FightUnitSlot slot in roster.ActiveEnemies)
            {
                if (!HasValidPrefabData(slot))
                    return false;
            }

            return true;
        }

        private static bool HasValidPrefabData(FightUnitSlot slot)
        {
            FightCharacterDefinition definition = slot != null ? slot.CharacterDefinition : null;
            return slot != null &&
                   slot.HasCharacter &&
                   slot.ActorInstance != null &&
                   definition != null &&
                   definition.MaxHp > 0 &&
                   definition.AttackPower >= 0 &&
                   definition.SkillDamage >= 0 &&
                   definition.SkillEffectPrefab != null &&
                   definition.AttackIntervalBeats > 0 &&
                   definition.IdleFrames.Count > 0;
        }

        private static int TotalEnemyHp()
        {
            int total = 0;
            FightUnitSlot[] slots = Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None);
            foreach (FightUnitSlot slot in slots)
            {
                if (slot.Team == FightUnitSlot.UnitTeam.Enemy)
                    total += slot.CurrentHp;
            }

            return total;
        }

        private static void FailAndExit(string failure)
        {
            SessionState.SetString(FailureKey, failure);
            EditorApplication.ExitPlaymode();
        }

        private static void RegisterCallbacks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void CleanupCallbacks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
        }

        private static void ClearSessionState()
        {
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(StartedAtKey);
            SessionState.EraseBool(LightAttemptedKey);
            SessionState.EraseBool(HeavyAttemptedKey);
            SessionState.EraseBool(GuardAttemptedKey);
            SessionState.EraseInt(InitialEnemyHpKey);
            SessionState.EraseBool(PassedKey);
            SessionState.EraseString(FailureKey);
        }
    }
}
