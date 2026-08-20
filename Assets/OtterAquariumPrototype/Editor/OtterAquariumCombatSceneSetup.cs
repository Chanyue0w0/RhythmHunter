using System.Collections.Generic;
using System.Linq;
using RhythmHunter.OtterAquariumPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    /// <summary>
    /// Creates the aquarium-owned combat scene once from the stable FightDemo
    /// scene. The target is intentionally not overwritten after creation so
    /// future production work can continue in OtterAquariumPrototype/Scenes.
    /// </summary>
    public static class OtterAquariumCombatSceneSetup
    {
        public const string SourceScenePath = "Assets/FightDemo/Scenes/FightScene.unity";
        public const string ScenePath = "Assets/OtterAquariumPrototype/Scenes/OtterAquariumCombat.unity";

        [InitializeOnLoadMethod]
        private static void QueueInitialSetup()
        {
            EditorApplication.delayCall += TryCreateInitialScene;
        }

        private static void TryCreateInitialScene()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += TryCreateInitialScene;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                CreateCombatScene();
        }

        [MenuItem("Rhythm Hunter/Create Otter Aquarium Combat Scene")]
        public static void CreateCombatScene()
        {
            SceneAsset source = AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath);
            if (source == null)
            {
                Debug.LogError($"[OtterAquariumCombat] Source fight scene is missing: {SourceScenePath}");
                return;
            }

            SceneAsset existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (existing == null && !AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
            {
                Debug.LogError($"[OtterAquariumCombat] Could not copy {SourceScenePath} to {ScenePath}.");
                return;
            }

            AddOwnershipMarker();
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!OtterAquariumCombatValidation.ValidateScene(false))
            {
                Debug.LogError("[OtterAquariumCombat] The copied combat scene failed integration validation.");
                return;
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"[OtterAquariumCombat] Combat scene ready for future work: {ScenePath}");
        }

        private static void AddOwnershipMarker()
        {
            Scene previousScene = SceneManager.GetActiveScene();
            Scene targetScene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = targetScene.IsValid() && targetScene.isLoaded;
            if (!wasLoaded)
                targetScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            SceneManager.SetActiveScene(targetScene);
            OtterAquariumCombatSceneMarker marker = FindInScene<OtterAquariumCombatSceneMarker>(targetScene);
            if (marker == null)
            {
                GameObject markerObject = new("OtterAquariumCombatProject", typeof(OtterAquariumCombatSceneMarker));
                SceneManager.MoveGameObjectToScene(markerObject, targetScene);
                marker = markerObject.GetComponent<OtterAquariumCombatSceneMarker>();
            }

            marker.Configure(SourceScenePath, false);
            EditorUtility.SetDirty(marker);
            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorSceneManager.SaveScene(targetScene, ScenePath);

            if (previousScene.IsValid() && previousScene.isLoaded)
                SceneManager.SetActiveScene(previousScene);
            if (!wasLoaded)
                EditorSceneManager.CloseScene(targetScene, true);
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
