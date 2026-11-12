using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    // Scene-only virtual fill lights. No Unity Light, shared material edits or shadow passes.
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class Art2CharacterFillLighting : MonoBehaviour
    {
        [Serializable]
        public sealed class Fill
        {
            public bool enabled = true;
            public Color color = Color.white;
            [Range(0, 2)] public float intensity = .18f;
            [Tooltip("World-space direction towards the light, not its travel direction.")]
            public Vector3 direction = new Vector3(0, 1, -1);
            [Range(0, 1), Tooltip("Soft diffuse wrap; higher values keep the unlit side more readable.")]
            public float softness = .6f;
        }
        public bool enableFill = true;
        public Fill heroes = new Fill { color = new Color(1, .94f, .85f), intensity = .22f };
        public Fill enemies = new Fill { color = new Color(.85f, .93f, 1), intensity = .16f };

        private sealed class Entry
        {
            public SpriteRenderer renderer;
            public FightUnitSlot.UnitTeam team;
            public Color originalColor;
            public Vector4 originalDirection;
        }
        private static readonly int ColorId = Shader.PropertyToID("_CharacterFillColor");
        private static readonly int DirectionId = Shader.PropertyToID("_CharacterFillDirection");
        private readonly List<Entry> entries = new();
        private MaterialPropertyBlock block;

        public void Register(SpriteRenderer renderer, FightUnitSlot.UnitTeam team)
        {
            if (renderer == null) return;
            block ??= new MaterialPropertyBlock();
            foreach (var entry in entries)
                if (entry.renderer == renderer) { entry.team = team; return; }
            renderer.GetPropertyBlock(block);
            entries.Add(new Entry { renderer = renderer, team = team,
                originalColor = block.GetColor(ColorId), originalDirection = block.GetVector(DirectionId) });
        }

        private void LateUpdate()
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry.renderer == null) { entries.RemoveAt(i); continue; }
                var fill = entry.team == FightUnitSlot.UnitTeam.Hero ? heroes : enemies;
                Color tint = fill.color;
                tint.a = enableFill && fill.enabled ? Mathf.Max(0, fill.intensity) : 0;
                Vector3 direction = fill.direction.sqrMagnitude > .0001f ? fill.direction.normalized : Vector3.back;
                // Keep sprite texture, floor recovery and all other presentation properties intact.
                entry.renderer.GetPropertyBlock(block);
                block.SetColor(ColorId, tint);
                block.SetVector(DirectionId, new Vector4(direction.x, direction.y, direction.z, Mathf.Clamp01(fill.softness)));
                entry.renderer.SetPropertyBlock(block);
            }
        }

        private void OnDisable()
        {
            foreach (var entry in entries)
            {
                if (entry.renderer == null) continue;
                entry.renderer.GetPropertyBlock(block);
                block.SetColor(ColorId, entry.originalColor);
                block.SetVector(DirectionId, entry.originalDirection);
                entry.renderer.SetPropertyBlock(block);
            }
        }
    }
}
