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
        [MenuItem("Rhythm Hunter/Otter Aquarium/Validate Zoo Goblin Demo 1")]
        public static void ValidateFromMenu()
        {
            ValidateScene(true);
        }

        public static bool ValidateScene(bool logSuccess)
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(OtterGoblinDemo1SceneBuilder.ScenePath);
            OtterGoblinDemo1LevelData data =
                AssetDatabase.LoadAssetAtPath<OtterGoblinDemo1LevelData>(OtterGoblinDemo1SceneBuilder.DataPath);
            if (sceneAsset == null || data == null)
            {
                Debug.LogError($"[OtterGoblinDemo1Validation] Missing scene or chart. Scene={sceneAsset != null}, Data={data != null}");
                return false;
            }

            bool chartValid = data.Validate(out string chartError);
            Scene scene = SceneManager.GetSceneByPath(OtterGoblinDemo1SceneBuilder.ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(OtterGoblinDemo1SceneBuilder.ScenePath, OpenSceneMode.Additive);

            FmodBeatClock clock = Find<FmodBeatClock>(scene);
            OtterGoblinDemo1Runner runner = Find<OtterGoblinDemo1Runner>(scene);
            OtterGoblinDemo1Input input = Find<OtterGoblinDemo1Input>(scene);
            OtterGoblinDemo1Presenter presenter = Find<OtterGoblinDemo1Presenter>(scene);
            SpriteRenderer background = FindNamed<SpriteRenderer>(scene, "ZooBackground");
            SpriteRenderer goblin = FindNamed<SpriteRenderer>(scene, "GoblinSprite");
            Transform goblinRoot = FindNamedTransform(scene, "ZooGoblin");
            Transform otter = FindNamedTransform(scene, "Otter");
            TextMesh health = FindNamed<TextMesh>(scene, "Health");
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
                && health != null
                && data.OtterMaxHealth == 3
                && data.DamagePerMiss == 1
                && Mathf.Approximately(data.AuthoredBpm, 120f)
                && data.TotalBars == 33
                && data.Phrases.Count == 15
                && singleCount == 3
                && tripleCount == 2
                && doubleSingleCount == 5
                && tripleThenSingleCount == 5
                && data.GoodWindowMs >= data.PerfectWindowMs
                && Mathf.Approximately(data.MusicVolume, 0.55f)
                && data.MusicEventPath == "event:/ZooGoblinFight/BGM/Goblin Patrol"
                && data.WarningSoundEventPath == "event:/ZooGoblinFight/SoundEffects/Warning"
                && data.AttackSoundEventPath == "event:/ZooGoblinFight/SoundEffects/AxeGoblin_NormalAttack"
                && data.BlockSoundEventPath == "event:/ZooGoblinFight/SoundEffects/BeatTapping";

            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);

            if (!valid)
            {
                Debug.LogError(
                    "[OtterGoblinDemo1Validation] Validation failed. "
                    + $"Chart={chartValid} ({chartError}), Clock={clock != null}, Runner={runner != null}, "
                    + $"Input={input != null}, Presenter={presenter != null}, Background={background != null}, "
                    + $"Goblin={goblin != null}, GoblinRoot={goblinRoot != null}, "
                    + $"Otter={otter != null}, HealthHUD={health != null}, HP={data.OtterMaxHealth}, "
                    + $"Damage={data.DamagePerMiss}, Phrases={data.Phrases.Count}, "
                    + $"Single={singleCount}, Triple={tripleCount}, "
                    + $"DoubleSingle={doubleSingleCount}, TripleThenSingle={tripleThenSingleCount}");
                return false;
            }

            if (logSuccess)
                Debug.Log("OTTER_GOBLIN_DEMO1_VALIDATION_PASS");
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
