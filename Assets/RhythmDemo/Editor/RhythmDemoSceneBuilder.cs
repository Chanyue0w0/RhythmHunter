using System.Collections.Generic;
using System.Linq;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmHunter.RhythmDemoEditor
{
    public static class RhythmDemoSceneBuilder
    {
        public const string ScenePath = "Assets/RhythmDemo/Scenes/BeatTimingDemo.unity";

        private static readonly Color BackgroundColor = new(0.015f, 0.025f, 0.05f, 1f);
        private static readonly Color PanelColor = new(0.035f, 0.065f, 0.11f, 0.97f);
        private static readonly Color Cyan = new(0.18f, 0.9f, 1f, 1f);
        private static readonly Color DimCyan = new(0.08f, 0.28f, 0.34f, 1f);
        private static readonly Color PrimaryText = new(0.9f, 0.96f, 1f, 1f);
        private static readonly Color SecondaryText = new(0.55f, 0.68f, 0.78f, 1f);

        [InitializeOnLoadMethod]
        private static void QueueInitialSceneBuild()
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

        [MenuItem("Rhythm Hunter/Build FMOD Beat Demo")]
        public static void BuildScene()
        {
            EnsureFolders();

            Scene previousScene = SceneManager.GetActiveScene();
            NewSceneMode creationMode = Application.isBatchMode
                ? NewSceneMode.Single
                : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, creationMode);
            scene.name = "BeatTimingDemo";
            SceneManager.SetActiveScene(scene);

            CreateCamera();
            CreateEventSystem();

            Canvas canvas = CreateCanvas();
            Image background = CreateImage("Background", canvas.transform, BackgroundColor);
            Stretch(background.rectTransform);

            Image panel = CreateImage("DemoPanel", canvas.transform, PanelColor);
            SetRect(panel.rectTransform, Vector2.zero, new Vector2(1180f, 900f));
            Outline panelOutline = panel.gameObject.AddComponent<Outline>();
            panelOutline.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.22f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Text title = CreateText(
                "Title",
                panel.transform,
                font,
                "FMOD BEAT INPUT DEMO",
                42,
                FontStyle.Bold,
                PrimaryText,
                new Vector2(0f, 382f),
                new Vector2(1050f, 64f));

            Text status = CreateText(
                "PlaybackStatus",
                panel.transform,
                font,
                "WAITING FOR FMOD...",
                20,
                FontStyle.Bold,
                SecondaryText,
                new Vector2(0f, 326f),
                new Vector2(900f, 36f));

            Image[] beatDots = new Image[4];
            for (int i = 0; i < beatDots.Length; i++)
            {
                Image dot = CreateImage($"BeatDot_{i + 1}", panel.transform, DimCyan);
                SetRect(dot.rectTransform, new Vector2((i - 1.5f) * 58f, 270f), new Vector2(26f, 26f));
                dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                beatDots[i] = dot;
            }

            Image pulse = CreateImage("BeatPulse", panel.transform, DimCyan);
            SetRect(pulse.rectTransform, new Vector2(0f, 145f), new Vector2(150f, 150f));
            pulse.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Outline pulseOutline = pulse.gameObject.AddComponent<Outline>();
            pulseOutline.effectColor = new Color(1f, 1f, 1f, 0.25f);
            pulseOutline.effectDistance = new Vector2(3f, -3f);

            Slider progress = CreateProgressSlider(panel.transform);
            SetRect(progress.GetComponent<RectTransform>(), new Vector2(0f, 30f), new Vector2(760f, 18f));

            Text result = CreateText(
                "JudgementResult",
                panel.transform,
                font,
                "READY",
                72,
                FontStyle.Bold,
                Cyan,
                new Vector2(0f, -80f),
                new Vector2(900f, 100f));

            Text delta = CreateText(
                "JudgementDelta",
                panel.transform,
                font,
                "Press on the beat",
                25,
                FontStyle.Bold,
                SecondaryText,
                new Vector2(0f, -142f),
                new Vector2(900f, 46f));

            Text timing = CreateText(
                "TimingReadout",
                panel.transform,
                font,
                "EVENT   event:/Combat soundtracks/Combat 01\nTIME    --     BPM --\nJUDGE   +/-120 ms     OFFSET +30 ms",
                18,
                FontStyle.Normal,
                SecondaryText,
                new Vector2(0f, -245f),
                new Vector2(1000f, 106f));
            timing.alignment = TextAnchor.MiddleLeft;

            Text statistics = CreateText(
                "Statistics",
                panel.transform,
                font,
                "PERFECT  000     MISS  000     ACCURACY  100.0%     AVG  0.0 ms",
                18,
                FontStyle.Bold,
                PrimaryText,
                new Vector2(0f, -329f),
                new Vector2(1030f, 40f));

            Text instructions = CreateText(
                "Instructions",
                panel.transform,
                font,
                "SPACE  /  ENTER  /  LEFT CLICK  /  GAMEPAD SOUTH",
                18,
                FontStyle.Bold,
                Cyan,
                new Vector2(0f, -385f),
                new Vector2(1000f, 40f));

            GameObject controllerObject = new("RhythmDemoController");
            FmodBeatClock clock = controllerObject.AddComponent<FmodBeatClock>();
            FmodRhythmJudge judge = controllerObject.AddComponent<FmodRhythmJudge>();
            RhythmTapInput tapInput = controllerObject.AddComponent<RhythmTapInput>();
            RhythmDemoPresenter presenter = controllerObject.AddComponent<RhythmDemoPresenter>();

            clock.Configure("event:/Combat soundtracks/Combat 01", 1f, true);
            judge.Configure(clock, 120f, 30f);
            tapInput.Configure(judge);
            presenter.Configure(
                clock,
                judge,
                status,
                result,
                delta,
                timing,
                statistics,
                pulse,
                progress,
                beatDots);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (previousScene.IsValid() && previousScene.isLoaded && previousScene != scene)
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"[RhythmDemoSceneBuilder] Demo scene created: {ScenePath}");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/RhythmDemo"))
                AssetDatabase.CreateFolder("Assets", "RhythmDemo");

            if (!AssetDatabase.IsValidFolder("Assets/RhythmDemo/Scenes"))
                AssetDatabase.CreateFolder("Assets/RhythmDemo", "Scenes");
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new(
                "Main Camera",
                typeof(Camera),
                typeof(AudioListener),
                typeof(FMODUnity.StudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void CreateEventSystem()
        {
            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new(
                "DemoCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static Slider CreateProgressSlider(Transform parent)
        {
            GameObject sliderObject = CreateUiObject("BeatProgress", parent);
            Slider slider = sliderObject.AddComponent<Slider>();
            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.transition = Selectable.Transition.None;

            Image background = CreateImage("Background", sliderObject.transform, new Color(0.06f, 0.16f, 0.22f, 1f));
            Stretch(background.rectTransform);

            GameObject fillArea = CreateUiObject("Fill Area", sliderObject.transform);
            Stretch(fillArea.GetComponent<RectTransform>(), 3f);

            Image fill = CreateImage("Fill", fillArea.transform, Cyan);
            Stretch(fill.rectTransform);
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = background;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            string content,
            int fontSize,
            FontStyle fontStyle,
            Color color,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject textObject = CreateUiObject(name, parent);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            SetRect(text.rectTransform, anchoredPosition, size);
            return text;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = CreateUiObject(name, parent);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void SetRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private static void Stretch(RectTransform rectTransform, float inset = 0f)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(inset, inset);
            rectTransform.offsetMax = new Vector2(-inset, -inset);
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(scene => scene.path != scenePath)
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
