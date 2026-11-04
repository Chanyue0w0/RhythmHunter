using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed class FightQteRing : MonoBehaviour
    {
        private LineRenderer inner, outer;
        private TextMesh label;
        public void Initialize(Material material)
        {
            inner = Circle("Inner", material); outer = Circle("Outer", material);
            var text = new GameObject("Button"); text.transform.SetParent(transform, false);
            label = text.AddComponent<TextMesh>();
            label.text = "A"; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 48; label.characterSize = .055f;
            var renderer = label.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = label.font.material; renderer.sortingOrder = 222;
        }
        private LineRenderer Circle(string name, Material material)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = false; line.loop = true;
            line.positionCount = 64; line.startWidth = line.endWidth = .035f; line.sortingOrder = 221;
            for (int i = 0; i < 64; i++)
            { float angle = i * Mathf.PI * 2 / 64; line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0)); }
            return line;
        }
        public void Present(Vector3 position, float progress, float radius, FightQteNote.Result outcome)
        {
            gameObject.SetActive(true); transform.position = position;
            inner.transform.localScale = Vector3.one * radius;
            outer.transform.localScale = Vector3.one * radius * Mathf.Lerp(2.2f, 1, Mathf.Clamp01(progress));
            Color color = outcome == FightQteNote.Result.Hit ? new Color(.3f, 1, .5f) :
                outcome == FightQteNote.Result.Miss ? new Color(1, .3f, .3f) : Color.white;
            outer.startColor = outer.endColor = label.color = color;
            inner.startColor = inner.endColor = new Color(1, .85f, .25f);
            label.text = outcome == FightQteNote.Result.Hit ? "OK" : outcome == FightQteNote.Result.Miss ? "MISS" : "A";
        }
    }
}
