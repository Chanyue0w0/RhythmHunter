using RhythmHunter.OtterAquariumPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    [InitializeOnLoad]
    public static class OtterAquariumPlayModeValidation
    {
        private const string ActiveKey = "OtterAquarium.Smoke.Active";
        private const string AttemptedKey = "OtterAquarium.Smoke.Attempted.ExplorationCameraV15";
        private const string PassedKey = "OtterAquarium.Smoke.Passed";
        private const string FailureKey = "OtterAquarium.Smoke.Failure";
        private const string StartedAtKey = "OtterAquarium.Smoke.StartedAt";
        private const string StepKey = "OtterAquarium.Smoke.Step";
        private const string StepStartedAtKey = "OtterAquarium.Smoke.StepStartedAt";
        private const string PreviousSceneKey = "OtterAquarium.Smoke.PreviousScene";
        private const double TimeoutSeconds = 6.0;

        static OtterAquariumPlayModeValidation()
        {
            if (SessionState.GetBool(ActiveKey, false))
                RegisterCallbacks();
            else
                EditorApplication.delayCall += RunInitialSmokeTestOnce;
        }

        [MenuItem("Rhythm Hunter/Run Sea Otter Aquarium Play Mode Smoke Test")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!OtterAquariumValidation.ValidateScene(true))
                return;

            Scene previous = SceneManager.GetActiveScene();
            if (previous.isDirty)
            {
                Debug.LogWarning("OTTER_AQUARIUM_SMOKE_TEST_SKIPPED: The active scene has unsaved changes.");
                return;
            }

            SessionState.SetString(PreviousSceneKey, previous.path ?? string.Empty);
            EditorSceneManager.OpenScene(OtterAquariumSceneBuilder.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(PassedKey, false);
            SessionState.SetString(FailureKey, "Sea otter aquarium smoke test timed out.");
            SessionState.SetFloat(StartedAtKey, 0f);
            SessionState.SetInt(StepKey, 0);
            SessionState.SetFloat(StepStartedAtKey, 0f);
            RegisterCallbacks();
            EditorApplication.EnterPlaymode();
        }

        private static void RunInitialSmokeTestOnce()
        {
            if (SessionState.GetBool(AttemptedKey, false)
                || EditorApplication.isPlayingOrWillChangePlaymode
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(OtterAquariumSceneBuilder.ScenePath) == null)
            {
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
            string failure = SessionState.GetString(FailureKey, "Unknown sea otter aquarium smoke test failure.");
            string previousScene = SessionState.GetString(PreviousSceneKey, string.Empty);
            ClearRunState();

            if (!string.IsNullOrEmpty(previousScene) && AssetDatabase.LoadAssetAtPath<SceneAsset>(previousScene) != null)
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);

            if (passed)
                Debug.Log("OTTER_AQUARIUM_PLAY_MODE_SMOKE_TEST_PASS");
            else
                Debug.LogError($"OTTER_AQUARIUM_PLAY_MODE_SMOKE_TEST_FAIL: {failure}");
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedAtKey, 0f);
            OtterMovementController movement = Object.FindFirstObjectByType<OtterMovementController>();
            OtterSurfaceSensor sensor = Object.FindFirstObjectByType<OtterSurfaceSensor>();
            OtterVfxController vfx = Object.FindFirstObjectByType<OtterVfxController>();
            OtterVisualPresenter presenter = Object.FindFirstObjectByType<OtterVisualPresenter>();
            OtterCameraFollow camera = Object.FindFirstObjectByType<OtterCameraFollow>();
            OtterPrototypeHud hud = Object.FindFirstObjectByType<OtterPrototypeHud>();

            if (movement == null || sensor == null || vfx == null || presenter == null || camera == null || hud == null)
            {
                FailAndExit("One or more runtime prototype systems are missing.");
                return;
            }

            Rigidbody2D body = movement.GetComponent<Rigidbody2D>();
            ParticleSystem[] particles = movement.GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystemRenderer[] particleRenderers = movement.GetComponentsInChildren<ParticleSystemRenderer>(true);
            bool hasInvalidParticleMaterial = System.Array.Exists(
                particleRenderers,
                renderer => renderer.sharedMaterial == null
                    || renderer.sharedMaterial.shader == null
                    || renderer.sharedMaterial.shader.name == "Hidden/InternalErrorShader");
            if (body == null || particles.Length < 6 || particleRenderers.Length < 6 || hasInvalidParticleMaterial)
            {
                FailAndExit("Rigidbody2D, water VFX systems, or valid particle materials are missing.");
                return;
            }

            int step = SessionState.GetInt(StepKey, 0);
            double stepElapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StepStartedAtKey, 0f);
            if (step == 0 && elapsed >= 0.45)
            {
                if (movement.CurrentSurface != AquariumSurfaceType.Water || !movement.IsWet)
                {
                    FailAndExit($"Spawn surface was {movement.CurrentSurface}; expected Water.");
                    return;
                }

                foreach (ParticleSystem particleSystem in particles)
                    particleSystem.Emit(3);
                SessionState.SetInt(StepKey, 1);
                SessionState.SetFloat(StepStartedAtKey, (float)EditorApplication.timeSinceStartup);
                return;
            }

            if (step == 1 && stepElapsed >= 0.65)
            {
                bool renderersRemainValid = !System.Array.Exists(
                    particleRenderers,
                    renderer => renderer.sharedMaterial == null
                        || renderer.sharedMaterial.mainTexture == null
                        || renderer.sharedMaterial.shader == null
                        || renderer.sharedMaterial.shader.name == "Hidden/InternalErrorShader");
                if (!renderersRemainValid)
                {
                    FailAndExit("A water VFX renderer lost its material, texture, or shader during emission.");
                    return;
                }

                SessionState.SetBool(PassedKey, true);
                SessionState.SetString(FailureKey, string.Empty);
                EditorApplication.ExitPlaymode();
                return;
            }

            if (elapsed >= TimeoutSeconds)
                FailAndExit("Sea otter aquarium smoke test timed out.");
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
            SessionState.EraseFloat(StepStartedAtKey);
            SessionState.EraseString(PreviousSceneKey);
        }
    }
}
