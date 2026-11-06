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
            if (renderers.Length != 9 || renderers.Any(r => !r.sprite || !r.sharedMaterial))
                throw new InvalidOperationException("Expected nine valid cave cards.");
            if (UnityEngine.Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None).Length != 6)
                throw new InvalidOperationException("Expected six active battle slots.");
            if (GameObject.Find("CaveEnvironment") || GameObject.Find("ForestEnvironment"))
                throw new InvalidOperationException("Original flat backgrounds must remain inactive.");
            if (UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("Expected one active audio listener.");
            Debug.Log("Art2 validation passed: one camera/listener, nine cave cards, six battle slots and active combat.");
        }

        [MenuItem("Rhythm Hunter/Art2/Validate Atmosphere")]
        public static void ValidateAtmosphere()
        {
            Validate();
            var camera = GameObject.Find("Art2_TestCamera").GetComponent<Camera>();
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
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
            if (lights.Length != 4 || lights.Any(light => light.shadows != LightShadows.None))
                throw new InvalidOperationException("Expected four shadowless lights.");
            const string folder = "Assets/FightDemo/Art2Prototype/";
            foreach (var name in new[] { "Art2ActorLit", "CaveMiddleLit", "CaveDistantLit", "CaveTorchLit", "Art2ContactShadow", "CaveGround" })
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

        public static void ValidateAtmosphereBatch()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateAtmosphere();
        }
    }
}
