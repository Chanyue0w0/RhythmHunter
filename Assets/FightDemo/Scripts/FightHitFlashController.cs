using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    [DisallowMultipleComponent, DefaultExecutionOrder(220)]
    public sealed class FightHitFlashController : MonoBehaviour
    {
        public enum FlashMode { WholeSprite, RightToLeft }
        [SerializeField] private bool flashEnabled = false;
        [SerializeField, Min(.001f)] private float durationSeconds = .08f;
        [SerializeField] private Color flashColor = Color.white;
        [Tooltip("0 = original artwork, 1 = solid flash color. Lower values retain sprite detail and reduce glare.")]
        [SerializeField, Range(0f, 1f)] private float flashStrength = .35f;
        [SerializeField] private FlashMode mode = FlashMode.WholeSprite;
        [SerializeField, Range(.05f, 1f)] private float sweepWidth = .35f;
        [Tooltip("Only these sprite parts flash; do not include shadows, particles or status icons.")]
        [SerializeField] private SpriteRenderer[] targets;
        [Tooltip("Optional. Right-to-left follows this camera; otherwise uses Camera.main.")]
        [SerializeField] private Camera viewCamera;
        private readonly List<SpriteRenderer> activeTargets = new();
        private readonly HashSet<SpriteRenderer> warned = new();
        private MaterialPropertyBlock properties;
        private FightUnitSlot slot;
        private float elapsed;
        public bool IsFlashing { get; private set; }
        private static readonly int Amount = Shader.PropertyToID("_FlashAmount");
        private static readonly int Tint = Shader.PropertyToID("_FlashColor");
        private static readonly int Sweep = Shader.PropertyToID("_FlashSweep");
        private static readonly int Axis = Shader.PropertyToID("_FlashAxis");

        public void Configure(SpriteRenderer[] renderers, Camera camera = null)
        { ClearFlash(); targets = renderers; viewCamera = camera; }

        private void Reset()
        {
            var animator = GetComponentInChildren<FightCharacterCombatAnimator>(true);
            targets = animator && animator.TargetRenderer
                ? new[] { animator.TargetRenderer } : GetComponentsInChildren<SpriteRenderer>(true);
        }
        private void OnEnable()
        {
            properties ??= new MaterialPropertyBlock();
            if (targets == null || targets.Length == 0) Reset();
            slot = GetComponentInParent<FightUnitSlot>();
            if (!slot) return;
            slot.DamageConfirmed += OnDamage;
            slot.HealthChanged += OnHealthChanged;
        }
        private void OnDamage(FightUnitSlot unit, FightUnitSlot.DamageKind kind)
        {
            if (unit.Team == FightUnitSlot.UnitTeam.Enemy && unit.CurrentHp > 0) TriggerFlash();
        }
        private void OnHealthChanged(FightUnitSlot unit, float hp, float maximum)
        { if (hp <= 0) ClearFlash(); }

        public void TriggerFlash()
        {
            if (!flashEnabled || !isActiveAndEnabled) return;
            ClearFlash();
            properties ??= new MaterialPropertyBlock();
            if (targets == null) return;
            foreach (var renderer in targets)
            {
                if (!renderer || activeTargets.Contains(renderer)) continue;
                if (!renderer.sharedMaterial || !renderer.sharedMaterial.HasProperty(Amount))
                {
                    if (warned.Add(renderer)) Debug.LogWarning("[HitFlash] Material does not support _FlashAmount; left unchanged: " + renderer.name, renderer);
                    continue;
                }
                activeTargets.Add(renderer);
            }
            if (activeTargets.Count == 0) return;
            if (!viewCamera) viewCamera = Camera.main;
            elapsed = 0;
            IsFlashing = true;
            ApplyFlash(0);
        }
        private void LateUpdate()
        {
            if (!IsFlashing) return;
            if (!flashEnabled) { ClearFlash(); return; }
            var fight = slot ? slot.CombatController : null;
            if (fight && (fight.IsPaused || fight.TimingCalibrationActive || (fight.BeatClock && fight.BeatClock.IsPaused))) return;
            Advance(Time.deltaTime);
        }
        private void Advance(float seconds)
        {
            elapsed += Mathf.Max(0, seconds);
            if (elapsed >= Mathf.Max(.001f, durationSeconds)) { ClearFlash(); return; }
            ApplyFlash(elapsed / Mathf.Max(.001f, durationSeconds));
        }
        private void ApplyFlash(float progress)
        {
            Vector3 right = viewCamera ? viewCamera.transform.right : Vector3.right;
            float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
            if (mode == FlashMode.RightToLeft)
                foreach (var renderer in activeTargets)
                {
                    if (!renderer || !renderer.sprite) continue;
                    Bounds b = renderer.sprite.bounds;
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 p = b.center + new Vector3((i & 1) == 0 ? -b.extents.x : b.extents.x,
                            (i & 2) == 0 ? -b.extents.y : b.extents.y, 0);
                        if (renderer.flipX) p.x = -p.x;
                        if (renderer.flipY) p.y = -p.y;
                        float x = Vector3.Dot(renderer.transform.TransformPoint(p), right);
                        minimum = Mathf.Min(minimum, x); maximum = Mathf.Max(maximum, x);
                    }
                }
            bool sweep = mode == FlashMode.RightToLeft && minimum <= maximum;
            Vector4 sweepData = sweep ? new Vector4(minimum, maximum, progress, sweepWidth) : Vector4.zero;
            foreach (var renderer in activeTargets)
            {
                if (!renderer) continue;
                // Merge only our properties; preserve animation texture, silhouette, focus and lighting blocks.
                renderer.GetPropertyBlock(properties);
                properties.SetFloat(Amount, Mathf.Clamp01(flashStrength));
                properties.SetColor(Tint, flashColor);
                properties.SetVector(Sweep, sweepData);
                properties.SetVector(Axis, right);
                renderer.SetPropertyBlock(properties);
            }
        }
        public void ClearFlash()
        {
            if (properties != null)
                foreach (var renderer in activeTargets)
                {
                    if (!renderer) continue;
                    renderer.GetPropertyBlock(properties);
                    properties.SetFloat(Amount, 0);
                    renderer.SetPropertyBlock(properties);
                }
            activeTargets.Clear(); IsFlashing = false; elapsed = 0;
        }
        private void OnDisable()
        {
            if (slot) { slot.DamageConfirmed -= OnDamage; slot.HealthChanged -= OnHealthChanged; }
            slot = null;
            ClearFlash();
        }
    }
}
