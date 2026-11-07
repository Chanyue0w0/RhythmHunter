using UnityEngine;
using UnityEngine.Rendering;

namespace RhythmHunter.FightDemo.Art2
{
    // Four persistent visual quads; no per-frame creation or material instances.
    [ExecuteAlways, RequireComponent(typeof(Light)), DisallowMultipleComponent]
    public sealed class Art2TorchHalo : MonoBehaviour
    {
        [SerializeField] private Material haloMaterial;
        [Min(0.01f)] [SerializeField] private float radius = 0.95f;
        [Min(0)] [SerializeField] private float brightness = 1.4f;
        [Min(0.01f)] [SerializeField] private float referenceLightIntensity = 10f;
        [SerializeField] private Vector3 localOffset;
        private Light source;
        private GameObject visual;
        private Mesh mesh;
        private MeshRenderer visualRenderer;
        private MaterialPropertyBlock properties;
        private static readonly int HaloColor = Shader.PropertyToID("_HaloColor");

        private void OnEnable()
        {
            source = GetComponent<Light>();
            properties = new MaterialPropertyBlock();
            mesh = new Mesh { name = "Art2 Torch Halo Quad", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(1,1,0), new Vector3(-1,1,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            visual = new GameObject("Torch_RoundGlow_Preview") { hideFlags = HideFlags.HideAndDontSave };
            visual.transform.SetParent(transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            visualRenderer = visual.AddComponent<MeshRenderer>();
            visualRenderer.shadowCastingMode = ShadowCastingMode.Off;
            visualRenderer.receiveShadows = false;
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            SyncVisual();
        }

        private void LateUpdate() => SyncVisual();

        public void SyncVisual()
        {
            if (visualRenderer == null || source == null) return;
            visualRenderer.enabled = source.enabled && haloMaterial != null;
            visualRenderer.sharedMaterial = haloMaterial;
            visual.layer = gameObject.layer;
            visual.transform.position = transform.TransformPoint(localOffset);
            // Match the light's current intensity, including edit-mode flicker preview.
            Color color = source.color.linear * (Mathf.Max(0, brightness) *
                Mathf.Max(0, source.intensity) / Mathf.Max(0.01f, referenceLightIntensity));
            color.a = 1;
            properties.SetColor(HaloColor, color);
            visualRenderer.SetPropertyBlock(properties);
        }

        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (visual == null) return;
            SyncVisual();
            // A camera-facing circle, rather than a flattened ellipse on the tilted wall.
            visual.transform.rotation = camera.transform.rotation;
            var scale = transform.lossyScale;
            float size = Mathf.Max(.01f, radius);
            visual.transform.localScale = new Vector3(size / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                size / Mathf.Max(.001f, Mathf.Abs(scale.y)), size / Mathf.Max(.001f, Mathf.Abs(scale.z)));
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            if (visual != null) visual.SetActive(false);
            Release(visual);
            Release(mesh);
            visual = null;
            mesh = null;
        }

        private static void Release(Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }
    }
}
