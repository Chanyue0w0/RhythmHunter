using System.Collections.Generic;
using System.Linq;
using RhythmHunter.RhythmArena;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.RhythmArenaEditor
{
    public static class RhythmArenaPrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/Prototype/RhythmArena/Scenes/RhythmArenaPrototype.unity";
        private const string MaterialPath = "Assets/Prototype/RhythmArena/Materials/RhythmArenaPixel.mat";
        private const float RingRadius = 3.6f;
        private const int RingSegmentCount = 64;

        private static readonly Color White = Color.white;
        private static readonly Color DimWhite = new(0.48f, 0.48f, 0.48f, 1f);
        private static readonly Color DarkGray = new(0.12f, 0.12f, 0.12f, 1f);

        [MenuItem("Rhythm Hunter/Build Rhythm Arena Prototype")]
        public static void BuildScene()
        {
            EnsureFolders();
            Material pixelMaterial = GetOrCreatePixelMaterial();

            Scene previousScene = SceneManager.GetActiveScene();
            NewSceneMode mode = Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            scene.name = "RhythmArenaPrototype";
            SceneManager.SetActiveScene(scene);

            GameObject root = new("RhythmArenaPrototype");
            CreateCamera(root.transform);

            GameObject rhythmSystem = CreateChild("RhythmSystem", root.transform);
            GameObject clockObject = CreateChild("RhythmClock", rhythmSystem.transform);
            FmodBeatClock fmodClock = clockObject.AddComponent<FmodBeatClock>();
            fmodClock.Configure("event:/Combat soundtracks/Combat 01", 0.25f, true, 0.55f);
            RhythmClock rhythmClock = clockObject.AddComponent<RhythmClock>();
            rhythmClock.Configure(fmodClock, 100f, 4, 0.10f, 0.25f);

            GameObject resolverObject = CreateChild("CombatResolver", rhythmSystem.transform);
            CombatResolver resolver = resolverObject.AddComponent<CombatResolver>();

            GameObject arenaObject = CreateChild("Arena", root.transform);
            RhythmArenaRing ring = arenaObject.AddComponent<RhythmArenaRing>();
            Renderer[] ringSegments = CreateRingSegments(arenaObject.transform, pixelMaterial);
            Transform[] beatPoints = CreateBeatPoints(arenaObject.transform, pixelMaterial);
            Transform cursor = CreateCursor(arenaObject.transform, pixelMaterial);
            CreateBeatLabels(arenaObject.transform);

            GameObject heroObject = CreateChild("Hero", root.transform);
            heroObject.transform.localPosition = new Vector3(1.7f, 0f, -0.25f);
            Transform heroVisual = CreateHeroVisual(heroObject.transform, pixelMaterial);
            PlayerCombatController player = heroObject.AddComponent<PlayerCombatController>();
            GameObject shield = CreateQuad(
                "GuardShield",
                heroObject.transform,
                pixelMaterial,
                new Vector3(-0.58f, 0f, -0.15f),
                new Vector3(0.14f, 0.85f, 1f),
                White);
            shield.SetActive(false);
            Transform heroHpFill = CreateHpBar("HeroHP", heroObject.transform, pixelMaterial, new Vector3(0f, 0.92f, 0f));

            GameObject enemyObject = CreateChild("Enemy", root.transform);
            enemyObject.transform.localPosition = new Vector3(-1.7f, 0f, -0.25f);
            Transform enemyVisual = CreateEnemyVisual(enemyObject.transform, pixelMaterial);
            EnemyPatternController enemyPattern = enemyObject.AddComponent<EnemyPatternController>();
            Transform enemyHpFill = CreateHpBar("EnemyHP", enemyObject.transform, pixelMaterial, new Vector3(0f, 0.92f, 0f));

            TextMesh title = CreateWorldText(
                "Title",
                root.transform,
                "RHYTHM ARENA  //  THE BEATBOUND HERO",
                new Vector3(0f, 5.05f, -0.5f),
                0.095f,
                White);
            title.fontStyle = FontStyle.Bold;

            TextMesh rhythmReadout = CreateWorldText(
                "RhythmReadout",
                root.transform,
                "100 BPM   WAITING FOR FMOD",
                new Vector3(0f, 4.58f, -0.5f),
                0.072f,
                DimWhite);

            TextMesh status = CreateWorldText(
                "CombatStatus",
                root.transform,
                "READ THE RING. RED IS DANGER.",
                new Vector3(0f, -4.55f, -0.5f),
                0.09f,
                White);
            status.fontStyle = FontStyle.Bold;

            CreateWorldText(
                "Controls",
                root.transform,
                "J / X  QUICK [1]     K / Y  HEAVY [2]     L / B  BREAK [1.5]     SPACE / A  GUARD [0.5]",
                new Vector3(0f, -5.05f, -0.5f),
                0.058f,
                DimWhite);

            Renderer[] heroRenderers = heroVisual.GetComponentsInChildren<Renderer>(true);
            Renderer[] enemyRenderers = enemyVisual.GetComponentsInChildren<Renderer>(true);

            player.Configure(rhythmClock, resolver, enemyPattern, heroVisual, shield);
            enemyPattern.Configure(
                rhythmClock,
                resolver,
                new[] { 2f, 3.5f, 5.25f, 7f, 9.5f, 11.75f },
                12f,
                1);
            resolver.Configure(
                rhythmClock,
                player,
                enemyPattern,
                enemyVisual,
                heroRenderers,
                enemyRenderers,
                heroHpFill,
                enemyHpFill,
                status,
                rhythmReadout);
            ring.Configure(rhythmClock, player, enemyPattern, RingRadius, ringSegments, cursor, beatPoints);

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
            Debug.Log($"[RhythmArenaPrototype] Scene created: {ScenePath}");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Prototype");
            EnsureFolder("Assets/Prototype", "RhythmArena");
            EnsureFolder("Assets/Prototype/RhythmArena", "Scenes");
            EnsureFolder("Assets/Prototype/RhythmArena", "Scripts");
            EnsureFolder("Assets/Prototype/RhythmArena", "Editor");
            EnsureFolder("Assets/Prototype/RhythmArena", "Materials");
            EnsureFolder("Assets/Prototype/RhythmArena", "Sprites");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static Material GetOrCreatePixelMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            material = new Material(shader)
            {
                name = "RhythmArenaPixel",
                color = White
            };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", White);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void CreateCamera(Transform parent)
        {
            GameObject cameraObject = new(
                "Main Camera",
                typeof(Camera),
                typeof(AudioListener),
                typeof(FMODUnity.StudioListener));
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 5.7f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
        }

        private static Renderer[] CreateRingSegments(Transform parent, Material material)
        {
            GameObject ringRoot = CreateChild("Ring", parent);
            Renderer[] renderers = new Renderer[RingSegmentCount];
            for (int i = 0; i < RingSegmentCount; i++)
            {
                float phase = i * 4f / RingSegmentCount;
                Vector3 position = RhythmArenaRing.PhaseToLocalPosition(phase, RingRadius);
                GameObject segment = CreateQuad(
                    $"PixelSegment_{i:00}",
                    ringRoot.transform,
                    material,
                    position,
                    new Vector3(0.17f, 0.075f, 1f),
                    White);
                segment.transform.localRotation = Quaternion.Euler(0f, 0f, -phase * 90f);
                renderers[i] = segment.GetComponent<Renderer>();
            }

            return renderers;
        }

        private static Transform[] CreateBeatPoints(Transform parent, Material material)
        {
            Transform[] points = new Transform[4];
            for (int i = 0; i < points.Length; i++)
            {
                GameObject point = CreateQuad(
                    $"BeatPoint_{i + 1}",
                    parent,
                    material,
                    RhythmArenaRing.PhaseToLocalPosition(i, RingRadius),
                    new Vector3(0.27f, 0.27f, 1f),
                    White);
                point.transform.localPosition += new Vector3(0f, 0f, -0.05f);
                point.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                points[i] = point.transform;
            }
            return points;
        }

        private static Transform CreateCursor(Transform parent, Material material)
        {
            GameObject cursor = CreateQuad(
                "CurrentBeatCursor",
                parent,
                material,
                RhythmArenaRing.PhaseToLocalPosition(0f, RingRadius),
                Vector3.one * 0.25f,
                White);
            cursor.transform.localPosition += new Vector3(0f, 0f, -0.18f);
            cursor.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return cursor.transform;
        }

        private static void CreateBeatLabels(Transform parent)
        {
            Vector3[] positions =
            {
                new(0f, 4.08f, -0.4f),
                new(4.08f, 0f, -0.4f),
                new(0f, -4.08f, -0.4f),
                new(-4.08f, 0f, -0.4f)
            };

            for (int i = 0; i < positions.Length; i++)
                CreateWorldText($"BeatLabel_{i + 1}", parent, (i + 1).ToString(), positions[i], 0.11f, White);
        }

        private static Transform CreateHeroVisual(Transform parent, Material material)
        {
            GameObject visual = CreateChild("HeroVisual", parent);
            CreateQuad("Head", visual.transform, material, new Vector3(0f, 0.3f, 0f), new Vector3(0.34f, 0.34f, 1f), White);
            CreateQuad("Body", visual.transform, material, new Vector3(0f, -0.08f, 0f), new Vector3(0.28f, 0.48f, 1f), White);
            CreateQuad("Sword", visual.transform, material, new Vector3(-0.34f, -0.02f, 0f), new Vector3(0.38f, 0.08f, 1f), White);
            CreateQuad("Feet", visual.transform, material, new Vector3(0f, -0.43f, 0f), new Vector3(0.52f, 0.12f, 1f), White);
            return visual.transform;
        }

        private static Transform CreateEnemyVisual(Transform parent, Material material)
        {
            GameObject visual = CreateChild("EnemyVisual", parent);
            CreateQuad("Head", visual.transform, material, new Vector3(0f, 0.3f, 0f), new Vector3(0.48f, 0.38f, 1f), White);
            CreateQuad("HornTop", visual.transform, material, new Vector3(-0.18f, 0.56f, 0f), new Vector3(0.12f, 0.22f, 1f), White);
            CreateQuad("HornBottom", visual.transform, material, new Vector3(-0.18f, 0.04f, 0f), new Vector3(0.12f, 0.18f, 1f), White);
            CreateQuad("Body", visual.transform, material, new Vector3(0f, -0.13f, 0f), new Vector3(0.42f, 0.5f, 1f), White);
            CreateQuad("Feet", visual.transform, material, new Vector3(0f, -0.47f, 0f), new Vector3(0.62f, 0.12f, 1f), White);
            return visual.transform;
        }

        private static Transform CreateHpBar(string name, Transform parent, Material material, Vector3 position)
        {
            GameObject bar = CreateChild(name, parent);
            bar.transform.localPosition = position;
            CreateQuad("Background", bar.transform, material, Vector3.zero, new Vector3(1.12f, 0.13f, 1f), DarkGray);
            GameObject fill = CreateQuad("Fill", bar.transform, material, new Vector3(0f, 0f, -0.03f), new Vector3(1f, 0.07f, 1f), White);
            return fill.transform;
        }

        private static GameObject CreateQuad(
            string name,
            Transform parent,
            Material material,
            Vector3 localPosition,
            Vector3 localScale,
            Color color)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPosition;
            quad.transform.localScale = localScale;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = material;

            MaterialPropertyBlock block = new();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
            return quad;
        }

        private static TextMesh CreateWorldText(
            string name,
            Transform parent,
            string content,
            Vector3 localPosition,
            float characterSize,
            Color color)
        {
            GameObject textObject = CreateChild(name, parent);
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

        private static GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        public static void ApplyBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(scene => scene.path != ScenePath)
                .Select(scene => new EditorBuildSettingsScene(scene.path, false))
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
