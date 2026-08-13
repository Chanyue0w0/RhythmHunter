using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmHunter.FightDemoEditor
{
    // Batch-mode smoke test for the fourth-beat tank guard loop.
    [InitializeOnLoad]
    public static class FightScenePlayModeValidation
    {
        private const double TimeoutSeconds = 20.0;
        private const string ActiveKey = "FightDemo.Validation.Active";
        private const string StartedAtKey = "FightDemo.Validation.StartedAt";
        private const string AttemptedKey = "FightDemo.Validation.Attempted";
        private const string PassedKey = "FightDemo.Validation.Passed";
        private const string FailureKey = "FightDemo.Validation.Failure";

        static FightScenePlayModeValidation()
        {
            if (SessionState.GetBool(ActiveKey, false))
                RegisterCallbacks();
        }

        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FightSceneBuilder.ScenePath) == null)
                FightSceneBuilder.BuildScene();

            if (!ValidateInputAsset(out string inputFailure))
            {
                Debug.LogError($"FIGHT_SCENE_SMOKE_TEST_FAIL: {inputFailure}");
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(FightSceneBuilder.ScenePath);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetFloat(StartedAtKey, 0f);
            SessionState.SetBool(AttemptedKey, false);
            SessionState.SetBool(PassedKey, false);
            SessionState.SetString(FailureKey, "FightScene validation timed out.");

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
            string failure = SessionState.GetString(FailureKey, "Unknown FightScene validation failure.");
            ClearSessionState();

            if (passed)
            {
                Debug.Log("FIGHT_SCENE_SMOKE_TEST_PASS");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"FIGHT_SCENE_SMOKE_TEST_FAIL: {failure}");
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
                FailAndExit("FightInputRouter did not resolve the Beats and Bard actions.");
                return;
            }

            if (clock != null && fight != null && clock.ReceivedBeatCount >= 4 &&
                clock.LatestBeat.Beat == 4 && !SessionState.GetBool(AttemptedKey, false) &&
                clock.TryGetBeatPhase(out float phase) && phase < 0.08f)
            {
                SessionState.SetBool(AttemptedKey, true);
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Tank);
            }

            if (fight != null && SessionState.GetBool(AttemptedKey, false) && fight.BlockedAttackCount >= 1)
            {
                bool passed = fight.PartyHp == fight.MaxPartyHp && fight.ReceivedAttackCount == 0 &&
                              clock != null && clock.IsPlaying;
                SessionState.SetBool(PassedKey, passed);
                SessionState.SetString(
                    FailureKey,
                    passed
                        ? string.Empty
                        : $"Guard resolution invalid. HP={fight.PartyHp}/{fight.MaxPartyHp}, Hits={fight.ReceivedAttackCount}.");
                EditorApplication.ExitPlaymode();
                return;
            }

            if (elapsed >= TimeoutSeconds)
                EditorApplication.ExitPlaymode();
        }

        private static bool ValidateInputAsset(out string failure)
        {
            failure = string.Empty;
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(FightSceneBuilder.InputActionsPath);
            if (asset == null)
            {
                failure = "FightControl.inputactions is missing.";
                return false;
            }

            return HasBindings(asset, "Character1Attack", "<Keyboard>/q", "<Gamepad>/buttonWest", out failure) &&
                   HasBindings(asset, "Character2Attack", "<Keyboard>/w", "<Gamepad>/buttonNorth", out failure) &&
                   HasBindings(asset, "Character3Attack", "<Keyboard>/e", "<Gamepad>/buttonEast", out failure) &&
                   HasBindings(asset, "FeverUlt", "<Keyboard>/r", "<Gamepad>/buttonSouth", out failure);
        }

        private static bool HasBindings(
            InputActionAsset asset,
            string actionName,
            string keyboardPath,
            string gamepadPath,
            out string failure)
        {
            failure = string.Empty;
            InputAction action = asset.FindAction($"Abilities/{actionName}", false);
            if (action == null)
            {
                failure = $"Missing input action: Abilities/{actionName}.";
                return false;
            }

            bool hasKeyboard = false;
            bool hasGamepad = false;
            foreach (InputBinding binding in action.bindings)
            {
                hasKeyboard |= binding.path == keyboardPath;
                hasGamepad |= binding.path == gamepadPath;
            }

            if (hasKeyboard && hasGamepad)
                return true;

            failure = $"{actionName} bindings do not match {keyboardPath} and {gamepadPath}.";
            return false;
        }

        private static void FailAndExit(string failure)
        {
            SessionState.SetString(FailureKey, failure);
            EditorApplication.ExitPlaymode();
        }

        private static void RegisterCallbacks()
        {
            CleanupCallbacks();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
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
            SessionState.EraseBool(AttemptedKey);
            SessionState.EraseBool(PassedKey);
            SessionState.EraseString(FailureKey);
        }
    }
}
