using UnityEngine;

namespace RhythmHunter.PirateOceanPrototype
{
    /// <summary>
    /// Generates one continuous, inexpensive water mesh. The layered sprite
    /// waves remain responsible for stylized crests and parallax detail.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PirateOceanSurface : MonoBehaviour
    {
        private const string GeneratedMeshName = "Pirate Ocean Surface (Generated)";
        private const string GeneratedMaterialName = "Pirate Ocean Surface Material (Generated)";

        [Header("Surface Geometry")]
        [SerializeField, Min(4f)] private float width = 28f;
        [SerializeField] private float surfaceY = -0.25f;
        [SerializeField] private float bottomY = -7f;
        [SerializeField, Range(16, 192)] private int segmentCount = 96;
        [SerializeField, Range(0f, 1f)] private float secondaryWaveRatio = 0.34f;
        [SerializeField, Range(0.1f, 4f)] private float secondaryFrequency = 1.73f;
        [SerializeField, Range(0.1f, 3f)] private float secondarySpeed = 0.63f;

        [Header("Water Appearance")]
        [SerializeField] private Color surfaceColor = new(0.08f, 0.44f, 0.58f, 1f);
        [SerializeField] private Color depthColor = new(0.018f, 0.09f, 0.2f, 1f);
        [SerializeField] private int sortingOrder = -75;

        [SerializeField, HideInInspector] private MeshFilter meshFilter;
        [SerializeField, HideInInspector] private MeshRenderer meshRenderer;

        private Mesh generatedMesh;
        private Material generatedMaterial;
        private Vector3[] vertices;
        private Vector2[] uvs;
        private Color[] colors;
        private int[] triangles;
        private int builtSegmentCount = -1;

        public float Width => width;
        public float SurfaceY => surfaceY;
        public float BottomY => bottomY;
        public int SegmentCount => segmentCount;

        public void Configure(
            float meshWidth,
            float topY,
            float lowerY,
            int segments,
            int rendererSortingOrder,
            Color topColor,
            Color lowerColor)
        {
            width = Mathf.Max(4f, meshWidth);
            surfaceY = topY;
            bottomY = Mathf.Min(lowerY, topY - 0.5f);
            segmentCount = Mathf.Clamp(segments, 16, 192);
            sortingOrder = rendererSortingOrder;
            surfaceColor = topColor;
            depthColor = lowerColor;
            EnsureResources();
            ApplyWave(0f, 1f, 0.34f, 1.25f, 0.82f, 1f);
        }

        private void OnEnable()
        {
            EnsureResources();
            ApplyWave(0f, 1f, 0.34f, 1.25f, 0.82f, 1f);
        }

        private void OnValidate()
        {
            width = Mathf.Max(4f, width);
            bottomY = Mathf.Min(bottomY, surfaceY - 0.5f);
            segmentCount = Mathf.Clamp(segmentCount, 16, 192);
            EnsureResources();
            ApplyWave(0f, 1f, 0.34f, 1.25f, 0.82f, 1f);
        }

        private void OnDestroy()
        {
            DestroyGeneratedObject(generatedMesh);
            DestroyGeneratedObject(generatedMaterial);
        }

        public void ApplyWave(
            float time,
            float intensity,
            float waveHeight,
            float waveSpeed,
            float frequency,
            float directionSign)
        {
            EnsureResources();
            EnsureTopology();
            if (generatedMesh == null || vertices == null)
                return;

            float halfWidth = width * 0.5f;
            float clampedIntensity = Mathf.Max(0f, intensity);
            float primaryAmplitude = Mathf.Max(0f, waveHeight) * clampedIntensity;
            float primaryFrequency = Mathf.Max(0.1f, frequency);
            float travel = time * Mathf.Max(0f, waveSpeed) * Mathf.Sign(directionSign);

            for (int i = 0; i <= segmentCount; i++)
            {
                float normalized = i / (float)segmentCount;
                float x = Mathf.Lerp(-halfWidth, halfWidth, normalized);
                float primary = Mathf.Sin(x * primaryFrequency + travel) * primaryAmplitude;
                float secondary = Mathf.Sin(
                    x * primaryFrequency * secondaryFrequency
                    - travel * secondarySpeed
                    + 1.7f) * primaryAmplitude * secondaryWaveRatio;

                int topIndex = i * 2;
                int bottomIndex = topIndex + 1;
                vertices[topIndex] = new Vector3(x, surfaceY + primary + secondary, 0f);
                vertices[bottomIndex] = new Vector3(x, bottomY, 0f);
                uvs[topIndex] = new Vector2(normalized, 1f);
                uvs[bottomIndex] = new Vector2(normalized, 0f);
                colors[topIndex] = surfaceColor;
                colors[bottomIndex] = depthColor;
            }

            generatedMesh.vertices = vertices;
            generatedMesh.uv = uvs;
            generatedMesh.colors = colors;
            generatedMesh.RecalculateBounds();
        }

        private void EnsureResources()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            if (generatedMesh == null)
            {
                generatedMesh = new Mesh
                {
                    name = GeneratedMeshName,
                    hideFlags = HideFlags.HideAndDontSave
                };
                generatedMesh.MarkDynamic();
                builtSegmentCount = -1;
            }

            if (meshFilter != null && meshFilter.sharedMesh != generatedMesh)
                meshFilter.sharedMesh = generatedMesh;

            if (generatedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");

                if (shader != null)
                {
                    generatedMaterial = new Material(shader)
                    {
                        name = GeneratedMaterialName,
                        hideFlags = HideFlags.HideAndDontSave,
                        color = Color.white
                    };
                }
            }

            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = sortingOrder;
                if (generatedMaterial != null && meshRenderer.sharedMaterial != generatedMaterial)
                    meshRenderer.sharedMaterial = generatedMaterial;
            }
        }

        private void EnsureTopology()
        {
            if (generatedMesh == null)
                return;

            if (builtSegmentCount == segmentCount && vertices != null && vertices.Length == (segmentCount + 1) * 2)
                return;

            int vertexCount = (segmentCount + 1) * 2;
            vertices = new Vector3[vertexCount];
            uvs = new Vector2[vertexCount];
            colors = new Color[vertexCount];
            triangles = new int[segmentCount * 6];

            for (int i = 0; i < segmentCount; i++)
            {
                int top = i * 2;
                int bottom = top + 1;
                int nextTop = top + 2;
                int nextBottom = top + 3;
                int triangle = i * 6;

                triangles[triangle] = top;
                triangles[triangle + 1] = nextTop;
                triangles[triangle + 2] = bottom;
                triangles[triangle + 3] = nextTop;
                triangles[triangle + 4] = nextBottom;
                triangles[triangle + 5] = bottom;
            }

            generatedMesh.Clear();
            generatedMesh.vertices = vertices;
            generatedMesh.uv = uvs;
            generatedMesh.colors = colors;
            generatedMesh.triangles = triangles;
            builtSegmentCount = segmentCount;
        }

        private static void DestroyGeneratedObject(Object generatedObject)
        {
            if (generatedObject == null)
                return;

            if (Application.isPlaying)
                Destroy(generatedObject);
            else
                DestroyImmediate(generatedObject);
        }
    }
}
