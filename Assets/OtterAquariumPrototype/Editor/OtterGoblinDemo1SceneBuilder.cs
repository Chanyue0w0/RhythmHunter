using System.Collections.Generic;
using System.Linq;
using FMODUnity;
using RhythmHunter.OtterAquariumPrototype;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    public static class OtterGoblinDemo1SceneBuilder
    {
        public const string ScenePath = "Assets/OtterAquariumPrototype/Scenes/OtterZooGoblinDemo1.unity";
        public const string DataPath = "Assets/OtterAquariumPrototype/Data/OtterZooGoblinDemo1Level.asset";
        public const string OtterVsDataPath = "Assets/OtterAquariumPrototype/Data/OtterZooGoblinOtterVsLevel.asset";
        public const string AxePrefabPath = "Assets/OtterAquariumPrototype/Prefabs/GoblinFlyingAxe.prefab";
        private const string RemovedOtterVsScenePath = "Assets/OtterAquariumPrototype/Scenes/OtterZooGoblinOtterVs.unity";

        public const string BackgroundPath = "Assets/OtterAquariumPrototype/Arts/Background/zoo_fightingbackground.png";
        private const string GoblinRoot = "Assets/OtterAquariumPrototype/Arts/Enemy/Goblin_Mercenary";
        private const string AxeSpritePath = GoblinRoot + "/Axe.png";
        private const float CharacterScale = 0.72f;
        private const float GoblinX = -4.65f;
        private const float OtterX = 4.65f;

        private static readonly Color Ink = new(0.025f, 0.045f, 0.055f, 0.96f);
        private static readonly Color Panel = new(0.04f, 0.12f, 0.14f, 0.93f);
        private static readonly Color Cyan = new(0.26f, 0.94f, 1f, 1f);
        private static readonly Color OtterBrown = new(0.34f, 0.18f, 0.08f, 1f);
        private static readonly Color OtterLight = new(0.82f, 0.62f, 0.38f, 1f);

        private sealed class Stage
        {
            public Transform EnemyRoot;
            public SpriteRenderer EnemyRenderer;
            public Sprite[] EnemyIdle;
            public Sprite[] EnemyAttack;
            public Sprite EnemyAttacked;
            public GameObject AxeProjectilePrefab;
            public Transform OtterRoot;
            public SpriteRenderer OtterBody;
            public SpriteRenderer Shield;
            public SpriteRenderer DangerFlash;
            public TextMesh Title;
            public TextMesh Phase;
            public TextMesh Phrase;
            public TextMesh Pattern;
            public TextMesh Judgement;
            public TextMesh Timing;
            public TextMesh FailureCount;
            public TextMesh Status;
        }

        [InitializeOnLoadMethod]
        private static void QueueInitialBuild()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += TryInitialBuild;
            EditorApplication.delayCall += TryEnsureAxePrefab;
            EditorApplication.delayCall += TryRedirectRemovedOtterVsScene;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += TryRedirectRemovedOtterVsScene;
        }

        private static void TryRedirectRemovedOtterVsScene()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += TryRedirectRemovedOtterVsScene;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || activeScene.path != RemovedOtterVsScenePath)
                return;
            if (activeScene.isDirty)
            {
                Debug.LogWarning(
                    "[OtterGoblinDemo1] The removed Otter vs scene still has unsaved changes. "
                    + $"Apply its LevelData to the shared scene manually before closing it: {ScenePath}");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[OtterGoblinDemo1] Redirected the removed Otter vs scene to shared scene: {ScenePath}");
        }

        private static void TryEnsureAxePrefab()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += TryEnsureAxePrefab;
                return;
            }
            EnsureAxePrefab();
        }

        private static void TryInitialBuild()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += TryInitialBuild;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                BuildScene();
        }

        [MenuItem("Rhythm Hunter/Otter Aquarium/Build Zoo Goblin Demo 1")]
        public static void BuildScene()
        {
            EnsureFolders();
            OtterGoblinDemo1LevelData data = EnsureData();
            BuildScene(data);
        }

        [MenuItem("Rhythm Hunter/Otter Aquarium/Apply Goblin Patrol To Shared Demo1 Scene")]
        public static void ApplyGoblinPatrolToSharedScene()
        {
            EnsureFolders();
            ApplyLevelToSharedScene(EnsureData());
        }

        [MenuItem("Rhythm Hunter/Otter Aquarium/Apply Otter vs To Shared Demo1 Scene")]
        public static void ApplyOtterVsToSharedScene()
        {
            EnsureFolders();
            ApplyLevelToSharedScene(EnsureOtterVsData());
        }

        public static bool ApplyLevelToSharedScene(OtterGoblinDemo1LevelData data)
        {
            if (data == null)
            {
                EditorUtility.DisplayDialog("關卡資料有錯誤", "找不到關卡資料。", "好");
                return false;
            }
            if (!data.Validate(out string error))
            {
                EditorUtility.DisplayDialog("關卡資料有錯誤", error, "好");
                return false;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            FmodBeatClock clock = FindInScene<FmodBeatClock>(scene);
            OtterGoblinDemo1Runner runner = FindInScene<OtterGoblinDemo1Runner>(scene);
            OtterGoblinDemo1Presenter presenter = FindInScene<OtterGoblinDemo1Presenter>(scene);
            if (clock == null || runner == null || presenter == null)
            {
                EditorUtility.DisplayDialog(
                    "共用 Scene 不完整",
                    "找不到 Demo1 Runner、FMOD Beat Clock 或 Presenter，請先執行 Build Zoo Goblin Demo 1。",
                    "好");
                return false;
            }

            Undo.RecordObjects(new Object[] { clock, runner, presenter }, "切換 Demo1 歌曲與譜面");
            runner.Configure(clock, data);
            presenter.RefreshLevelPresentation();
            EditorUtility.SetDirty(clock);
            EditorUtility.SetDirty(runner);
            EditorUtility.SetDirty(presenter);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            string dataPath = AssetDatabase.GetAssetPath(data);
            bool valid = OtterGoblinDemo1Validation.ValidateScene(false, ScenePath, dataPath);
            if (!valid)
            {
                EditorUtility.DisplayDialog("套用後驗證失敗", "請查看 Console 的 Demo1 驗證訊息。", "好");
                return false;
            }

            Selection.activeObject = data;
            Debug.Log($"[OtterGoblinDemo1] Applied '{data.DisplayName}' to shared scene: {ScenePath}");
            return true;
        }

        private static void BuildScene(OtterGoblinDemo1LevelData data)
        {
            Sprite shape = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite background = LoadSprite(BackgroundPath);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite[] idle =
            {
                LoadSprite($"{GoblinRoot}/idle/idle_1.png"),
                LoadSprite($"{GoblinRoot}/idle/idle_2.png")
            };
            Sprite[] attack =
            {
                LoadSprite($"{GoblinRoot}/attack/attack_1.png"),
                LoadSprite($"{GoblinRoot}/attack/attack_2.png"),
                LoadSprite($"{GoblinRoot}/attack/attack_3.png"),
                LoadSprite($"{GoblinRoot}/attack/attack_4.png")
            };
            Sprite attacked = LoadSprite($"{GoblinRoot}/attacked_1.png");
            GameObject axePrefab = EnsureAxePrefab();

            if (data == null || shape == null || background == null || font == null
                || idle.Any(sprite => sprite == null) || attack.Any(sprite => sprite == null)
                || attacked == null || axePrefab == null)
            {
                Debug.LogError("[OtterGoblinDemo1] Required data, font, background, or Goblin sprite is missing.");
                return;
            }

            Scene previous = SceneManager.GetActiveScene();
            bool replacing = previous.IsValid() && previous.path == ScenePath;
            NewSceneMode mode = Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            scene.name = "OtterZooGoblinDemo1";
            SceneManager.SetActiveScene(scene);

            if (!Application.isBatchMode && replacing)
                EditorSceneManager.CloseScene(previous, true);

            Transform root = new GameObject("OtterZooGoblinDemo1").transform;
            CreateCamera(root);
            Stage stage = CreateStage(
                root,
                shape,
                background,
                font,
                idle,
                attack,
                attacked,
                axePrefab,
                GetSceneTitle(data),
                data.TotalBars,
                data.AuthoredBpm);
            CreateController(root, data, stage);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string dataPath = AssetDatabase.GetAssetPath(data);
            if (!OtterGoblinDemo1Validation.ValidateScene(false, ScenePath, dataPath))
                Debug.LogError($"[OtterGoblinDemo1] Generated scene failed validation: {ScenePath}");

            if (!Application.isBatchMode && !replacing && previous.IsValid() && previous.isLoaded && previous != scene)
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"[OtterGoblinDemo1] Shared scene created: {ScenePath}");
        }

        private static Stage CreateStage(
            Transform root,
            Sprite shape,
            Sprite background,
            Font font,
            Sprite[] idle,
            Sprite[] attack,
            Sprite attacked,
            GameObject axePrefab,
            string sceneTitle,
            int totalBars,
            float authoredBpm)
        {
            Stage stage = new()
            {
                EnemyIdle = idle,
                EnemyAttack = attack,
                EnemyAttacked = attacked,
                AxeProjectilePrefab = axePrefab
            };

            Transform environment = Empty("Environment", root);
            SpriteRenderer backgroundRenderer = Sprite(
                "ZooBackground", environment, background, Color.white,
                new Vector3(0f, 0f, 5f), new Vector2(20f, 12f), -100, true);
            backgroundRenderer.color = Color.white;
            Sprite("AtmosphereShade", environment, shape, new Color(0.015f, 0.06f, 0.07f, 0.18f),
                new Vector3(0f, 0f, 4f), new Vector2(20f, 12f), -90);
            Sprite("Ground", environment, shape, new Color(0.04f, 0.08f, 0.065f, 0f),
                new Vector3(0f, -3.45f, 2.8f), new Vector2(20f, 3.1f), -50);
            stage.DangerFlash = Sprite("DangerFlash", environment, shape, new Color(1f, 0.08f, 0.08f, 0f),
                new Vector3(0f, 0f, -2f), new Vector2(20f, 12f), 80);

            Transform titlePanel = Empty("TitlePanel", root);
            Sprite("TitlePanelFill", titlePanel, shape, Ink, new Vector3(0f, 4.55f, 0f), new Vector2(18.6f, 1.15f), 90);
            stage.Title = Text("Title", titlePanel, font, sceneTitle, Color.white,
                new Vector3(-5.3f, 4.64f, -0.2f), 0.105f, FontStyle.Bold, 100, TextAnchor.MiddleLeft);
            stage.FailureCount = Text("FailureCount", titlePanel, font, "FAILURES   0", Color.white,
                new Vector3(5.8f, 4.64f, -0.2f), 0.1f, FontStyle.Bold, 100, TextAnchor.MiddleRight);

            Transform rhythmPanel = Empty("RhythmPanel", root);
            Sprite("RhythmPanelFill", rhythmPanel, shape, Panel, new Vector3(0f, 2.85f, 0f), new Vector2(14.7f, 2f), 88);
            stage.Phase = Text("Phase", rhythmPanel, font, "GET READY", Cyan,
                new Vector3(0f, 3.35f, -0.2f), 0.18f, FontStyle.Bold, 100);
            stage.Phrase = Text("Phrase", rhythmPanel, font, "INTRO • FIRST ATTACK AT BAR 005", Color.white,
                new Vector3(0f, 2.88f, -0.2f), 0.075f, FontStyle.Normal, 100);
            stage.Pattern = Text("Pattern", rhythmPanel, font, "— — — —", Cyan,
                new Vector3(0f, 2.43f, -0.2f), 0.125f, FontStyle.Bold, 100);

            CreateGoblin(root, idle[0], stage);
            CreateOtter(root, shape, stage);

            Transform resultPanel = Empty("ResultPanel", root);
            Sprite("ResultPanelFill", resultPanel, shape, Ink, new Vector3(0f, -3.93f, 0f), new Vector2(18.6f, 1.7f), 90);
            stage.Judgement = Text("Judgement", resultPanel, font, "SPACE / CLICK TO DEFEND", Color.white,
                new Vector3(0f, -3.55f, -0.2f), 0.15f, FontStyle.Bold, 100);
            stage.Timing = Text("Timing", resultPanel, font, "LISTEN TO THE WARNING • REPEAT IT WHEN THE AXE SWINGS", Cyan,
                new Vector3(0f, -4.02f, -0.2f), 0.075f, FontStyle.Normal, 100);
            stage.Status = Text(
                "Status",
                resultPanel,
                font,
                $"BAR 001/{totalBars:000}  BEAT 1/4   •   {authoredBpm:0.##} BPM\nP 00   G 00   M 00   EXTRA 00",
                new Color(0.72f, 0.84f, 0.84f, 1f), new Vector3(0f, -4.48f, -0.2f), 0.058f, FontStyle.Normal, 100);

            return stage;
        }

        private static void CreateGoblin(Transform parent, Sprite initialSprite, Stage stage)
        {
            Transform enemy = Empty("ZooGoblin", parent);
            enemy.localPosition = new Vector3(GoblinX, -1.65f, 0f);
            enemy.localScale = Vector3.one * CharacterScale;
            stage.EnemyRoot = enemy;
            SpriteRenderer renderer = Sprite(
                "GoblinSprite", enemy, initialSprite, Color.white,
                Vector3.zero, new Vector2(3.8f, 3.8f), 20, true);
            stage.EnemyRenderer = renderer;

            Transform label = Empty("GoblinLabel", parent);
            Sprite shape = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite("GoblinLabelPlate", label, shape, new Color(0.22f, 0.04f, 0.025f, 0.88f),
                new Vector3(GoblinX, 0.1f, 0f), new Vector2(3.1f, 0.46f), 24);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text("GoblinName", label, font, "AXE GOBLIN", new Color(1f, 0.7f, 0.35f, 1f),
                new Vector3(GoblinX, 0.12f, -0.2f), 0.068f, FontStyle.Bold, 30);
        }

        private static void CreateOtter(Transform parent, Sprite shape, Stage stage)
        {
            Transform otter = Empty("Otter", parent);
            otter.localPosition = new Vector3(OtterX, -1.9f, 0f);
            otter.localScale = new Vector3(-CharacterScale, CharacterScale, CharacterScale);
            stage.OtterRoot = otter;

            stage.Shield = Sprite("Shield", otter, shape, new Color(0.25f, 0.95f, 1f, 0f),
                new Vector3(0.55f, 0.18f, 0.6f), new Vector2(3.2f, 3.2f), 34);
            stage.OtterBody = Sprite("Body", otter, shape, OtterBrown,
                new Vector3(0f, -0.15f, 0f), new Vector2(2.2f, 2.75f), 20);
            Sprite("Belly", otter, shape, OtterLight,
                new Vector3(0.15f, -0.28f, -0.15f), new Vector2(1.42f, 1.85f), 21);
            Sprite("Head", otter, shape, OtterBrown,
                new Vector3(-0.12f, 1.05f, -0.12f), new Vector2(1.72f, 1.45f), 22);
            Sprite("Muzzle", otter, shape, OtterLight,
                new Vector3(-0.35f, 0.87f, -0.25f), new Vector2(0.86f, 0.62f), 23);
            Sprite("EarL", otter, shape, OtterLight,
                new Vector3(-0.7f, 1.62f, 0f), new Vector2(0.48f, 0.48f), 21);
            Sprite("EarR", otter, shape, OtterLight,
                new Vector3(0.5f, 1.58f, 0f), new Vector2(0.45f, 0.45f), 21);
            Sprite("EyeL", otter, shape, Color.black,
                new Vector3(-0.55f, 1.18f, -0.35f), new Vector2(0.16f, 0.2f), 24);
            Sprite("EyeR", otter, shape, Color.black,
                new Vector3(0.12f, 1.18f, -0.35f), new Vector2(0.16f, 0.2f), 24);
            Sprite("Nose", otter, shape, new Color(0.06f, 0.035f, 0.025f, 1f),
                new Vector3(-0.48f, 0.98f, -0.4f), new Vector2(0.25f, 0.18f), 25);
            SpriteRenderer arm = Sprite("GuardPaw", otter, shape, OtterBrown,
                new Vector3(0.9f, 0.25f, -0.25f), new Vector2(0.62f, 1.55f), 24);
            arm.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
            Sprite("ShellGuard", otter, shape, new Color(0.2f, 0.64f, 0.7f, 1f),
                new Vector3(0.75f, 0.18f, -0.4f), new Vector2(1.35f, 1.62f), 25);

            Transform label = Empty("OtterLabel", parent);
            Sprite("OtterLabelPlate", label, shape, new Color(0.025f, 0.16f, 0.18f, 0.9f),
                new Vector3(OtterX, 0.1f, 0f), new Vector2(3.1f, 0.46f), 24);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text("OtterName", label, font, "OTTER", Cyan,
                new Vector3(OtterX, 0.12f, -0.2f), 0.068f, FontStyle.Bold, 30);
        }

        private static void CreateController(Transform root, OtterGoblinDemo1LevelData data, Stage stage)
        {
            GameObject controllerObject = new("Demo1CombatController");
            controllerObject.transform.SetParent(root, false);
            FmodBeatClock clock = controllerObject.AddComponent<FmodBeatClock>();
            OtterGoblinDemo1Runner runner = controllerObject.AddComponent<OtterGoblinDemo1Runner>();
            OtterGoblinDemo1Input input = controllerObject.AddComponent<OtterGoblinDemo1Input>();
            OtterGoblinDemo1Presenter presenter = controllerObject.AddComponent<OtterGoblinDemo1Presenter>();

            clock.Configure(data.MusicEventPath, data.MusicStartDelaySeconds, true, data.MusicVolume);
            runner.Configure(clock, data);
            presenter.Configure(
                runner,
                stage.EnemyRoot,
                stage.EnemyRenderer,
                stage.EnemyIdle,
                stage.EnemyAttack,
                stage.EnemyAttacked,
                stage.AxeProjectilePrefab,
                stage.OtterRoot,
                stage.OtterBody,
                stage.Shield,
                stage.DangerFlash,
                stage.Title,
                stage.Phase,
                stage.Phrase,
                stage.Pattern,
                stage.Judgement,
                stage.Timing,
                stage.FailureCount,
                stage.Status);
            input.Configure(runner, presenter);
        }

        private static string GetSceneTitle(OtterGoblinDemo1LevelData data)
        {
            string eventPath = data != null ? data.MusicEventPath : string.Empty;
            int separator = string.IsNullOrWhiteSpace(eventPath) ? -1 : eventPath.LastIndexOf('/');
            string songName = separator >= 0 && separator + 1 < eventPath.Length
                ? eventPath.Substring(separator + 1)
                : data != null ? data.DisplayName : "NO LEVEL";
            return $"DEMO1  •  {songName.ToUpperInvariant()}";
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

        private static GameObject EnsureAxePrefab()
        {
            TextureImporter importer = AssetImporter.GetAtPath(AxeSpritePath) as TextureImporter;
            if (importer == null)
                return null;

            if (importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.filterMode != FilterMode.Point
                || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Sprite axeSprite = LoadSprite(AxeSpritePath);
            if (axeSprite == null)
                return null;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AxePrefabPath);
            if (prefab != null)
                return prefab;

            GameObject temporary = new("GoblinFlyingAxe", typeof(SpriteRenderer), typeof(RhythmTimelineProjectile));
            temporary.transform.localScale = Vector3.one * 1.15f;
            SpriteRenderer renderer = temporary.GetComponent<SpriteRenderer>();
            renderer.sprite = axeSprite;
            renderer.sortingOrder = 38;
            prefab = PrefabUtility.SaveAsPrefabAsset(temporary, AxePrefabPath);
            Object.DestroyImmediate(temporary);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static OtterGoblinDemo1LevelData EnsureData()
        {
            OtterGoblinDemo1LevelData data = AssetDatabase.LoadAssetAtPath<OtterGoblinDemo1LevelData>(DataPath);
            if (data != null)
                return data;

            data = ScriptableObject.CreateInstance<OtterGoblinDemo1LevelData>();
            data.ConfigureDemo1Defaults();
            if (!data.Validate(out string error))
            {
                Debug.LogError($"[OtterGoblinDemo1] Default chart is invalid: {error}");
                Object.DestroyImmediate(data);
                return null;
            }
            AssetDatabase.CreateAsset(data, DataPath);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        private static OtterGoblinDemo1LevelData EnsureOtterVsData()
        {
            OtterGoblinDemo1LevelData data =
                AssetDatabase.LoadAssetAtPath<OtterGoblinDemo1LevelData>(OtterVsDataPath);
            if (data != null)
                return data;

            data = ScriptableObject.CreateInstance<OtterGoblinDemo1LevelData>();
            data.ConfigureOtterVsDefaults();
            if (!data.Validate(out string error))
            {
                Debug.LogError($"[OtterGoblinDemo1] Otter vs chart is invalid: {error}");
                Object.DestroyImmediate(data);
                return null;
            }
            AssetDatabase.CreateAsset(data, OtterVsDataPath);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        private static void CreateCamera(Transform parent)
        {
            GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener), typeof(StudioListener));
            cameraObject.transform.SetParent(parent, false);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.01f, 0.025f, 0.03f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
        }

        private static SpriteRenderer Sprite(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            Vector3 position,
            Vector2 size,
            int order,
            bool preserveAspect = false)
        {
            GameObject gameObject = new(name, typeof(SpriteRenderer));
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            Vector2 source = sprite.bounds.size;
            if (preserveAspect)
            {
                float scale = Mathf.Min(size.x / Mathf.Max(0.001f, source.x), size.y / Mathf.Max(0.001f, source.y));
                gameObject.transform.localScale = Vector3.one * scale;
            }
            else
            {
                gameObject.transform.localScale = new Vector3(
                    size.x / Mathf.Max(0.001f, source.x),
                    size.y / Mathf.Max(0.001f, source.y),
                    1f);
            }
            return renderer;
        }

        private static TextMesh Text(
            string name,
            Transform parent,
            Font font,
            string content,
            Color color,
            Vector3 position,
            float characterSize,
            FontStyle style,
            int order,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            GameObject gameObject = new(name, typeof(TextMesh));
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            TextMesh text = gameObject.GetComponent<TextMesh>();
            text.font = font;
            text.text = content;
            text.fontSize = 48;
            text.characterSize = characterSize;
            text.fontStyle = style;
            text.color = color;
            text.anchor = anchor;
            text.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left
                : anchor == TextAnchor.MiddleRight ? TextAlignment.Right
                : TextAlignment.Center;
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = order;
            return text;
        }

        private static Transform Empty(string name, Transform parent)
        {
            Transform transform = new GameObject(name).transform;
            transform.SetParent(parent, false);
            return transform;
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/OtterAquariumPrototype", "Scenes");
            EnsureFolder("Assets/OtterAquariumPrototype", "Data");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void AddToBuildSettings(string scenePath)
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
