using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Builds the FightScene natural-light rig at runtime so the setup stays isolated
    /// to FightDemo and remains available after Play Mode script reloads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FightSceneNaturalLighting : MonoBehaviour
    {
        private const string RigName = "FightScene Natural Light Rig";
        private const string LitShaderName = "Universal Render Pipeline/2D/Sprite-Lit-Default";

        private VolumeProfile runtimeProfile;
        private Material litSpriteMaterial;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void ReapplyAfterEditorScriptReload()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (!UnityEditor.EditorApplication.isPlaying)
                    return;

                FightCombatController[] controllers = FindObjectsByType<FightCombatController>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                foreach (FightCombatController controller in controllers)
                {
                    if (controller.gameObject.scene.name == "FightScene")
                        Ensure(controller);
                }
            };
        }
#endif

        public static void Ensure(FightCombatController source)
        {
            if (source == null || source.gameObject.scene.name != "FightScene")
                return;

            Camera sceneCamera = null;
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Camera candidate in cameras)
            {
                if (candidate.gameObject.scene == source.gameObject.scene)
                {
                    sceneCamera = candidate;
                    break;
                }
            }

            if (sceneCamera == null)
                return;

            FightSceneNaturalLighting lighting = sceneCamera.GetComponent<FightSceneNaturalLighting>();
            if (lighting == null)
                lighting = sceneCamera.gameObject.AddComponent<FightSceneNaturalLighting>();

            lighting.Build(source.gameObject.scene, sceneCamera);
        }

        private void Build(Scene scene, Camera sceneCamera)
        {
            UniversalAdditionalCameraData cameraData = sceneCamera.GetUniversalAdditionalCameraData();
            cameraData.SetRenderer(1);
            cameraData.renderPostProcessing = true;
            sceneCamera.allowHDR = true;

            Transform rig = FindOrCreateRig(scene);
            SetupGlobalLight(rig);
            SetupSunLight(rig);
            SetupPostProcessing(rig);
            ApplyLitCharacterMaterials(scene);
            AdjustGroundShadows(scene);
            Debug.Log("[FightScene Lighting] Natural-light rig and post processing applied.", this);
        }

        private static Transform FindOrCreateRig(Scene scene)
        {
            GameObject existing = GameObject.Find(RigName);
            if (existing != null && existing.scene == scene)
                return existing.transform;

            GameObject rig = new(RigName);
            SceneManager.MoveGameObjectToScene(rig, scene);
            return rig.transform;
        }

        private static void SetupGlobalLight(Transform rig)
        {
            Light2D light = GetOrCreateLight(rig, "Cool Natural Fill");
            light.lightType = Light2D.LightType.Global;
            light.blendStyleIndex = 0;
            light.color = new Color(0.58f, 0.68f, 0.82f, 1f);
            light.intensity = 0.58f;
        }

        private static void SetupSunLight(Transform rig)
        {
            Light2D light = GetOrCreateLight(rig, "Warm Window Light");
            light.lightType = Light2D.LightType.Point;
            light.blendStyleIndex = 1;
            light.color = new Color(1f, 0.78f, 0.52f, 1f);
            light.intensity = 1.1f;
            light.pointLightInnerRadius = 1.5f;
            light.pointLightOuterRadius = 12f;
            light.falloffIntensity = 0.45f;
            light.transform.localPosition = new Vector3(-3.6f, 3.2f, -1f);
        }

        private static Light2D GetOrCreateLight(Transform rig, string lightName)
        {
            Transform child = rig.Find(lightName);
            GameObject lightObject;
            if (child == null)
            {
                lightObject = new GameObject(lightName);
                lightObject.transform.SetParent(rig, false);
            }
            else
            {
                lightObject = child.gameObject;
            }

            Light2D light = lightObject.GetComponent<Light2D>();
            return light != null ? light : lightObject.AddComponent<Light2D>();
        }

        private void SetupPostProcessing(Transform rig)
        {
            Transform child = rig.Find("Natural Light Post Processing");
            GameObject volumeObject;
            if (child == null)
            {
                volumeObject = new GameObject("Natural Light Post Processing");
                volumeObject.transform.SetParent(rig, false);
            }
            else
            {
                volumeObject = child.gameObject;
            }

            Volume volume = volumeObject.GetComponent<Volume>();
            if (volume == null)
                volume = volumeObject.AddComponent<Volume>();

            if (runtimeProfile == null)
            {
                runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                runtimeProfile.name = "FightScene Natural Light (Runtime)";
            }

            volume.isGlobal = true;
            volume.priority = 50f;
            volume.sharedProfile = runtimeProfile;

            Bloom bloom = GetOrAdd<Bloom>(runtimeProfile);
            bloom.active = true;
            bloom.threshold.Override(0.82f);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.62f);

            ColorAdjustments color = GetOrAdd<ColorAdjustments>(runtimeProfile);
            color.active = true;
            color.postExposure.Override(0.13f);
            color.contrast.Override(7f);
            color.saturation.Override(5f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.91f, 1f));

            Tonemapping tonemapping = GetOrAdd<Tonemapping>(runtimeProfile);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.Neutral);

            Vignette vignette = GetOrAdd<Vignette>(runtimeProfile);
            vignette.active = true;
            vignette.color.Override(new Color(0.012f, 0.022f, 0.05f, 1f));
            vignette.intensity.Override(0.25f);
            vignette.smoothness.Override(0.35f);
            vignette.rounded.Override(true);
        }

        private void ApplyLitCharacterMaterials(Scene scene)
        {
            Shader litShader = Shader.Find(LitShaderName);
            if (litShader == null)
            {
                Debug.LogWarning($"[FightScene Lighting] Shader not found: {LitShaderName}", this);
                return;
            }

            if (litSpriteMaterial == null)
            {
                litSpriteMaterial = new Material(litShader)
                {
                    name = "FightScene Character Lit (Runtime)"
                };
            }

            int characterCount = 0;
            BeatSyncedIdleAnimator[] animators = FindObjectsByType<BeatSyncedIdleAnimator>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (BeatSyncedIdleAnimator animator in animators)
            {
                SpriteRenderer renderer = animator.TargetRenderer;
                if (renderer == null || renderer.gameObject.scene != scene)
                    continue;

                renderer.sharedMaterial = litSpriteMaterial;
                characterCount++;
            }

            Debug.Log($"[FightScene Lighting] Natural light active on {characterCount} character renderers.", this);
        }

        private static void AdjustGroundShadows(Scene scene)
        {
            Transform[] sceneTransforms = FindObjectsByType<Transform>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (Transform sceneTransform in sceneTransforms)
            {
                if (sceneTransform.gameObject.scene != scene || sceneTransform.name != "SlotGround")
                    continue;

                SpriteRenderer shadow = sceneTransform.GetComponent<SpriteRenderer>();
                if (shadow == null)
                    continue;

                Color color = shadow.color;
                shadow.color = new Color(0.035f, 0.055f, 0.1f, Mathf.Max(0.34f, color.a));
                Vector3 position = sceneTransform.localPosition;
                sceneTransform.localPosition = new Vector3(0.12f, position.y - 0.03f, position.z);
            }
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
                return component;

            return profile.Add<T>(true);
        }

        private void OnDestroy()
        {
            if (runtimeProfile != null)
                Destroy(runtimeProfile);
            if (litSpriteMaterial != null)
                Destroy(litSpriteMaterial);
        }
    }
}
