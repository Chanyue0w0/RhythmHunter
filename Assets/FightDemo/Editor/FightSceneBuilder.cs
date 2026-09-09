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
    // Generates a replaceable three-slot-versus-three-slot world-space prototype scene.
    public static class FightSceneBuilder
    {
        public const string ScenePath = "Assets/FightDemo/Scenes/FightScene.unity";
        public const string Scene2Path = "Assets/FightDemo/Scenes/FightScene2.unity";
        public const string InputActionsPath = "Assets/InputActionMap/FightControl.inputactions";

        private static readonly string[] PaladinIdlePaths =
        {
            "Assets/FightDemo/Arts/角色/Paladin/Idle/Idle1.png",
            "Assets/FightDemo/Arts/角色/Paladin/Idle/Idle2.png",
            "Assets/FightDemo/Arts/角色/Paladin/Idle/Idle3.png"
        };

        private static readonly string[] BardIdlePaths =
        {
            "Assets/FightDemo/Arts/角色/Bard/Idle/Idle1.png",
            "Assets/FightDemo/Arts/角色/Bard/Idle/Idle2.png",
            "Assets/FightDemo/Arts/角色/Bard/Idle/Idle3.png"
        };

        private static readonly string[] MageIdlePaths =
        {
            "Assets/FightDemo/Arts/角色/Mage/Idle/Idle1.png",
            "Assets/FightDemo/Arts/角色/Mage/Idle/Idle2.png",
            "Assets/FightDemo/Arts/角色/Mage/Idle/Idle3.png"
        };

        private static readonly string[] GoblinMageIdlePaths =
        {
            "Assets/FightDemo/Arts/敵人/Goblin_Mage/idle/idle_1.png",
            "Assets/FightDemo/Arts/敵人/Goblin_Mage/idle/idle_2.png"
        };

        private static readonly string[] GoblinMercenaryIdlePaths =
        {
            "Assets/FightDemo/Arts/敵人/Goblin_Mercenary/idle/idle_1.png",
            "Assets/FightDemo/Arts/敵人/Goblin_Mercenary/idle/idle_2.png"
        };

        private static readonly string[] GoblinShieldIdlePaths =
        {
            "Assets/FightDemo/Arts/敵人/Goblin_Shield/idle_Holding/holding_1.png",
            "Assets/FightDemo/Arts/敵人/Goblin_Shield/idle_Holding/holding_2.png"
        };

        private static readonly Color Navy = new(0.018f, 0.025f, 0.045f, 1f);
        private static readonly Color Panel = new(0.035f, 0.055f, 0.085f, 0.94f);
        private static readonly Color EnemyZone = new(0.18f, 0.045f, 0.065f, 0.72f);
        private static readonly Color HeroZone = new(0.025f, 0.13f, 0.18f, 0.72f);
        private static readonly Color Cyan = new(0.2f, 0.92f, 1f, 1f);
        private static readonly Color Gold = new(1f, 0.68f, 0.16f, 1f);
        private static readonly Color Primary = new(0.92f, 0.96f, 1f, 1f);
        private static readonly Color Secondary = new(0.58f, 0.68f, 0.78f, 1f);

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

            Sprite worldSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite[] goblinMageIdle = LoadSpriteSequence(GoblinMageIdlePaths);
            Sprite[] goblinMercenaryIdle = LoadSpriteSequence(GoblinMercenaryIdlePaths);
            Sprite[] goblinShieldIdle = LoadSpriteSequence(GoblinShieldIdlePaths);
            Sprite[] paladinIdle = LoadSpriteSequence(PaladinIdlePaths);
            Sprite[] bardIdle = LoadSpriteSequence(BardIdlePaths);
            Sprite[] mageIdle = LoadSpriteSequence(MageIdlePaths);
            if (new[] { goblinMageIdle, goblinMercenaryIdle, goblinShieldIdle, paladinIdle, bardIdle, mageIdle }
                .Any(sequence => sequence.Length == 0))
            {
                Debug.LogError("[FightSceneBuilder] One or more required character idle sequences could not be loaded.");
                return;
            }
            Scene previousScene = SceneManager.GetActiveScene();
            NewSceneMode mode = Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            scene.name = "FightScene";
            SceneManager.SetActiveScene(scene);

            CreateCamera();
            CreateEventSystem();
            Transform battlefield = new GameObject("BattlefieldWorld").transform;
            CreateWorldSprite("WorldBackground", battlefield, worldSprite, Navy, new Vector3(0f, 0f, 4f), new Vector2(22f, 12f), -100);
            CreateReplaceableBackground(battlefield, new Vector2(22f, 12f));
            CreateWorldSprite("EnemyField", battlefield, worldSprite, EnemyZone, new Vector3(-4.3f, 0.2f, 2f), new Vector2(7.3f, 4.4f), -20);
            CreateWorldSprite("HeroField", battlefield, worldSprite, HeroZone, new Vector3(4.3f, 0.2f, 2f), new Vector2(7.3f, 4.4f), -20);
            CreateWorldSprite("CenterLine", battlefield, worldSprite, new Color(1f, 1f, 1f, 0.18f), new Vector3(0f, 0.1f, 1f), new Vector2(0.06f, 4.1f), -10);
            CreateWorldText("EnemyHeader", battlefield, font, "ENEMY FIELD", new Color(1f, 0.5f, 0.55f, 1f), new Vector3(-4.3f, 2.7f, 0f), 0.036f, FontStyle.Bold);
            CreateWorldText("HeroHeader", battlefield, font, "HERO FIELD", Cyan, new Vector3(4.3f, 2.7f, 0f), 0.036f, FontStyle.Bold);

            FightUnitSlot[] enemySlots =
            {
                CreateUnitSlot(battlefield, worldSprite, font, "EnemySlot_GoblinMage", "GOBLIN MAGE", FightUnitSlot.UnitTeam.Enemy, FightUnitSlot.UnitRole.Enemy, 0, new Vector3(-5.8f, 0.15f, 0f), 80, 12, new Color(0.55f, 0.16f, 0.2f, 1f), "SLOT 1", goblinMageIdle, 1.65f),
                CreateUnitSlot(battlefield, worldSprite, font, "EnemySlot_GoblinMercenary", "GOBLIN MERCENARY", FightUnitSlot.UnitTeam.Enemy, FightUnitSlot.UnitRole.Enemy, 1, new Vector3(-4.05f, 0.15f, 0f), 120, 18, new Color(0.85f, 0.2f, 0.25f, 1f), "ACTIVE", goblinMercenaryIdle, 1.65f),
                CreateUnitSlot(battlefield, worldSprite, font, "EnemySlot_GoblinShield", "GOBLIN SHIELD", FightUnitSlot.UnitTeam.Enemy, FightUnitSlot.UnitRole.Enemy, 2, new Vector3(-2.3f, 0.15f, 0f), 90, 14, new Color(0.55f, 0.16f, 0.2f, 1f), "SLOT 3", goblinShieldIdle, 1.65f)
            };

            FightUnitSlot[] heroSlots =
            {
                CreateUnitSlot(battlefield, worldSprite, font, "HeroSlot_Paladin", "PALADIN", FightUnitSlot.UnitTeam.Hero, FightUnitSlot.UnitRole.Tank, 0, new Vector3(2.3f, 0.15f, 0f), 120, 12, new Color(0.12f, 0.65f, 0.9f, 1f), "X / Q", paladinIdle, 1.72f),
                CreateUnitSlot(battlefield, worldSprite, font, "HeroSlot_Bard", "BARD", FightUnitSlot.UnitTeam.Hero, FightUnitSlot.UnitRole.Support, 1, new Vector3(4.05f, 0.15f, 0f), 85, 8, new Color(0.2f, 0.78f, 0.48f, 1f), "Y / W", bardIdle, 1.72f),
                CreateUnitSlot(battlefield, worldSprite, font, "HeroSlot_Mage", "MAGE", FightUnitSlot.UnitTeam.Hero, FightUnitSlot.UnitRole.Damage, 2, new Vector3(5.8f, 0.15f, 0f), 75, 24, new Color(0.72f, 0.3f, 0.88f, 1f), "B / E", mageIdle, 1.72f)
            };

            SpriteRenderer tankShield = CreateWorldSprite(
                "TankShieldEffect", heroSlots[0].transform, worldSprite, new Color(0.3f, 1f, 0.55f, 0f),
                new Vector3(0f, 0.05f, -0.5f), new Vector2(1.7f, 2.35f), 25);
            SpriteRenderer enemyTelegraph = CreateWorldSprite(
                "EnemyTelegraph", enemySlots[1].transform, worldSprite, new Color(1f, 0.22f, 0.25f, 0f),
                new Vector3(0f, 0.05f, -0.5f), new Vector2(1.55f, 2.2f), 24);

            Canvas canvas = CreateCanvas();
            RectTransform topUiRoot = CreateUiGroup("Top UI (Enable In Inspector)", canvas.transform);
            RectTransform bottomUiRoot = CreateUiGroup("Bottom UI (Enable In Inspector)", canvas.transform);
            Image topPanel = CreatePanel("TopHud", topUiRoot, new Vector2(0f, 475f), new Vector2(1500f, 105f), Panel);
            CreateText("Title", topPanel.transform, font, "RHYTHM HUNTER  •  WORLD-SPACE FIGHT PROTOTYPE", 25, FontStyle.Bold, Primary, new Vector2(0f, 23f), new Vector2(1300f, 40f));
            Text playback = CreateText("PlaybackStatus", topPanel.transform, font, "WAITING FOR FMOD BEAT CALLBACK...", 16, FontStyle.Bold, Gold, new Vector2(0f, -20f), new Vector2(1300f, 30f));

            Text warning = CreateText("AttackWarning", topUiRoot, font, "ENEMY ATTACKS ON EVERY FOURTH BEAT", 24, FontStyle.Bold, Primary, new Vector2(0f, 355f), new Vector2(1450f, 44f));
            Text result = CreateText("FightResult", bottomUiRoot, font, "GET READY", 42, FontStyle.Bold, Cyan, new Vector2(0f, -255f), new Vector2(900f, 60f));
            Text detail = CreateText("FightDetail", bottomUiRoot, font, "Press X / Q on beat 4 to guard", 18, FontStyle.Bold, Secondary, new Vector2(0f, -300f), new Vector2(1100f, 36f));

            Image rhythmPanel = CreatePanel("RhythmHud", bottomUiRoot, new Vector2(0f, -420f), new Vector2(1500f, 160f), Panel);
            Text cycle = CreateText("CycleReadout", rhythmPanel.transform, font, "BAR --  •  BEAT --/4", 20, FontStyle.Bold, Primary, new Vector2(-520f, 44f), new Vector2(380f, 38f));
            Image[] beatNodes = new Image[4];
            for (int i = 0; i < beatNodes.Length; i++)
            {
                Color color = i == 3 ? new Color(0.3f, 0.2f, 0.08f, 1f) : new Color(0.12f, 0.2f, 0.28f, 1f);
                Image node = CreateImage($"Beat_{i + 1}", rhythmPanel.transform, color);
                SetRect(node.rectTransform, new Vector2(-125f + i * 74f, 42f), new Vector2(i == 3 ? 46f : 36f, i == 3 ? 46f : 36f));
                node.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                beatNodes[i] = node;
                CreateText($"BeatLabel_{i + 1}", node.transform, font, (i + 1).ToString(), 15, FontStyle.Bold, Primary, Vector2.zero, new Vector2(50f, 50f)).rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            }

            Slider beatProgress = CreateSlider("BeatProgress", rhythmPanel.transform, new Vector2(150f, -28f), new Vector2(610f, 15f), Cyan, new Color(0.08f, 0.12f, 0.18f, 1f));
            Text statistics = CreateText("Statistics", rhythmPanel.transform, font, "CALLS  PERFECT 00  MISS 00     DEFENSE  BLOCK 00  HIT 00", 16, FontStyle.Bold, Secondary, new Vector2(330f, 44f), new Vector2(700f, 36f));
            Text health = CreateText("TankHealth", rhythmPanel.transform, font, "TANK HP   120 / 120", 16, FontStyle.Bold, Primary, new Vector2(-500f, -38f), new Vector2(300f, 30f));
            Slider healthBar = CreateSlider("TankHealthBar", rhythmPanel.transform, new Vector2(-265f, -38f), new Vector2(220f, 15f), new Color(0.3f, 1f, 0.55f, 1f), new Color(0.08f, 0.12f, 0.18f, 1f));
            CreateText("InputLegend", bottomUiRoot, font, "TANK  X / Q     SUPPORT  Y / W     DAMAGE  B / E     ULTIMATE  A / R  (REWORKING)", 16, FontStyle.Bold, Secondary, new Vector2(0f, -520f), new Vector2(1500f, 28f));

            topUiRoot.gameObject.SetActive(false);
            bottomUiRoot.gameObject.SetActive(false);

            Image flash = CreateImage("DamageFlash", canvas.transform, new Color(1f, 0.25f, 0.3f, 0f));
            Stretch(flash.rectTransform);
            flash.raycastTarget = false;

            GameObject controllerObject = new("FightDemoController");
            FmodBeatClock clock = controllerObject.AddComponent<FmodBeatClock>();
            FmodRhythmJudge judge = controllerObject.AddComponent<FmodRhythmJudge>();
            FightInputRouter input = controllerObject.AddComponent<FightInputRouter>();
            FightCombatController fight = controllerObject.AddComponent<FightCombatController>();
            FightScenePresenter hudPresenter = controllerObject.AddComponent<FightScenePresenter>();
            FightBattlefieldPresenter battlefieldPresenter = controllerObject.AddComponent<FightBattlefieldPresenter>();

            clock.Configure("event:/Combat soundtracks/Combat 01", 1f, true);
            judge.Configure(clock, 120f, 30f);
            input.Configure(controls);
            fight.Configure(clock, judge, input, heroSlots[0], enemySlots[1], 120, 18);
            hudPresenter.Configure(clock, judge, fight, playback, cycle, warning, result, detail, health, statistics, beatNodes, beatProgress, healthBar, flash);
            battlefieldPresenter.Configure(fight, enemySlots, heroSlots, tankShield, enemyTelegraph);
            BeatSyncedIdleAnimator[] idleAnimators = battlefield.GetComponentsInChildren<BeatSyncedIdleAnimator>(true);
            for (int i = 0; i < idleAnimators.Length; i++)
            {
                BeatSyncedIdleAnimator animator = idleAnimators[i];
                animator.Configure(fight, animator.TargetRenderer, animator.Frames, animator.CyclesPerBeat, animator.PingPong, i * 0.04f);
            }

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
            Debug.Log($"[FightSceneBuilder] World-space FightScene created: {ScenePath}");
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
            Color color,
            string inputLabel,
            IReadOnlyList<Sprite> idleFrames,
            float actorHeight)
        {
            GameObject slotObject = new(objectName);
            slotObject.transform.SetParent(parent, false);
            slotObject.transform.localPosition = position;
            FightUnitSlot slot = slotObject.AddComponent<FightUnitSlot>();

            CreateWorldSprite("SlotGround", slotObject.transform, sprite, new Color(color.r, color.g, color.b, 0.32f), new Vector3(0f, -1.25f, 0.3f), new Vector2(1.45f, 0.28f), 1);
            Transform actorRoot = new GameObject("ActorRoot (Assign Prefab Here)").transform;
            actorRoot.SetParent(slotObject.transform, false);
            Transform placeholder = new GameObject("PrototypePlaceholder").transform;
            placeholder.SetParent(actorRoot, false);
            CreateWorldSprite("Body", placeholder, sprite, new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 1f), new Vector3(0f, -0.05f, 0f), new Vector2(1.0f, 1.65f), 10);
            CreateWorldSprite("Core", placeholder, sprite, color, new Vector3(0f, 0.2f, -0.1f), new Vector2(0.58f, 0.72f), 11);
            CreateWorldText("PrefabLabel", placeholder, font, "PREFAB\nSLOT", new Color(1f, 1f, 1f, 0.82f), new Vector3(0f, 0.18f, -0.2f), 0.022f, FontStyle.Bold);
            placeholder.gameObject.SetActive(false);

            GameObject idleVisual = new("BeatSyncedIdle", typeof(SpriteRenderer), typeof(BeatSyncedIdleAnimator));
            idleVisual.transform.SetParent(actorRoot, false);
            idleVisual.transform.localPosition = new Vector3(0f, -0.15f, -0.2f);
            SpriteRenderer actorRenderer = idleVisual.GetComponent<SpriteRenderer>();
            actorRenderer.sprite = idleFrames[0];
            actorRenderer.sortingOrder = 12;
            actorRenderer.color = Color.white;
            float sourceHeight = Mathf.Max(0.01f, idleFrames[0].bounds.size.y);
            float actorScale = actorHeight / sourceHeight;
            idleVisual.transform.localScale = new Vector3(actorScale, actorScale, 1f);
            idleVisual.GetComponent<BeatSyncedIdleAnimator>().Configure(null, actorRenderer, idleFrames, 1f, false);

            Transform effectPoint = new GameObject("NormalAttackEffectSpawnPoint").transform;
            effectPoint.SetParent(slotObject.transform, false);
            effectPoint.localPosition = new Vector3(team == FightUnitSlot.UnitTeam.Hero ? -0.72f : 0.72f, 0.15f, -0.3f);

            CreateWorldText("UnitName", slotObject.transform, font, displayName, Primary, new Vector3(0f, -1.58f, 0f), 0.026f, FontStyle.Bold);
            CreateWorldText("RoleAndInput", slotObject.transform, font, $"{role.ToString().ToUpperInvariant()}  •  {inputLabel}", color, new Vector3(0f, -1.92f, 0f), 0.017f, FontStyle.Bold);
            CreateWorldSprite("HealthBackground", slotObject.transform, sprite, new Color(0.03f, 0.04f, 0.06f, 1f), new Vector3(0f, 1.34f, 0f), new Vector2(1.25f, 0.12f), 15);
            SpriteRenderer hpFill = CreateWorldSprite("HealthFill", slotObject.transform, sprite, new Color(0.3f, 1f, 0.55f, 1f), new Vector3(0f, 1.34f, -0.1f), new Vector2(1.2f, 0.075f), 16);
            TextMesh hpLabel = CreateWorldText("Stats", slotObject.transform, font, $"HP {hp}/{hp}  ATK {attack}", Secondary, new Vector3(0f, 1.63f, 0f), 0.015f, FontStyle.Normal);

            slot.Configure(objectName, displayName, team, role, index, hp, attack, color, actorRoot, effectPoint, placeholder.gameObject, sprite, hpFill, hpLabel);
            return slot;
        }

        private static Sprite[] LoadSpriteSequence(IEnumerable<string> paths)
        {
            List<Sprite> sprites = new();
            foreach (string path in paths)
            {
                Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
                if (sprite == null)
                {
                    Debug.LogError($"[FightSceneBuilder] Missing sprite frame: {path}");
                    continue;
                }

                sprites.Add(sprite);
            }

            return sprites.ToArray();
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener), typeof(FMODUnity.StudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Navy;
            camera.orthographic = true;
            camera.orthographicSize = 5.35f;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new("FightHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static RectTransform CreateUiGroup(string name, Transform parent)
        {
            GameObject groupObject = new(name, typeof(RectTransform));
            groupObject.transform.SetParent(parent, false);
            RectTransform rect = groupObject.GetComponent<RectTransform>();
            Stretch(rect);
            return rect;
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

        private static SpriteRenderer CreateReplaceableBackground(Transform parent, Vector2 size)
        {
            GameObject backgroundObject = new("BattleBackground (Assign Sprite In Inspector)", typeof(SpriteRenderer));
            backgroundObject.transform.SetParent(parent, false);
            backgroundObject.transform.localPosition = new Vector3(0f, 0f, 3f);
            SpriteRenderer renderer = backgroundObject.GetComponent<SpriteRenderer>();
            renderer.sprite = null;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.sortingOrder = -90;
            return renderer;
        }

        private static TextMesh CreateWorldText(string name, Transform parent, Font font, string content, Color color, Vector3 localPosition, float characterSize, FontStyle style)
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
            renderer.sortingOrder = 40;
            return text;
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

        private static Slider CreateSlider(string name, Transform parent, Vector2 position, Vector2 size, Color fillColor, Color backgroundColor)
        {
            GameObject sliderObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            SetRect(sliderObject.GetComponent<RectTransform>(), position, size);
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

        private static Text CreateText(string name, Transform parent, Font font, string content, int fontSize, FontStyle style, Color color, Vector2 position, Vector2 size)
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
