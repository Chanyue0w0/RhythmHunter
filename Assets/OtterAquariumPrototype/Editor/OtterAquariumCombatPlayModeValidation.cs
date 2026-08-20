using RhythmHunter.FightDemo;
using RhythmHunter.OtterAquariumPrototype;
using RhythmHunter.PirateOceanPrototype;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    [InitializeOnLoad]
    public static class OtterAquariumCombatPlayModeValidation
    {
        private const string ActiveKey = "OtterAquariumCombat.Smoke.Active";
        private const string AttemptedKey = "OtterAquariumCombat.Smoke.Attempted.PirateCameraV7";
        private const string PassedKey = "OtterAquariumCombat.Smoke.Passed";
        private const string FailureKey = "OtterAquariumCombat.Smoke.Failure";
        private const string StartedAtKey = "OtterAquariumCombat.Smoke.StartedAt";
        private const string StepKey = "OtterAquariumCombat.Smoke.CameraStep";
        private const string PreviousSceneKey = "OtterAquariumCombat.Smoke.PreviousScene";
        private const double TimeoutSeconds = 8.0;

        static OtterAquariumCombatPlayModeValidation()
        {
            if (SessionState.GetBool(ActiveKey, false))
                RegisterCallbacks();
            else
                EditorApplication.delayCall += RunInitialSmokeTestOnce;
        }

        [MenuItem("Rhythm Hunter/Run Otter Aquarium Combat Play Mode Smoke Test")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !OtterAquariumCombatValidation.ValidateScene(true))
                return;

            Scene previous = SceneManager.GetActiveScene();
            if (previous.isDirty)
            {
                Debug.LogWarning("OTTER_AQUARIUM_COMBAT_SMOKE_TEST_SKIPPED: The active scene has unsaved changes.");
                return;
            }

            SessionState.SetString(PreviousSceneKey, previous.path ?? string.Empty);
            EditorSceneManager.OpenScene(OtterAquariumCombatSceneSetup.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(PassedKey, false);
            SessionState.SetString(FailureKey, "Aquarium combat smoke test timed out.");
            SessionState.SetFloat(StartedAtKey, 0f);
            SessionState.SetInt(StepKey, 0);
            RegisterCallbacks();
            EditorApplication.EnterPlaymode();
        }

        private static void RunInitialSmokeTestOnce()
        {
            if (SessionState.GetBool(AttemptedKey, false)
                || EditorApplication.isPlayingOrWillChangePlaymode
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(OtterAquariumCombatSceneSetup.ScenePath) == null)
            {
                return;
            }

            // Scene setup and smoke-test callbacks are both delayed after a
            // domain reload. Wait until the rebuilt scene is fully imported
            // instead of consuming the one-shot attempt on the legacy scene.
            if (!OtterAquariumCombatValidation.ValidateScene(false))
            {
                EditorApplication.delayCall += RunInitialSmokeTestOnce;
                return;
            }

            SessionState.SetBool(AttemptedKey, true);
            Run();
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
            string failure = SessionState.GetString(FailureKey, "Unknown aquarium combat smoke test failure.");
            string previousScene = SessionState.GetString(PreviousSceneKey, string.Empty);
            ClearRunState();

            if (!string.IsNullOrEmpty(previousScene) && AssetDatabase.LoadAssetAtPath<SceneAsset>(previousScene) != null)
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);

            if (passed)
                Debug.Log("OTTER_AQUARIUM_COMBAT_PLAY_MODE_SMOKE_TEST_PASS");
            else
                Debug.LogError($"OTTER_AQUARIUM_COMBAT_PLAY_MODE_SMOKE_TEST_FAIL: {failure}");
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedAtKey, 0f);
            if (elapsed >= TimeoutSeconds)
            {
                FailAndExit("Aquarium combat smoke test timed out.");
                return;
            }
            if (elapsed < 0.75)
                return;

            FmodBeatClock clock = Object.FindFirstObjectByType<FmodBeatClock>();
            FmodRhythmJudge judge = Object.FindFirstObjectByType<FmodRhythmJudge>();
            FightInputRouter input = Object.FindFirstObjectByType<FightInputRouter>();
            FightCombatController fight = Object.FindFirstObjectByType<FightCombatController>();
            FightScenePresenter hud = Object.FindFirstObjectByType<FightScenePresenter>();
            FightBattlefieldPresenter battlefield = Object.FindFirstObjectByType<FightBattlefieldPresenter>();
            FightUnitSlot[] slots = Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None);
            OtterAquariumCombatSceneMarker marker = Object.FindFirstObjectByType<OtterAquariumCombatSceneMarker>();
            PirateBossCameraController cameraController = Object.FindFirstObjectByType<PirateBossCameraController>();

            bool hasWaveSystems = Object.FindFirstObjectByType<PirateOceanWaveController>() != null
                || Object.FindFirstObjectByType<PirateOceanSurface>() != null
                || Object.FindFirstObjectByType<PirateShipMotionController>() != null
                || Object.FindFirstObjectByType<PirateOceanRuntimePanel>() != null;

            if (clock != null && !string.IsNullOrEmpty(clock.LastError))
            {
                FailAndExit(clock.LastError);
                return;
            }

            bool valid = clock != null
                && judge != null
                && input != null
                && input.IsConfigured
                && fight != null
                && hud != null
                && battlefield != null
                && slots.Length == 6
                && marker != null
                && !marker.IncludesOceanWaves
                && marker.IncludesCinematicCamera
                && cameraController != null
                && cameraController.Brain != null
                && cameraController.ShipCombatCamera != null
                && cameraController.BossWideCamera != null
                && !hasWaveSystems;
            if (!valid)
            {
                FailAndExit(
                    $"Runtime systems invalid. Clock={clock != null}, Judge={judge != null}, Input={input?.IsConfigured ?? false}, "
                    + $"Fight={fight != null}, HUD={hud != null}, Battlefield={battlefield != null}, Slots={slots.Length}, "
                    + $"Marker={marker != null}, Camera={cameraController != null}, Waves={hasWaveSystems}.");
                return;
            }

            int step = SessionState.GetInt(StepKey, 0);
            if (step == 0)
            {
                cameraController.ShowBossWideView();
                SessionState.SetInt(StepKey, 1);
                return;
            }

            if (step == 1 && elapsed >= 1.15)
            {
                if (!cameraController.BossViewActive
                    || cameraController.BossWideCamera.Priority.Value <= cameraController.ShipCombatCamera.Priority.Value)
                {
                    FailAndExit("Boss wide camera did not become the live Cinemachine shot.");
                    return;
                }

                cameraController.ShowShipCombatView();
                SessionState.SetInt(StepKey, 2);
                return;
            }

            if (step == 2 && elapsed >= 1.55)
            {
                if (cameraController.BossViewActive
                    || cameraController.ShipCombatCamera.Priority.Value <= cameraController.BossWideCamera.Priority.Value)
                {
                    FailAndExit("Ship combat camera did not become the live Cinemachine shot.");
                    return;
                }

                SessionState.SetBool(PassedKey, true);
                SessionState.SetString(FailureKey, string.Empty);
                EditorApplication.ExitPlaymode();
            }
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Assert)
                FailAndExit($"Runtime {type}: {condition}");
        }

        private static void FailAndExit(string failure)
        {
            SessionState.SetString(FailureKey, failure);
            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
        }

        private static void RegisterCallbacks()
        {
            CleanupCallbacks();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
            Application.logMessageReceived += OnLogMessage;
        }

        private static void CleanupCallbacks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            Application.logMessageReceived -= OnLogMessage;
        }

        private static void ClearRunState()
        {
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseBool(PassedKey);
            SessionState.EraseString(FailureKey);
            SessionState.EraseFloat(StartedAtKey);
            SessionState.EraseInt(StepKey);
            SessionState.EraseString(PreviousSceneKey);
        }
    }
}
