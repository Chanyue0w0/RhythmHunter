using RhythmHunter.OtterAquariumPrototype;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    public static class OtterShellBeatLabValidation
    {
        [MenuItem("Rhythm Hunter/Otter Aquarium/Validate Shell Beat Lab")]
        public static void ValidateFromMenu()
        {
            ValidateScene(true);
        }

        public static bool ValidateScene(bool logSuccess)
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(OtterShellBeatLabSceneBuilder.ScenePath);
            OtterRhythmLevelData levelData = AssetDatabase.LoadAssetAtPath<OtterRhythmLevelData>(OtterShellBeatLabSceneBuilder.LevelDataPath);
            if (sceneAsset == null || levelData == null)
            {
                Debug.LogError(
                    $"[OtterShellBeatLabValidation] Missing scene or level data. "
                    + $"Scene={sceneAsset != null}, Data={levelData != null}");
                return false;
            }

            Scene scene = SceneManager.GetSceneByPath(OtterShellBeatLabSceneBuilder.ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(OtterShellBeatLabSceneBuilder.ScenePath, OpenSceneMode.Additive);

            FmodBeatClock clock = FindInScene<FmodBeatClock>(scene);
            OtterRhythmLevelRunner runner = FindInScene<OtterRhythmLevelRunner>(scene);
            OtterRhythmLevelData linkedLevel = runner != null ? runner.LevelData : null;
            OtterRhythmInput input = FindInScene<OtterRhythmInput>(scene);
            OtterRhythmPresenter presenter = FindInScene<OtterRhythmPresenter>(scene);
            OtterMovementController movement = FindInScene<OtterMovementController>(scene);
            SpriteRenderer background = FindNamedInScene<SpriteRenderer>(scene, "ZooBackground");
            Transform crab = FindNamedTransform(scene, "CrabConductor");
            Transform otter = FindNamedTransform(scene, "PlayerOtter");
            TextMesh result = FindNamedInScene<TextMesh>(scene, "JudgementResult");

            bool valid = clock != null
                && runner != null
                && linkedLevel != null
                && input != null
                && presenter != null
                && movement == null
                && background != null
                && AssetDatabase.GetAssetPath(background.sprite) == OtterAquariumSceneBuilder.BackgroundSpritePath
                && crab != null
                && otter != null
                && result != null
                && linkedLevel.Phrases.Count > 0
                && linkedLevel.TotalBars >= 4
                && linkedLevel.Ppq >= 24
                && !string.IsNullOrWhiteSpace(linkedLevel.MusicEventPath)
                && linkedLevel.GoodWindowMs >= linkedLevel.PerfectWindowMs;

            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);

            if (!valid)
            {
                Debug.LogError(
                    "[OtterShellBeatLabValidation] Validation failed. "
                    + $"Clock={clock != null}, Runner={runner != null}, DataLinked={linkedLevel != null}, "
                    + $"Input={input != null}, Presenter={presenter != null}, MovementDisabled={movement == null}, "
                    + $"Background={background != null}, Crab={crab != null}, Otter={otter != null}, Result={result != null}, "
                    + $"Phrases={linkedLevel?.Phrases.Count ?? 0}, Bars={linkedLevel?.TotalBars ?? 0}");
                return false;
            }

            if (logSuccess)
                Debug.Log("[OtterShellBeatLabValidation] Scene and data passed all checks.");
            return true;
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

        private static T FindNamedInScene<T>(Scene scene, string objectName) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T component in root.GetComponentsInChildren<T>(true))
                {
                    if (component.name == objectName)
                        return component;
                }
            }
            return null;
        }

        private static Transform FindNamedTransform(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == objectName)
                        return transform;
                }
            }
            return null;
        }
    }
}
