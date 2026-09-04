using System.Collections.Generic;
using System.Linq;
using RhythmHunter.RhythmArena;
using RhythmHunter.RhythmDemo;
using RhythmHunter.TopDownBeatCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmHunter.TopDownBeatCombatEditor
{
    public static class TopDownBeatCombatSceneBuilder
    {
        public const string ScenePath = "Assets/Prototype/TopDownBeatCombat/Scenes/TopDownBeatCombatPrototype.unity";
        private const string MaterialFolder = "Assets/Prototype/TopDownBeatCombat/Materials";

        private static readonly Color FloorA = new(0.018f, 0.035f, 0.045f, 1f);
        private static readonly Color FloorB = new(0.025f, 0.052f, 0.064f, 1f);
        private static readonly Color Wall = new(0.07f, 0.16f, 0.18f, 1f);
        private static readonly Color Cyan = new(0.2f, 0.9f, 1f, 1f);
        private static readonly Color White = new(0.92f, 0.98f, 1f, 1f);
        private static readonly Color HudDark = new(0.008f, 0.018f, 0.024f, 0.94f);

        [MenuItem("Rhythm Hunter/Build Top Down Beat Combat Prototype")]
        public static void BuildScene()
        {
            EnsureFolders();
            Material floorA = GetOrCreateMaterial("FloorA", FloorA);
            Material floorB = GetOrCreateMaterial("FloorB", FloorB);
            Material wall = GetOrCreateMaterial("Wall", Wall);
            Material white = GetOrCreateMaterial("White", White);
            Material cyan = GetOrCreateMaterial("Cyan", Cyan);

            Scene previousScene = SceneManager.GetActiveScene();
            NewSceneMode mode = Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            scene.name = "TopDownBeatCombatPrototype";
            SceneManager.SetActiveScene(scene);

            GameObject root = new("TopDownBeatCombatPrototype");
            CreateArena(root.transform, floorA, floorB, wall);

            GameObject rhythmObject = CreateChild("FMOD Rhythm System", root.transform);
            FmodBeatClock fmodClock = rhythmObject.AddComponent<FmodBeatClock>();
            fmodClock.Configure("event:/Combat soundtracks/Combat 01", 0.25f, true, 0.5f);
            RhythmClock rhythmClock = rhythmObject.AddComponent<RhythmClock>();
            rhythmClock.Configure(fmodClock, 100f, 4, 0.10f, 0.25f);

            GameObject playerObject = CreatePlayer(root.transform, out PixelFourDirectionPresenter presenter, out Transform attackFlash);
            TopDownBeatPlayer player = playerObject.GetComponent<TopDownBeatPlayer>();

            TopDownBeatCamera cameraRig = CreateCamera(root.transform, playerObject.transform, player);
            player.Configure(rhythmClock, presenter, cameraRig, attackFlash);

            CreateDummy(root.transform, white);
            SoundfallBeatHud hud = CreateBeatHud(root.transform, rhythmClock, player);
            _ = hud;
            CreateEventSystem(root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            ApplyBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (previousScene.IsValid() && previousScene.isLoaded && previousScene != scene && !Application.isBatchMode)
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"[TopDownBeatCombat] Scene created: {ScenePath}");
        }

        public static void ApplyBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(item => item.path != ScenePath)
                .Select(item => new EditorBuildSettingsScene(item.path, false))
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void CreateArena(Transform parent, Material floorA, Material floorB, Material wall)
        {
            GameObject arena = CreateChild("Pixel Arena", parent);
            GameObject floor = CreateChild("Floor Tiles", arena.transform);
            for (int y = -7; y <= 7; y++)
            for (int x = -10; x <= 10; x++)
            {
                Material tileMaterial = (x + y & 1) == 0 ? floorA : floorB;
                CreateQuad($"Tile_{x}_{y}", floor.transform, tileMaterial, new Vector3(x, y, 1f), new Vector3(0.96f, 0.96f, 1f));
            }

            GameObject boundary = CreateChild("Boundary", arena.transform);
            CreateQuad("Wall_North", boundary.transform, wall, new Vector3(0f, 7.7f, 0.5f), new Vector3(21.5f, 0.55f, 1f));
            CreateQuad("Wall_South", boundary.transform, wall, new Vector3(0f, -7.7f, 0.5f), new Vector3(21.5f, 0.55f, 1f));
            CreateQuad("Wall_East", boundary.transform, wall, new Vector3(10.7f, 0f, 0.5f), new Vector3(0.55f, 15.9f, 1f));
            CreateQuad("Wall_West", boundary.transform, wall, new Vector3(-10.7f, 0f, 0.5f), new Vector3(0.55f, 15.9f, 1f));

            GameObject markers = CreateChild("Arena Markers", arena.transform);
            for (int i = -4; i <= 4; i++)
            {
                if (i == 0)
                    continue;
                CreateQuad($"Marker_{i}", markers.transform, wall, new Vector3(i * 2f, 0f, 0.7f), new Vector3(0.12f, 0.12f, 1f));
            }
        }

        private static GameObject CreatePlayer(
            Transform parent,
            out PixelFourDirectionPresenter presenter,
            out Transform attackFlash)
        {
            GameObject player = new(
                "Pixel Hero",
                typeof(SpriteRenderer),
                typeof(Rigidbody2D),
                typeof(CapsuleCollider2D),
                typeof(PixelFourDirectionPresenter),
                typeof(TopDownBeatPlayer));
            player.transform.SetParent(parent, false);
            player.transform.position = new Vector3(-1.55f, 0f, -1f);

            SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = 20;
            presenter = player.GetComponent<PixelFourDirectionPresenter>();
            presenter.Configure(false, Cyan, White, new Color(0.02f, 0.08f, 0.1f, 1f));

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            CapsuleCollider2D collider = player.GetComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.58f, 0.68f);
            collider.offset = new Vector2(0f, 0.32f);

            GameObject flash = CreateChild("Attack Flash", player.transform);
            SpriteRenderer flashRenderer = flash.AddComponent<SpriteRenderer>();
            flashRenderer.color = White;
            flashRenderer.sortingOrder = 19;
            flashRenderer.sprite = CreateRuntimeBuilderSprite();
            flash.SetActive(false);
            attackFlash = flash.transform;

            GameObject label = CreateChild("Player Label", player.transform);
            TextMesh text = CreateWorldText(label, "HERO", new Vector3(0f, 1.45f, 0f), 0.055f, Cyan);
            text.fontStyle = FontStyle.Bold;
            return player;
        }

        private static void CreateDummy(Transform parent, Material whiteMaterial)
        {
            GameObject dummy = new(
                "Training Dummy",
                typeof(SpriteRenderer),
                typeof(CircleCollider2D),
                typeof(PixelFourDirectionPresenter),
                typeof(BeatTrainingDummy));
            dummy.transform.SetParent(parent, false);
            dummy.transform.position = new Vector3(0f, 0f, -0.9f);

            SpriteRenderer renderer = dummy.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = 18;
            PixelFourDirectionPresenter presenter = dummy.GetComponent<PixelFourDirectionPresenter>();
            presenter.Configure(true, new Color(0.85f, 0.52f, 0.16f, 1f), White, new Color(0.16f, 0.07f, 0.02f, 1f));
            dummy.GetComponent<CircleCollider2D>().radius = 0.45f;

            GameObject hpBar = CreateChild("World HP Bar", dummy.transform);
            hpBar.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            CreateQuad("Background", hpBar.transform, GetOrCreateMaterial("HpBack", new Color(0.08f, 0.03f, 0.03f, 1f)), Vector3.zero, new Vector3(1.8f, 0.16f, 1f));
            GameObject fill = CreateQuad("Fill", hpBar.transform, whiteMaterial, new Vector3(0f, 0f, -0.05f), new Vector3(1.65f, 0.09f, 1f));

            TextMesh hpText = CreateWorldText(CreateChild("HP Text", dummy.transform), "TRAINING DUMMY  200/200", new Vector3(0f, 1.72f, 0f), 0.05f, White);
            TextMesh damageText = CreateWorldText(CreateChild("Damage Text", dummy.transform), string.Empty, new Vector3(0f, 2.08f, 0f), 0.07f, White);
            damageText.fontStyle = FontStyle.Bold;

            dummy.GetComponent<BeatTrainingDummy>().Configure(renderer, fill.transform, hpText, damageText);
        }

        private static TopDownBeatCamera CreateCamera(Transform parent, Transform target, TopDownBeatPlayer player)
        {
            GameObject cameraObject = new(
                "Main Camera",
                typeof(Camera),
                typeof(AudioListener),
                typeof(FMODUnity.StudioListener),
                typeof(TopDownBeatCamera));
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.position = new Vector3(-0.7f, 0f, -10f);
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.005f, 0.01f, 0.014f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5.6f;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            TopDownBeatCamera rig = cameraObject.GetComponent<TopDownBeatCamera>();
            rig.Configure(target, player);
            return rig;
        }

        private static SoundfallBeatHud CreateBeatHud(Transform parent, RhythmClock clock, TopDownBeatPlayer player)
        {
            GameObject canvasObject = new(
                "Beat HUD Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image panel = CreateImage("Beat HUD Panel", canvas.transform, HudDark);
            SetRect(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1120f, 185f), new Vector2(0.5f, 0f));

            Text title = CreateUiText("Title", panel.transform, "BEAT DRIVE", 24, FontStyle.Bold, Cyan);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(115f, -34f), new Vector2(190f, 36f), new Vector2(0f, 1f));

            Text beatReadout = CreateUiText("Beat Readout", panel.transform, "WAITING FOR FMOD", 20, FontStyle.Bold, White);
            SetRect(beatReadout.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-220f, -34f), new Vector2(400f, 36f), new Vector2(1f, 1f));

            Image trackBack = CreateImage("Beat Track", panel.transform, new Color(0.025f, 0.065f, 0.078f, 1f));
            SetRect(trackBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(880f, 64f), new Vector2(0.5f, 0.5f));
            trackBack.gameObject.AddComponent<RectMask2D>();

            RectTransform[] ticks = new RectTransform[11];
            Image[] tickImages = new Image[ticks.Length];
            for (int i = 0; i < ticks.Length; i++)
            {
                Image tick = CreateImage($"Beat Tick {i}", trackBack.transform, White);
                SetRect(tick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7f, 36f), new Vector2(0.5f, 0.5f));
                ticks[i] = tick.rectTransform;
                tickImages[i] = tick;
            }

            Image hitLineImage = CreateImage("Center Hit Line", trackBack.transform, new Color(1f, 0.88f, 0.2f, 1f));
            SetRect(hitLineImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 58f), new Vector2(0.5f, 0.5f));

            Text result = CreateUiText("Result", panel.transform, "ATTACK ON THE CENTER LINE", 22, FontStyle.Bold, White);
            SetRect(result.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(600f, 38f), new Vector2(0.5f, 0f));

            Text controls = CreateUiText(
                "Controls",
                canvas.transform,
                "WASD / LEFT STICK  MOVE     J / LMB / X  ATTACK     SPACE / RMB / A  QUICK DODGE",
                18,
                FontStyle.Bold,
                White);
            SetRect(controls.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(950f, 40f), new Vector2(0.5f, 1f));

            SoundfallBeatHud hud = canvasObject.AddComponent<SoundfallBeatHud>();
            hud.Configure(clock, player, ticks, tickImages, hitLineImage.rectTransform, beatReadout, result);
            return hud;
        }

        private static void CreateEventSystem(Transform parent)
        {
            GameObject eventSystem = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(parent, false);
        }

        private static Sprite CreateRuntimeBuilderSprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static TextMesh CreateWorldText(GameObject textObject, string content, Vector3 localPosition, float characterSize, Color color)
        {
            textObject.transform.localPosition = localPosition;
            TextMesh text = textObject.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font;
            text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            text.text = content;
            text.fontSize = 48;
            text.characterSize = characterSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            return text;
        }

        private static Text CreateUiText(string name, Transform parent, string content, int size, FontStyle style, Color color)
        {
            GameObject textObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
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

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 size,
            Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static GameObject CreateQuad(string name, Transform parent, Material material, Vector3 position, Vector3 scale)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = position;
            quad.transform.localScale = scale;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.GetComponent<Renderer>().sharedMaterial = material;
            return quad;
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Prototype", "TopDownBeatCombat");
            EnsureFolder("Assets/Prototype/TopDownBeatCombat", "Scripts");
            EnsureFolder("Assets/Prototype/TopDownBeatCombat", "Editor");
            EnsureFolder("Assets/Prototype/TopDownBeatCombat", "Scenes");
            EnsureFolder("Assets/Prototype/TopDownBeatCombat", "Materials");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
