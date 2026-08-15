using RhythmHunter.FightDemo;
using RhythmHunter.PirateOceanPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.PirateOceanPrototypeEditor
{
    /// <summary>
    /// Builds the isolated pirate-ocean scene skeleton used to prototype waves,
    /// ship motion, and the later boss camera transition.
    /// </summary>
    public static class PirateOceanPrototypeSceneBuilder
    {
        // Kept as a public constant so later prototype stages can reopen or rebuild this scene.
        public const string ScenePath = "Assets/PirateOceanPrototype/Scenes/PirateOceanPrototype.unity";

        private static readonly Color SkyTop = new(0.035f, 0.12f, 0.2f, 1f);
        private static readonly Color SkyHorizon = new(0.18f, 0.42f, 0.5f, 1f);
        private static readonly Color OceanFar = new(0.04f, 0.28f, 0.42f, 1f);
        private static readonly Color OceanNear = new(0.025f, 0.16f, 0.3f, 1f);
        private static readonly Color Foam = new(0.62f, 0.9f, 0.9f, 0.8f);
        private static readonly Color HullDark = new(0.16f, 0.07f, 0.035f, 1f);
        private static readonly Color HullWood = new(0.42f, 0.19f, 0.07f, 1f);
        private static readonly Color DeckWood = new(0.62f, 0.36f, 0.12f, 1f);
        private static readonly Color SailCanvas = new(0.82f, 0.73f, 0.52f, 1f);
        private static readonly Color EnemyRed = new(0.9f, 0.2f, 0.16f, 1f);
        private static readonly Color HeroCyan = new(0.12f, 0.82f, 0.9f, 1f);
        private static readonly Color GuideGold = new(1f, 0.72f, 0.2f, 1f);

        [InitializeOnLoadMethod]
        private static void QueueInitialBuild()
        {
            EditorApplication.delayCall += TryBuildInitialScene;
        }

        private static void TryBuildInitialScene()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += TryBuildInitialScene;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null || !SceneContainsCurrentPrototypeSystems())
                BuildScene();
        }

        private static bool SceneContainsCurrentPrototypeSystems()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            bool foundWaveController = false;
            bool foundContinuousSurface = false;
            bool foundShipMotion = false;
            bool foundBossCamera = false;
            bool foundRuntimePanel = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<PirateOceanWaveController>(true) != null)
                    foundWaveController = true;
                if (root.GetComponentInChildren<PirateOceanSurface>(true) != null)
                    foundContinuousSurface = true;
                if (root.GetComponentInChildren<PirateShipMotionController>(true) != null)
                    foundShipMotion = true;
                if (root.GetComponentInChildren<PirateBossCameraController>(true) != null)
                    foundBossCamera = true;
                if (root.GetComponentInChildren<PirateOceanRuntimePanel>(true) != null)
                    foundRuntimePanel = true;
            }

            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);

            return foundWaveController
                && foundContinuousSurface
                && foundShipMotion
                && foundBossCamera
                && foundRuntimePanel;
        }

        [MenuItem("Rhythm Hunter/Build Pirate Ocean Prototype Scene")]
        public static void BuildScene()
        {
            EnsureFolders();

            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (sprite == null || font == null)
            {
                Debug.LogError("[PirateOceanPrototype] Required built-in sprite or font was not found.");
                return;
            }

            Scene previousScene = SceneManager.GetActiveScene();
            bool replacingLoadedPrototype = previousScene.IsValid()
                && previousScene.isLoaded
                && previousScene.path == ScenePath;
            NewSceneMode mode = Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            scene.name = "PirateOceanPrototype";
            SceneManager.SetActiveScene(scene);

            if (!Application.isBatchMode && replacingLoadedPrototype)
                EditorSceneManager.CloseScene(previousScene, true);

            Camera mainCamera = CreateCamera();

            Transform prototypeRoot = new GameObject("PirateOceanPrototype").transform;
            Transform environmentRoot = CreateEmpty("EnvironmentRoot", prototypeRoot);
            PirateOceanWaveController waveController = CreateEnvironment(environmentRoot, sprite, font);

            Transform shipSystemRoot = CreateEmpty("ShipSystemRoot", prototypeRoot);
            Transform shipMotionRoot = CreateEmpty("ShipMotionRoot (Visuals Only)", shipSystemRoot);
            Transform shipVisualRoot = CreateEmpty("ShipVisualRoot", shipMotionRoot);
            CreateShip(shipVisualRoot, sprite);

            Transform combatRoot = CreateEmpty("DeckCombatRoot (Stable Logic)", shipSystemRoot);
            Transform deckVisualRoot = CreateEmpty("DeckVisualRoot", shipMotionRoot);
            CreateCombatSlots(combatRoot, deckVisualRoot, sprite, font);

            PirateShipMotionController shipMotion = shipSystemRoot.gameObject.AddComponent<PirateShipMotionController>();
            shipMotion.Configure(shipMotionRoot, combatRoot);

            Transform cameraTargets = CreateEmpty("CameraTargets", prototypeRoot);
            Transform shipCombatTarget = CreateMarker("ShipCombatTarget", cameraTargets, new Vector3(0f, 0.25f, 0f));
            Transform bossWideTarget = CreateMarker("BossWideTarget", cameraTargets, new Vector3(0f, 3.1f, 0f));

            Transform bossRoot = CreateEmpty("BossPreviewRoot (Wide Shot)", prototypeRoot);
            bossRoot.localPosition = new Vector3(0f, 8.1f, 0f);
            CreateBossPlaceholder(bossRoot, sprite, font);

            PirateBossCameraController bossCamera = CreateCinemachineSystem(
                prototypeRoot,
                mainCamera,
                shipCombatTarget,
                bossWideTarget);
            CreateRuntimePanel(prototypeRoot, waveController, shipMotion, bossCamera);

            CreateWorldText(
                "PrototypeTitle",
                prototypeRoot,
                font,
                "PIRATE SHIP COMBAT  |  3 ENEMIES  vs  3 HEROES",
                new Color(0.9f, 0.96f, 1f, 0.75f),
                new Vector3(0f, 4.55f, 0f),
                0.022f,
                FontStyle.Bold,
                100);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!Application.isBatchMode
                && !replacingLoadedPrototype
                && previousScene.IsValid()
                && previousScene.isLoaded
                && previousScene != scene)
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"[PirateOceanPrototype] Scene skeleton created: {ScenePath}");
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CinemachineBrain));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyTop;
            camera.orthographic = true;
            camera.orthographicSize = 5.35f;
            return camera;
        }

        private static PirateBossCameraController CreateCinemachineSystem(
            Transform prototypeRoot,
            Camera mainCamera,
            Transform shipCombatTarget,
            Transform bossWideTarget)
        {
            Transform cameraSystemRoot = CreateEmpty("CinemachineCameraSystem", prototypeRoot);
            CinemachineCamera combatCamera = CreateCinemachineCamera(
                "ShipCombatCamera",
                cameraSystemRoot,
                shipCombatTarget,
                5.35f,
                20);
            CinemachineCamera bossCamera = CreateCinemachineCamera(
                "BossWideCamera",
                cameraSystemRoot,
                bossWideTarget,
                8.1f,
                10);

            PirateBossCameraController controller = cameraSystemRoot.gameObject.AddComponent<PirateBossCameraController>();
            controller.Configure(mainCamera.GetComponent<CinemachineBrain>(), combatCamera, bossCamera);
            return controller;
        }

        private static void CreateRuntimePanel(
            Transform prototypeRoot,
            PirateOceanWaveController waveController,
            PirateShipMotionController shipMotion,
            PirateBossCameraController bossCamera)
        {
            GameObject panelObject = new("RuntimeControlPanel", typeof(PirateOceanRuntimePanel));
            panelObject.transform.SetParent(prototypeRoot, false);
            panelObject.GetComponent<PirateOceanRuntimePanel>().Configure(waveController, shipMotion, bossCamera);
        }

        private static CinemachineCamera CreateCinemachineCamera(
            string name,
            Transform parent,
            Transform framingTarget,
            float orthographicSize,
            int priority)
        {
            GameObject cameraObject = new(name, typeof(CinemachineCamera));
            cameraObject.transform.SetParent(parent, false);
            Vector3 targetPosition = framingTarget.position;
            cameraObject.transform.position = new Vector3(targetPosition.x, targetPosition.y, -10f);

            CinemachineCamera camera = cameraObject.GetComponent<CinemachineCamera>();
            LensSettings lens = camera.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = orthographicSize;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 1000f;
            camera.Lens = lens;
            camera.Priority = priority;
            return camera;
        }

        private static PirateOceanWaveController CreateEnvironment(Transform root, Sprite sprite, Font font)
        {
            CreateWorldSprite("Sky", root, sprite, SkyTop, new Vector3(0f, 2.2f, 5f), new Vector2(24f, 15f), -100);
            CreateWorldSprite("HorizonBand", root, sprite, SkyHorizon, new Vector3(0f, -0.1f, 4f), new Vector2(24f, 4.2f), -95);

            Transform oceanRoot = CreateEmpty("OceanVisualRoot (Wave Stage)", root);
            CreateWorldSprite("OceanDepthBackdrop", oceanRoot, sprite, OceanFar, new Vector3(0f, -3.55f, 3f), new Vector2(28f, 7.1f), -85);
            CreateWorldSprite("OceanNearBase", oceanRoot, sprite, OceanNear, new Vector3(0f, -4.25f, 2f), new Vector2(24f, 3.3f), 20);

            GameObject continuousWaterObject = new("ContinuousWaterSurface", typeof(PirateOceanSurface));
            continuousWaterObject.transform.SetParent(oceanRoot, false);
            PirateOceanSurface continuousSurface = continuousWaterObject.GetComponent<PirateOceanSurface>();
            continuousSurface.Configure(
                28f,
                -0.25f,
                -7f,
                96,
                -75,
                new Color(0.08f, 0.44f, 0.58f, 1f),
                new Color(0.018f, 0.09f, 0.2f, 1f));

            Transform farBand = CreateEmpty("FarWaveBand", oceanRoot);
            Transform midBand = CreateEmpty("MidWaveBand", oceanRoot);
            Transform nearBand = CreateEmpty("NearWaveBand", oceanRoot);
            Transform foamBand = CreateEmpty("FoamBand", oceanRoot);

            Transform[] farSegments = CreateWaveBand("FarWave", farBand, sprite, new Color(0.2f, 0.55f, 0.64f, 0.62f), -70, 8, 3.15f, -0.55f, new Vector2(2.7f, 0.09f));
            Transform[] midSegments = CreateWaveBand("MidWave", midBand, sprite, new Color(0.12f, 0.46f, 0.6f, 0.78f), -60, 9, 2.75f, -1.62f, new Vector2(2.25f, 0.13f));
            Transform[] nearSegments = CreateWaveBand("NearWave", nearBand, sprite, new Color(0.07f, 0.34f, 0.52f, 1f), 24, 8, 3.2f, -2.48f, new Vector2(2.7f, 0.2f));
            SpriteRenderer[] foamSegments = CreateFoamBand(foamBand, sprite, 11, 2.25f, -2.3f);

            PirateOceanWaveController waveController = oceanRoot.gameObject.AddComponent<PirateOceanWaveController>();
            waveController.Configure(continuousSurface, farSegments, midSegments, nearSegments, foamSegments);

            CreateWorldText(
                "OceanStageNote",
                oceanRoot,
                font,
                "OCEAN WAVE CONTROLLER  -  ADJUST SEA STATE IN THE INSPECTOR",
                new Color(0.75f, 0.94f, 1f, 0.45f),
                new Vector3(0f, -4.65f, 0f),
                0.014f,
                FontStyle.Normal,
                101);
            return waveController;
        }

        private static Transform[] CreateWaveBand(
            string prefix,
            Transform parent,
            Sprite sprite,
            Color color,
            int sortingOrder,
            int count,
            float spacing,
            float y,
            Vector2 size)
        {
            Transform[] segments = new Transform[count];
            float startX = -(count - 1) * spacing * 0.5f;
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer renderer = CreateWorldSprite(
                    $"{prefix}_{i + 1:00}",
                    parent,
                    sprite,
                    color,
                    new Vector3(startX + i * spacing, y, 0f),
                    size,
                    sortingOrder);
                segments[i] = renderer.transform;
            }

            return segments;
        }

        private static SpriteRenderer[] CreateFoamBand(Transform parent, Sprite sprite, int count, float spacing, float y)
        {
            SpriteRenderer[] segments = new SpriteRenderer[count];
            float startX = -(count - 1) * spacing * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float width = i % 2 == 0 ? 1.35f : 0.9f;
                segments[i] = CreateWorldSprite(
                    $"Foam_{i + 1:00}",
                    parent,
                    sprite,
                    Foam,
                    new Vector3(startX + i * spacing, y, 0f),
                    new Vector2(width, 0.075f),
                    26);
            }

            return segments;
        }

        private static void CreateShip(Transform root, Sprite sprite)
        {
            CreateWorldSprite("Hull", root, sprite, HullDark, new Vector3(0f, -1.85f, 0f), new Vector2(13.9f, 1.8f), 0);
            CreateWorldSprite("HullWoodBand", root, sprite, HullWood, new Vector3(0f, -1.45f, -0.1f), new Vector2(13.35f, 0.75f), 2);
            CreateWorldSprite("Deck", root, sprite, DeckWood, new Vector3(0f, -1.05f, -0.2f), new Vector2(13.6f, 0.22f), 5);
            CreateWorldSprite("LeftRail", root, sprite, HullWood, new Vector3(-6.55f, -0.65f, 0f), new Vector2(0.18f, 0.85f), 6);
            CreateWorldSprite("RightRail", root, sprite, HullWood, new Vector3(6.55f, -0.65f, 0f), new Vector2(0.18f, 0.85f), 6);

            Transform mastRoot = CreateEmpty("MastAndSail", root);
            CreateWorldSprite("Mast", mastRoot, sprite, HullDark, new Vector3(0f, 1.1f, 0.4f), new Vector2(0.16f, 4.35f), 3);
            CreateWorldSprite("Yard", mastRoot, sprite, HullWood, new Vector3(0f, 2.45f, 0.3f), new Vector2(2.65f, 0.12f), 3);
            CreateWorldSprite("Sail", mastRoot, sprite, SailCanvas, new Vector3(0f, 1.65f, 0.2f), new Vector2(1.75f, 1.35f), 4);

            CreateWorldSprite("ShipMotionPivotGuide", root, sprite, new Color(GuideGold.r, GuideGold.g, GuideGold.b, 0.35f), new Vector3(0f, -1.05f, -0.5f), new Vector2(0.22f, 0.22f), 30);
        }

        private static void CreateCombatSlots(Transform combatRoot, Transform deckVisualRoot, Sprite sprite, Font font)
        {
            Transform enemyRoot = CreateEmpty("EnemySlots_Left (Stable)", combatRoot);
            Transform heroRoot = CreateEmpty("HeroSlots_Right (Stable)", combatRoot);
            Transform enemyVisualRoot = CreateEmpty("EnemyVisuals_Left", deckVisualRoot);
            Transform heroVisualRoot = CreateEmpty("HeroVisuals_Right", deckVisualRoot);

            CreateUnitSlot(enemyRoot, enemyVisualRoot, sprite, font, "EnemySlot_1", "BOARDER 1", FightUnitSlot.UnitTeam.Enemy, FightUnitSlot.UnitRole.Enemy, 0, new Vector3(-5.8f, 0.15f, 0f), 80, 12, EnemyRed);
            CreateUnitSlot(enemyRoot, enemyVisualRoot, sprite, font, "EnemySlot_2", "BOARDER 2", FightUnitSlot.UnitTeam.Enemy, FightUnitSlot.UnitRole.Enemy, 1, new Vector3(-4.05f, 0.15f, 0f), 120, 18, EnemyRed);
            CreateUnitSlot(enemyRoot, enemyVisualRoot, sprite, font, "EnemySlot_3", "BOARDER 3", FightUnitSlot.UnitTeam.Enemy, FightUnitSlot.UnitRole.Enemy, 2, new Vector3(-2.3f, 0.15f, 0f), 90, 14, EnemyRed);

            CreateUnitSlot(heroRoot, heroVisualRoot, sprite, font, "HeroSlot_Tank", "TANK", FightUnitSlot.UnitTeam.Hero, FightUnitSlot.UnitRole.Tank, 0, new Vector3(2.3f, 0.15f, 0f), 120, 12, HeroCyan);
            CreateUnitSlot(heroRoot, heroVisualRoot, sprite, font, "HeroSlot_Support", "SUPPORT", FightUnitSlot.UnitTeam.Hero, FightUnitSlot.UnitRole.Support, 1, new Vector3(4.05f, 0.15f, 0f), 85, 8, HeroCyan);
            CreateUnitSlot(heroRoot, heroVisualRoot, sprite, font, "HeroSlot_Damage", "DAMAGE", FightUnitSlot.UnitTeam.Hero, FightUnitSlot.UnitRole.Damage, 2, new Vector3(5.8f, 0.15f, 0f), 75, 24, HeroCyan);
        }

        private static FightUnitSlot CreateUnitSlot(
            Transform logicalParent,
            Transform visualParent,
            Sprite sprite,
            Font font,
            string objectName,
            string displayName,
            FightUnitSlot.UnitTeam team,
            FightUnitSlot.UnitRole role,
            int index,
            Vector3 position,
            int hp,
            int attack,
            Color color)
        {
            GameObject slotObject = new(objectName);
            slotObject.transform.SetParent(logicalParent, false);
            slotObject.transform.localPosition = position;
            FightUnitSlot slot = slotObject.AddComponent<FightUnitSlot>();

            Transform slotVisualRoot = CreateEmpty($"{objectName}_Visual", visualParent);
            slotVisualRoot.localPosition = position;
            CreateWorldSprite("SlotGround", slotVisualRoot, sprite, new Color(color.r, color.g, color.b, 0.28f), new Vector3(0f, -1.25f, 0.3f), new Vector2(1.45f, 0.22f), 8);

            Transform actorRoot = CreateEmpty("ActorRoot (Assign Prefab Here)", slotVisualRoot);
            Transform placeholder = CreateEmpty("PrototypePlaceholder", actorRoot);
            CreateWorldSprite("Body", placeholder, sprite, new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 1f), new Vector3(0f, -0.05f, 0f), new Vector2(0.9f, 1.55f), 10);
            CreateWorldSprite("Accent", placeholder, sprite, color, new Vector3(0f, 0.18f, -0.1f), new Vector2(0.52f, 0.62f), 11);
            CreateWorldText("PrefabLabel", placeholder, font, "PREFAB\nSLOT", Color.white, new Vector3(0f, 0.15f, -0.2f), 0.018f, FontStyle.Bold, 12);

            Transform effectPoint = CreateEmpty("NormalAttackEffectSpawnPoint", slotVisualRoot);
            effectPoint.localPosition = new Vector3(team == FightUnitSlot.UnitTeam.Hero ? -0.72f : 0.72f, 0.15f, -0.3f);

            CreateWorldText("UnitName", slotVisualRoot, font, displayName, Color.white, new Vector3(0f, -1.55f, 0f), 0.021f, FontStyle.Bold, 15);
            CreateWorldSprite("HealthBackground", slotVisualRoot, sprite, new Color(0.025f, 0.035f, 0.045f, 1f), new Vector3(0f, 1.28f, 0f), new Vector2(1.18f, 0.11f), 15);
            SpriteRenderer hpFill = CreateWorldSprite("HealthFill", slotVisualRoot, sprite, color, new Vector3(0f, 1.28f, -0.1f), new Vector2(1.13f, 0.065f), 16);
            TextMesh hpLabel = CreateWorldText("Stats", slotVisualRoot, font, $"HP {hp}/{hp}", new Color(0.78f, 0.9f, 0.94f, 1f), new Vector3(0f, 1.52f, 0f), 0.012f, FontStyle.Normal, 17);

            slot.Configure(objectName, displayName, team, role, index, hp, attack, color, actorRoot, effectPoint, placeholder.gameObject, sprite, hpFill, hpLabel);
            return slot;
        }

        private static void CreateBossPlaceholder(Transform root, Sprite sprite, Font font)
        {
            CreateWorldSprite("BossSilhouette", root, sprite, new Color(0.13f, 0.055f, 0.2f, 1f), Vector3.zero, new Vector2(4.4f, 3.1f), -30);
            CreateWorldSprite("BossCore", root, sprite, new Color(0.55f, 0.16f, 0.55f, 1f), new Vector3(0f, 0.25f, -0.1f), new Vector2(1.5f, 1.2f), -29);
            CreateWorldSprite("TentacleLeft", root, sprite, new Color(0.25f, 0.08f, 0.3f, 1f), new Vector3(-2.5f, -1.2f, 0f), new Vector2(1.1f, 3.5f), -30).transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
            CreateWorldSprite("TentacleRight", root, sprite, new Color(0.25f, 0.08f, 0.3f, 1f), new Vector3(2.5f, -1.2f, 0f), new Vector2(1.1f, 3.5f), -30).transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
            CreateWorldText("BossLabel", root, font, "SEA MONSTER BOSS\nWIDE-SHOT PLACEHOLDER", new Color(1f, 0.55f, 0.8f, 1f), new Vector3(0f, 0.2f, -0.2f), 0.026f, FontStyle.Bold, -20);
        }

        private static Transform CreateEmpty(string name, Transform parent)
        {
            Transform result = new GameObject(name).transform;
            if (parent != null)
                result.SetParent(parent, false);
            return result;
        }

        private static Transform CreateMarker(string name, Transform parent, Vector3 localPosition)
        {
            Transform marker = CreateEmpty(name, parent);
            marker.localPosition = localPosition;
            return marker;
        }

        private static SpriteRenderer CreateWorldSprite(string name, Transform parent, Sprite sprite, Color color, Vector3 localPosition, Vector2 size, int sortingOrder)
        {
            GameObject gameObject = new(name, typeof(SpriteRenderer));
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;

            Vector2 nativeSize = sprite != null ? sprite.bounds.size : Vector2.one;
            float scaleX = nativeSize.x > Mathf.Epsilon ? size.x / nativeSize.x : size.x;
            float scaleY = nativeSize.y > Mathf.Epsilon ? size.y / nativeSize.y : size.y;
            gameObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);

            SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static TextMesh CreateWorldText(
            string name,
            Transform parent,
            Font font,
            string content,
            Color color,
            Vector3 localPosition,
            float characterSize,
            FontStyle style,
            int sortingOrder)
        {
            GameObject gameObject = new(name, typeof(TextMesh));
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;

            TextMesh text = gameObject.GetComponent<TextMesh>();
            text.font = font;
            text.text = content;
            text.fontSize = 48;
            text.characterSize = characterSize;
            text.fontStyle = style;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;

            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = sortingOrder;
            return text;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/PirateOceanPrototype"))
                AssetDatabase.CreateFolder("Assets", "PirateOceanPrototype");
            if (!AssetDatabase.IsValidFolder("Assets/PirateOceanPrototype/Scenes"))
                AssetDatabase.CreateFolder("Assets/PirateOceanPrototype", "Scenes");
            if (!AssetDatabase.IsValidFolder("Assets/PirateOceanPrototype/Editor"))
                AssetDatabase.CreateFolder("Assets/PirateOceanPrototype", "Editor");
            if (!AssetDatabase.IsValidFolder("Assets/PirateOceanPrototype/Scripts"))
                AssetDatabase.CreateFolder("Assets/PirateOceanPrototype", "Scripts");
        }
    }
}
