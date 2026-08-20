using System.Collections.Generic;
using RhythmHunter.FightDemo;
using RhythmHunter.OtterAquariumPrototype;
using RhythmHunter.PirateOceanPrototype;
using RhythmHunter.RhythmDemo;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    public static class OtterAquariumCombatValidation
    {
        [MenuItem("Rhythm Hunter/Validate Otter Aquarium Combat Scene")]
        public static void ValidateFromMenu()
        {
            ValidateScene(true);
        }

        public static bool ValidateScene(bool logSuccess)
        {
            SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(OtterAquariumCombatSceneSetup.ScenePath);
            if (asset == null)
            {
                Debug.LogError($"[OtterAquariumCombatValidation] Scene is missing: {OtterAquariumCombatSceneSetup.ScenePath}");
                return false;
            }

            Scene scene = SceneManager.GetSceneByPath(OtterAquariumCombatSceneSetup.ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(OtterAquariumCombatSceneSetup.ScenePath, OpenSceneMode.Additive);

            List<string> failures = new();
            RequireCount(FindAllInScene<OtterAquariumCombatSceneMarker>(scene), 1, "aquarium combat marker", failures);
            RequireCount(FindAllInScene<FmodBeatClock>(scene), 1, "FMOD beat clock", failures);
            RequireCount(FindAllInScene<FmodRhythmJudge>(scene), 1, "rhythm judge", failures);
            RequireCount(FindAllInScene<FightInputRouter>(scene), 1, "fight input router", failures);
            RequireCount(FindAllInScene<FightCombatController>(scene), 1, "fight combat controller", failures);
            RequireCount(FindAllInScene<FightScenePresenter>(scene), 1, "fight HUD presenter", failures);
            RequireCount(FindAllInScene<FightBattlefieldPresenter>(scene), 1, "battlefield presenter", failures);
            RequireCount(FindAllInScene<FightUnitSlot>(scene), 6, "fight unit slots", failures);
            RequireCount(FindAllInScene<PirateBossCameraController>(scene), 1, "cinematic camera controller", failures);
            RequireCount(FindAllInScene<CinemachineBrain>(scene), 1, "Cinemachine brain", failures);
            RequireCount(FindAllInScene<CinemachineCamera>(scene), 2, "Cinemachine cameras", failures);

            if (FindAllInScene<PirateOceanWaveController>(scene).Length != 0)
                failures.Add("PirateOceanWaveController must not be present in the aquarium combat scene.");
            if (FindAllInScene<PirateOceanSurface>(scene).Length != 0)
                failures.Add("PirateOceanSurface must not be present in the aquarium combat scene.");
            if (FindAllInScene<PirateShipMotionController>(scene).Length != 0)
                failures.Add("PirateShipMotionController must not be present in the aquarium combat scene.");
            if (FindAllInScene<PirateOceanRuntimePanel>(scene).Length != 0)
                failures.Add("PirateOceanRuntimePanel must not be present in the aquarium combat scene.");

            PirateBossCameraController[] cameraControllers = FindAllInScene<PirateBossCameraController>(scene);
            if (cameraControllers.Length == 1
                && (cameraControllers[0].Brain == null
                    || cameraControllers[0].ShipCombatCamera == null
                    || cameraControllers[0].BossWideCamera == null))
            {
                failures.Add("Cinematic camera controller references are incomplete.");
            }

            OtterAquariumCombatSceneMarker[] markers = FindAllInScene<OtterAquariumCombatSceneMarker>(scene);
            if (markers.Length == 1
                && (markers[0].IncludesOceanWaves
                    || !markers[0].IncludesCinematicCamera
                    || markers[0].LayoutRevision != OtterAquariumCombatSceneSetup.CurrentLayoutRevision
                    || markers[0].SourceScene != OtterAquariumCombatSceneSetup.SourceScenePath))
            {
                failures.Add("Aquarium combat ownership marker contains incorrect source, wave, camera, or revision settings.");
            }

            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);

            if (failures.Count > 0)
            {
                Debug.LogError("[OtterAquariumCombatValidation] Validation failed:\n- " + string.Join("\n- ", failures));
                return false;
            }

            if (logSuccess)
                Debug.Log("OTTER_AQUARIUM_COMBAT_SCENE_VALIDATION_PASS");
            return true;
        }

        private static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            List<T> results = new();
            foreach (GameObject root in scene.GetRootGameObjects())
                results.AddRange(root.GetComponentsInChildren<T>(true));
            return results.ToArray();
        }

        private static void RequireCount<T>(T[] components, int expected, string label, List<string> failures)
        {
            if (components.Length != expected)
                failures.Add($"Expected {expected} {label}; found {components.Length}.");
        }
    }
}
