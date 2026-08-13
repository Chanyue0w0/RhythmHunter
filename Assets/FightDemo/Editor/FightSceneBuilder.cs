using System.Collections.Generic;
using System.Linq;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmHunter.FightDemoEditor
{
    // Generates a replaceable three-slot-versus-three-slot prototype scene.
    public static class FightSceneBuilder
    {
        public const string ScenePath = "Assets/FightDemo/Scenes/FightScene.unity";
        public const string InputActionsPath = "Assets/InputActionMap/FightControl.inputactions";

        private static readonly Color Navy = new(0.018f, 0.025f, 0.045f, 1f);
        private static readonly Color Panel = new(0.035f, 0.055f, 0.085f, 0.96f);
        private static readonly Color EnemyPanel = new(0.13f, 0.045f, 0.06f, 0.96f);
        private static readonly Color HeroPanel = new(0.035f, 0.09f, 0.13f, 0.96f);
        private static readonly Color Cyan = new(0.2f, 0.92f, 1f, 1f);
        private static readonly Color Gold = new(1f, 0.68f, 0.16f, 1f);
        private static readonly Color Primary = new(0.92f, 0.96f, 1f, 1f);
        private static readonly Color Secondary = new(0.58f, 0.68f, 0.78f, 1f);

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

        [MenuItem("Rhythm Hunter/Build Fight Scene Demo")]
        public static void BuildScene()
        {
            EnsureFolders();

            InputActionAsset controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (controls == null)
            {
                Debug.LogError($"[FightSceneBuilder] Missing Input Action Asset: {InputActionsPath}");
                return;
            }

            Scene previousScene = SceneManager.GetActiveScene();
            NewSceneMode mode = Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            scene.name = "FightScene";
            SceneManager.SetActiveScene(scene);

            CreateCamera();
            CreateEventSystem();
            Canvas canvas = CreateCanvas();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Image background = CreateImage("Background", canvas.transform, Navy);
            Stretch(background.rectTransform);

            Text title = CreateText(
                "Title", canvas.transform, font, "RHYTHM HUNTER  •  FIGHT PROTOTYPE",
                34, FontStyle.Bold, Primary, new Vector2(0f, 500f), new Vector2(1300f, 55f));
            Text playback = CreateText(
                "PlaybackStatus", canvas.transform, font, "WAITING FOR FMOD BEAT CALLBACK...",
                18, FontStyle.Bold, Gold, new Vector2(0f, 458f), new Vector2(1300f, 38f));

            Image enemyTeamPanel = CreatePanel("EnemyTeam", canvas.transform, new Vector2(-500f, 95f), new Vector2(870f, 600f), EnemyPanel);
            Image heroTeamPanel = CreatePanel("HeroTeam", canvas.transform, new Vector2(500f, 95f), new Vector2(870f, 600f), HeroPanel);

            CreateText("EnemyHeader", enemyTeamPanel.transform, font, "ENEMY SLOTS  •  LEFT SIDE",
                22, FontStyle.Bold, new Color(1f, 0.48f, 0.52f, 1f), new Vector2(0f, 255f), new Vector2(760f, 42f));
            CreateText("HeroHeader", heroTeamPanel.transform, font, "HERO SLOTS  •  RIGHT SIDE",
                22, FontStyle.Bold, Cyan, new Vector2(0f, 255f), new Vector2(760f, 42f));

            Image[] enemySlots = new Image[3];
            enemySlots[0] = CreateUnitCard(enemyTeamPanel.transform, font, "EnemySlot_1", "ENEMY 1", "PLACEHOLDER", -270f, new Color(0.34f, 0.11f, 0.14f, 1f), "1");
            enemySlots[1] = CreateUnitCard(enemyTeamPanel.transform, font, "EnemySlot_2", "ENEMY 2", "ACTIVE ATTACKER", 0f, new Color(0.52f, 0.18f, 0.22f, 1f), "2");
            enemySlots[2] = CreateUnitCard(enemyTeamPanel.transform, font, "EnemySlot_3", "ENEMY 3", "PLACEHOLDER", 270f, new Color(0.34f, 0.11f, 0.14f, 1f), "3");

            Image[] heroSlots = new Image[3];
            heroSlots[0] = CreateUnitCard(heroTeamPanel.transform, font, "HeroSlot_Tank", "TANK", "GUARD", -270f, new Color(0.1f, 0.42f, 0.58f, 1f), "X  /  Q");
            heroSlots[1] = CreateUnitCard(heroTeamPanel.transform, font, "HeroSlot_Support", "SUPPORT", "SKILL LATER", 0f, new Color(0.15f, 0.5f, 0.34f, 1f), "Y  /  W");
            heroSlots[2] = CreateUnitCard(heroTeamPanel.transform, font, "HeroSlot_Damage", "DAMAGE", "SKILL LATER", 270f, new Color(0.48f, 0.2f, 0.58f, 1f), "B  /  E");

            Image shield = CreateImage("TankShieldEffect", heroSlots[0].transform, new Color(0.3f, 1f, 0.55f, 0f));
            Stretch(shield.rectTransform, new Vector2(-8f, -8f), new Vector2(8f, 8f));
            shield.raycastTarget = false;
            Outline shieldOutline = shield.gameObject.AddComponent<Outline>();
            shieldOutline.effectColor = new Color(0.5f, 1f, 0.72f, 0.8f);
            shieldOutline.effectDistance = new Vector2(4f, -4f);

            Text warning = CreateText(
                "AttackWarning", canvas.transform, font, "ENEMY ATTACKS ON EVERY FOURTH BEAT",
                28, FontStyle.Bold, Primary, new Vector2(0f, -230f), new Vector2(1500f, 50f));

            Text result = CreateText(
                "FightResult", canvas.transform, font, "GET READY",
                48, FontStyle.Bold, Cyan, new Vector2(0f, -280f), new Vector2(1000f, 70f));
            Text detail = CreateText(
                "FightDetail", canvas.transform, font, "Press X / Q on beat 4 to guard",
                20, FontStyle.Bold, Secondary, new Vector2(0f, -326f), new Vector2(1200f, 42f));

            Image rhythmPanel = CreatePanel("RhythmPanel", canvas.transform, new Vector2(0f, -430f), new Vector2(1450f, 160f), Panel);
            Text cycle = CreateText(
                "CycleReadout", rhythmPanel.transform, font, "BAR --  •  BEAT --/4",
                22, FontStyle.Bold, Primary, new Vector2(-505f, 48f), new Vector2(390f, 42f));

            Image[] beatNodes = new Image[4];
            for (int i = 0; i < beatNodes.Length; i++)
            {
                Color color = i == 3 ? new Color(0.3f, 0.2f, 0.08f, 1f) : new Color(0.12f, 0.2f, 0.28f, 1f);
                Image node = CreateImage($"Beat_{i + 1}", rhythmPanel.transform, color);
                SetRect(node.rectTransform, new Vector2(-115f + i * 78f, 38f), new Vector2(i == 3 ? 48f : 38f, i == 3 ? 48f : 38f));
                node.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                beatNodes[i] = node;
                CreateText($"BeatLabel_{i + 1}", node.transform, font, (i + 1).ToString(), 16,
                    FontStyle.Bold, Primary, Vector2.zero, new Vector2(52f, 52f)).rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            }

            Slider beatProgress = CreateSlider("BeatProgress", rhythmPanel.transform, new Vector2(155f, -30f), new Vector2(620f, 16f), Cyan, new Color(0.08f, 0.12f, 0.18f, 1f));
            Text statistics = CreateText(
                "Statistics", rhythmPanel.transform, font, "CALLS  PERFECT 00  MISS 00     DEFENSE  BLOCK 00  HIT 00",
                17, FontStyle.Bold, Secondary, new Vector2(320f, 48f), new Vector2(700f, 40f));

            Text health = CreateText(
                "PartyHealth", heroTeamPanel.transform, font, "PARTY HP   5 / 5",
                18, FontStyle.Bold, Primary, new Vector2(-180f, -255f), new Vector2(360f, 36f));
            Slider healthBar = CreateSlider("PartyHealthBar", heroTeamPanel.transform, new Vector2(195f, -255f), new Vector2(350f, 18f), new Color(0.3f, 1f, 0.55f, 1f), new Color(0.08f, 0.12f, 0.18f, 1f));

            CreateText(
                "InputLegend", canvas.transform, font,
                "TANK  X / Q     SUPPORT  Y / W     DAMAGE  B / E     ULTIMATE  A / R  (REWORKING)",
                17, FontStyle.Bold, Secondary, new Vector2(0f, -520f), new Vector2(1500f, 30f));

            Image flash = CreateImage("DamageFlash", canvas.transform, new Color(1f, 0.25f, 0.3f, 0f));
            Stretch(flash.rectTransform);
            flash.raycastTarget = false;

            GameObject controllerObject = new("FightDemoController");
            FmodBeatClock clock = controllerObject.AddComponent<FmodBeatClock>();
            FmodRhythmJudge judge = controllerObject.AddComponent<FmodRhythmJudge>();
            FightInputRouter input = controllerObject.AddComponent<FightInputRouter>();
            FightCombatController fight = controllerObject.AddComponent<FightCombatController>();
            FightScenePresenter presenter = controllerObject.AddComponent<FightScenePresenter>();

            clock.Configure("event:/Combat soundtracks/Combat 01", 1f, true);
            judge.Configure(clock, 120f, 30f);
            input.Configure(controls);
            fight.Configure(clock, judge, input, 5, 1);
            presenter.Configure(
                clock, judge, fight,
                playback, cycle, warning, result, detail, health, statistics,
                beatNodes, beatProgress, healthBar, enemySlots, heroSlots, shield, flash);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!Application.isBatchMode && previousScene.IsValid() && previousScene.isLoaded && previousScene != scene)
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"[FightSceneBuilder] FightScene created: {ScenePath}");
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
            camera.backgroundColor = Navy;
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new(
                "FightCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static Image CreatePanel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            Image panel = CreateImage(name, parent, color);
            SetRect(panel.rectTransform, position, size);
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.12f);
            outline.effectDistance = new Vector2(2f, -2f);
            return panel;
        }

        private static Image CreateUnitCard(
            Transform parent,
            Font font,
            string objectName,
            string displayName,
            string role,
            float x,
            Color color,
            string inputLabel)
        {
            Image card = CreateImage(objectName, parent, new Color(color.r * 0.42f, color.g * 0.42f, color.b * 0.42f, 1f));
            SetRect(card.rectTransform, new Vector2(x, 15f), new Vector2(235f, 420f));
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(color.r, color.g, color.b, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            Image portrait = CreateImage("PrefabPlaceholder", card.transform, color);
            SetRect(portrait.rectTransform, new Vector2(0f, 62f), new Vector2(165f, 210f));
            portrait.raycastTarget = false;
            CreateText("PlaceholderGlyph", portrait.transform, font, "PREFAB\nSLOT", 24, FontStyle.Bold,
                new Color(1f, 1f, 1f, 0.72f), Vector2.zero, new Vector2(150f, 100f));

            CreateText("UnitName", card.transform, font, displayName, 23, FontStyle.Bold,
                Primary, new Vector2(0f, -82f), new Vector2(210f, 38f));
            CreateText("Role", card.transform, font, role, 15, FontStyle.Bold,
                Secondary, new Vector2(0f, -118f), new Vector2(210f, 30f));
            CreateText("Input", card.transform, font, inputLabel, 18, FontStyle.Bold,
                new Color(color.r + (1f - color.r) * 0.35f, color.g + (1f - color.g) * 0.35f, color.b + (1f - color.b) * 0.35f, 1f),
                new Vector2(0f, -164f), new Vector2(210f, 34f));
            return card;
        }

        private static Slider CreateSlider(string name, Transform parent, Vector2 position, Vector2 size, Color fillColor, Color backgroundColor)
        {
            GameObject sliderObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            SetRect(sliderRect, position, size);

            Image background = CreateImage("Background", sliderObject.transform, backgroundColor);
            Stretch(background.rectTransform);
            Image fillArea = CreateImage("Fill Area", sliderObject.transform, Color.clear);
            Stretch(fillArea.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            Image fill = CreateImage("Fill", fillArea.transform, fillColor);
            Stretch(fill.rectTransform);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = background;
            return slider;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            Vector2 position,
            Vector2 size)
        {
            GameObject textObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            SetRect(text.rectTransform, position, size);
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin ?? Vector2.zero;
            rect.offsetMax = offsetMax ?? Vector2.zero;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/FightDemo"))
                AssetDatabase.CreateFolder("Assets", "FightDemo");
            if (!AssetDatabase.IsValidFolder("Assets/FightDemo/Scenes"))
                AssetDatabase.CreateFolder("Assets/FightDemo", "Scenes");
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
