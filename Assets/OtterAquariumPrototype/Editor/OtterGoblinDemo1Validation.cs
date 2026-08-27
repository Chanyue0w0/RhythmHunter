using RhythmHunter.OtterAquariumPrototype;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    public static class OtterGoblinDemo1Validation
    {
        [MenuItem("Rhythm Hunter/Otter Aquarium/Validate Shared Zoo Goblin Demo 1 Scene")]
        public static void ValidateFromMenu()
        {
            ValidateScene(true);
        }

        public static bool ValidateScene(bool logSuccess)
        {
            return ValidateScene(logSuccess, OtterGoblinDemo1SceneBuilder.ScenePath, null);
        }

        public static bool ValidateScene(
            bool logSuccess,
            string scenePath,
            string dataPath)
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (sceneAsset == null)
            {
                Debug.LogError($"[OtterGoblinDemo1Validation] Missing shared scene: {scenePath}");
                return false;
            }

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            FmodBeatClock clock = Find<FmodBeatClock>(scene);
            OtterGoblinDemo1Runner runner = Find<OtterGoblinDemo1Runner>(scene);
            OtterGoblinDemo1LevelData data = string.IsNullOrWhiteSpace(dataPath)
                ? runner != null ? runner.LevelData : null
                : AssetDatabase.LoadAssetAtPath<OtterGoblinDemo1LevelData>(dataPath);
            if (data == null)
            {
                if (!wasLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                Debug.LogError("[OtterGoblinDemo1Validation] The shared scene has no selected LevelData.");
                return false;
            }

            bool chartValid = data.Validate(out string chartError);
            OtterGoblinDemo1Input input = Find<OtterGoblinDemo1Input>(scene);
            OtterGoblinDemo1Presenter presenter = Find<OtterGoblinDemo1Presenter>(scene);
            SpriteRenderer background = FindNamed<SpriteRenderer>(scene, "ZooBackground");
            SpriteRenderer goblin = FindNamed<SpriteRenderer>(scene, "GoblinSprite");
            Transform goblinRoot = FindNamedTransform(scene, "ZooGoblin");
            Transform otter = FindNamedTransform(scene, "Otter");
            TextMesh failureCount = FindNamed<TextMesh>(scene, "FailureCount");
            int singleCount = 0;
            int tripleCount = 0;
            int doubleSingleCount = 0;
            int tripleThenSingleCount = 0;
            foreach (OtterGoblinDemo1LevelData.AttackPhrase phrase in data.Phrases)
            {
                if (phrase.Kind == OtterGoblinDemo1LevelData.AttackKind.Single)
                    singleCount++;
                else if (phrase.Kind == OtterGoblinDemo1LevelData.AttackKind.Triple)
                    tripleCount++;
                else if (phrase.Kind == OtterGoblinDemo1LevelData.AttackKind.DoubleSingle)
                    doubleSingleCount++;
                else if (phrase.Kind == OtterGoblinDemo1LevelData.AttackKind.TripleThenSingle)
                    tripleThenSingleCount++;
            }

            bool valid = chartValid
                && clock != null
                && clock.MusicEventPath == data.MusicEventPath
                && Mathf.Approximately(clock.MusicVolume, data.MusicVolume)
                && runner != null
                && runner.LevelData == data
                && input != null
                && presenter != null
                && presenter.AxeProjectilePrefab != null
                && presenter.AxeProjectilePrefab.GetComponent<RhythmTimelineProjectile>() != null
                && background != null
                && background.sprite != null
                && AssetDatabase.GetAssetPath(background.sprite) == OtterGoblinDemo1SceneBuilder.BackgroundPath
                && background.color == Color.white
                && goblin != null
                && goblin.sprite != null
                && goblinRoot != null
                && goblinRoot.position.x <= -4.5f
                && Mathf.Approximately(goblinRoot.localScale.x, 0.72f)
                && otter != null
                && otter.position.x >= 4.5f
                && Mathf.Approximately(Mathf.Abs(otter.localScale.x), 0.72f)
                && otter.position.x - goblin.transform.position.x >= 8f
                && failureCount != null
                && data.ExtraInputStunBeats >= 0.25f
                && data.AuthoredBpm > 0f
                && data.TotalBars >= 4
                && data.Phrases.Count > 0
                && data.GoodWindowMs >= data.PerfectWindowMs
                && !string.IsNullOrWhiteSpace(data.MusicEventPath)
                && !string.IsNullOrWhiteSpace(data.MissSoundEventPath);

            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);

            if (!valid)
            {
                Debug.LogError(
                    "[OtterGoblinDemo1Validation] Validation failed. "
                    + $"Chart={chartValid} ({chartError}), Clock={clock != null}, Runner={runner != null}, "
                    + $"Input={input != null}, Presenter={presenter != null}, Background={background != null}, "
                    + $"Goblin={goblin != null}, GoblinRoot={goblinRoot != null}, "
                    + $"Otter={otter != null}, FailureHUD={failureCount != null}, "
                    + $"Stun={data.ExtraInputStunBeats:0.##} beats, "
                    + $"Phrases={data.Phrases.Count}, "
                    + $"Single={singleCount}, Triple={tripleCount}, "
                    + $"DoubleSingle={doubleSingleCount}, TripleThenSingle={tripleThenSingleCount}");
                return false;
            }

            if (logSuccess)
                Debug.Log($"OTTER_GOBLIN_DEMO1_VALIDATION_PASS: {scenePath}");
            return true;
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }
            return null;
        }

        private static T FindNamed<T>(Scene scene, string objectName) where T : Component
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
