using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RhythmHunter.FightDemo
{
    /// <summary>A reusable damage popup. Glyph references and particle components are cached.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class FightDamageNumber : MonoBehaviour
    {
        internal sealed class Glyph
        {
            internal int digit;
            internal GameObject root;
            internal ParticleSystem[] particles;
            internal ParticleSystemRenderer[] renderers;
            internal SpriteRenderer point;
            internal Vector3 scale;
            internal SpriteRenderer[] sprites;
            internal Color[] spriteColors;
        }

        private FightCombatController controller;
        private static readonly int PopupOpacity = Shader.PropertyToID("_PopupOpacity");
        private MaterialPropertyBlock opacityProperties;
        private void SetOpacity(SpriteRenderer renderer, Color original, float alpha)
        {
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(PopupOpacity))
            {
                renderer.color = original;
                opacityProperties ??= new MaterialPropertyBlock();
                renderer.GetPropertyBlock(opacityProperties);
                opacityProperties.SetFloat(PopupOpacity, alpha);
                renderer.SetPropertyBlock(opacityProperties);
            }
            else
            {
                original.a *= alpha;
                renderer.color = original;
            }
        }
        private SortingGroup sortingGroup;
        internal void SetSortingOrder(int order)
        {
            if (sortingGroup == null) sortingGroup = gameObject.GetComponent<SortingGroup>();
            if (sortingGroup == null) sortingGroup = gameObject.AddComponent<SortingGroup>();
            sortingGroup.sortingOrder = order;
        }
        private readonly List<Glyph> glyphs = new(8);
        private Vector3 origin;
        private float elapsed, lifetime, rise;
        private float horizontalTravel, jumpHeight;
        internal void SetMotion(float horizontal, float height) { horizontalTravel = horizontal; jumpHeight = height; }
        private bool paused;
        private Camera viewCamera;
        internal bool Leased { get; private set; }

        internal void Begin(FightCombatController fight, Vector3 position, float size, float seconds, float riseDistance, Camera camera = null)
        {
            controller = fight;
            viewCamera = camera;
            transform.rotation = camera != null ? camera.transform.rotation : Quaternion.identity;
            transform.position = origin = position;
            transform.localScale = Vector3.one * size;
            elapsed = 0;
            lifetime = seconds;
            rise = riseDistance;
            paused = false;
            horizontalTravel = 0; jumpHeight = .15f;
            Leased = true;
            gameObject.SetActive(true);
        }

        internal void Add(Glyph glyph, float x, int sortingOrder, uint seed)
        {
            glyphs.Add(glyph);
            glyph.root.transform.SetParent(transform, false);
            glyph.root.transform.localPosition = new Vector3(x, glyph.point != null ? -.12f : 0, 0);
            glyph.root.transform.localRotation = Quaternion.identity;
            glyph.root.transform.localScale = glyph.scale;
            if (glyph.point != null)
            {
                glyph.point.color = glyph.spriteColors[0];
                glyph.point.sortingOrder = sortingOrder;
            }
            for (int i = 0; i < glyph.sprites.Length; i++)
            {
                SetOpacity(glyph.sprites[i], glyph.spriteColors[i], 1f);
                glyph.sprites[i].sortingOrder = sortingOrder;
            }
            glyph.root.SetActive(true);
            for (int i = 0; i < glyph.particles.Length; i++)
            {
                var particles = glyph.particles[i];
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.randomSeed = seed;
                var main = particles.main;
                main.startLifetime = lifetime;
                if (glyph.renderers[i] != null) glyph.renderers[i].sortingOrder = sortingOrder;
                particles.Play(false);
                particles.Emit(1);
            }
        }

        internal void ReturnGlyphs()
        {
            if (!Leased) return;
            Leased = false;
            foreach (var glyph in glyphs) controller.ReturnDamageGlyph(glyph);
            glyphs.Clear();
            paused = false;
            elapsed = 0;
            gameObject.SetActive(false);
            viewCamera = null;
            transform.localRotation = Quaternion.identity;
        }

        private void Update() => Advance(Time.deltaTime);
        private void LateUpdate()
        {
            if (!Leased || viewCamera == null) return;
            transform.rotation = viewCamera.transform.rotation;
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            float t = Mathf.Clamp01(elapsed / lifetime);
            Vector3 up = viewCamera != null ? viewCamera.transform.up : Vector3.up;
            Vector3 right = viewCamera != null ? viewCamera.transform.right : Vector3.right;
            transform.position = origin + right * (horizontalTravel * t) + up * (rise * t + Mathf.Sin(t * Mathf.PI) * jumpHeight);
        }

        // Also permits deterministic editor validation without waiting for real frames.
        internal void Advance(float deltaTime)
        {
            if (!Leased || controller == null) return;
            bool freeze = controller.IsPaused || controller.TimingCalibrationActive;
            if (paused != freeze)
            {
                paused = freeze;
                foreach (var glyph in glyphs)
                    foreach (var particles in glyph.particles)
                        if (freeze) particles.Pause(false); else particles.Play(false);
            }
            if (freeze) return;
            elapsed += deltaTime;
            if (elapsed >= lifetime) { controller.ReturnDamageNumber(this); return; }
            float t = Mathf.Clamp01(elapsed / lifetime);
            UpdatePosition();
            // Fade immediately so older hits recede as new numbers appear.
            float alpha = 1 - t;
            foreach (var glyph in glyphs)
                for (int i = 0; i < glyph.sprites.Length; i++)
                {
                    SetOpacity(glyph.sprites[i], glyph.spriteColors[i], alpha);
                }
        }
    }
}
