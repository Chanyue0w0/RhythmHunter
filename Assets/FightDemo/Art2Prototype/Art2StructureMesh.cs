using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class Art2StructureMesh : MonoBehaviour
    {
        public enum Shape { Block, Rock }
        [SerializeField] private Shape shape;
        [SerializeField] private Vector3 size = Vector3.one;
        [SerializeField, Range(-1.5f, 1.5f)] private float topDepthOffsetPerHeight = 1;
        [SerializeField, Range(5, 12)] private int rockSides = 7;
        [SerializeField] private int rockSeed = 1;

        private Mesh generated;
        private bool dirty = true;
        private void OnEnable() => dirty = true;
        private void OnValidate() => dirty = true;
        private void Update()
        {
            if (!dirty && generated != null) return;
            Rebuild();
        }

        [ContextMenu("Rebuild Mesh")]
        public void Rebuild()
        {
            if (generated == null)
                generated = new Mesh { name = "Art2 " + name, hideFlags = HideFlags.HideAndDontSave };
            Build(generated, shape, size, topDepthOffsetPerHeight, rockSides, rockSeed);
            GetComponent<MeshFilter>().sharedMesh = generated;
            dirty = false;
        }

        // Pure geometry construction; no gameplay objects, source art or random state are changed.
        public static void Build(Mesh mesh, Shape shape, Vector3 size, float skew, int sides, int seed)
        {
            size = new Vector3(Mathf.Max(.05f, size.x), Mathf.Max(.05f, size.y), Mathf.Max(.05f, size.z));
            sides = Mathf.Clamp(sides, 5, 12);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.one);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            }
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Triangle(a, b, c); Triangle(a, c, d);
            }
            if (shape == Shape.Block)
            {
                float x = size.x * .5f, z = size.z * .5f, y = size.y, shift = skew * y;
                var p = new[] {
                    new Vector3(-x,0,-z), new Vector3(x,0,-z), new Vector3(x,0,z), new Vector3(-x,0,z),
                    new Vector3(-x,y,-z+shift), new Vector3(x,y,-z+shift), new Vector3(x,y,z+shift), new Vector3(-x,y,z+shift)
                };
                Quad(p[0],p[4],p[5],p[1]); Quad(p[3],p[2],p[6],p[7]);
                Quad(p[0],p[3],p[7],p[4]); Quad(p[1],p[5],p[6],p[2]);
                Quad(p[4],p[7],p[6],p[5]); Quad(p[0],p[1],p[2],p[3]);
            }
            else
            {
                var random = new System.Random(seed);
                var rings = new Vector3[3, sides];
                float[] height = { 0, .35f, .82f }, radius = { .72f, 1, .58f };
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2 / sides;
                    float variation = .85f + (float)random.NextDouble() * .25f;
                    for (int row = 0; row < 3; row++)
                    {
                        float y = height[row] * size.y;
                        rings[row,i] = new Vector3(Mathf.Cos(angle)*size.x*.5f*radius[row]*variation,
                            y, Mathf.Sin(angle)*size.z*.5f*radius[row]*variation + y*skew);
                    }
                }
                var top = new Vector3(size.x*.07f, size.y, size.y*skew);
                for (int i = 0; i < sides; i++)
                {
                    int next = (i + 1) % sides;
                    Triangle(Vector3.zero, rings[0,i], rings[0,next]);
                    Quad(rings[0,i], rings[1,i], rings[1,next], rings[0,next]);
                    Quad(rings[1,i], rings[2,i], rings[2,next], rings[1,next]);
                    Triangle(top, rings[2,next], rings[2,i]);
                }
            }
            mesh.Clear();
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        }

        private void OnDisable()
        {
            if (generated == null) return;
            var filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == generated) filter.sharedMesh = null;
            if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated);
            generated = null;
        }
    }
}
