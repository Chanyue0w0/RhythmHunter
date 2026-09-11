using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Optional behaviour for a normal-attack VFX prefab. Prefabs without this component
    /// are still spawned and cleaned up by FightUnitSlot.
    /// </summary>
    public sealed class FightAttackEffect : MonoBehaviour
    {
        public enum VisualStyle
        {
            Normal,
            Light,
            Heavy,
            Skill
        }

        [SerializeField, Min(0f)] private float travelDistance = 2.2f;
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0.55f, 1f, 0f);

        private Vector3 origin;
        private Vector3 direction;
        private float lifetime;
        private float elapsed;
        private float baseScale = 1f;
        private float verticalArc;
        private float rotationSpeed;
        private SpriteRenderer spriteRenderer;
        private Color startColor = Color.white;
        private bool moveDuringPlayback = true;

        public void Play(Vector3 travelDirection, float duration)
        {
            Play(travelDirection, duration, VisualStyle.Normal, Color.white);
        }

        public void Play(Vector3 travelDirection, float duration, VisualStyle style, Color color)
        {
            origin = transform.position;
            direction = travelDirection.sqrMagnitude > 0f ? travelDirection.normalized : Vector3.left;
            moveDuringPlayback = true;
            ApplyStyle(style, duration);
            spriteRenderer = GetComponent<SpriteRenderer>();
            startColor = color;
            if (spriteRenderer != null)
                spriteRenderer.color = startColor;
            elapsed = 0f;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Plays this visual at an authored impact/cast anchor. This is deliberately
        /// visual-only: gameplay damage has already resolved from the beat judgement.
        /// </summary>
        public void PlayInPlace(float duration, VisualStyle style, Color color)
        {
            Play(Vector3.left, duration, style, color);
            moveDuringPlayback = false;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / lifetime);
            Vector3 perpendicular = new(-direction.y, direction.x, 0f);
            float arc = Mathf.Sin(progress * Mathf.PI) * verticalArc;
            transform.position = moveDuringPlayback
                ? origin + direction * (travelDistance * progress) + perpendicular * arc
                : origin;
            transform.localScale = Vector3.one * (baseScale * Mathf.Max(0f, scaleCurve.Evaluate(progress)));
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

            if (spriteRenderer != null)
            {
                Color color = startColor;
                color.a *= 1f - progress * progress;
                spriteRenderer.color = color;
            }

            if (progress >= 1f)
                Destroy(gameObject);
        }

        private void ApplyStyle(VisualStyle style, float requestedDuration)
        {
            switch (style)
            {
                case VisualStyle.Light:
                    lifetime = Mathf.Max(0.12f, requestedDuration * 0.62f);
                    travelDistance = 3.1f;
                    baseScale = 0.72f;
                    verticalArc = 0.08f;
                    rotationSpeed = 0f;
                    break;
                case VisualStyle.Heavy:
                    lifetime = Mathf.Max(0.32f, requestedDuration * 1.3f);
                    travelDistance = 2.5f;
                    baseScale = 2.15f;
                    verticalArc = 0.28f;
                    rotationSpeed = 520f;
                    break;
                case VisualStyle.Skill:
                    lifetime = Mathf.Max(0.4f, requestedDuration * 1.6f);
                    travelDistance = 3.5f;
                    baseScale = 2.75f;
                    verticalArc = 0.5f;
                    rotationSpeed = -680f;
                    break;
                default:
                    lifetime = Mathf.Max(0.05f, requestedDuration);
                    baseScale = 1f;
                    verticalArc = 0f;
                    rotationSpeed = 0f;
                    break;
            }
        }
    }
}
