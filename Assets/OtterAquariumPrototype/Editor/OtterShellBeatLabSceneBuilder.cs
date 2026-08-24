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
    public static class OtterShellBeatLabSceneBuilder
    {
        public const string ScenePath = "Assets/OtterAquariumPrototype/Scenes/OtterShellBeatLab.unity";
        public const string LevelDataPath = "Assets/OtterAquariumPrototype/Data/OtterShellBeatLevel.asset";

        private static readonly Color DeepWater = new(0.025f, 0.12f, 0.16f, 0.96f);
        private static readonly Color Panel = new(0.03f, 0.2f, 0.23f, 0.93f);
        private static readonly Color Cyan = new(0.32f, 0.94f, 1f, 1f);
        private static readonly Color OtterBrown = new(0.34f, 0.18f, 0.09f, 1f);
        private static readonly Color OtterLight = new(0.78f, 0.58f, 0.35f, 1f);
        private static readonly Color ShellBlue = new(0.23f, 0.78f, 0.84f, 1f);
        private static readonly Color CrabCoral = new(0.95f, 0.34f, 0.24f, 1f);

        private sealed class StageReferences
        {
            public Transform OtterRoot;
            public Transform LeftPaw;
            public Transform RightPaw;
            public Transform ShellRoot;
            public Transform ShellLeft;
            public Transform ShellRight;
            public SpriteRenderer ShellRenderer;
            public Transform CrabHammer;
            public SpriteRenderer CueRipple;
            public SpriteRenderer BeatGlow;
            public TextMesh Instruction;
            public TextMesh Result;
            public TextMesh Detail;
            public TextMesh Statistics;
        }

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

        [MenuItem("Rhythm Hunter/Otter Aquarium/Build Shell Beat Lab")]
        public static void BuildScene()
        {
            EnsureFolders();
            OtterRhythmLevelData levelData = EnsureLevelData();
            Sprite shape = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(OtterAquariumSceneBuilder.BackgroundSpritePath);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (levelData == null || shape == null || background == null || font == null)
            {
                Debug.LogError("[OtterShellBeatLab] Required level, shape, background, or font asset is missing.");
                return;
            }

            Scene previousScene = SceneManager.GetActiveScene();
            bool replacingLoadedScene = previousScene.IsValid() && previousScene.path == ScenePath;
            NewSceneMode mode = Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            scene.name = "OtterShellBeatLab";
            SceneManager.SetActiveScene(scene);

            if (!Application.isBatchMode && replacingLoadedScene)
                EditorSceneManager.CloseScene(previousScene, true);

            Transform root = new GameObject("OtterShellBeatLab").transform;
            CreateCamera(root);
            StageReferences stage = CreateStage(root, shape, background, font);
            CreateController(root, levelData, stage);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!OtterShellBeatLabValidation.ValidateScene(false))
                Debug.LogError("[OtterShellBeatLab] Generated scene failed validation.");

            if (!Application.isBatchMode
                && !replacingLoadedScene
                && previousScene.IsValid()
                && previousScene.isLoaded
                && previousScene != scene)
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"[OtterShellBeatLab] Scene created: {ScenePath}");
        }

        private static StageReferences CreateStage(Transform root, Sprite shape, Sprite background, Font font)
        {
            Transform environment = CreateEmpty("Environment", root);
            SpriteRenderer backgroundRenderer = CreateSprite(
                "ZooBackground",
                environment,
                background,
                new Color(0.45f, 0.72f, 0.72f, 1f),
                new Vector3(0f, 0f, 5f),
                new Vector2(22f, 16.5f),
                -100);
            float backgroundScale = 22f / background.bounds.size.x;
            backgroundRenderer.transform.localScale = Vector3.one * backgroundScale;
            Vector3 center = background.bounds.center * backgroundScale;
            backgroundRenderer.transform.localPosition = new Vector3(-center.x, -center.y, 5f);

            CreateSprite("BackgroundShade", environment, shape, DeepWater, new Vector3(0f, 0f, 4f), new Vector2(22f, 14f), -90);
            CreateSprite("LabPanel", environment, shape, Panel, new Vector3(0f, -0.35f, 2f), new Vector2(12.6f, 7.8f), -20);
            CreateSprite("WaterLine", environment, shape, new Color(0.2f, 0.85f, 0.9f, 0.22f), new Vector3(0f, -2.25f, 1.8f), new Vector2(12.2f, 0.08f), -18);

            SpriteRenderer beatGlow = CreateSprite(
                "BeatGlow",
                environment,
                shape,
                new Color(0.25f, 0.95f, 1f, 0.08f),
                new Vector3(1.8f, -0.45f, 1.4f),
                new Vector2(5.2f, 3.5f),
                -10);

            StageReferences stage = new() { BeatGlow = beatGlow };
            CreateOtter(environment, shape, stage);
            CreateCrab(environment, shape, stage);

            CreateText("Title", root, font, "SEA OTTER SHELL BEAT LAB", Cyan, new Vector3(0f, 4.75f, 0f), 0.047f, FontStyle.Bold, 200);
            stage.Instruction = CreateText(
                "Instruction",
                root,
                font,
                "WAITING FOR FMOD...",
                Color.white,
                new Vector3(0f, 3.95f, 0f),
                0.023f,
                FontStyle.Bold,
                200);
            stage.Result = CreateText(
                "JudgementResult",
                root,
                font,
                "GET READY",
                Cyan,
                new Vector3(0f, 2.95f, 0f),
                0.056f,
                FontStyle.Bold,
                200);
            stage.Detail = CreateText(
                "TimingDetail",
                root,
                font,
                "Listen to the crab, then repeat on the next bar",
                new Color(0.75f, 0.9f, 0.93f, 1f),
                new Vector3(0f, 2.3f, 0f),
                0.021f,
                FontStyle.Bold,
                200);
            stage.Statistics = CreateText(
                "Statistics",
                root,
                font,
                "BEAT --   •   PHRASE --   •   P 00  G 00  M 00  EXTRA 00\nPATTERN --",
                new Color(0.7f, 0.88f, 0.9f, 1f),
                new Vector3(0f, -3.45f, 0f),
                0.018f,
                FontStyle.Bold,
                200);
            CreateText(
                "Controls",
                root,
                font,
                "SPACE  /  ENTER  /  LEFT CLICK  /  GAMEPAD SOUTH",
                Cyan,
                new Vector3(0f, -4.65f, 0f),
                0.019f,
                FontStyle.Bold,
                200);
            CreateText(
                "AudioNote",
                root,
                font,
                "SFX slots are intentionally empty in OtterShellBeatLevel.asset",
                new Color(0.55f, 0.72f, 0.75f, 1f),
                new Vector3(0f, -5.05f, 0f),
                0.014f,
                FontStyle.Normal,
                200);
            return stage;
        }

        private static void CreateOtter(Transform parent, Sprite shape, StageReferences stage)
        {
            Transform otter = CreateEmpty("PlayerOtter", parent);
            otter.localPosition = new Vector3(1.8f, -0.55f, 0f);
            stage.OtterRoot = otter;

            CreateSprite("Shadow", otter, shape, new Color(0f, 0.05f, 0.06f, 0.45f), new Vector3(0f, -0.92f, 0.8f), new Vector2(4.1f, 0.72f), 2);
            CreateSprite("Tail", otter, shape, OtterBrown, new Vector3(1.55f, -0.52f, 0.4f), new Vector2(1.8f, 0.55f), 5).transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            CreateSprite("Body", otter, shape, OtterBrown, Vector3.zero, new Vector2(3.8f, 2.15f), 8);
            CreateSprite("Belly", otter, shape, OtterLight, new Vector3(0f, -0.12f, -0.1f), new Vector2(2.65f, 1.5f), 9);

            Transform head = CreateEmpty("Head", otter);
            head.localPosition = new Vector3(-1.18f, 0.72f, -0.3f);
            CreateSprite("HeadShape", head, shape, OtterBrown, Vector3.zero, new Vector2(1.75f, 1.55f), 12);
            CreateSprite("LeftEar", head, shape, OtterBrown, new Vector3(-0.56f, 0.52f, 0.1f), new Vector2(0.48f, 0.48f), 11);
            CreateSprite("RightEar", head, shape, OtterBrown, new Vector3(0.56f, 0.52f, 0.1f), new Vector2(0.48f, 0.48f), 11);
            CreateSprite("Muzzle", head, shape, OtterLight, new Vector3(-0.06f, -0.23f, -0.2f), new Vector2(1.02f, 0.7f), 13);
            CreateSprite("LeftEye", head, shape, new Color(0.03f, 0.02f, 0.01f, 1f), new Vector3(-0.35f, 0.18f, -0.4f), new Vector2(0.16f, 0.2f), 15);
            CreateSprite("RightEye", head, shape, new Color(0.03f, 0.02f, 0.01f, 1f), new Vector3(0.32f, 0.18f, -0.4f), new Vector2(0.16f, 0.2f), 15);
            CreateSprite("Nose", head, shape, new Color(0.08f, 0.04f, 0.02f, 1f), new Vector3(-0.03f, -0.18f, -0.5f), new Vector2(0.28f, 0.2f), 16);

            Transform shellRoot = CreateEmpty("ShellRoot", otter);
            shellRoot.localPosition = new Vector3(0.45f, 0.04f, -0.5f);
            stage.ShellRoot = shellRoot;
            SpriteRenderer leftShell = CreateSprite("ShellLeft", shellRoot, shape, ShellBlue, new Vector3(-0.3f, 0f, 0f), new Vector2(0.82f, 1.05f), 20);
            SpriteRenderer rightShell = CreateSprite("ShellRight", shellRoot, shape, new Color(0.18f, 0.65f, 0.72f, 1f), new Vector3(0.3f, 0f, 0f), new Vector2(0.82f, 1.05f), 20);
            CreateSprite("Pearl", shellRoot, shape, new Color(1f, 0.94f, 0.72f, 1f), new Vector3(0f, -0.08f, -0.2f), new Vector2(0.38f, 0.38f), 22);
            stage.ShellLeft = leftShell.transform;
            stage.ShellRight = rightShell.transform;
            stage.ShellRenderer = rightShell;

            Transform leftPaw = CreateEmpty("LeftPaw", otter);
            leftPaw.localPosition = new Vector3(-0.38f, 0.38f, -0.8f);
            CreateSprite("LeftPawShape", leftPaw, shape, OtterBrown, Vector3.zero, new Vector2(0.68f, 0.85f), 24).transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
            Transform rightPaw = CreateEmpty("RightPaw", otter);
            rightPaw.localPosition = new Vector3(1.12f, 0.35f, -0.8f);
            CreateSprite("RightPawShape", rightPaw, shape, OtterBrown, Vector3.zero, new Vector2(0.68f, 0.85f), 24).transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            stage.LeftPaw = leftPaw;
            stage.RightPaw = rightPaw;
        }

        private static void CreateCrab(Transform parent, Sprite shape, StageReferences stage)
        {
            Transform crab = CreateEmpty("CrabConductor", parent);
            crab.localPosition = new Vector3(-3.25f, -0.75f, 0f);

            stage.CueRipple = CreateSprite("CueRipple", crab, shape, new Color(0.3f, 1f, 1f, 0f), new Vector3(0f, 0.3f, 0.6f), new Vector2(2.2f, 2.2f), 3);
            CreateSprite("CrabBody", crab, shape, CrabCoral, Vector3.zero, new Vector2(1.8f, 1.05f), 10);
            CreateSprite("CrabBelly", crab, shape, new Color(1f, 0.58f, 0.38f, 1f), new Vector3(0f, -0.16f, -0.1f), new Vector2(1.2f, 0.55f), 11);
            CreateSprite("EyeStemL", crab, shape, CrabCoral, new Vector3(-0.42f, 0.62f, 0f), new Vector2(0.16f, 0.55f), 10);
            CreateSprite("EyeStemR", crab, shape, CrabCoral, new Vector3(0.42f, 0.62f, 0f), new Vector2(0.16f, 0.55f), 10);
            CreateSprite("EyeL", crab, shape, Color.white, new Vector3(-0.42f, 0.86f, -0.1f), new Vector2(0.34f, 0.34f), 12);
            CreateSprite("EyeR", crab, shape, Color.white, new Vector3(0.42f, 0.86f, -0.1f), new Vector2(0.34f, 0.34f), 12);
            CreateSprite("PupilL", crab, shape, Color.black, new Vector3(-0.42f, 0.86f, -0.2f), new Vector2(0.13f, 0.16f), 13);
            CreateSprite("PupilR", crab, shape, Color.black, new Vector3(0.42f, 0.86f, -0.2f), new Vector2(0.13f, 0.16f), 13);

            for (int i = 0; i < 3; i++)
            {
                float y = -0.18f - i * 0.22f;
                SpriteRenderer leftLeg = CreateSprite($"LegL{i}", crab, shape, CrabCoral, new Vector3(-0.9f, y, 0.1f), new Vector2(0.72f, 0.16f), 9);
                leftLeg.transform.localRotation = Quaternion.Euler(0f, 0f, 18f + i * 10f);
                SpriteRenderer rightLeg = CreateSprite($"LegR{i}", crab, shape, CrabCoral, new Vector3(0.9f, y, 0.1f), new Vector2(0.72f, 0.16f), 9);
                rightLeg.transform.localRotation = Quaternion.Euler(0f, 0f, -18f - i * 10f);
            }

            Transform hammer = CreateEmpty("CueHammer", crab);
            hammer.localPosition = new Vector3(1.05f, 0.58f, -0.3f);
            CreateSprite("HammerArm", hammer, shape, CrabCoral, new Vector3(0.42f, 0f, 0f), new Vector2(0.95f, 0.25f), 15);
            CreateSprite("HammerStone", hammer, shape, new Color(0.38f, 0.43f, 0.4f, 1f), new Vector3(0.94f, 0f, -0.1f), new Vector2(0.58f, 0.58f), 16);
            stage.CrabHammer = hammer;
        }

        private static void CreateController(Transform root, OtterRhythmLevelData levelData, StageReferences stage)
        {
            GameObject controllerObject = new("OtterShellBeatController");
            controllerObject.transform.SetParent(root, false);
            FmodBeatClock clock = controllerObject.AddComponent<FmodBeatClock>();
            OtterRhythmLevelRunner runner = controllerObject.AddComponent<OtterRhythmLevelRunner>();
            OtterRhythmInput input = controllerObject.AddComponent<OtterRhythmInput>();
            OtterRhythmPresenter presenter = controllerObject.AddComponent<OtterRhythmPresenter>();

            clock.Configure(levelData.MusicEventPath, levelData.MusicStartDelaySeconds, true);
            runner.Configure(clock, levelData);
            input.Configure(runner);
            presenter.Configure(
                runner,
                stage.OtterRoot,
                stage.LeftPaw,
                stage.RightPaw,
                stage.ShellRoot,
                stage.ShellLeft,
                stage.ShellRight,
                stage.ShellRenderer,
                stage.CrabHammer,
                stage.CueRipple,
                stage.BeatGlow,
                stage.Instruction,
                stage.Result,
                stage.Detail,
                stage.Statistics);
        }

        private static OtterRhythmLevelData EnsureLevelData()
        {
            OtterRhythmLevelData data = AssetDatabase.LoadAssetAtPath<OtterRhythmLevelData>(LevelDataPath);
            if (data != null)
            {
                if (data.EnsureAuthoringDefaults())
                {
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                }
                return data;
            }

            data = ScriptableObject.CreateInstance<OtterRhythmLevelData>();
            data.ConfigurePrototypeDefaults();
            AssetDatabase.CreateAsset(data, LevelDataPath);
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
            camera.backgroundColor = new Color(0.01f, 0.06f, 0.08f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5.65f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
        }

        private static SpriteRenderer CreateSprite(
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
            SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            Vector2 spriteSize = sprite.bounds.size;
            gameObject.transform.localScale = new Vector3(
                size.x / Mathf.Max(0.001f, spriteSize.x),
                size.y / Mathf.Max(0.001f, spriteSize.y),
                1f);
            return renderer;
        }

        private static TextMesh CreateText(
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
            text.color = color;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = sortingOrder;
            return text;
        }

        private static Transform CreateEmpty(string name, Transform parent)
        {
            Transform transform = new GameObject(name).transform;
            transform.SetParent(parent, false);
            return transform;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/OtterAquariumPrototype", "Scenes");
            EnsureFolder("Assets/OtterAquariumPrototype", "Scripts");
            EnsureFolder("Assets/OtterAquariumPrototype", "Editor");
            EnsureFolder("Assets/OtterAquariumPrototype", "Data");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
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
