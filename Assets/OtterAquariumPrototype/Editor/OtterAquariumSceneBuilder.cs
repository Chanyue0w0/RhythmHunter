using System.Collections.Generic;
using System.Linq;
using RhythmHunter.OtterAquariumPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    public static class OtterAquariumSceneBuilder
    {
        public const string ScenePath = "Assets/OtterAquariumPrototype/Scenes/OtterAquarium.unity";
        public const string BackgroundSpritePath = "Assets/OtterAquariumPrototype/Arts/Background/zoo_background.png";
        public const string WaterParticleMaterialPath = "Assets/OtterAquariumPrototype/Materials/M_WaterParticles.mat";
        public const string WaterParticleTexturePath = "Assets/OtterAquariumPrototype/Materials/T_WaterParticle.asset";

        private static readonly Vector2 BackgroundImageSize = new(1448f, 1086f);
        private static readonly Vector2 BackgroundWorldSize = new(22f, 16.5f);
        private const string SurfaceZoneLayoutName = "ImageMatchedSurfaceZones_v1";

        private static readonly Color DeepTeal = new(0.035f, 0.18f, 0.2f, 1f);
        private static readonly Color WaterDeep = new(0.06f, 0.42f, 0.52f, 1f);
        private static readonly Color WaterMid = new(0.11f, 0.62f, 0.68f, 1f);
        private static readonly Color WaterShallow = new(0.35f, 0.83f, 0.78f, 1f);
        private static readonly Color Foam = new(0.78f, 1f, 0.96f, 0.72f);
        private static readonly Color Sand = new(0.86f, 0.78f, 0.58f, 1f);
        private static readonly Color SandLight = new(0.96f, 0.89f, 0.7f, 1f);
        private static readonly Color Path = new(0.64f, 0.69f, 0.64f, 1f);
        private static readonly Color Rock = new(0.42f, 0.43f, 0.35f, 1f);
        private static readonly Color RockLight = new(0.63f, 0.61f, 0.47f, 1f);
        private static readonly Color Glass = new(0.55f, 0.94f, 1f, 0.58f);
        private static readonly Color OtterBrown = new(0.34f, 0.18f, 0.09f, 1f);
        private static readonly Color OtterLight = new(0.76f, 0.56f, 0.34f, 1f);
        private static readonly Color Accent = new(1f, 0.62f, 0.22f, 1f);

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

            Material particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(WaterParticleMaterialPath);
            Sprite backgroundSprite = LoadBackgroundSprite();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null
                || particleMaterial == null
                || backgroundSprite == null
                || !SceneUsesWaterParticleMaterial(particleMaterial)
                || !SceneUsesZooBackground(backgroundSprite))
            {
                BuildScene();
            }
        }

        [MenuItem("Rhythm Hunter/Build Sea Otter Aquarium Prototype")]
        public static void BuildScene()
        {
            EnsureFolders();
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite backgroundSprite = LoadBackgroundSprite();
            Material waterParticleMaterial = EnsureWaterParticleMaterial();
            if (sprite == null || backgroundSprite == null || waterParticleMaterial == null)
            {
                Debug.LogError($"[OtterAquarium] Required assets are missing. Expected background: {BackgroundSpritePath}");
                return;
            }

            Scene previousScene = SceneManager.GetActiveScene();
            bool replacingLoadedPrototype = previousScene.IsValid() && previousScene.path == ScenePath;
            NewSceneMode mode = Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            scene.name = "OtterAquarium";
            SceneManager.SetActiveScene(scene);

            if (!Application.isBatchMode && replacingLoadedPrototype)
                EditorSceneManager.CloseScene(previousScene, true);

            Transform root = new GameObject("OtterAquariumPrototype").transform;
            Transform environment = CreateEmpty("Environment", root);
            CreateEnvironment(environment, backgroundSprite);
            CreateSurfaceZones(environment);
            CreateBounds(environment);

            OtterMovementController otter = CreateOtter(root, sprite, waterParticleMaterial);
            CreateCamera(root, otter.transform);
            OtterPrototypeHud hud = new GameObject("PrototypeHUD", typeof(OtterPrototypeHud)).GetComponent<OtterPrototypeHud>();
            hud.transform.SetParent(root, false);
            hud.Configure(otter);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool valid = OtterAquariumValidation.ValidateScene(false);
            if (!valid)
                Debug.LogError("[OtterAquarium] Generated scene did not pass validation.");

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
            Debug.Log($"[OtterAquarium] Prototype scene created: {ScenePath}");
        }

        private static void CreateEnvironment(Transform root, Sprite backgroundSprite)
        {
            GameObject backgroundObject = new("ZooBackground", typeof(SpriteRenderer));
            backgroundObject.transform.SetParent(root, false);
            SpriteRenderer renderer = backgroundObject.GetComponent<SpriteRenderer>();
            renderer.sprite = backgroundSprite;
            renderer.color = Color.white;
            renderer.sortingOrder = -100;

            float scale = BackgroundWorldSize.x / backgroundSprite.bounds.size.x;
            backgroundObject.transform.localScale = new Vector3(scale, scale, 1f);
            Vector3 scaledCenter = backgroundSprite.bounds.center * scale;
            backgroundObject.transform.localPosition = new Vector3(-scaledCenter.x, -scaledCenter.y, 5f);
        }

        private static void CreateRockIsland(Transform parent, Sprite sprite, Font font, string name, Vector2 position, Vector2 size, string label)
        {
            Transform island = CreateEmpty(name, parent);
            island.localPosition = position;
            CreateWorldSprite("RockShadow", island, sprite, new Color(0.12f, 0.13f, 0.1f, 0.48f), new Vector3(0.15f, -0.16f, 0.8f), size + new Vector2(0.25f, 0.2f), 16);
            CreateWorldSprite("RockBase", island, sprite, Rock, Vector3.zero, size, 18);
            CreateWorldSprite("DryTop", island, sprite, RockLight, new Vector3(-0.1f, 0.18f, -0.1f), size * 0.74f, 19);
            CreateWorldSprite("WarmPatch", island, sprite, Sand, new Vector3(0.12f, 0.25f, -0.2f), size * 0.44f, 20);
            CreateWorldText("IslandLabel", island, font, label, new Color(0.2f, 0.17f, 0.11f, 0.68f), new Vector3(0f, 0.22f, -0.3f), 0.017f, FontStyle.Bold, 21);
        }

        private static void CreatePoolDetails(Transform pool, Sprite sprite)
        {
            for (int i = 0; i < 9; i++)
            {
                float x = -6.2f + i * 1.55f;
                float y = Mathf.Sin(i * 1.7f) * 2.6f;
                CreateWorldSprite($"WaterGlint_{i + 1:00}", pool, sprite, new Color(0.8f, 1f, 0.96f, 0.3f), new Vector3(x, y, 1.4f), new Vector2(0.75f, 0.05f), -52);
            }

            Transform kelp = CreateEmpty("KelpBeds", pool);
            for (int i = 0; i < 6; i++)
            {
                Vector3 position = new(-6.4f + i * 2.55f, i % 2 == 0 ? -3.25f : 3.15f, 1f);
                SpriteRenderer leaf = CreateWorldSprite($"Kelp_{i + 1:00}", kelp, sprite, new Color(0.08f, 0.42f, 0.25f, 0.7f), position, new Vector2(0.18f, 0.72f), -45);
                leaf.transform.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? -18f : 16f);
            }

            CreateWorldSprite("FloatingRing", pool, sprite, Accent, new Vector3(5.3f, 2.4f, 0f), new Vector2(1.1f, 0.42f), 12);
            CreateWorldSprite("FloatingRingInner", pool, sprite, WaterMid, new Vector3(5.3f, 2.4f, -0.1f), new Vector2(0.62f, 0.18f), 13);
        }

        private static void CreateVisitorProps(Transform root, Sprite sprite, Font font)
        {
            Transform glass = CreateEmpty("GlassViewingRail", root);
            CreateWorldSprite("TopGlass", glass, sprite, Glass, new Vector3(0f, 5.2f, 0f), new Vector2(18.2f, 0.14f), 55);
            CreateWorldSprite("BottomGlass", glass, sprite, Glass, new Vector3(0f, -5.2f, 0f), new Vector2(18.2f, 0.14f), 260);
            CreateWorldSprite("LeftGlass", glass, sprite, Glass, new Vector3(-9.1f, 0f, 0f), new Vector2(0.14f, 10.5f), 150);
            CreateWorldSprite("RightGlass", glass, sprite, Glass, new Vector3(9.1f, 0f, 0f), new Vector2(0.14f, 10.5f), 150);

            CreateSign(root, sprite, font, new Vector2(-9.6f, 6.35f), "SEA OTTERS\nENRICHMENT POOL");
            CreateSign(root, sprite, font, new Vector2(9.6f, -6.35f), "KEEPERS ONLY\nWET FLOOR");

            for (int i = 0; i < 5; i++)
            {
                float x = -5.8f + i * 2.9f;
                Transform planter = CreateEmpty($"Planter_{i + 1:00}", root);
                planter.localPosition = new Vector3(x, i % 2 == 0 ? 6.45f : -6.45f, 0f);
                CreateWorldSprite("Pot", planter, sprite, new Color(0.32f, 0.24f, 0.17f, 1f), Vector3.zero, new Vector2(0.72f, 0.34f), 80);
                CreateWorldSprite("Plant", planter, sprite, new Color(0.18f, 0.5f, 0.25f, 1f), new Vector3(0f, 0.32f, -0.1f), new Vector2(0.46f, 0.7f), 81);
            }
        }

        private static void CreateSign(Transform parent, Sprite sprite, Font font, Vector2 position, string text)
        {
            Transform sign = CreateEmpty("ExhibitSign", parent);
            sign.localPosition = position;
            CreateWorldSprite("SignShadow", sign, sprite, new Color(0.08f, 0.1f, 0.08f, 0.5f), new Vector3(0.08f, -0.08f, 0.2f), new Vector2(2.8f, 0.82f), 70);
            CreateWorldSprite("SignBoard", sign, sprite, new Color(0.12f, 0.29f, 0.26f, 1f), Vector3.zero, new Vector2(2.8f, 0.82f), 71);
            CreateWorldText("SignText", sign, font, text, new Color(0.92f, 1f, 0.83f, 1f), new Vector3(0f, 0f, -0.1f), 0.018f, FontStyle.Bold, 72);
        }

        private static void CreateSurfaceZones(Transform root)
        {
            Transform zones = CreateEmpty(SurfaceZoneLayoutName, root);

            // The broad pale-blue pool footprint. Higher-priority deep-water
            // and land paths below cut this into the visible image regions.
            CreateImagePolygonZone(
                "ShallowWater_MainPool",
                zones,
                AquariumSurfaceType.ShallowWater,
                20,
                0.82f,
                P(105, 150), P(250, 112), P(520, 115), P(610, 210), P(690, 320),
                P(820, 365), P(930, 300), P(1060, 315), P(1190, 380), P(1325, 520),
                P(1350, 760), P(1280, 870), P(1110, 880), P(970, 830), P(730, 890),
                P(500, 880), P(300, 850), P(130, 850), P(80, 700), P(75, 280));

            CreateImagePolygonZone(
                "DeepWater_CentralBasin",
                zones,
                AquariumSurfaceType.Water,
                50,
                1f,
                P(160, 500), P(210, 380), P(330, 300), P(480, 315), P(610, 405),
                P(730, 450), P(880, 420), P(1000, 360), P(1120, 415), P(1220, 545),
                P(1260, 660), P(1170, 760), P(980, 820), P(720, 860), P(470, 830),
                P(270, 760), P(150, 650));

            // Walkable rock and sand areas which visually sit inside the pool
            // outline. Land priority intentionally overrides both water zones.
            CreateImagePolygonZone("Land_CentralRockTerrace", zones, AquariumSurfaceType.Land, 100, 1f,
                P(535, 70), P(1015, 65), P(1010, 150), P(950, 190), P(950, 270),
                P(900, 320), P(810, 385), P(715, 370), P(650, 410), P(610, 355),
                P(650, 300), P(600, 240), P(560, 200));
            CreateImagePolygonZone("Land_UpperLeftRocks", zones, AquariumSurfaceType.Land, 100, 1f,
                P(180, 90), P(550, 85), P(590, 160), P(520, 210), P(420, 210),
                P(340, 235), P(255, 220), P(200, 175));
            CreateImagePolygonZone("Land_UpperRightSand", zones, AquariumSurfaceType.Land, 100, 1f,
                P(955, 125), P(1305, 130), P(1340, 245), P(1320, 340), P(1240, 360),
                P(1150, 330), P(1030, 315), P(930, 280), P(900, 210));
            CreateImagePolygonZone("Land_RightSand", zones, AquariumSurfaceType.Land, 100, 1f,
                P(1270, 330), P(1370, 340), P(1360, 790), P(1290, 830), P(1230, 760),
                P(1240, 650), P(1180, 600), P(1210, 520), P(1270, 480));
            CreateImagePolygonZone("Land_RightRockIsland", zones, AquariumSurfaceType.Land, 100, 1f,
                P(1040, 380), P(1180, 360), P(1270, 430), P(1260, 520),
                P(1170, 570), P(1040, 540), P(990, 480));
            CreateImagePolygonZone("Land_BottomLeftSand", zones, AquariumSurfaceType.Land, 100, 1f,
                P(75, 700), P(180, 690), P(300, 760), P(400, 880), P(100, 890));
            CreateImagePolygonZone("Land_BottomRightSand", zones, AquariumSurfaceType.Land, 100, 1f,
                P(1190, 700), P(1300, 650), P(1370, 730), P(1360, 890), P(1130, 890), P(1080, 820));
            CreateImagePolygonZone("Land_LeftRockShelf", zones, AquariumSurfaceType.Land, 100, 1f,
                P(65, 245), P(140, 225), P(165, 330), P(125, 435), P(70, 445));
            CreateImagePolygonZone("Land_LeftLowerRocks", zones, AquariumSurfaceType.Land, 100, 1f,
                P(65, 420), P(125, 405), P(155, 500), P(130, 650), P(80, 690));
        }

        private static void CreateImagePolygonZone(
            string name,
            Transform parent,
            AquariumSurfaceType surface,
            int priority,
            float speedMultiplier,
            params Vector2[] worldPoints)
        {
            GameObject zoneObject = new(name, typeof(PolygonCollider2D), typeof(AquariumSurfaceZone));
            zoneObject.transform.SetParent(parent, false);
            PolygonCollider2D polygon = zoneObject.GetComponent<PolygonCollider2D>();
            polygon.points = worldPoints;
            polygon.isTrigger = true;
            zoneObject.GetComponent<AquariumSurfaceZone>().Configure(surface, priority, speedMultiplier);
        }

        private static Vector2 P(float imageX, float imageY)
        {
            return ImagePixelToWorld(new Vector2(imageX, imageY));
        }

        public static Vector2 ImagePixelToWorld(Vector2 pixel)
        {
            return new Vector2(
                (pixel.x / BackgroundImageSize.x - 0.5f) * BackgroundWorldSize.x,
                (0.5f - pixel.y / BackgroundImageSize.y) * BackgroundWorldSize.y);
        }

        private static void CreateEllipseZone(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 radius,
            AquariumSurfaceType surface,
            int priority,
            float speedMultiplier)
        {
            GameObject zoneObject = new(name, typeof(PolygonCollider2D), typeof(AquariumSurfaceZone));
            zoneObject.transform.SetParent(parent, false);
            zoneObject.transform.localPosition = position;
            PolygonCollider2D polygon = zoneObject.GetComponent<PolygonCollider2D>();
            Vector2[] points = new Vector2[32];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = Mathf.PI * 2f * i / points.Length;
                points[i] = new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
            }
            polygon.points = points;
            polygon.isTrigger = true;
            zoneObject.GetComponent<AquariumSurfaceZone>().Configure(surface, priority, speedMultiplier);
        }

        private static void CreateBounds(Transform root)
        {
            GameObject boundary = new("AquariumInnerWallBoundary", typeof(EdgeCollider2D));
            boundary.transform.SetParent(root, false);
            EdgeCollider2D edge = boundary.GetComponent<EdgeCollider2D>();
            edge.edgeRadius = 0.06f;
            edge.points = new[]
            {
                P(115, 145), P(150, 100), P(1280, 100), P(1340, 155),
                P(1360, 830), P(1300, 890), P(145, 890), P(85, 830), P(115, 145)
            };
        }

        private static OtterMovementController CreateOtter(Transform parent, Sprite sprite, Material waterParticleMaterial)
        {
            GameObject otterObject = new("PlayerSeaOtter", typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(OtterSurfaceSensor), typeof(OtterMovementController));
            otterObject.transform.SetParent(parent, false);
            otterObject.transform.localPosition = ImagePixelToWorld(new Vector2(720f, 650f));

            Rigidbody2D body = otterObject.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CapsuleCollider2D capsule = otterObject.GetComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(0.78f, 1.35f);

            OtterMovementController movement = otterObject.GetComponent<OtterMovementController>();
            movement.Configure(6.8f, 4.1f, 9.4f, 7.2f);

            Transform visualRoot = CreateEmpty("VisualRoot", otterObject.transform);
            Transform bodyRoot = CreateEmpty("BodyRoot (Replace With Art)", visualRoot);
            List<SpriteRenderer> renderers = new();
            SpriteRenderer shadow = CreateWorldSprite("Shadow", visualRoot, sprite, new Color(0.04f, 0.08f, 0.08f, 0.25f), new Vector3(0f, -0.64f, 0.3f), new Vector2(1.15f, 0.36f), 190);
            renderers.Add(shadow);

            renderers.Add(CreateWorldSprite("Tail", bodyRoot, sprite, OtterBrown, new Vector3(0f, -0.88f, 0.15f), new Vector2(0.45f, 0.95f), 200));
            renderers[^1].transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            renderers.Add(CreateWorldSprite("Body", bodyRoot, sprite, OtterBrown, new Vector3(0f, -0.18f, 0f), new Vector2(1.04f, 1.55f), 201));
            renderers.Add(CreateWorldSprite("Belly", bodyRoot, sprite, OtterLight, new Vector3(0f, -0.12f, -0.1f), new Vector2(0.64f, 0.94f), 202));
            renderers.Add(CreateWorldSprite("Head", bodyRoot, sprite, OtterBrown, new Vector3(0f, 0.72f, -0.2f), new Vector2(1.08f, 0.9f), 203));
            renderers.Add(CreateWorldSprite("LeftEar", bodyRoot, sprite, OtterLight, new Vector3(-0.42f, 0.98f, -0.1f), new Vector2(0.3f, 0.3f), 202));
            renderers.Add(CreateWorldSprite("RightEar", bodyRoot, sprite, OtterLight, new Vector3(0.42f, 0.98f, -0.1f), new Vector2(0.3f, 0.3f), 202));
            renderers.Add(CreateWorldSprite("Muzzle", bodyRoot, sprite, new Color(0.88f, 0.72f, 0.5f, 1f), new Vector3(0f, 0.57f, -0.3f), new Vector2(0.62f, 0.38f), 204));
            renderers.Add(CreateWorldSprite("LeftEye", bodyRoot, sprite, new Color(0.03f, 0.025f, 0.02f, 1f), new Vector3(-0.24f, 0.81f, -0.4f), new Vector2(0.12f, 0.14f), 205));
            renderers.Add(CreateWorldSprite("RightEye", bodyRoot, sprite, new Color(0.03f, 0.025f, 0.02f, 1f), new Vector3(0.24f, 0.81f, -0.4f), new Vector2(0.12f, 0.14f), 205));
            renderers.Add(CreateWorldSprite("Nose", bodyRoot, sprite, new Color(0.08f, 0.045f, 0.025f, 1f), new Vector3(0f, 0.64f, -0.5f), new Vector2(0.19f, 0.14f), 206));

            OtterVisualPresenter presenter = otterObject.AddComponent<OtterVisualPresenter>();
            presenter.Configure(movement, visualRoot, bodyRoot, shadow, renderers.ToArray());

            ParticleSystem trail = CreateParticleSystem("SwimTrail", otterObject.transform, new Vector3(0f, -0.62f, 0f), new Color(0.72f, 1f, 1f, 0.72f), 0.24f, 0.65f, 1.8f, true, 198, waterParticleMaterial);
            ParticleSystem entry = CreateParticleSystem("EntrySplash", otterObject.transform, Vector3.zero, new Color(0.72f, 1f, 1f, 0.9f), 0.34f, 0.75f, 3.6f, false, 250, waterParticleMaterial);
            ParticleSystem exit = CreateParticleSystem("ExitDrops", otterObject.transform, Vector3.zero, new Color(0.62f, 0.94f, 1f, 0.82f), 0.18f, 0.65f, 2.6f, false, 250, waterParticleMaterial);
            ParticleSystem slide = CreateParticleSystem("SlideSpray", otterObject.transform, new Vector3(0f, -0.52f, 0f), new Color(0.72f, 0.94f, 0.9f, 0.72f), 0.14f, 0.45f, 1.4f, true, 199, waterParticleMaterial);
            ParticleSystem turn = CreateParticleSystem("TurnSplash", otterObject.transform, Vector3.zero, new Color(0.86f, 1f, 1f, 0.82f), 0.22f, 0.5f, 2.3f, false, 245, waterParticleMaterial);

            OtterVfxController vfx = otterObject.AddComponent<OtterVfxController>();
            vfx.Configure(movement, otterObject.GetComponent<OtterSurfaceSensor>(), trail, entry, exit, slide, turn);
            return movement;
        }

        private static ParticleSystem CreateParticleSystem(
            string name,
            Transform parent,
            Vector3 position,
            Color color,
            float size,
            float lifetime,
            float speed,
            bool loop,
            int sortingOrder,
            Material particleMaterial)
        {
            GameObject particleObject = new(name, typeof(ParticleSystem));
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = position;
            particleObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem particles = particleObject.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = loop;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = color;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size * 1.25f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime * 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed * 1.25f);
            main.maxParticles = 120;
            main.gravityModifier = 0.28f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 0.22f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1.15f));

            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = particleMaterial;
            return particles;
        }

        private static Material EnsureWaterParticleMaterial()
        {
            EnsureFolders();

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterParticleTexturePath);
            if (texture == null)
            {
                const int size = 32;
                texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "T_WaterParticle",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                Color[] pixels = new Color[size * size];
                Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
                float radius = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                        float alpha = 1f - Mathf.SmoothStep(0.58f, 0.98f, distance);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply(false, false);
                AssetDatabase.CreateAsset(texture, WaterParticleTexturePath);
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("[OtterAquarium] No compatible transparent particle shader was found.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(WaterParticleMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_WaterParticles" };
                AssetDatabase.CreateAsset(material, WaterParticleMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.color = Color.white;
            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(texture);
            return material;
        }

        private static Sprite LoadBackgroundSprite()
        {
            return AssetDatabase.LoadAllAssetsAtPath(BackgroundSpritePath)
                .OfType<Sprite>()
                .FirstOrDefault();
        }

        private static bool SceneUsesZooBackground(Sprite expectedSprite)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            bool hasExpectedBackground = false;
            bool hasImageMatchedZones = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.name == "ZooBackground" && renderer.sprite == expectedSprite)
                        hasExpectedBackground = true;
                }

                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == SurfaceZoneLayoutName)
                        hasImageMatchedZones = true;
                }
            }

            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);
            return hasExpectedBackground && hasImageMatchedZones;
        }

        private static bool SceneUsesWaterParticleMaterial(Material expectedMaterial)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            int matched = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (ParticleSystemRenderer renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    if (renderer.sharedMaterial == expectedMaterial)
                        matched++;
                }
            }

            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);
            return matched >= 5;
        }

        private static void CreateCamera(Transform parent, Transform target)
        {
            GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener), typeof(OtterCameraFollow));
            cameraObject.transform.SetParent(parent, false);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0.5f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.75f, 0.82f, 0.66f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5.65f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            cameraObject.GetComponent<OtterCameraFollow>().Configure(target, new Vector2(-1.1f, -2.55f), new Vector2(1.1f, 2.55f));
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
            EnsureFolder("Assets", "OtterAquariumPrototype");
            EnsureFolder("Assets/OtterAquariumPrototype", "Scenes");
            EnsureFolder("Assets/OtterAquariumPrototype", "Scripts");
            EnsureFolder("Assets/OtterAquariumPrototype", "Editor");
            EnsureFolder("Assets/OtterAquariumPrototype", "Prefabs");
            EnsureFolder("Assets/OtterAquariumPrototype", "Materials");
            EnsureFolder("Assets/OtterAquariumPrototype", "VFX");
            EnsureFolder("Assets/OtterAquariumPrototype", "Arts");
            EnsureFolder("Assets/OtterAquariumPrototype/Arts", "Background");
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
