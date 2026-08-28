using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FMODUnity;
using RhythmHunter.OtterAquariumPrototype;
using RhythmHunter.RhythmDemo;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    public static class OtterMainMenuSceneBuilder
    {
        public const string ScenePath = "Assets/OtterAquariumPrototype/Scenes/OtterMainMenu.unity";
        public const string GameplayScenePath =
            "Assets/OtterAquariumPrototype/Scenes/OtterZooGoblinDemo1.unity";

        private const string FloatingFolder = "Assets/OtterAquariumPrototype/Arts/Otter/floating";
        private const string VideoFolder = "Assets/OtterAquariumPrototype/Video";
        private const string MenuMusicEventPath = "event:/ZooGoblinFight/BGM/Otter vs";
        private const float DefaultMenuMusicVolume = 0.35f;

        private static readonly Color DeepWater = new(0.012f, 0.075f, 0.105f, 1f);
        private static readonly Color VideoFrame = new(0.12f, 0.78f, 0.82f, 0.92f);
        private static readonly Color Cream = new(1f, 0.94f, 0.72f, 1f);
        private static readonly Color ButtonBlue = new(0.06f, 0.35f, 0.43f, 0.88f);

        [InitializeOnLoadMethod]
        private static void QueueInitialBuild()
        {
            EditorApplication.delayCall += TryInitialBuild;
            EditorApplication.delayCall += TryUpgradeMenuMusic;
        }

        private static void TryInitialBuild()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryInitialBuild;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                BuildSceneInternal();
            else
                EnsureBuildSettings();
        }

        [MenuItem("Rhythm Hunter/Otter Aquarium/Build Main Menu Scene")]
        public static void BuildScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                && !EditorUtility.DisplayDialog(
                    "Rebuild Otter Main Menu",
                    "This replaces the existing OtterMainMenu Scene. Continue?",
                    "Rebuild",
                    "Cancel"))
            {
                return;
            }

            BuildSceneInternal();
        }

        public static void BuildSceneForAutomation()
        {
            BuildSceneInternal();
        }

        private static void BuildSceneInternal()
        {
            Sprite[] floatingFrames = LoadFloatingFrames();
            VideoClip[] videoClips = LoadVideoClips();
            if (floatingFrames.Length == 0)
            {
                Debug.LogError($"[OtterMainMenu] No floating sprites were found in {FloatingFolder}.");
                return;
            }

            if (videoClips.Length == 0)
            {
                Debug.LogError($"[OtterMainMenu] No videos were found in {VideoFolder}.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets");

            Scene previousScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            scene.name = "OtterMainMenu";

            GameObject root = new("OtterMainMenu");
            OtterMainMenuController controller = root.AddComponent<OtterMainMenuController>();
            FmodBeatClock menuMusic = root.AddComponent<FmodBeatClock>();
            menuMusic.Configure(MenuMusicEventPath, 0f, true, DefaultMenuMusicVolume);
            CreateCamera(root.transform);
            CreateEventSystem(root.transform);

            Canvas canvas = CreateCanvas(root.transform);
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            CreateBackground(canvas.transform);
            CreateTitle(canvas.transform);

            List<RawImage> previews = new();
            List<VideoPlayer> players = new();
            CreateVideoTiles(canvas.transform, videoClips, previews, players);

            RectTransform otterRect = CreateOtter(
                canvas.transform,
                floatingFrames[0],
                out Image otterImage,
                out Button startButton);
            Button exitButton = CreateExitButton(canvas.transform);

            controller.Configure(
                GameplayScenePath,
                startButton,
                exitButton,
                canvasRect,
                otterRect,
                otterImage,
                floatingFrames,
                previews.ToArray(),
                players.ToArray());
            EditorUtility.SetDirty(controller);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            if (previousScene.IsValid() && previousScene.isLoaded)
                SceneManager.SetActiveScene(previousScene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EnsureBuildSettings();
            Debug.Log(
                $"[OtterMainMenu] Built {ScenePath} with {floatingFrames.Length} floating frames "
                + $"and {videoClips.Length} looping corner videos.");
        }

        private static void TryUpgradeMenuMusic()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryUpgradeMenuMusic;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
                return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                return;

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "OtterMainMenu");
            if (root == null || root.GetComponent<FmodBeatClock>() != null)
            {
                if (!wasLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                return;
            }

            if (wasLoaded && scene.isDirty)
            {
                Debug.LogWarning(
                    "[OtterMainMenu] Waiting to add menu BGM because the Scene has unsaved edits. "
                    + "Save it, then reload scripts or rebuild the Main Menu Scene.");
                return;
            }

            FmodBeatClock menuMusic = root.AddComponent<FmodBeatClock>();
            menuMusic.Configure(MenuMusicEventPath, 0f, true, DefaultMenuMusicVolume);
            EditorUtility.SetDirty(menuMusic);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);

            Debug.Log(
                $"[OtterMainMenu] Added Inspector-adjustable menu BGM: {MenuMusicEventPath} "
                + $"at volume {DefaultMenuMusicVolume:0.00}.");
        }

        private static void CreateCamera(Transform parent)
        {
            GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener), typeof(StudioListener));
            cameraObject.transform.SetParent(parent, false);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = DeepWater;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
        }

        private static void CreateEventSystem(Transform parent)
        {
            GameObject eventSystemObject = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(parent, false);
        }

        private static Canvas CreateCanvas(Transform parent)
        {
            GameObject canvasObject = new("MainMenuCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateBackground(Transform parent)
        {
            Image background = CreateImage("Water Background", parent, DeepWater);
            Stretch(background.rectTransform);

            Image upperGlow = CreateImage("Upper Water Glow", parent, new Color(0.04f, 0.27f, 0.31f, 0.42f));
            SetRect(upperGlow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 0f), new Vector2(1920f, 250f), new Vector2(0.5f, 1f));
        }

        private static void CreateTitle(Transform parent)
        {
            TextMeshProUGUI title = CreateText("Title", parent, "Otter Heroooooo", 76f, Cream, FontStyles.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -44f), new Vector2(760f, 110f), new Vector2(0.5f, 1f));
            title.alignment = TextAlignmentOptions.Center;

            Shadow shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.05f, 0.08f, 0.9f);
            shadow.effectDistance = new Vector2(5f, -6f);
        }

        private static void CreateVideoTiles(
            Transform parent,
            IReadOnlyList<VideoClip> clips,
            ICollection<RawImage> previews,
            ICollection<VideoPlayer> players)
        {
            var placements = new[]
            {
                new VideoPlacement(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -36f), new Vector2(350f, 197f)),
                new VideoPlacement(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -36f), new Vector2(350f, 197f)),
                new VideoPlacement(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 36f), new Vector2(350f, 197f)),
                new VideoPlacement(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 36f), new Vector2(350f, 197f)),
                new VideoPlacement(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(300f, 169f))
            };

            for (int index = 0; index < clips.Count; index++)
            {
                VideoPlacement placement = placements[Mathf.Min(index, placements.Length - 1)];
                if (index >= placements.Length)
                    placement.Position += new Vector2(-320f * (index - placements.Length + 1), 0f);

                Image frame = CreateImage($"Video {index + 1} Frame", parent, VideoFrame);
                SetRect(frame.rectTransform, placement.Anchor, placement.Anchor, placement.Position,
                    placement.Size + new Vector2(14f, 14f), placement.Pivot);

                GameObject previewObject = new($"Video {index + 1} - {clips[index].name}", typeof(RectTransform),
                    typeof(RawImage), typeof(AspectRatioFitter), typeof(VideoPlayer));
                previewObject.transform.SetParent(frame.transform, false);
                RectTransform previewRect = previewObject.GetComponent<RectTransform>();
                Stretch(previewRect, 7f);

                RawImage preview = previewObject.GetComponent<RawImage>();
                preview.color = Color.white;
                preview.raycastTarget = false;

                AspectRatioFitter aspect = previewObject.GetComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = clips[index].height > 0
                    ? (float)clips[index].width / clips[index].height
                    : 16f / 9f;

                VideoPlayer player = previewObject.GetComponent<VideoPlayer>();
                player.source = VideoSource.VideoClip;
                player.clip = clips[index];
                player.playOnAwake = false;
                player.waitForFirstFrame = true;
                player.skipOnDrop = true;
                player.isLooping = true;
                player.audioOutputMode = VideoAudioOutputMode.None;

                previews.Add(preview);
                players.Add(player);
            }
        }

        private static RectTransform CreateOtter(
            Transform parent,
            Sprite firstFrame,
            out Image otterImage,
            out Button startButton)
        {
            GameObject otterObject = new("Floating Otter", typeof(RectTransform));
            otterObject.transform.SetParent(parent, false);
            RectTransform otterRect = otterObject.GetComponent<RectTransform>();
            SetRect(otterRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -70f), new Vector2(310f, 504f), new Vector2(0.5f, 0.5f));

            GameObject imageObject = new("Floating Animation", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(otterRect, false);
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            Stretch(imageRect);
            otterImage = imageObject.GetComponent<Image>();
            otterImage.sprite = firstFrame;
            otterImage.preserveAspect = true;
            otterImage.raycastTarget = false;

            Image buttonImage = CreateImage("Start Game Button", otterRect, ButtonBlue);
            SetRect(buttonImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 50f), new Vector2(205f, 72f), new Vector2(0.5f, 0.5f));
            startButton = buttonImage.gameObject.AddComponent<Button>();
            ConfigureButtonColors(startButton, ButtonBlue, new Color(0.1f, 0.52f, 0.6f, 0.96f));

            TextMeshProUGUI label = CreateText("Label", buttonImage.transform, "開始遊戲", 35f, Color.white, FontStyles.Bold);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return otterRect;
        }

        private static Button CreateExitButton(Transform parent)
        {
            Image buttonImage = CreateImage("Exit Button - Prototype No Action", parent,
                new Color(0.015f, 0.12f, 0.16f, 0.84f));
            SetRect(buttonImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 34f), new Vector2(210f, 62f), new Vector2(0.5f, 0f));
            Button button = buttonImage.gameObject.AddComponent<Button>();
            ConfigureButtonColors(button, buttonImage.color, new Color(0.08f, 0.28f, 0.32f, 0.94f));

            TextMeshProUGUI label = CreateText("Label", buttonImage.transform, "離開遊戲", 30f,
                new Color(0.78f, 0.9f, 0.9f, 1f), FontStyles.Normal);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return button;
        }

        private static Sprite[] LoadFloatingFrames()
        {
            string[] paths = AssetDatabase.FindAssets("t:Texture2D", new[] { FloatingFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(GetFloatingFrameOrder)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return paths.Select(LoadSprite).Where(sprite => sprite != null).ToArray();
        }

        private static int GetFloatingFrameOrder(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.StartsWith("floating_", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(name.Substring("floating_".Length), out int floatingIndex))
            {
                return floatingIndex;
            }

            if (name.StartsWith("F", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(name.Substring(1), out int fIndex))
            {
                return 4 + fIndex;
            }

            return int.MaxValue;
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        }

        private static VideoClip[] LoadVideoClips()
        {
            return AssetDatabase.FindAssets("t:VideoClip", new[] { VideoFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(AssetDatabase.LoadAssetAtPath<VideoClip>)
                .Where(clip => clip != null)
                .ToArray();
        }

        private static void EnsureBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(scene => scene.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));

            int gameplayIndex = scenes.FindIndex(scene => scene.path == GameplayScenePath);
            if (gameplayIndex < 0)
                scenes.Add(new EditorBuildSettingsScene(GameplayScenePath, true));
            else if (!scenes[gameplayIndex].enabled)
                scenes[gameplayIndex] = new EditorBuildSettingsScene(GameplayScenePath, true);

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string text,
            float fontSize,
            Color color,
            FontStyles fontStyle)
        {
            GameObject textObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.fontStyle = fontStyle;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        private static void ConfigureButtonColors(Button button, Color normal, Color highlighted)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = new Color(highlighted.r * 0.75f, highlighted.g * 0.75f,
                highlighted.b * 0.75f, highlighted.a);
            colors.selectedColor = highlighted;
            colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.35f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            rect.localScale = Vector3.one;
        }

        private struct VideoPlacement
        {
            public VideoPlacement(Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
            {
                Anchor = anchor;
                Pivot = pivot;
                Position = position;
                Size = size;
            }

            public Vector2 Anchor { get; }
            public Vector2 Pivot { get; }
            public Vector2 Position { get; set; }
            public Vector2 Size { get; }
        }
    }
}
