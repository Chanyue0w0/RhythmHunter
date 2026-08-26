using System.Collections.Generic;
using System.Linq;
using RhythmHunter.OtterAquariumPrototype;
using RhythmHunter.PirateOceanPrototype;
using RhythmHunter.PirateOceanPrototypeEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    /// <summary>
    /// Rebuilds the aquarium-owned combat scene from PirateFightScene, retaining
    /// its fight staging and cinematic camera while stripping all wave systems.
    /// </summary>
    public static class OtterAquariumCombatSceneSetup
    {
        public const string SourceScenePath = "Assets/PirateOceanPrototype/Scenes/PirateFightScene.unity";
        public const string ScenePath = "Assets/OtterAquariumPrototype/Scenes/OtterAquariumCombat.unity";
        public const int CurrentLayoutRevision = 4;

        [MenuItem("Rhythm Hunter/Rebuild Otter Aquarium Combat From Pirate Fight")]
        public static void RebuildCombatScene()
        {
            SceneAsset source = AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath);
            if (source == null)
            {
                PirateFightSceneBuilder.BuildScene();
                source = AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath);
                if (source == null)
                {
                    Debug.LogError($"[OtterAquariumCombat] Source pirate fight scene is missing: {SourceScenePath}");
                    return;
                }
            }

            Scene previousScene = SceneManager.GetActiveScene();
            bool replacingLoadedTarget = previousScene.IsValid()
                && previousScene.isLoaded
                && previousScene.path == ScenePath;
            Scene loadedTarget = SceneManager.GetSceneByPath(ScenePath);
            Scene temporaryScene = default;
            bool createdTemporaryScene = false;
            if (loadedTarget.IsValid() && loadedTarget.isLoaded)
            {
                if (SceneManager.sceneCount == 1)
                {
                    temporaryScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    SceneManager.SetActiveScene(temporaryScene);
                    createdTemporaryScene = true;
                }
                EditorSceneManager.CloseScene(loadedTarget, true);
            }

            SceneAsset existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            bool copied = existing == null
                ? AssetDatabase.CopyAsset(SourceScenePath, ScenePath)
                : ReplaceSceneContentsPreservingMeta();
            if (!copied)
            {
                Debug.LogError($"[OtterAquariumCombat] Could not copy {SourceScenePath} to {ScenePath}.");
                return;
            }

            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            OpenSceneMode openMode = Application.isBatchMode ? OpenSceneMode.Single : OpenSceneMode.Additive;
            Scene targetScene = EditorSceneManager.OpenScene(ScenePath, openMode);
            SceneManager.SetActiveScene(targetScene);

            StripWaveSystems(targetScene);
            AddOwnershipMarker(targetScene);
            UpdateSceneLabels(targetScene);
            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorSceneManager.SaveScene(targetScene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!OtterAquariumCombatValidation.ValidateScene(false))
            {
                Debug.LogError("[OtterAquariumCombat] The copied combat scene failed integration validation.");
                return;
            }

            if (replacingLoadedTarget)
            {
                SceneManager.SetActiveScene(targetScene);
                if (createdTemporaryScene && temporaryScene.IsValid() && temporaryScene.isLoaded)
                    EditorSceneManager.CloseScene(temporaryScene, true);
            }
            else if (!Application.isBatchMode
                && !replacingLoadedTarget
                && previousScene.IsValid()
                && previousScene.isLoaded
                && previousScene != targetScene)
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(targetScene, true);
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"[OtterAquariumCombat] Pirate fight staging rebuilt without waves: {ScenePath}");
        }

        private static bool ReplaceSceneContentsPreservingMeta()
        {
            FileUtil.ReplaceFile(SourceScenePath, ScenePath);
            return true;
        }

        private static void StripWaveSystems(Scene targetScene)
        {
            foreach (PirateShipMotionController shipMotion in FindAllInScene<PirateShipMotionController>(targetScene))
            {
                shipMotion.ResetToBaseline();
                Object.DestroyImmediate(shipMotion);
            }

            foreach (PirateOceanWaveController waveController in FindAllInScene<PirateOceanWaveController>(targetScene))
                Object.DestroyImmediate(waveController);
            foreach (PirateOceanSurface surface in FindAllInScene<PirateOceanSurface>(targetScene))
                Object.DestroyImmediate(surface.gameObject);
            foreach (PirateOceanRuntimePanel panel in FindAllInScene<PirateOceanRuntimePanel>(targetScene))
                Object.DestroyImmediate(panel.gameObject);

            string[] waveVisualRoots = { "FarWaveBand", "MidWaveBand", "NearWaveBand", "FoamBand", "OceanStageNote" };
            foreach (GameObject gameObject in FindAllGameObjects(targetScene))
            {
                if (gameObject == null)
                    continue;
                if (waveVisualRoots.Contains(gameObject.name))
                    Object.DestroyImmediate(gameObject);
            }
        }

        private static void AddOwnershipMarker(Scene targetScene)
        {
            OtterAquariumCombatSceneMarker marker = FindInScene<OtterAquariumCombatSceneMarker>(targetScene);
            if (marker == null)
            {
                GameObject markerObject = new("OtterAquariumCombatProject", typeof(OtterAquariumCombatSceneMarker));
                SceneManager.MoveGameObjectToScene(markerObject, targetScene);
                marker = markerObject.GetComponent<OtterAquariumCombatSceneMarker>();
            }

            marker.Configure(SourceScenePath, false, true, CurrentLayoutRevision);
            EditorUtility.SetDirty(marker);
        }

        private static void UpdateSceneLabels(Scene targetScene)
        {
            foreach (GameObject root in targetScene.GetRootGameObjects())
            {
                if (root.name == "PirateOceanPrototype")
                    root.name = "OtterAquariumCombatStage";
                else if (root.name == "PirateFightController")
                    root.name = "OtterAquariumCombatController";
                else if (root.name == "PirateFightHudCanvas")
                    root.name = "OtterAquariumCombatHudCanvas";
            }

            foreach (TextMesh text in FindAllInScene<TextMesh>(targetScene))
            {
                if (text.name == "PrototypeTitle")
                    text.text = "OTTER AQUARIUM COMBAT  |  RHYTHM BATTLE + CINEMATIC CAMERA";
            }

            foreach (Text text in FindAllInScene<Text>(targetScene))
            {
                if (text.name == "Title")
                    text.text = "RHYTHM HUNTER  |  OTTER AQUARIUM COMBAT";
                else if (text.name == "FightDetail")
                    text.text = "Press Q on beat 4 to guard  |  B toggles combat / boss camera";
            }
        }

        private static bool SceneRequiresRebuild()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            OtterAquariumCombatSceneMarker marker = FindInScene<OtterAquariumCombatSceneMarker>(scene);
            bool needsRebuild = marker == null
                || marker.LayoutRevision < CurrentLayoutRevision
                || marker.SourceScene != SourceScenePath
                || marker.IncludesOceanWaves
                || !marker.IncludesCinematicCamera
                || FindInScene<PirateBossCameraController>(scene) == null;

            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);
            return needsRebuild;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }
            return null;
        }

        private static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            List<T> results = new();
            foreach (GameObject root in scene.GetRootGameObjects())
                results.AddRange(root.GetComponentsInChildren<T>(true));
            return results.ToArray();
        }

        private static GameObject[] FindAllGameObjects(Scene scene)
        {
            List<GameObject> results = new();
            foreach (GameObject root in scene.GetRootGameObjects())
                results.AddRange(root.GetComponentsInChildren<Transform>(true).Select(item => item.gameObject));
            return results.ToArray();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(scene => scene.path != scenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
