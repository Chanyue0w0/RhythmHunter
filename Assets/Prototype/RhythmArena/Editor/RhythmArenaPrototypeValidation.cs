using System;
using RhythmHunter.RhythmArena;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.RhythmArenaEditor
{
    [InitializeOnLoad]
    public static class RhythmArenaPrototypeValidation
    {
        private const double TimeoutSeconds = 12.0;
        private const string ActiveKey = "RhythmArena.Validation.Active";
        private const string StartedAtKey = "RhythmArena.Validation.StartedAt";
        private const string TestStartedKey = "RhythmArena.Validation.TestStarted";
        private const string StartingEnemyHpKey = "RhythmArena.Validation.StartingEnemyHp";
        private const string PassedKey = "RhythmArena.Validation.Passed";
        private const string FailureKey = "RhythmArena.Validation.Failure";

        static RhythmArenaPrototypeValidation()
        {
            if (SessionState.GetBool(ActiveKey, false))
                RegisterCallbacks();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RhythmArenaPrototypeSceneBuilder.ScenePath) == null)
                EditorApplication.delayCall += BuildMissingPrototypeScene;
            else
                EditorApplication.delayCall += RhythmArenaPrototypeSceneBuilder.ApplyBuildSettings;
        }

        private static void BuildMissingPrototypeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(RhythmArenaPrototypeSceneBuilder.ScenePath) != null)
            {
                return;
            }

            RhythmArenaPrototypeSceneBuilder.BuildScene();
        }

        [MenuItem("Rhythm Hunter/Validate Rhythm Arena Prototype")]
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RhythmArenaPrototypeSceneBuilder.ScenePath) == null)
                RhythmArenaPrototypeSceneBuilder.BuildScene();

            EditorSceneManager.OpenScene(RhythmArenaPrototypeSceneBuilder.ScenePath);
            ValidateSceneStructure();

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetFloat(StartedAtKey, 0f);
            SessionState.SetBool(TestStartedKey, false);
            SessionState.SetInt(StartingEnemyHpKey, 0);
            SessionState.SetBool(PassedKey, false);
            SessionState.SetString(FailureKey, "Validation timed out.");
            RegisterCallbacks();
            EditorApplication.EnterPlaymode();
        }

        private static void ValidateSceneStructure()
        {
            RhythmClock clock = UnityEngine.Object.FindFirstObjectByType<RhythmClock>();
            RhythmArenaRing ring = UnityEngine.Object.FindFirstObjectByType<RhythmArenaRing>();
            PlayerCombatController player = UnityEngine.Object.FindFirstObjectByType<PlayerCombatController>();
            EnemyPatternController pattern = UnityEngine.Object.FindFirstObjectByType<EnemyPatternController>();
            CombatResolver resolver = UnityEngine.Object.FindFirstObjectByType<CombatResolver>();
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();

            if (clock == null || ring == null || player == null || pattern == null || resolver == null)
                throw new InvalidOperationException("Prototype scene is missing one or more core combat components.");
            if (ring.RingSegments == null || ring.RingSegments.Length != 64)
                throw new InvalidOperationException("World-space arena must contain exactly 64 pixel ring segments.");
            if (canvas != null)
                throw new InvalidOperationException("Prototype core must not use a Canvas.");

            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length == 0 || buildScenes[0].path != RhythmArenaPrototypeSceneBuilder.ScenePath || !buildScenes[0].enabled)
                throw new InvalidOperationException("Rhythm Arena Prototype is not the first enabled Build scene.");
            for (int i = 1; i < buildScenes.Length; i++)
            {
                if (buildScenes[i].enabled)
                    throw new InvalidOperationException($"Unexpected enabled Build scene: {buildScenes[i].path}");
            }
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
            string failure = SessionState.GetString(FailureKey, "Unknown validation failure.");
            ClearSessionState();

            if (passed)
            {
                Debug.Log("RHYTHM_ARENA_PROTOTYPE_SMOKE_TEST_PASS");
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"RHYTHM_ARENA_PROTOTYPE_SMOKE_TEST_FAIL: {failure}");
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedAtKey, 0f);
            RhythmClock clock = UnityEngine.Object.FindFirstObjectByType<RhythmClock>();
            PlayerCombatController player = UnityEngine.Object.FindFirstObjectByType<PlayerCombatController>();
            CombatResolver resolver = UnityEngine.Object.FindFirstObjectByType<CombatResolver>();
            EnemyPatternController pattern = UnityEngine.Object.FindFirstObjectByType<EnemyPatternController>();

            if (clock == null || player == null || resolver == null || pattern == null)
            {
                FailAndExit("Runtime core component was not found.");
                return;
            }

            if (!clock.IsReady)
            {
                if (elapsed >= TimeoutSeconds)
                    FailAndExit("FMOD clock and fallback clock did not become ready.");
                return;
            }

            if (!SessionState.GetBool(TestStartedKey, false))
            {
                double originalNextAttack = pattern.NextAttackBeat;
                pattern.ShiftNextAttack(0.5f);
                if (Math.Abs(pattern.NextAttackBeat - originalNextAttack - 0.5) > 0.01)
                {
                    FailAndExit("Enemy timeline shift did not move the next attack by 0.5 beat.");
                    return;
                }

                SessionState.SetInt(StartingEnemyHpKey, resolver.EnemyHp);
                bool started = player.TryStartAction(PlayerCombatController.ActionType.QuickSlash);
                double duration = player.ActionEndBeat - player.ActionStartBeat;
                if (!started || Math.Abs(duration - 1.0) > 0.01 ||
                    player.TryStartAction(PlayerCombatController.ActionType.HeavySlash))
                {
                    FailAndExit("Fixed Quick duration or action lock validation failed.");
                    return;
                }

                SessionState.SetBool(TestStartedKey, true);
                return;
            }

            int startingHp = SessionState.GetInt(StartingEnemyHpKey, 0);
            if (!player.IsBusy && resolver.EnemyHp < startingHp)
            {
                if (!clock.IsUsingFmod)
                {
                    FailAndExit("Combat resolved on the fallback clock instead of the FMOD beat source.");
                    return;
                }

                SessionState.SetBool(PassedKey, true);
                SessionState.SetString(FailureKey, string.Empty);
                EditorApplication.ExitPlaymode();
                return;
            }

            if (elapsed >= TimeoutSeconds)
                FailAndExit("Quick Slash did not resolve damage within the validation timeout.");
        }

        private static void FailAndExit(string reason)
        {
            SessionState.SetString(FailureKey, reason);
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
            SessionState.EraseBool(TestStartedKey);
            SessionState.EraseInt(StartingEnemyHpKey);
            SessionState.EraseBool(PassedKey);
            SessionState.EraseString(FailureKey);
        }
    }
}
