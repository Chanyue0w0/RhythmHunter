using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
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

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                BuildScene();
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
            NewSceneMode mode = Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            scene.name = "PirateOceanPrototype";
            SceneManager.SetActiveScene(scene);

            CreateCamera();

            Transform prototypeRoot = new GameObject("PirateOceanPrototype").transform;
            Transform environmentRoot = CreateEmpty("EnvironmentRoot", prototypeRoot);
            CreateEnvironment(environmentRoot, sprite, font);

            Transform shipSystemRoot = CreateEmpty("ShipSystemRoot", prototypeRoot);
            Transform shipVisualRoot = CreateEmpty("ShipVisualRoot (Future Motion)", shipSystemRoot);
            CreateShip(shipVisualRoot, sprite);

            Transform combatRoot = CreateEmpty("DeckCombatRoot (Stable Logic)", shipSystemRoot);
            CreateCombatSlots(combatRoot, sprite, font);

            Transform cameraTargets = CreateEmpty("CameraTargets", prototypeRoot);
            CreateMarker("ShipCombatTarget", cameraTargets, new Vector3(0f, 0.25f, 0f));
            CreateMarker("BossWideTarget", cameraTargets, new Vector3(0f, 3.1f, 0f));

            Transform bossRoot = CreateEmpty("BossPreviewRoot (Wide Shot)", prototypeRoot);
            bossRoot.localPosition = new Vector3(0f, 8.1f, 0f);
            CreateBossPlaceholder(bossRoot, sprite, font);

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

            if (!Application.isBatchMode && previousScene.IsValid() && previousScene.isLoaded && previousScene != scene)
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"[PirateOceanPrototype] Scene skeleton created: {ScenePath}");
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyTop;
            camera.orthographic = true;
            camera.orthographicSize = 5.35f;
        }

        private static void CreateEnvironment(Transform root, Sprite sprite, Font font)
        {
            CreateWorldSprite("Sky", root, sprite, SkyTop, new Vector3(0f, 2.2f, 5f), new Vector2(24f, 15f), -100);
            CreateWorldSprite("HorizonBand", root, sprite, SkyHorizon, new Vector3(0f, -0.1f, 4f), new Vector2(24f, 4.2f), -95);

            Transform oceanRoot = CreateEmpty("OceanVisualRoot (Wave Stage)", root);
            CreateWorldSprite("OceanFar", oceanRoot, sprite, OceanFar, new Vector3(0f, -2.6f, 3f), new Vector2(24f, 5.2f), -80);
            CreateWorldSprite("OceanNear", oceanRoot, sprite, OceanNear, new Vector3(0f, -4.25f, 2f), new Vector2(24f, 3.3f), 20);
            CreateWorldSprite("FoamGuide", oceanRoot, sprite, Foam, new Vector3(0f, -2.25f, 1f), new Vector2(24f, 0.12f), 19);

            CreateWorldText(
                "OceanStageNote",
                oceanRoot,
                font,
                "OCEAN VISUAL ROOT  -  WAVE SYSTEM WILL BE ADDED IN STAGE 2",
                new Color(0.75f, 0.94f, 1f, 0.45f),
                new Vector3(0f, -4.65f, 0f),
                0.014f,
                FontStyle.Normal,
                101);
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

        private static void CreateCombatSlots(Transform combatRoot, Sprite sprite, Font font)
        {
            Transform enemyRoot = CreateEmpty("EnemySlots_Left", combatRoot);
            Transform heroRoot = CreateEmpty("HeroSlots_Right", combatRoot);

            CreateUnitSlot(enemyRoot, sprite, font, "EnemySlot_1", "BOARDER 1", FightUnitSlot.UnitTeam.Enemy, FightUnitSlot.UnitRole.Enemy, 0, new Vector3(-5.8f, 0.15f, 0f), 80, 12, EnemyRed);
            CreateUnitSlot(enemyRoot, sprite, font, "EnemySlot_2", "BOARDER 2", FightUnitSlot.UnitTeam.Enemy, FightUnitSlot.UnitRole.Enemy, 1, new Vector3(-4.05f, 0.15f, 0f), 120, 18, EnemyRed);
            CreateUnitSlot(enemyRoot, sprite, font, "EnemySlot_3", "BOARDER 3", FightUnitSlot.UnitTeam.Enemy, FightUnitSlot.UnitRole.Enemy, 2, new Vector3(-2.3f, 0.15f, 0f), 90, 14, EnemyRed);

            CreateUnitSlot(heroRoot, sprite, font, "HeroSlot_Tank", "TANK", FightUnitSlot.UnitTeam.Hero, FightUnitSlot.UnitRole.Tank, 0, new Vector3(2.3f, 0.15f, 0f), 120, 12, HeroCyan);
            CreateUnitSlot(heroRoot, sprite, font, "HeroSlot_Support", "SUPPORT", FightUnitSlot.UnitTeam.Hero, FightUnitSlot.UnitRole.Support, 1, new Vector3(4.05f, 0.15f, 0f), 85, 8, HeroCyan);
            CreateUnitSlot(heroRoot, sprite, font, "HeroSlot_Damage", "DAMAGE", FightUnitSlot.UnitTeam.Hero, FightUnitSlot.UnitRole.Damage, 2, new Vector3(5.8f, 0.15f, 0f), 75, 24, HeroCyan);
        }

        private static FightUnitSlot CreateUnitSlot(
            Transform parent,
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
            slotObject.transform.SetParent(parent, false);
            slotObject.transform.localPosition = position;
            FightUnitSlot slot = slotObject.AddComponent<FightUnitSlot>();

            CreateWorldSprite("SlotGround", slotObject.transform, sprite, new Color(color.r, color.g, color.b, 0.28f), new Vector3(0f, -1.25f, 0.3f), new Vector2(1.45f, 0.22f), 8);

            Transform actorRoot = CreateEmpty("ActorRoot (Assign Prefab Here)", slotObject.transform);
            Transform placeholder = CreateEmpty("PrototypePlaceholder", actorRoot);
            CreateWorldSprite("Body", placeholder, sprite, new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 1f), new Vector3(0f, -0.05f, 0f), new Vector2(0.9f, 1.55f), 10);
            CreateWorldSprite("Accent", placeholder, sprite, color, new Vector3(0f, 0.18f, -0.1f), new Vector2(0.52f, 0.62f), 11);
            CreateWorldText("PrefabLabel", placeholder, font, "PREFAB\nSLOT", Color.white, new Vector3(0f, 0.15f, -0.2f), 0.018f, FontStyle.Bold, 12);

            Transform effectPoint = CreateEmpty("NormalAttackEffectSpawnPoint", slotObject.transform);
            effectPoint.localPosition = new Vector3(team == FightUnitSlot.UnitTeam.Hero ? -0.72f : 0.72f, 0.15f, -0.3f);

            CreateWorldText("UnitName", slotObject.transform, font, displayName, Color.white, new Vector3(0f, -1.55f, 0f), 0.021f, FontStyle.Bold, 15);
            CreateWorldSprite("HealthBackground", slotObject.transform, sprite, new Color(0.025f, 0.035f, 0.045f, 1f), new Vector3(0f, 1.28f, 0f), new Vector2(1.18f, 0.11f), 15);
            SpriteRenderer hpFill = CreateWorldSprite("HealthFill", slotObject.transform, sprite, color, new Vector3(0f, 1.28f, -0.1f), new Vector2(1.13f, 0.065f), 16);
            TextMesh hpLabel = CreateWorldText("Stats", slotObject.transform, font, $"HP {hp}/{hp}", new Color(0.78f, 0.9f, 0.94f, 1f), new Vector3(0f, 1.52f, 0f), 0.012f, FontStyle.Normal, 17);

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

        private static void CreateMarker(string name, Transform parent, Vector3 localPosition)
        {
            Transform marker = CreateEmpty(name, parent);
            marker.localPosition = localPosition;
        }

        private static SpriteRenderer CreateWorldSprite(string name, Transform parent, Sprite sprite, Color color, Vector3 localPosition, Vector2 size, int sortingOrder)
        {
            GameObject gameObject = new(name, typeof(SpriteRenderer));
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localScale = new Vector3(size.x, size.y, 1f);

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
        }
    }
}
