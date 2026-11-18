using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace RhythmHunter.FightDemo.Art2
{
    public sealed class RoarDistortionFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;
        private Material material;
        private RoarPass pass;
        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (!shader) shader = Shader.Find("Hidden/RhythmHunter/RoarDistortion");
            if (shader) material = CoreUtils.CreateEngineMaterial(shader);
            // After transparents AND the floor-recovery pass, before Overlay UI.
            pass = new RoarPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing, requiresIntermediateTexture = true };
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (!material || data.cameraData.cameraType != CameraType.Game || data.cameraData.isPreviewCamera) return;
            for (int i = RoarDistortionController.Active.Count - 1; i >= 0; i--)
            {
                var effect = RoarDistortionController.Active[i];
                if (effect && effect.TryParameters(data.cameraData.camera, out var center, out var wave))
                {
                    // First version: newest visible roar wins; no additional full-screen copies for overlaps.
                    pass.material = material; pass.center = center; pass.wave = wave;
                    renderer.EnqueuePass(pass); return;
                }
            }
        }
        protected override void Dispose(bool disposing) { CoreUtils.Destroy(material); }
        private sealed class RoarPass : ScriptableRenderPass
        {
            public Material material;
            public Vector4 center, wave;
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var descriptor = graph.GetTextureDesc(source);
                descriptor.name = "Roar Distortion Output"; descriptor.clearBuffer = false;
                descriptor.depthBufferBits = DepthBits.None; descriptor.msaaSamples = MSAASamples.None;
                descriptor.filterMode = FilterMode.Point;
                var destination = graph.CreateTexture(descriptor);
                material.SetVector("_RoarCenterRadius", center);
                material.SetVector("_RoarWave", wave);
                material.SetVector("_RoarResolution", new Vector4(camera.cameraTargetDescriptor.width, camera.cameraTargetDescriptor.height, 0, 0));
                graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0), passName: "Roar ring refraction");
                resources.cameraColor = destination;
            }
        }
    }
}
