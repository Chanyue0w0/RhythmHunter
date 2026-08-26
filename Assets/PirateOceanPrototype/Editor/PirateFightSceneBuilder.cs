using System.Collections.Generic;
using System.Linq;
using RhythmHunter.FightDemo;
using RhythmHunter.FightDemoEditor;
using RhythmHunter.PirateOceanPrototype;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmHunter.PirateOceanPrototypeEditor
{
    /// <summary>
    /// Creates a separate integration scene by combining the verified pirate
    /// environment with cloned FightScene controller and HUD objects.
    /// Neither source demo scene is modified.
    /// </summary>
    public static class PirateFightSceneBuilder
    {
        public const string ScenePath = "Assets/PirateOceanPrototype/Scenes/PirateFightScene.unity";

        private static bool SceneContainsIntegratedSystems()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            bool valid = FindComponents<PirateOceanWaveController>(scene).Length == 1
                && FindComponents<PirateShipMotionController>(scene).Length == 1
                && FindComponents<PirateBossCameraController>(scene).Length == 1
                && FindComponents<PirateOceanRuntimePanel>(scene).Length == 1
                && FindComponents<FightUnitSlot>(scene).Length == 6
                && FindComponents<FightCombatController>(scene).Length == 1
                && FindComponents<FightScenePresenter>(scene).Length == 1
                && FindComponents<FightBattlefieldPresenter>(scene).Length == 1;

            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);
            return valid;
        }

        [MenuItem("Rhythm Hunter/Build Pirate Fight Scene")]
        public static void BuildScene()
        {
            EnsureSourceScenes();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PirateOceanPrototypeSceneBuilder.ScenePath) == null
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(FightSceneBuilder.ScenePath) == null)
            {
                Debug.LogError("[PirateFightScene] One or more source scenes could not be generated.");
                return;
            }

            InputActionAsset controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(FightSceneBuilder.InputActionsPath);
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (controls == null || sprite == null)
            {
                Debug.LogError("[PirateFightScene] Fight controls or built-in world sprite is missing.");
                return;
            }

            Scene previousScene = SceneManager.GetActiveScene();
            bool replacingLoadedTarget = previousScene.IsValid()
                && previousScene.isLoaded
                && previousScene.path == ScenePath;

            Scene loadedTarget = SceneManager.GetSceneByPath(ScenePath);
            if (loadedTarget.IsValid() && loadedTarget.isLoaded)
                EditorSceneManager.CloseScene(loadedTarget, true);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                AssetDatabase.DeleteAsset(ScenePath);

            if (!AssetDatabase.CopyAsset(PirateOceanPrototypeSceneBuilder.ScenePath, ScenePath))
            {
                Debug.LogError("[PirateFightScene] Failed to copy the pirate prototype scene.");
                return;
            }

            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            OpenSceneMode targetMode = Application.isBatchMode ? OpenSceneMode.Single : OpenSceneMode.Additive;
            Scene targetScene = EditorSceneManager.OpenScene(ScenePath, targetMode);
            SceneManager.SetActiveScene(targetScene);

            Scene fightSource = SceneManager.GetSceneByPath(FightSceneBuilder.ScenePath);
            bool fightSourceWasLoaded = fightSource.IsValid() && fightSource.isLoaded;
            if (!fightSourceWasLoaded)
                fightSource = EditorSceneManager.OpenScene(FightSceneBuilder.ScenePath, OpenSceneMode.Additive);

            GameObject sourceCanvas = FindRoot(fightSource, "FightHudCanvas");
            GameObject sourceController = FindRoot(fightSource, "FightDemoController");
            if (sourceCanvas == null || sourceController == null)
            {
                Debug.LogError("[PirateFightScene] FightScene HUD or controller root is missing.");
                CloseTemporarySource(fightSource, fightSourceWasLoaded);
                return;
            }

            GameObject canvasClone = Object.Instantiate(sourceCanvas);
            canvasClone.name = "PirateFightHudCanvas";
            SceneManager.MoveGameObjectToScene(canvasClone, targetScene);

            GameObject controllerClone = Object.Instantiate(sourceController);
            controllerClone.name = "PirateFightController";
            SceneManager.MoveGameObjectToScene(controllerClone, targetScene);

            CreateEventSystem(targetScene);
            AddFmodListener(targetScene);
            ConfigureFightSystems(targetScene, controllerClone, canvasClone, controls, sprite);
            UpdateHudCopy(canvasClone);
            UpdatePrototypeTitle(targetScene);

            CloseTemporarySource(fightSource, fightSourceWasLoaded);

            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorSceneManager.SaveScene(targetScene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!PirateFightSceneValidation.ValidateScene(false))
                Debug.LogError("[PirateFightScene] Generated scene failed integration validation.");

            if (!Application.isBatchMode
                && !replacingLoadedTarget
                && previousScene.IsValid()
                && previousScene.isLoaded
                && previousScene != targetScene)
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(targetScene, true);
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"[PirateFightScene] Integrated scene created: {ScenePath}");
        }

        private static void EnsureSourceScenes()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PirateOceanPrototypeSceneBuilder.ScenePath) == null)
                PirateOceanPrototypeSceneBuilder.BuildScene();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FightSceneBuilder.ScenePath) == null)
                FightSceneBuilder.BuildScene();
        }

        private static void ConfigureFightSystems(
            Scene targetScene,
            GameObject controllerObject,
            GameObject canvasObject,
            InputActionAsset controls,
            Sprite sprite)
        {
            FightUnitSlot[] slots = FindComponents<FightUnitSlot>(targetScene);
            FightUnitSlot[] enemies = slots
                .Where(slot => slot.Team == FightUnitSlot.UnitTeam.Enemy)
                .OrderBy(slot => slot.SlotIndex)
                .ToArray();
            FightUnitSlot[] heroes = slots
                .Where(slot => slot.Team == FightUnitSlot.UnitTeam.Hero)
                .OrderBy(slot => slot.SlotIndex)
                .ToArray();

            if (enemies.Length != 3 || heroes.Length != 3)
            {
                Debug.LogError($"[PirateFightScene] Expected 3 enemies and 3 heroes, found {enemies.Length}/{heroes.Length}.");
                return;
            }

            SpriteRenderer tankShield = CreateWorldSprite(
                "TankShieldEffect",
                heroes[0].ActorRoot.parent,
                sprite,
                new Color(0.3f, 1f, 0.55f, 0f),
                new Vector3(0f, 0.05f, -0.5f),
                new Vector2(1.7f, 2.35f),
                25);
            SpriteRenderer enemyTelegraph = CreateWorldSprite(
                "EnemyTelegraph",
                enemies[1].ActorRoot.parent,
                sprite,
                new Color(1f, 0.22f, 0.25f, 0f),
                new Vector3(0f, 0.05f, -0.5f),
                new Vector2(1.55f, 2.2f),
                24);

            FmodBeatClock clock = controllerObject.GetComponent<FmodBeatClock>();
            FmodRhythmJudge judge = controllerObject.GetComponent<FmodRhythmJudge>();
            FightInputRouter input = controllerObject.GetComponent<FightInputRouter>();
            FightCombatController fight = controllerObject.GetComponent<FightCombatController>();
            FightScenePresenter hud = controllerObject.GetComponent<FightScenePresenter>();
            FightBattlefieldPresenter battlefield = controllerObject.GetComponent<FightBattlefieldPresenter>();

            clock.Configure("event:/Combat soundtracks/Combat 01", 1f, true);
            judge.Configure(clock, 120f, 30f);
            input.Configure(controls);
            fight.Configure(clock, judge, input, heroes[0], enemies[1], 120, 18);
            battlefield.Configure(fight, enemies, heroes, tankShield, enemyTelegraph);
            hud.Configure(
                clock,
                judge,
                fight,
                FindChild<Text>(canvasObject.transform, "PlaybackStatus"),
                FindChild<Text>(canvasObject.transform, "CycleReadout"),
                FindChild<Text>(canvasObject.transform, "AttackWarning"),
                FindChild<Text>(canvasObject.transform, "FightResult"),
                FindChild<Text>(canvasObject.transform, "FightDetail"),
                FindChild<Text>(canvasObject.transform, "TankHealth"),
                FindChild<Text>(canvasObject.transform, "Statistics"),
                new[]
                {
                    FindChild<Image>(canvasObject.transform, "Beat_1"),
                    FindChild<Image>(canvasObject.transform, "Beat_2"),
                    FindChild<Image>(canvasObject.transform, "Beat_3"),
                    FindChild<Image>(canvasObject.transform, "Beat_4")
                },
                FindChild<Slider>(canvasObject.transform, "BeatProgress"),
                FindChild<Slider>(canvasObject.transform, "TankHealthBar"),
                FindChild<Image>(canvasObject.transform, "DamageFlash"));
        }

        private static void UpdateHudCopy(GameObject canvasObject)
        {
            Text title = FindChild<Text>(canvasObject.transform, "Title");
            if (title != null)
                title.text = "RHYTHM HUNTER  |  PIRATE SHIP BATTLE";

            Text detail = FindChild<Text>(canvasObject.transform, "FightDetail");
            if (detail != null)
                detail.text = "Press Q on beat 4 to guard  |  F1 ocean lab  |  B boss camera";
        }

        private static void UpdatePrototypeTitle(Scene scene)
        {
            foreach (TextMesh text in FindComponents<TextMesh>(scene))
            {
                if (text.name == "PrototypeTitle")
                    text.text = "PIRATE FIGHT SCENE  |  RHYTHM COMBAT + OCEAN SYSTEM";
            }
        }

        private static void CreateEventSystem(Scene scene)
        {
            if (FindComponents<EventSystem>(scene).Length > 0)
                return;

            GameObject eventSystem = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
        }

        private static void AddFmodListener(Scene scene)
        {
            Camera[] cameras = FindComponents<Camera>(scene);
            if (cameras.Length == 0)
                return;

            if (cameras[0].GetComponent<FMODUnity.StudioListener>() == null)
                cameras[0].gameObject.AddComponent<FMODUnity.StudioListener>();
        }

        private static SpriteRenderer CreateWorldSprite(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            Vector3 localPosition,
            Vector2 size,
            int sortingOrder)
        {
            GameObject gameObject = new(name, typeof(SpriteRenderer));
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            Vector2 nativeSize = sprite != null ? sprite.bounds.size : Vector2.one;
            gameObject.transform.localScale = new Vector3(
                nativeSize.x > Mathf.Epsilon ? size.x / nativeSize.x : size.x,
                nativeSize.y > Mathf.Epsilon ? size.y / nativeSize.y : size.y,
                1f);

            SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static T FindChild<T>(Transform root, string name) where T : Component
        {
            return root.GetComponentsInChildren<T>(true).FirstOrDefault(component => component.name == name);
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
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

        private static void CloseTemporarySource(Scene scene, bool wasLoaded)
        {
            if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        private static void AddSceneToBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(item => item.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
