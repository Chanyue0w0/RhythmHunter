using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    // One small UI mesh: transparent centre, soft edge fade, no texture or post-processing changes.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FightScreenEdgeGraphic : MaskableGraphic
    {
        [Range(.01f, .45f)] public float width = .16f;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float thickness = Mathf.Min(rect.width, rect.height) * Mathf.Clamp(width, .01f, .45f);
            const int rings = 12;
            for (int ring = 0; ring <= rings; ring++)
            {
                float t = ring / (float)rings;
                float inset = t * thickness;
                var tint = color; tint.a *= 1 - Mathf.SmoothStep(0, 1, t);
                mesh.AddVert(new Vector3(rect.xMin + inset, rect.yMin + inset), tint, Vector2.zero);
                mesh.AddVert(new Vector3(rect.xMin + inset, rect.yMax - inset), tint, Vector2.zero);
                mesh.AddVert(new Vector3(rect.xMax - inset, rect.yMax - inset), tint, Vector2.zero);
                mesh.AddVert(new Vector3(rect.xMax - inset, rect.yMin + inset), tint, Vector2.zero);
                if (ring == 0) continue;
                for (int side = 0; side < 4; side++)
                {
                    int a = (ring - 1) * 4 + side, b = (ring - 1) * 4 + (side + 1) % 4;
                    int c = ring * 4 + side, d = ring * 4 + (side + 1) % 4;
                    mesh.AddTriangle(a, b, d); mesh.AddTriangle(a, d, c);
                }
            }
        }
    }
}
