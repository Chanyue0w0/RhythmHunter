using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    /// <summary>URP ignores camera renderer overrides for SceneView. Scope the default
    /// renderer override to one editor camera render without dirtying project assets.</summary>
    [InitializeOnLoad]
    public static class FightVfxScenePreview
    {
        private static readonly FieldInfo DefaultRenderer = typeof(UniversalRenderPipelineAsset)
            .GetField("m_DefaultRendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        private static UniversalRenderPipelineAsset overriddenAsset;
        private static int savedIndex;
        private static bool savedDepth, savedOpaque;
        private static Camera previewCamera;

        static FightVfxScenePreview()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
            RenderPipelineManager.endContextRendering += (_, _) => Restore();
            AssemblyReloadEvents.beforeAssemblyReload += Restore;
            EditorApplication.quitting += Restore;
            // Also recover if an exception prevents the render-end callback.
            EditorApplication.update += Restore;
        }

        private static bool NeedsUniversalScenePreview()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                return UsesParticlePreview(stage) ||
                    stage.assetPath == "Assets/FightDemo/Prefabs/ArtBattle/CaveEnvironment_2_5D.prefab" ||
                    stage.assetPath == "Assets/FightDemo/Prefabs/ArtBattle/CaveStructureSample.prefab";
            string path = SceneManager.GetActiveScene().path;
            return path == "Assets/FightDemo/Scenes/Art2_FightScene.unity" ||
                (path.StartsWith("Assets/Vefects/Pixel Craft VFX URP/") && path.EndsWith(".unity"));
        }

        private static bool UsesParticlePreview(PrefabStage stage)
        {
            // Keep the authored battle VFX preview policy unchanged. Other purchased
            // particle prefabs automatically get the universal renderer, without asset edits.
            return stage != null && !stage.assetPath.StartsWith("Assets/FightDemo/Prefabs/VFX/") &&
                stage.prefabContentsRoot != null &&
                stage.prefabContentsRoot.GetComponentInChildren<ParticleSystem>(true) != null;
        }

        private static void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            // URP 17 ignores AdditionalCameraData on Preview cameras, just as on SceneView.
            bool diagnostic = camera.cameraType == CameraType.Preview && camera.name == "Art2_SurfaceDiagnosticCamera";
            if ((!diagnostic && (camera.cameraType != CameraType.SceneView || !NeedsUniversalScenePreview())) || DefaultRenderer == null) return;
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) return;
            int index = -1;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool art2 = diagnostic || (stage == null && SceneManager.GetActiveScene().path == "Assets/FightDemo/Scenes/Art2_FightScene.unity") ||
                (stage != null && stage.assetPath.StartsWith("Assets/FightDemo/Prefabs/ArtBattle/Cave"));
            string rendererName = art2 ? "Art2ForwardPlusRenderer" : "UniversalVFXRenderer";
            var renderers = asset.rendererDataList;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i].name == rendererName) { index = i; break; }
            if (index < 0) return;
            Restore();
            savedIndex = (int)DefaultRenderer.GetValue(asset);
            savedDepth = asset.supportsCameraDepthTexture;
            savedOpaque = asset.supportsCameraOpaqueTexture;
            overriddenAsset = asset;
            previewCamera = camera;
            DefaultRenderer.SetValue(asset, index);
            asset.supportsCameraDepthTexture = true;
            asset.supportsCameraOpaqueTexture = true;
        }

        private static void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == previewCamera) Restore();
        }

        private static void Restore()
        {
            if (overriddenAsset != null)
            {
                DefaultRenderer?.SetValue(overriddenAsset, savedIndex);
                overriddenAsset.supportsCameraDepthTexture = savedDepth;
                overriddenAsset.supportsCameraOpaqueTexture = savedOpaque;
            }
            overriddenAsset = null;
            previewCamera = null;
        }

        [MenuItem("Rhythm Hunter/VFX/Enable Scene View Preview")]
        private static void EnablePreview()
        {
            var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.in2DMode = false;
            view.sceneViewState.showParticleSystems = true;
            view.sceneViewState.alwaysRefresh = true;
            view.sceneLighting = true;
            view.Repaint();
        }

        [MenuItem("Rhythm Hunter/Art2/Frame Scene Preview")]
        private static void FrameArt2Preview()
        {
            if (SceneManager.GetActiveScene().path != "Assets/FightDemo/Scenes/Art2_FightScene.unity")
            {
                Debug.LogWarning("Open Art2_FightScene before framing its preview.");
                return;
            }
            var cameraObject = GameObject.Find("Art2_TestCamera");
            var camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
            if (camera == null) return;
            var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.in2DMode = false;
            view.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Textured);
            view.sceneLighting = true;
            view.sceneViewState.showImageEffects = true;
            view.sceneViewState.showParticleSystems = true;
            view.sceneViewState.alwaysRefresh = true;
            float distance = camera.orthographicSize /
                Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            view.LookAt(camera.transform.position + camera.transform.forward * distance,
                camera.transform.rotation, camera.orthographicSize * 1.2f, camera.orthographic, true);
            view.Repaint();
        }
    }
}
