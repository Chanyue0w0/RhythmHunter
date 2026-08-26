using System.Collections.Generic;
using RhythmHunter.FightDemo;
using RhythmHunter.PirateOceanPrototype;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.PirateOceanPrototypeEditor
{
    /// <summary>
    /// Repeatable edit-time and Play Mode integration checks for the generated
    /// pirate-ocean prototype scene.
    /// </summary>
    [InitializeOnLoad]
    public static class PirateOceanPrototypeValidation
    {
        private const string ActiveKey = "PirateOcean.Validation.Active";
        private const string PassedKey = "PirateOcean.Validation.Passed";
        private const string FailureKey = "PirateOcean.Validation.Failure";
        private const string StartedAtKey = "PirateOcean.Validation.StartedAt";
        private const string StepKey = "PirateOcean.Validation.Step";
        private const double TimeoutSeconds = 8.0;

        private static readonly Dictionary<string, Vector3> ExpectedSlotPositions = new()
        {
            { "EnemySlot_1", new Vector3(-5.8f, 0.15f, 0f) },
            { "EnemySlot_2", new Vector3(-4.05f, 0.15f, 0f) },
            { "EnemySlot_3", new Vector3(-2.3f, 0.15f, 0f) },
            { "HeroSlot_Tank", new Vector3(2.3f, 0.15f, 0f) },
            { "HeroSlot_Support", new Vector3(4.05f, 0.15f, 0f) },
            { "HeroSlot_Damage", new Vector3(5.8f, 0.15f, 0f) }
        };

        static PirateOceanPrototypeValidation()
        {
            if (SessionState.GetBool(ActiveKey, false))
                RegisterCallbacks();
        }

        [MenuItem("Rhythm Hunter/Validate Pirate Ocean Scene Setup")]
        public static void ValidateSceneFromMenu()
        {
            ValidateScene(true);
        }

        [MenuItem("Rhythm Hunter/Run Pirate Ocean Play Mode Smoke Test")]
        public static void RunPlayModeSmokeTest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("PIRATE_OCEAN_SMOKE_TEST: Exit Play Mode before starting validation.");
                return;
            }

            if (!ValidateScene(true))
                return;

            EditorSceneManager.OpenScene(PirateOceanPrototypeSceneBuilder.ScenePath);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(PassedKey, false);
            SessionState.SetString(FailureKey, "Pirate ocean validation timed out.");
            SessionState.SetFloat(StartedAtKey, 0f);
            SessionState.SetInt(StepKey, 0);
            RegisterCallbacks();
            EditorApplication.EnterPlaymode();
        }

        public static bool ValidateScene(bool logResult)
        {
            Scene scene = SceneManager.GetSceneByPath(PirateOceanPrototypeSceneBuilder.ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(PirateOceanPrototypeSceneBuilder.ScenePath, OpenSceneMode.Additive);

            List<string> failures = ValidateLoadedScene(scene);

            if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);

            if (failures.Count == 0)
            {
                if (logResult)
                    Debug.Log("PIRATE_OCEAN_SCENE_VALIDATION_PASS");
                return true;
            }

            if (logResult)
                Debug.LogError("PIRATE_OCEAN_SCENE_VALIDATION_FAIL:\n- " + string.Join("\n- ", failures));
            return false;
        }

        private static List<string> ValidateLoadedScene(Scene scene)
        {
            List<string> failures = new();
            PirateOceanWaveController[] waves = FindComponents<PirateOceanWaveController>(scene);
            PirateOceanSurface[] surfaces = FindComponents<PirateOceanSurface>(scene);
            PirateShipMotionController[] ships = FindComponents<PirateShipMotionController>(scene);
            PirateBossCameraController[] cameraControllers = FindComponents<PirateBossCameraController>(scene);
            PirateOceanRuntimePanel[] panels = FindComponents<PirateOceanRuntimePanel>(scene);
            FightUnitSlot[] slots = FindComponents<FightUnitSlot>(scene);
            CinemachineCamera[] cameras = FindComponents<CinemachineCamera>(scene);
            CinemachineBrain[] brains = FindComponents<CinemachineBrain>(scene);

            RequireCount(waves, 1, "PirateOceanWaveController", failures);
            RequireCount(surfaces, 1, "PirateOceanSurface", failures);
            RequireCount(ships, 1, "PirateShipMotionController", failures);
            RequireCount(cameraControllers, 1, "PirateBossCameraController", failures);
            RequireCount(panels, 1, "PirateOceanRuntimePanel", failures);
            RequireCount(slots, 6, "FightUnitSlot", failures);
            RequireCount(cameras, 2, "CinemachineCamera", failures);
            RequireCount(brains, 1, "CinemachineBrain", failures);

            if (waves.Length == 1 && waves[0].ContinuousSurface != (surfaces.Length == 1 ? surfaces[0] : null))
                failures.Add("Wave controller does not reference the continuous ocean surface.");

            PirateShipMotionController ship = ships.Length == 1 ? ships[0] : null;
            if (ship != null && (ship.MotionVisualRoot == null || ship.StableCombatRoot == null))
                failures.Add("Ship motion visual/stable roots are incomplete.");
            else if (ship != null && ship.StableCombatRoot.IsChildOf(ship.MotionVisualRoot))
                failures.Add("Stable combat root must not be a child of the moving visual root.");

            ValidateSlots(slots, ship, failures);

            PirateBossCameraController cameraController = cameraControllers.Length == 1 ? cameraControllers[0] : null;
            if (cameraController != null)
            {
                if (cameraController.Brain == null
                    || cameraController.ShipCombatCamera == null
                    || cameraController.BossWideCamera == null)
                {
                    failures.Add("Boss camera controller references are incomplete.");
                }
                else
                {
                    ValidateOrthographicCamera(cameraController.ShipCombatCamera, "Ship combat", failures);
                    ValidateOrthographicCamera(cameraController.BossWideCamera, "Boss wide", failures);
                }
            }

            if (panels.Length == 1)
            {
                PirateOceanRuntimePanel panel = panels[0];
                if (panel.OceanWaves != (waves.Length == 1 ? waves[0] : null)
                    || panel.ShipMotion != ship
                    || panel.BossCamera != cameraController)
                {
                    failures.Add("Runtime panel references do not match the scene systems.");
                }
            }

            return failures;
        }

        private static void ValidateSlots(
            FightUnitSlot[] slots,
            PirateShipMotionController ship,
            List<string> failures)
        {
            int heroCount = 0;
            int enemyCount = 0;
            HashSet<string> names = new();

            foreach (FightUnitSlot slot in slots)
            {
                names.Add(slot.name);
                if (slot.Team == FightUnitSlot.UnitTeam.Hero)
                    heroCount++;
                else
                    enemyCount++;

                if (!ExpectedSlotPositions.TryGetValue(slot.name, out Vector3 expectedPosition))
                    failures.Add($"Unexpected fight slot: {slot.name}.");
                else if (Vector3.Distance(slot.transform.localPosition, expectedPosition) > 0.001f)
                    failures.Add($"{slot.name} moved from its stable coordinate {expectedPosition}.");

                if (slot.ActorRoot == null || slot.NormalAttackEffectSpawnPoint == null)
                    failures.Add($"{slot.name} is missing actor/effect visual references.");

                if (ship != null && ship.MotionVisualRoot != null)
                {
                    if (slot.transform.IsChildOf(ship.MotionVisualRoot))
                        failures.Add($"{slot.name} logic transform is incorrectly under the moving root.");
                    if (slot.ActorRoot != null && !slot.ActorRoot.IsChildOf(ship.MotionVisualRoot))
                        failures.Add($"{slot.name} actor visual is not under the moving root.");
                }
            }

            if (heroCount != 3 || enemyCount != 3 || names.Count != ExpectedSlotPositions.Count)
                failures.Add($"Expected three heroes and three enemies; found Heroes={heroCount}, Enemies={enemyCount}.");
        }

        private static void ValidateOrthographicCamera(
            CinemachineCamera camera,
            string label,
            List<string> failures)
        {
            if (camera.Lens.ModeOverride != LensSettings.OverrideModes.Orthographic)
                failures.Add($"{label} camera is not orthographic.");
            if (camera.Lens.OrthographicSize <= 0f)
                failures.Add($"{label} camera has an invalid orthographic size.");
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
            string failure = SessionState.GetString(FailureKey, "Unknown pirate ocean validation failure.");
            ClearSessionState();

            if (passed)
                Debug.Log("PIRATE_OCEAN_PLAY_MODE_SMOKE_TEST_PASS");
            else
                Debug.LogError($"PIRATE_OCEAN_PLAY_MODE_SMOKE_TEST_FAIL: {failure}");
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedAtKey, 0f);
            PirateOceanWaveController waves = Object.FindFirstObjectByType<PirateOceanWaveController>();
            PirateOceanSurface surface = Object.FindFirstObjectByType<PirateOceanSurface>();
            PirateShipMotionController ship = Object.FindFirstObjectByType<PirateShipMotionController>();
            PirateBossCameraController camera = Object.FindFirstObjectByType<PirateBossCameraController>();
            PirateOceanRuntimePanel panel = Object.FindFirstObjectByType<PirateOceanRuntimePanel>();
            FightUnitSlot[] slots = Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None);

            if (waves == null || surface == null || ship == null || camera == null || panel == null || slots.Length != 6)
            {
                FailAndExit("One or more runtime systems are missing.");
                return;
            }

            if (elapsed < 0.35)
                return;

            if (!RuntimeMotionIsValid(surface, ship, slots, out string motionFailure))
            {
                FailAndExit(motionFailure);
                return;
            }

            int step = SessionState.GetInt(StepKey, 0);
            if (step == 0)
            {
                panel.ApplyStormPreset();
                camera.ShowBossWideView();
                SessionState.SetInt(StepKey, 1);
                return;
            }

            if (step == 1 && elapsed >= 0.75)
            {
                if (!camera.BossViewActive
                    || camera.BossWideCamera.Priority.Value <= camera.ShipCombatCamera.Priority.Value
                    || !Mathf.Approximately(waves.Intensity, 1.5f)
                    || !Mathf.Approximately(ship.MotionIntensity, 1.45f))
                {
                    FailAndExit("Storm preset or Boss camera priority did not apply.");
                    return;
                }

                panel.ApplyCombatPreset();
                camera.ShowShipCombatView();
                SessionState.SetInt(StepKey, 2);
                return;
            }

            if (step == 2 && elapsed >= 1.15)
            {
                bool passed = !camera.BossViewActive
                    && camera.ShipCombatCamera.Priority.Value > camera.BossWideCamera.Priority.Value
                    && Mathf.Approximately(waves.Intensity, 1f)
                    && Mathf.Approximately(ship.MotionIntensity, 1f)
                    && panel.OceanWaves == waves
                    && panel.ShipMotion == ship
                    && panel.BossCamera == camera;

                if (!passed)
                {
                    FailAndExit("Combat preset, ship camera, or runtime panel references are invalid.");
                    return;
                }

                SessionState.SetBool(PassedKey, true);
                SessionState.SetString(FailureKey, string.Empty);
                EditorApplication.ExitPlaymode();
                return;
            }

            if (elapsed >= TimeoutSeconds)
                FailAndExit("Pirate ocean validation timed out.");
        }

        private static bool RuntimeMotionIsValid(
            PirateOceanSurface surface,
            PirateShipMotionController ship,
            FightUnitSlot[] slots,
            out string failure)
        {
            failure = string.Empty;
            foreach (FightUnitSlot slot in slots)
            {
                if (!ExpectedSlotPositions.TryGetValue(slot.name, out Vector3 expected)
                    || Vector3.Distance(slot.transform.localPosition, expected) > 0.001f)
                {
                    failure = $"Stable combat slot drifted during Play Mode: {slot.name}.";
                    return false;
                }
            }

            if (ship.MotionVisualRoot == null
                || (ship.MotionVisualRoot.localPosition.sqrMagnitude < 0.000001f
                    && Quaternion.Angle(ship.MotionVisualRoot.localRotation, Quaternion.identity) < 0.01f))
            {
                failure = "Ship visual root did not animate.";
                return false;
            }

            MeshFilter filter = surface.GetComponent<MeshFilter>();
            Vector3[] vertices = filter != null && filter.sharedMesh != null
                ? filter.sharedMesh.vertices
                : null;
            if (vertices == null || vertices.Length == 0)
            {
                failure = "Continuous ocean mesh was not generated.";
                return false;
            }

            bool hasWaveVariation = false;
            for (int i = 0; i < vertices.Length; i += 2)
                hasWaveVariation |= Mathf.Abs(vertices[i].y - surface.SurfaceY) > 0.001f;

            if (!hasWaveVariation)
            {
                failure = "Continuous ocean mesh did not animate.";
                return false;
            }

            return true;
        }

        private static T[] FindComponents<T>(Scene scene) where T : Component
        {
            List<T> results = new();
            if (!scene.IsValid() || !scene.isLoaded)
                return results.ToArray();

            foreach (GameObject root in scene.GetRootGameObjects())
                results.AddRange(root.GetComponentsInChildren<T>(true));
            return results.ToArray();
        }

        private static void RequireCount<T>(T[] values, int expected, string label, List<string> failures)
        {
            if (values.Length != expected)
                failures.Add($"Expected {expected} {label} component(s), found {values.Length}.");
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
            SessionState.EraseBool(PassedKey);
            SessionState.EraseString(FailureKey);
            SessionState.EraseFloat(StartedAtKey);
            SessionState.EraseInt(StepKey);
        }
    }
}
