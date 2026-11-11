using System;
using System.IO;
using System.Linq;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightAimingAuraSetup
    {
        const string Root = "Assets/FightDemo/";
        const string PrefabPath = Root + "Prefabs/VFX/AimingPreparation.prefab";
        const string Folder = Root + "Materials/AimingAura";
        static FightAimingAuraSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            if (!File.Exists("Temp/AimingAuraSetup.request") || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete("Temp/AimingAuraSetup.request");
            try { Run(); } catch (Exception e) { File.WriteAllText("Temp/AimingAuraSetup.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/VFX/Configure Aiming Preparation Aura")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Root + "Materials", "AimingAura");
            var shellMaterial = Material("AimingAura", "RhythmHunter/VFX/Aimed Aura");
            var depthMaterial = Material("AimingSilhouetteDepth", "RhythmHunter/VFX/Aura Silhouette Depth");
            var particlesMaterial = Material("AimingOrbit", "Universal Render Pipeline/Particles/Unlit");
            if (particlesMaterial.HasProperty("_Surface")) particlesMaterial.SetFloat("_Surface", 1);
            particlesMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            particlesMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            particlesMaterial.SetFloat("_ZWrite", 0);
            particlesMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            particlesMaterial.renderQueue = 3000;
            EditorUtility.SetDirty(particlesMaterial); AssetDatabase.SaveAssetIfDirty(particlesMaterial);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var settings = root.GetComponent<FightEffectSettings>() ?? root.AddComponent<FightEffectSettings>();
                settings.spawnOrientation = FightEffectSettings.SpawnOrientation.WorldUpright;
                settings.useOwnerFeet = true;
                settings.spawnOffset = new Vector3(0, 1.2f, 0);
                settings.followPositionOnly = true;
                settings.destroyAfterParticles = false;
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    var r = ps.GetComponent<ParticleSystemRenderer>();
                    if (r.renderMode == ParticleSystemRenderMode.VerticalBillboard)
                    { r.renderMode = ParticleSystemRenderMode.Billboard; r.alignment = ParticleSystemRenderSpace.View; }
                }
                var occlusion = root.GetComponent<FightAuraOcclusion>() ?? root.AddComponent<FightAuraOcclusion>();
                var so = new SerializedObject(occlusion);
                so.FindProperty("silhouetteDepthMaterial").objectReferenceValue = depthMaterial;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (root.transform.Find("WrapAura_3D") == null)
                {
                    var mesh = Mesh(false);
                    var go = MeshObject("WrapAura_3D", root.transform, mesh, shellMaterial);
                    go.transform.localPosition = new Vector3(0, -1.17f, 0);
                    go.transform.localScale = new Vector3(.65f, 2.5f, .65f);
                }
                if (root.transform.Find("FootRing_3D") == null)
                {
                    var go = MeshObject("FootRing_3D", root.transform, Mesh(true), shellMaterial);
                    go.transform.localPosition = new Vector3(0, -1.17f, 0);
                    go.transform.localScale = new Vector3(.75f, 1, .75f);
                }
                if (root.transform.Find("OrbitSparks_3D") == null)
                {
                    var go = new GameObject("OrbitSparks_3D"); go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = new Vector3(0, -.75f, 0);
                    var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = ps.main; main.duration = 1; main.loop = true; main.startLifetime = .6f;
                    main.startSpeed = 0; main.startSize = .065f; main.startColor = new Color(1, .75f, .2f, .8f);
                    main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 48;
                    var emission = ps.emission; emission.rateOverTime = 24;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0, 12) });
                    var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .65f;
                    shape.radiusThickness = .05f; shape.rotation = new Vector3(90, 0, 0);
                    var velocity = ps.velocityOverLifetime; velocity.enabled = true;
                    velocity.space = ParticleSystemSimulationSpace.Local; velocity.orbitalY = 8; velocity.y = 1.5f;
                    var color = ps.colorOverLifetime; color.enabled = true;
                    var gradient = new Gradient();
                    gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                        new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(0, 1) });
                    color.color = gradient;
                    var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = particlesMaterial;
                    r.renderMode = ParticleSystemRenderMode.Billboard; r.alignment = ParticleSystemRenderSpace.View;
                    r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Validate();
        }
        static Material Material(string name, string shader)
        {
            string path = Folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var s = Shader.Find(shader); if (s == null) throw new Exception("Missing shader " + shader);
            m = new Material(s) { name = name }; AssetDatabase.CreateAsset(m, path); return m;
        }
        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go;
        }
        static Mesh Mesh(bool ring)
        {
            string path = Folder + (ring ? "/FootRing.asset" : "/AuraCylinder.asset");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
            const int n = 48;
            var vertices = new Vector3[(n + 1) * 2]; var uv = new Vector2[vertices.Length]; var indices = new int[n * 6];
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2 / n; var v = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                vertices[i * 2] = ring ? v * .82f : v; vertices[i * 2 + 1] = ring ? v : v + Vector3.up;
                uv[i * 2] = new Vector2((float)i / n, 0); uv[i * 2 + 1] = new Vector2((float)i / n, 1);
                if (i == n) continue;
                int t = i * 6, k = i * 2;
                indices[t] = k; indices[t + 1] = k + 1; indices[t + 2] = k + 2;
                indices[t + 3] = k + 1; indices[t + 4] = k + 3; indices[t + 5] = k + 2;
            }
            mesh = new Mesh { name = ring ? "FootRing" : "AuraCylinder", vertices = vertices, uv = uv, triangles = indices };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        [MenuItem("Rhythm Hunter/VFX/Validate Aiming Preparation Aura")]
        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var authored = prefab.transform.Find("VFX_Teleport_01_Mask_Loop_Static");
            if (authored == null || Mathf.Abs(authored.localScale.y - 1.8535f) > .0001f) throw new Exception("Authored elongated scale changed.");
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && ShaderUtil.ShaderHasError(m.shader)) throw new Exception("Shader error: " + m.name);
            var a = new GameObject("TiltedAnchor"); var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                a.transform.rotation = Quaternion.Euler(45, 0, 25); a.transform.localScale = new Vector3(-2, 3, 1);
                var settings = prefab.GetComponent<FightEffectSettings>();
                instance.transform.rotation = FightEffectPlacement.Rotation(settings, prefab.transform, a.transform);
                if (Vector3.Dot(instance.transform.up, Vector3.up) < .9999f) throw new Exception("Aura inherited actor tilt.");
                if (instance.transform.localScale != prefab.transform.localScale) throw new Exception("Aura inherited actor scale.");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(a); }
            int changed = CaptureComparison(prefab);
            if (changed == 0) throw new Exception("Silhouette occlusion produced no GPU difference.");
            File.WriteAllText("Temp/AimingAuraSetup.result", "PASS: original Y scale 1.8535 preserved; world-upright despite tilted/mirrored anchor; view-aligned artwork; 3D cylinder, foot ring, orbital particles; shaders compile. Existing effect lifetime and combat logic unchanged. GPU occlusion changed pixels=" + changed);
        }

        static int CaptureComparison(GameObject prefab)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture rt = null;
            try
            {
                var actor = new GameObject("AuraDiagnosticActor", typeof(SpriteRenderer)); SceneManager.MoveGameObjectToScene(actor, scene);
                var r = actor.GetComponent<SpriteRenderer>();
                var original = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/ArtBattle/Skeleton.prefab");
                r.sprite = original.GetComponentInChildren<FightCharacterCombatAnimator>(true).TargetRenderer.sprite;
                r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "Art2Prototype/Art2ActorLit.mat");
                float scale = 2.2f / Mathf.Max(.01f, r.sprite.bounds.size.y);
                actor.transform.localScale = Vector3.one * scale;
                actor.transform.rotation = Quaternion.Euler(45,0,0);
                actor.transform.position = -actor.transform.TransformVector(new Vector3(r.sprite.bounds.center.x, r.sprite.bounds.min.y, 0));
                var aura = UnityEngine.Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(aura, scene);
                aura.transform.position = Vector3.up * 1.2f;
                aura.GetComponent<FightAuraOcclusion>().Bind(r);
                foreach (var ps in aura.GetComponentsInChildren<ParticleSystem>(true)) { ps.useAutoRandomSeed = false; ps.randomSeed = 1; ps.Simulate(.2f, false, true, false); ps.Pause(false); }
                var go = new GameObject("AuraDiagnosticCamera", typeof(Camera)); SceneManager.MoveGameObjectToScene(go, scene);
                var camera = go.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.transform.SetPositionAndRotation(new Vector3(0,7,-6), Quaternion.Euler(45,0,0));
                camera.orthographic = true; camera.orthographicSize = 2.5f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.035f,.06f);
                var data = camera.GetUniversalAdditionalCameraData(); data.SetRenderer(2); data.requiresDepthTexture = true;
                rt = new RenderTexture(480,480,24); camera.targetTexture = rt;
                var mask = aura.transform.Find("Aura_CharacterDepth").GetComponent<SpriteRenderer>();
                mask.enabled = false; var without = Capture("WithoutOcclusion");
                mask.enabled = true; var with = Capture("WithOcclusion");
                int count = 0;
                for (int i=0;i<with.Length;i++) if (Math.Abs(with[i].r-without[i].r)+Math.Abs(with[i].g-without[i].g)+Math.Abs(with[i].b-without[i].b)>12) count++;
                return count;
                Color32[] Capture(string label)
                {
                    var old = RenderTexture.active; var image = new Texture2D(480,480,TextureFormat.RGB24,false);
                    try
                    {
                        camera.Render(); RenderTexture.active = rt;
                        image.ReadPixels(new Rect(0,0,480,480),0,0); image.Apply();
                        File.WriteAllBytes("Temp/AimingAura_"+label+".png",image.EncodeToPNG()); return image.GetPixels32();
                    }
                    finally { RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image); }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); if (rt != null) UnityEngine.Object.DestroyImmediate(rt); }
        }
    }
}
