using UnityEngine;

namespace RhythmHunter.FightDemo
{
    // Lightweight pooled prototype art. Prefab LineRenderer/material remain freely replaceable.
    [RequireComponent(typeof(LineRenderer))]
    public sealed class FightBossRhythmVisual : MonoBehaviour
    {
        private LineRenderer line;
        private float baseWidth;
        private MaterialPropertyBlock properties;
        private void Awake() { line = GetComponent<LineRenderer>(); baseWidth = line.widthMultiplier; }
        public void Present(Vector3 position, Quaternion facing, float scale, Color color)
        {
            if (!line) { line = GetComponent<LineRenderer>(); baseWidth = line.widthMultiplier; }
            transform.SetPositionAndRotation(position, facing);
            transform.localScale = Vector3.one * scale;
            line.startColor = line.endColor = color;
            properties ??= new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color); line.SetPropertyBlock(properties);
            line.widthMultiplier = baseWidth;
        }
    }
}
