using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemo.Editor
{
    public static class Art2CavePrototypeValidation
    {
        const string ScenePath = "Assets/FightDemo/Scenes/Art2_FightScene.unity";
        [MenuItem("Rhythm Hunter/Art2/Validate Cave Layout")]
        public static void Validate()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Wrong validation scene.");
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras.Length != 1 || cameras[0].name != "Art2_TestCamera")
                throw new InvalidOperationException("Exactly one prototype camera must be active.");
            if (UnityEngine.Object.FindObjectsByType<FightCombatController>(FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("Exactly one battle controller must be active.");
            var environment = GameObject.Find("CaveEnvironment_2_5D");
            if (!environment) throw new InvalidOperationException("2.5D environment must be active.");
            var renderers = environment.GetComponentsInChildren<SpriteRenderer>();
            if (renderers.Length != 15 || renderers.Any(r => !r.sprite || !r.sharedMaterial))
                throw new InvalidOperationException("Expected fifteen cave cards, including four independent wood/rock sprites and four torch bodies.");
            var torches = renderers.Where(r => r.name.StartsWith("WallTorch_")).ToArray();
            if (torches.Length != 4 || torches.Count(r => AssetDatabase.GetAssetPath(r.sprite).EndsWith("/cave_00_left(no fire).png")) != 2 ||
                torches.Count(r => AssetDatabase.GetAssetPath(r.sprite).EndsWith("/cave_00_right(no fire).png")) != 2 ||
                torches.Any(r => r.sharedMaterial.GetFloat("_EmissionStrength") != 0))
                throw new InvalidOperationException("Expected two left and two right non-emissive torch bodies.");
            if (UnityEngine.Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None).Length != 6)
                throw new InvalidOperationException("Expected six active battle slots.");
            if (GameObject.Find("CaveEnvironment") || GameObject.Find("ForestEnvironment"))
                throw new InvalidOperationException("Original flat backgrounds must remain inactive.");
            if (UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("Expected one active audio listener.");
            Debug.Log("Art2 validation passed: one camera/listener, fifteen cave cards, six battle slots and active combat.");
        }

        [MenuItem("Rhythm Hunter/Art2/Validate Atmosphere")]
        public static void ValidateAtmosphere()
        {
            Validate();
            var camera = GameObject.Find("Art2_TestCamera").GetComponent<Camera>();
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            var rendererSetting = new SerializedObject(data).FindProperty("m_RendererIndex");
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            int rendererIndex = rendererSetting.intValue;
            if (pipeline == null || rendererIndex < 0 || rendererIndex >= pipeline.rendererDataList.Length ||
                pipeline.rendererDataList[rendererIndex].name != "Art2ForwardPlusRenderer")
                throw new InvalidOperationException("Art2 requires its dedicated Forward+ renderer for all torch lights.");
            var volume = GameObject.Find("Art2_PostProcessing").GetComponent<Volume>();
            if (!camera.allowHDR || !data.renderPostProcessing || volume.sharedProfile == null ||
                (data.volumeLayerMask.value & (1 << volume.gameObject.layer)) == 0)
                throw new InvalidOperationException("Art2 camera/Volume configuration is incomplete.");
            if (!volume.sharedProfile.TryGet<Bloom>(out var bloom) || bloom.intensity.value > .3f ||
                !volume.sharedProfile.TryGet<ColorAdjustments>(out _) ||
                !volume.sharedProfile.TryGet<Tonemapping>(out _))
                throw new InvalidOperationException("Expected restrained Bloom, color grading and tonemapping.");
            var rig = GameObject.Find("Art2_Atmosphere");
            var lights = rig.GetComponentsInChildren<Light>();
            if (lights.Length != 6 || lights.Any(light => light.shadows != LightShadows.None))
                throw new InvalidOperationException("Expected six shadowless atmosphere lights, including four torches.");
            if (rig.GetComponentsInChildren<Art2.Art2TorchFlicker>().Length != 4)
                throw new InvalidOperationException("Expected four independent torch flickers.");
            const string folder = "Assets/FightDemo/Art2Prototype/";
            foreach (var name in new[] { "Art2ActorLit", "CaveMiddleLit", "CaveDistantLit", "CaveTorchLit", "Art2ContactShadow", "CaveGround", "CaveRockLeftLit", "CaveRockRightLit", "CaveWoodLeftLit", "CaveWoodRightLit" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(folder + name + ".mat");
                if (!material || !material.shader) throw new InvalidOperationException("Missing material/shader: " + name);
                var messages = ShaderUtil.GetShaderMessages(material.shader);
                if (messages.Any(message => message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error))
                    throw new InvalidOperationException("Shader import errors: " + name);
            }
            var actor = AssetDatabase.LoadAssetAtPath<Material>(folder + "Art2ActorLit.mat");
            var distant = AssetDatabase.LoadAssetAtPath<Material>(folder + "CaveDistantLit.mat");
            if (actor.GetFloat("_BlurRadius") != 0 || distant.GetFloat("_BlurRadius") <= 0)
                throw new InvalidOperationException("Only distant scenery should use the blur material.");
            Debug.Log("Art2 atmosphere configuration and currently imported shader messages validated. Play Mode visual/performance review is still required.");
        }

        [MenuItem("Rhythm Hunter/Art2/Validate Scenery Surface Lighting")]
        public static void ValidateSurfaceLighting()
        {
            ValidateAtmosphere();
            const string folder = "Assets/FightDemo/Art2Prototype/";
            foreach (var (kind, width, height) in new[] { ("RockLeft", 464, 192), ("RockRight", 296, 144),
                ("WoodLeft", 288, 136), ("WoodRight", 320, 288) })
            {
                var path = folder + "Cave" + kind + "Normal.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "Cave" + kind + "Lit.mat");
                var texture = material.GetTexture("_NormalMap");
                if (importer == null || importer.textureType != TextureImporterType.NormalMap ||
                    importer.sRGBTexture || importer.mipmapEnabled || importer.filterMode != FilterMode.Point ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed ||
                    texture == null || texture.width != width || texture.height != height ||
                    material.GetFloat("_UseNormalMap") != 1)
                    throw new InvalidOperationException("Invalid cropped sprite normal map setup: " + kind);
            }
            var scenery = GameObject.Find("CaveEnvironment_2_5D");
            if (scenery.GetComponentsInChildren<Art2.Art2SceneryContactShadows>().Length != 4)
                throw new InvalidOperationException("Expected four independent rock/wood contact-shadow components.");
            foreach (var r in scenery.GetComponentsInChildren<SpriteRenderer>().Where(r =>
                r.name.StartsWith("Rock_") || r.name.StartsWith("WoodenBarricade_")))
                if (r.sprite.texture.width != r.sharedMaterial.GetTexture("_NormalMap").width ||
                    r.sprite.texture.height != r.sharedMaterial.GetTexture("_NormalMap").height ||
                    r.shadowCastingMode != ShadowCastingMode.TwoSided || !r.receiveShadows)
                    throw new InvalidOperationException("Split sprite lighting mismatch: " + r.name);
            Debug.Log("Scenery normal textures/materials and contact-shadow components validated; visual comparison still required.");
        }

        [MenuItem("Rhythm Hunter/Art2/Validate Sprite Shadows")]
        public static void ValidateSpriteShadows()
        {
            ValidateAtmosphere();
            var environment = GameObject.Find("CaveEnvironment_2_5D");
            var casters = environment.GetComponentsInChildren<SpriteRenderer>()
                .Where(r => r.shadowCastingMode == ShadowCastingMode.TwoSided).ToArray();
            if (casters.Length < 2 || casters.Any(r => !r.receiveShadows ||
                r.sharedMaterial.FindPass("ShadowCaster") < 0))
                throw new InvalidOperationException("Wood/rock cards require two-sided silhouette shadow casting.");
            var actorMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/FightDemo/Art2Prototype/Art2ActorLit.mat");
            if (actorMaterial.FindPass("ShadowCaster") < 0 || actorMaterial.GetFloat("_ShadowCutoff") <= 0)
                throw new InvalidOperationException("Actor material requires alpha-clipped shadow casting.");
            if (RenderSettings.sun == null || RenderSettings.sun.shadows == LightShadows.None)
                throw new InvalidOperationException("The main directional light must cast shadows.");
            Debug.Log("Art2 sprite shadow settings validated. Verify animated silhouettes and ground contact in Play Mode.");
        }

        public static void ValidateAtmosphereBatch()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateAtmosphere();
        }

        [MenuItem("Rhythm Hunter/Art2/Validate Structure Sample")]
        public static void ValidateStructureSample()
        {
            ValidateAtmosphere();
            var sample = GameObject.Find("CaveStructureSample");
            if (sample == null) throw new InvalidOperationException("Structure sample is missing.");
            var shapes = sample.GetComponentsInChildren<Art2.Art2StructureMesh>();
            if (shapes.Length != 9) throw new InvalidOperationException("Expected seven structural blocks and two rocks.");
            int totalTriangles = 0;
            foreach (var shape in shapes)
            {
                shape.Rebuild();
                var mesh = shape.GetComponent<MeshFilter>().sharedMesh;
                var renderer = shape.GetComponent<MeshRenderer>();
                if (mesh == null || mesh.vertexCount == 0 || mesh.bounds.min.y < -.001f ||
                    !renderer.receiveShadows || renderer.shadowCastingMode != ShadowCastingMode.On)
                    throw new InvalidOperationException("Invalid geometry/shadow settings: " + shape.name);
                if (mesh.normals.Any(normal => !float.IsFinite(normal.x) || !float.IsFinite(normal.y) ||
                    !float.IsFinite(normal.z) || normal.sqrMagnitude < .9f))
                    throw new InvalidOperationException("Invalid face normals: " + shape.name);
                totalTriangles += mesh.triangles.Length / 3;
            }
            var testLight = GameObject.Find("ShadowTestLight_MoveMe").GetComponent<Light>();
            if (testLight.type != LightType.Directional || testLight.shadows == LightShadows.None || RenderSettings.sun != testLight)
                throw new InvalidOperationException("Shadow test light must be the shadow-casting scene sun.");
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null || !pipeline.supportsMainLightShadows)
                throw new InvalidOperationException("The current pipeline must support main-light shadows.");
            Debug.Log($"Art2 structure sample validated: {shapes.Length} meshes, {totalTriangles} triangles, main-light shadows configured. Visual shadow review still required.");
        }
    }
}
