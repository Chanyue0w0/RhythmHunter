using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Expanding, rotating shield pulse used to make the guard action visually distinct.
    /// </summary>
    public sealed class FightGuardEffect : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Color color;
        private float duration;
        private float elapsed;
        private float startScale;
        private float endScale;
        private float rotationSpeed;

        public void Play(Color shieldColor, float seconds, float fromScale, float toScale, float degreesPerSecond)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            color = shieldColor;
            duration = Mathf.Max(0.1f, seconds);
            startScale = Mathf.Max(0f, fromScale);
            endScale = Mathf.Max(startScale, toScale);
            rotationSpeed = degreesPerSecond;
            elapsed = 0f;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - progress) * (1f - progress);
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, eased);
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

            if (spriteRenderer != null)
            {
                Color shown = color;
                shown.a *= 1f - progress;
                spriteRenderer.color = shown;
            }

            if (progress >= 1f)
                Destroy(gameObject);
        }
    }
}
