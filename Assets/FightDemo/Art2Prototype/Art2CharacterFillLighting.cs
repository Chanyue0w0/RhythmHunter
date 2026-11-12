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
        [Header("Directional Sprite Edge Highlight")]
        public bool enableRim = true;
        public Color rimColor = new Color(1, .9f, .7f);
        [Range(0, 1)] public float rimIntensity = .18f;
        [Range(.25f, 3)] public float rimWidthPixels = 1;
        [Tooltip("Optional: use this light's direction projected into each sprite's plane.")]
        public Light rimLight;
        public Vector2 rimDirection = new Vector2(-1, 1);
        public Fill heroes = new Fill { color = new Color(1, .94f, .85f), intensity = .22f };
        public Fill enemies = new Fill { color = new Color(.85f, .93f, 1), intensity = .16f };

        private sealed class Entry
        {
            public SpriteRenderer renderer;
            public FightUnitSlot.UnitTeam team;
            public Color originalColor;
            public Vector4 originalDirection;
            public Color originalRim;
            public Vector4 originalRimParams;
        }
        private static readonly int ColorId = Shader.PropertyToID("_CharacterFillColor");
        private static readonly int DirectionId = Shader.PropertyToID("_CharacterFillDirection");
        private static readonly int RimId = Shader.PropertyToID("_CharacterRimColor");
        private static readonly int RimParamsId = Shader.PropertyToID("_CharacterRimParams");
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
                originalColor = block.GetColor(ColorId), originalDirection = block.GetVector(DirectionId),
                originalRim = block.GetColor(RimId), originalRimParams = block.GetVector(RimParamsId) });
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
                Vector2 edgeDirection = rimDirection;
                if (rimLight != null)
                {
                    Vector3 world = rimLight.type == LightType.Directional ? -rimLight.transform.forward : rimLight.transform.position - entry.renderer.bounds.center;
                    Vector3 local = entry.renderer.transform.InverseTransformDirection(world);
                    edgeDirection = new Vector2(local.x, local.y);
                }
                edgeDirection.Normalize();
                if (entry.renderer.flipX) edgeDirection.x *= -1;
                if (entry.renderer.flipY) edgeDirection.y *= -1;
                Color edgeTint = rimColor; edgeTint.a = enableRim ? rimIntensity : 0;
                block.SetColor(RimId, edgeTint);
                block.SetVector(RimParamsId, new Vector4(edgeDirection.x, edgeDirection.y, rimWidthPixels, 0));
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
                block.SetColor(RimId, entry.originalRim);
                block.SetVector(RimParamsId, entry.originalRimParams);
                entry.renderer.SetPropertyBlock(block);
            }
        }
    }
}
