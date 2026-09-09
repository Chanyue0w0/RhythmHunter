using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    /// <summary>
    /// Batch-mode smoke test for FightScene2 front-hero input, ally auto-attacks,
    /// mana gain, skill configuration, and beat guard timing.
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
                bool passed = fight.FrontHero.UnitSlot != null &&
                              fight.SecondHero.UnitSlot != null &&
                              fight.ThirdHero.UnitSlot != null &&
                              fight.FrontHero.CurrentMana >= 1 &&
                              fight.FrontHero.UnitSlot.NormalAttackPlayCount >= 2 &&
                              fight.SecondHero.UnitSlot.NormalAttackPlayCount >= 5 &&
                              fight.SecondHero.SkillActivationCount >= 1 &&
                              fight.ThirdHero.UnitSlot.NormalAttackPlayCount >= 2 &&
                              currentEnemyHp < initialEnemyHp &&
                              clock.IsPlaying;

                SessionState.SetBool(PassedKey, passed);
                SessionState.SetString(
                    FailureKey,
                    passed
                        ? string.Empty
                        : $"Invalid flow. Mana={fight.FrontHero.CurrentMana}, " +
                          $"FrontAttacks={fight.FrontHero.UnitSlot?.NormalAttackPlayCount ?? 0}, " +
                          $"BardAttacks={fight.SecondHero.UnitSlot?.NormalAttackPlayCount ?? 0}, " +
                          $"BardSkills={fight.SecondHero.SkillActivationCount}, " +
                          $"MageAttacks={fight.ThirdHero.UnitSlot?.NormalAttackPlayCount ?? 0}, " +
                          $"EnemyHP={currentEnemyHp}/{initialEnemyHp}, Blocks={fight.BlockedAttackCount}, " +
                          $"ClockPlaying={clock.IsPlaying}.");
                EditorApplication.ExitPlaymode();
                return;
            }

            if (elapsed >= TimeoutSeconds)
                EditorApplication.ExitPlaymode();
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
