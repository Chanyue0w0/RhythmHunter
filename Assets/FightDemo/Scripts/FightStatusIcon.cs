using UnityEngine;

namespace RhythmHunter.FightDemo
{
    // Presentation only. The controller's pool owns lifetime and recycling.
    [DefaultExecutionOrder(100)]
    public sealed class FightStatusIcon : MonoBehaviour
    {
        public enum PositionAnchor { SpriteTop, ActorOrigin, ImpactAnchor }
        [Tooltip("SpriteTop includes transparent sprite padding. ActorOrigin gives a stable authored position.")]
        [SerializeField] private PositionAnchor positionAnchor;
        [Tooltip("Offset in world axes for 2D; camera axes when Status Icon Camera is assigned. Y=up; negative Z=toward camera.")]
        [SerializeField] private Vector3 headOffset = new(0, 0, -.5f);
        [Header("Status Trigger Pulse")]
        [SerializeField, Min(.01f)] private float triggerPulseBeats = .35f;
        [SerializeField, Min(1)] private float triggerPulseScale = 1.6f;
        [SerializeField, Min(.01f)] private float popupLifetime = .5f;
        [Tooltip("Screen-space horizontal travel: positive = right, negative = left.")]
        [SerializeField] private float popupRightDistance = .6f;
        [SerializeField, Min(0)] private float popupJumpHeight = .25f;
        [Header("Aimed State Artwork")]
        [SerializeField] private Sprite aimedGreenSprite;
        [SerializeField] private Sprite aimedGoldSprite;
        [SerializeField] private Color aimedGreenTint = new(.4f, 1, .4f, 1);
        [SerializeField] private Color aimedGoldTint = new(1, .78f, .15f, 1);
        private SpriteRenderer[] sprites;
        private Color[] colors;
        private SpriteRenderer targetSprite;
        private Vector3 origin;
        private float elapsed;
        private Vector3 baseScale;
        private float pulseElapsed;
        private bool pulsing;
        private Camera viewCamera;
        private Vector3 sceneOffset;
        private Quaternion baseRotation;
        internal bool Finishing { get; private set; }
        internal FightUnitSlot Target { get; private set; }
        internal int Kind { get; private set; }
        internal bool IsPopup => Kind == 0 || Kind == 3;
        internal float Lane { get; set; }

        internal void Initialize(int kind)
        {
            Kind = kind;
            baseScale = transform.localScale;
            baseRotation = transform.localRotation;
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            colors = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) colors[i] = sprites[i].color;
        }

        internal void Begin(FightUnitSlot target, float lane, Camera camera = null, Vector3 offset = default)
        {
            viewCamera = camera;
            sceneOffset = offset;
            Target = target; Lane = lane; elapsed = 0;
            pulsing = Finishing = false;
            transform.localScale = baseScale;
            targetSprite = target.CombatAnimator != null ? target.CombatAnimator.TargetRenderer : null;
            if (targetSprite == null && target.ActorInstance != null)
                targetSprite = target.ActorInstance.GetComponentInChildren<SpriteRenderer>();
            RestoreAlpha();
            origin = HeadPosition();
            UpdatePose();
            gameObject.SetActive(true);
        }

        private Vector3 HeadPosition()
        {
            if (viewCamera != null && positionAnchor == PositionAnchor.SpriteTop && Target != null && Target.HeadAnchor != null)
                return Target.HeadAnchor.position;
            if (Target != null && positionAnchor == PositionAnchor.ActorOrigin)
                return Target.ActorInstance != null ? Target.ActorInstance.transform.position : Target.transform.position;
            if (Target != null && positionAnchor == PositionAnchor.ImpactAnchor)
                return Target.ImpactEffectAnchor.position;
            if (targetSprite != null && targetSprite.sprite != null)
            {
                if (viewCamera != null)
                {
                    // A rotated world AABB's top/centre is not the sprite's actual head.
                    Bounds bounds = targetSprite.sprite.bounds;
                    Vector3 top = bounds.center + Vector3.up * bounds.extents.y;
                    if (targetSprite.flipX) top.x = -top.x;
                    if (targetSprite.flipY) top.y = -top.y;
                    return targetSprite.transform.TransformPoint(top);
                }
                return new Vector3(targetSprite.bounds.center.x, targetSprite.bounds.max.y, targetSprite.bounds.center.z);
            }
            return Target != null ? Target.ImpactEffectAnchor.position : origin;
        }

        internal void Trigger(bool consumed)
        {
            pulsing = true; Finishing = consumed; pulseElapsed = 0;
            RestoreAlpha();
            transform.localScale = baseScale;
        }

        internal void SetAimedAppearance(bool gold)
        {
            if (Kind != 4) return;
            var sprite = gold ? aimedGoldSprite : aimedGreenSprite;
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprite != null) sprites[i].sprite = sprite;
                colors[i] = gold ? aimedGoldTint : aimedGreenTint;
            }
            if (!pulsing) RestoreAlpha();
        }

        internal bool Advance(float seconds, float beats)
        {
            if (!IsPopup)
            {
                if (Target == null || !Target.HasCharacter || Target.CurrentHp <= 0) return false;
                UpdatePose();
                if (pulsing)
                {
                    pulseElapsed += beats;
                    float p = Mathf.Clamp01(pulseElapsed / Mathf.Max(.01f, triggerPulseBeats));
                    // Fast expansion, followed by a shrink and fade. Gameplay state
                    // is already resolved; this pulse never delays status consumption.
                    float size = p < .3f ? Mathf.Lerp(1, triggerPulseScale, p / .3f)
                        : Mathf.Lerp(triggerPulseScale, Kind == 4 ? 1 : .6f, (p - .3f) / .7f);
                    transform.localScale = baseScale * size;
                    SetAlpha(Kind == 4 && !Finishing ? 1 : 1 - Mathf.InverseLerp(.45f, 1, p));
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
            UpdatePose();
            SetAlpha(1 - t);
            return t < 1;
        }

        internal void Release()
        {
            gameObject.SetActive(false);
            Target = null; targetSprite = null; elapsed = 0;
            pulsing = Finishing = false;
            transform.localScale = baseScale;
            transform.localRotation = baseRotation;
            viewCamera = null;
            sceneOffset = Vector3.zero;
            RestoreAlpha();
        }

        // Runs after the Art2 camera's LateUpdate, including while the game is paused.
        private void LateUpdate() => UpdatePose();

        private void UpdatePose()
        {
            if (Target == null && !IsPopup) return;
            Vector3 offset = headOffset + sceneOffset + Vector3.right * Lane;
            if (IsPopup)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, popupLifetime));
                offset += new Vector3(popupRightDistance * t, popupJumpHeight * 4 * t * (1 - t), 0);
            }
            Vector3 anchor = IsPopup ? origin : HeadPosition();
            if (viewCamera != null)
            {
                transform.rotation = viewCamera.transform.rotation * baseRotation;
                offset = viewCamera.transform.TransformDirection(offset);
            }
            transform.position = anchor + offset;
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
