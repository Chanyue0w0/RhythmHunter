using System.Reflection;
using UnityEditor;
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

        private static bool IsVefectsScene()
        {
            string path = SceneManager.GetActiveScene().path;
            return path.StartsWith("Assets/Vefects/Pixel Craft VFX URP/") && path.EndsWith(".unity");
        }

        private static void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.SceneView || !IsVefectsScene() || DefaultRenderer == null) return;
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) return;
            int index = -1;
            var renderers = asset.rendererDataList;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i].name == "UniversalVFXRenderer") { index = i; break; }
            if (index < 0) return;
            Restore();
            savedIndex = (int)DefaultRenderer.GetValue(asset);
            overriddenAsset = asset;
            previewCamera = camera;
            DefaultRenderer.SetValue(asset, index);
        }

        private static void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == previewCamera) Restore();
        }

        private static void Restore()
        {
            if (overriddenAsset != null) DefaultRenderer?.SetValue(overriddenAsset, savedIndex);
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
    }
}
