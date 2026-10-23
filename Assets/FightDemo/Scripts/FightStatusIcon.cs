using UnityEngine;

namespace RhythmHunter.FightDemo
{
    // Presentation only. The controller's pool owns lifetime and recycling.
    public sealed class FightStatusIcon : MonoBehaviour
    {
        public enum PositionAnchor { SpriteTop, ActorOrigin, ImpactAnchor }
        [Tooltip("SpriteTop includes transparent sprite padding. ActorOrigin gives a stable authored position.")]
        [SerializeField] private PositionAnchor positionAnchor;
        [Tooltip("World-space offset. Lower Y (including negative values) moves the icon down.")]
        [SerializeField] private Vector3 headOffset = new(0, 0, -.5f);
        [Header("Status Trigger Pulse")]
        [SerializeField, Min(.01f)] private float triggerPulseBeats = .35f;
        [SerializeField, Min(1)] private float triggerPulseScale = 1.6f;
        [SerializeField, Min(.01f)] private float popupLifetime = .5f;
        [SerializeField, Min(0)] private float popupRightDistance = .6f;
        [SerializeField, Min(0)] private float popupJumpHeight = .25f;
        private SpriteRenderer[] sprites;
        private Color[] colors;
        private SpriteRenderer targetSprite;
        private Vector3 origin;
        private float elapsed;
        private Vector3 baseScale;
        private float pulseElapsed;
        private bool pulsing;
        internal bool Finishing { get; private set; }
        internal FightUnitSlot Target { get; private set; }
        internal int Kind { get; private set; }
        internal float Lane { get; set; }

        internal void Initialize(int kind)
        {
            Kind = kind;
            baseScale = transform.localScale;
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            colors = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) colors[i] = sprites[i].color;
        }

        internal void Begin(FightUnitSlot target, float lane)
        {
            Target = target; Lane = lane; elapsed = 0;
            pulsing = Finishing = false;
            transform.localScale = baseScale;
            targetSprite = target.CombatAnimator != null ? target.CombatAnimator.TargetRenderer : null;
            if (targetSprite == null && target.ActorInstance != null)
                targetSprite = target.ActorInstance.GetComponentInChildren<SpriteRenderer>();
            RestoreAlpha();
            origin = HeadPosition() + headOffset;
            transform.position = origin + Vector3.right * lane;
            gameObject.SetActive(true);
        }

        private Vector3 HeadPosition()
        {
            if (Target != null && positionAnchor == PositionAnchor.ActorOrigin)
                return Target.ActorInstance != null ? Target.ActorInstance.transform.position : Target.transform.position;
            if (Target != null && positionAnchor == PositionAnchor.ImpactAnchor)
                return Target.ImpactEffectAnchor.position;
            if (targetSprite != null && targetSprite.sprite != null)
                return new Vector3(targetSprite.bounds.center.x, targetSprite.bounds.max.y, targetSprite.bounds.center.z);
            return Target != null ? Target.ImpactEffectAnchor.position : origin;
        }

        internal void Trigger(bool consumed)
        {
            pulsing = true; Finishing = consumed; pulseElapsed = 0;
            RestoreAlpha();
            transform.localScale = baseScale;
        }

        internal bool Advance(float seconds, float beats)
        {
            if (Kind != 0)
            {
                if (Target == null || !Target.HasCharacter || Target.CurrentHp <= 0) return false;
                transform.position = HeadPosition() + headOffset + Vector3.right * Lane;
                if (pulsing)
                {
                    pulseElapsed += beats;
                    float p = Mathf.Clamp01(pulseElapsed / Mathf.Max(.01f, triggerPulseBeats));
                    // Fast expansion, followed by a shrink and fade. Gameplay state
                    // is already resolved; this pulse never delays status consumption.
                    float size = p < .3f ? Mathf.Lerp(1, triggerPulseScale, p / .3f)
                        : Mathf.Lerp(triggerPulseScale, .6f, (p - .3f) / .7f);
                    transform.localScale = baseScale * size;
                    SetAlpha(1 - Mathf.InverseLerp(.45f, 1, p));
                    if (p >= 1)
                    {
                        if (Finishing) return false;
                        pulsing = false;
                        transform.localScale = baseScale;
                        RestoreAlpha(); // Bleeding is still active until its final tick.
                    }
                }
                return true;
            }
            elapsed += seconds;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, popupLifetime));
            transform.position = origin + new Vector3(popupRightDistance * t, popupJumpHeight * 4 * t * (1 - t), 0);
            SetAlpha(1 - t);
            return t < 1;
        }

        internal void Release()
        {
            gameObject.SetActive(false);
            Target = null; targetSprite = null; elapsed = 0;
            pulsing = Finishing = false;
            transform.localScale = baseScale;
            RestoreAlpha();
        }

        private void RestoreAlpha()
        {
            SetAlpha(1);
        }

        private void SetAlpha(float alpha)
        {
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] != null) { var color = colors[i]; color.a *= alpha; sprites[i].color = color; }
        }
    }
}
