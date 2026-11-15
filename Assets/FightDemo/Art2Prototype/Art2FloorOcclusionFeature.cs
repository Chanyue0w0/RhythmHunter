using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace RhythmHunter.FightDemo.Art2
{
    public sealed class Art2FloorOcclusionFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader occluderShader;
        private Material depthMaterial;
        private RecoveryPass pass;
        private readonly List<Renderer> suppressedEffects = new();
        private readonly List<Renderer> effectCandidates = new();
        public override void Create()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RestoreEffects();
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
            CoreUtils.Destroy(depthMaterial);
            pass?.DisposeMeshes();
            if (occluderShader == null) occluderShader = Shader.Find("Hidden/RhythmHunter/Art2FloorOccluder");
            if (occluderShader != null) depthMaterial = CoreUtils.CreateEngineMaterial(occluderShader);
            pass = new RecoveryPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents, SuppressedEffects = suppressedEffects };
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (depthMaterial == null) return;
            foreach (var settings in Art2FloorOcclusion.Active)
                if (settings != null && settings.Matches(renderingData.cameraData.camera))
                {
                    pass.settings = settings; pass.depthMaterial = depthMaterial;
                    renderer.EnqueuePass(pass); return;
                }
        }
        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            RestoreEffects();
            if (!isActive || depthMaterial == null) return;
            foreach (var settings in Art2FloorOcclusion.Active)
            {
                if (settings == null || !settings.Matches(camera) || settings.floors.Length == 0) continue;
                CollectEffects(camera, settings.gameObject.scene, effectCandidates);
                foreach (var renderer in effectCandidates)
                {
                    if (renderer.forceRenderingOff) continue;
                    suppressedEffects.Add(renderer);
                    renderer.forceRenderingOff = true;
                }
                break;
            }
        }
        private void EndCamera(ScriptableRenderContext context, Camera camera) => RestoreEffects();
        private void RestoreEffects()
        {
            foreach (var renderer in suppressedEffects) if (renderer != null) renderer.forceRenderingOff = false;
            suppressedEffects.Clear();
        }
        private static void CollectEffects(Camera camera, UnityEngine.SceneManagement.Scene scene, List<Renderer> result)
        {
            result.Clear();
            foreach (var effect in Art2EffectPresentation.Active)
            {
                if (effect == null || effect.gameObject.scene != scene || !effect.isActiveAndEnabled || !effect.IgnoreFloorOcclusion) continue;
                foreach (var renderer in effect.FloorRenderers)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                        (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0 ||
                        renderer is not ParticleSystemRenderer && renderer is not SpriteRenderer && renderer is not MeshRenderer ||
                        renderer.sharedMaterial == null || renderer.sharedMaterial.renderQueue < 2500) continue;
                    if (!result.Contains(renderer)) result.Add(renderer);
                }
            }
            result.Sort((a, b) =>
            {
                int order = SortingLayer.GetLayerValueFromID(a.sortingLayerID).CompareTo(SortingLayer.GetLayerValueFromID(b.sortingLayerID));
                if (order == 0) order = a.sortingOrder.CompareTo(b.sortingOrder);
                if (order == 0) order = (b.bounds.center - camera.transform.position).sqrMagnitude.CompareTo((a.bounds.center - camera.transform.position).sqrMagnitude);
                return order;
            });
        }
        protected override void Dispose(bool disposing)
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RestoreEffects(); CoreUtils.Destroy(depthMaterial); pass?.DisposeMeshes();
        }
        private sealed class RecoveryPass : ScriptableRenderPass
        {
            public Art2FloorOcclusion settings;
            public Material depthMaterial;
            readonly List<Renderer> occluders = new(), warriors = new(), floors = new();
            readonly List<Renderer> effects = new();
            public List<Renderer> SuppressedEffects;
            private sealed class EffectDrawData
            {
                public Renderer[] renderers;
                public TextureHandle sampledDepth, originalDepth;
            }
            readonly Dictionary<Sprite, Mesh> spriteMeshes = new();
            readonly Dictionary<Renderer, MaterialPropertyBlock> propertyBlocks = new();
            readonly List<Renderer> staleRenderers = new();
            public void DisposeMeshes()
            {
                foreach (var mesh in spriteMeshes.Values) CoreUtils.Destroy(mesh);
                spriteMeshes.Clear(); propertyBlocks.Clear();
            }
            static readonly int FloorId = Shader.PropertyToID("_Art2FloorOnlyDepth"), OtherId = Shader.PropertyToID("_Art2NonFloorDepth");
            private sealed class DrawData { public Renderer[] renderers; public Material depth; public bool recovery; public DepthSprite[] sprites; }
            private struct DepthSprite { public Mesh mesh; public Matrix4x4 matrix; public MaterialPropertyBlock properties; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var camera = frameData.Get<UniversalCameraData>();
                var resources = frameData.Get<UniversalResourceData>();
                staleRenderers.Clear();
                foreach (var renderer in propertyBlocks.Keys) if (renderer == null) staleRenderers.Add(renderer);
                foreach (var renderer in staleRenderers) propertyBlocks.Remove(renderer);
                settings.Collect(occluders, warriors); floors.Clear();
                effects.Clear();
                if (SuppressedEffects != null) effects.AddRange(SuppressedEffects);
                foreach (var effect in effects) { occluders.Remove(effect); warriors.Remove(effect); }
                foreach (var floor in settings.floors) { Art2FloorOcclusion.Add(floors, floor); occluders.Remove(floor); }
                if (effects.Count == 0 && (warriors.Count == 0 || floors.Count == 0)) return;
                var desc = camera.cameraTargetDescriptor;
                desc.msaaSamples = 1; desc.depthBufferBits = 32;
                desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
                var floorDepth = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "Art2 Floor Only Depth", true, FilterMode.Point);
                var otherDepth = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "Art2 Non-floor Depth", true, FilterMode.Point);
                Depth(graph, floorDepth, floors, FloorId, "Art2 floor mask");
                Depth(graph, otherDepth, occluders, OtherId, "Art2 foreground silhouettes");
                using (var builder = graph.AddRasterRenderPass<DrawData>("Art2 recover floor-hidden warrior pixels", out var data))
                {
                    data.renderers = warriors.ToArray(); data.recovery = true; data.depth = null; data.sprites = null;
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.UseTexture(floorDepth, AccessFlags.Read); builder.UseTexture(otherDepth, AccessFlags.Read);
                    if (resources.mainShadowsTexture.IsValid()) builder.UseTexture(resources.mainShadowsTexture, AccessFlags.Read);
                    if (resources.additionalShadowsTexture.IsValid()) builder.UseTexture(resources.additionalShadowsTexture, AccessFlags.Read);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((DrawData d, RasterGraphContext context) => Draw(d, context));
                }
                if (effects.Count > 0)
                {
                    // A separate sampleable copy avoids reading a bound depth attachment in soft-particle shaders.
                    var sampledDepth = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "Art2 VFX soft-particle non-floor depth", true, FilterMode.Point);
                    Depth(graph, sampledDepth, occluders, Shader.PropertyToID("_Art2VfxNonFloorDepth"), "Art2 VFX sampled depth");
                    using var builder = graph.AddRasterRenderPass<EffectDrawData>("Art2 VFX ignore floor", out var data);
                    data.renderers = effects.ToArray(); data.sampledDepth = sampledDepth; data.originalDepth = resources.cameraDepthTexture;
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(otherDepth, AccessFlags.ReadWrite);
                    builder.UseTexture(sampledDepth, AccessFlags.Read);
                    if (data.originalDepth.IsValid()) builder.UseTexture(data.originalDepth, AccessFlags.Read);
                    if (resources.cameraOpaqueTexture.IsValid()) builder.UseTexture(resources.cameraOpaqueTexture, AccessFlags.Read);
                    if (resources.mainShadowsTexture.IsValid()) builder.UseTexture(resources.mainShadowsTexture, AccessFlags.Read);
                    if (resources.additionalShadowsTexture.IsValid()) builder.UseTexture(resources.additionalShadowsTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((EffectDrawData d, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture("_CameraDepthTexture", d.sampledDepth);
                        foreach (var renderer in d.renderers)
                        {
                            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                            var materials = renderer.sharedMaterials;
                            for (int i = 0; i < materials.Length; i++)
                            {
                                var material = materials[i];
                                if (material == null) continue;
                                // Draw only the color pass, never ShadowCaster/Depth passes.
                                int index = material.FindPass("ForwardLit");
                                if (index < 0) index = material.FindPass("ForwardUnlit");
                                if (index < 0) index = material.FindPass("UniversalForward");
                                if (index < 0) index = material.FindPass("Forward");
                                if (index < 0) index = 0;
                                context.cmd.DrawRenderer(renderer, material, i, index);
                            }
                        }
                        if (d.originalDepth.IsValid()) context.cmd.SetGlobalTexture("_CameraDepthTexture", d.originalDepth);
                    });
                }
            }
            private void Depth(RenderGraph graph, TextureHandle texture, List<Renderer> renderers, int globalId, string name)
            {
                using var builder = graph.AddRasterRenderPass<DrawData>(name, out var data);
                data.renderers = renderers.ToArray(); data.depth = depthMaterial; data.recovery = false;
                data.sprites = new DepthSprite[data.renderers.Length];
                for (int i = 0; i < data.renderers.Length; i++)
                {
                    if (data.renderers[i] is not SpriteRenderer spriteRenderer || spriteRenderer.sprite == null) continue;
                    var sprite = spriteRenderer.sprite;
                    if (!spriteMeshes.TryGetValue(sprite, out var mesh))
                    {
                        mesh = new Mesh { name = "Floor occlusion silhouette " + sprite.name, hideFlags = HideFlags.HideAndDontSave };
                        var points = sprite.vertices; var vertices = new Vector3[points.Length];
                        for (int v = 0; v < points.Length; v++) vertices[v] = points[v];
                        var sourceIndices = sprite.triangles; var indices = new int[sourceIndices.Length];
                        for (int t = 0; t < indices.Length; t++) indices[t] = sourceIndices[t];
                        mesh.vertices = vertices; mesh.uv = sprite.uv; mesh.triangles = indices; mesh.RecalculateBounds();
                        spriteMeshes.Add(sprite, mesh);
                    }
                    if (!propertyBlocks.TryGetValue(spriteRenderer, out var properties))
                    { properties = new MaterialPropertyBlock(); propertyBlocks.Add(spriteRenderer, properties); }
                    spriteRenderer.GetPropertyBlock(properties);
                    if (properties.GetTexture("_MainTex") == null) properties.SetTexture("_MainTex", sprite.texture);
                    properties.SetColor("_Tint", spriteRenderer.color);
                    data.sprites[i] = new DepthSprite { mesh = mesh, properties = properties,
                        matrix = spriteRenderer.localToWorldMatrix * Matrix4x4.Scale(new Vector3(spriteRenderer.flipX ? -1 : 1, spriteRenderer.flipY ? -1 : 1, 1)) };
                }
                builder.SetRenderAttachmentDepth(texture, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(texture, globalId);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((DrawData d, RasterGraphContext context) => Draw(d, context));
            }
            private static void Draw(DrawData data, RasterGraphContext context)
            {
                for (int i = 0; i < data.renderers.Length; i++)
                {
                    var renderer = data.renderers[i];
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    if (!data.recovery && data.sprites[i].mesh != null)
                    {
                        var sprite = data.sprites[i];
                        context.cmd.DrawMesh(sprite.mesh, sprite.matrix, data.depth, 0, 0, sprite.properties);
                        continue;
                    }
                    var material = data.recovery ? renderer.sharedMaterial : data.depth;
                    if (material == null) continue;
                    int index = data.recovery ? material.FindPass("FloorRecovery") : renderer is SpriteRenderer ? 0 : 1;
                    if (index >= 0) context.cmd.DrawRenderer(renderer, material, 0, index);
                }
            }
        }
    }
}
