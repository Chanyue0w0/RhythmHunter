using System.Collections;
using RhythmHunter.RhythmArena;
using UnityEngine;

namespace RhythmHunter.TopDownBeatCombat
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class BeatTrainingDummy : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHp = 200;
        [SerializeField, Min(0.1f)] private float respawnDelay = 1.2f;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Transform hpFill;
        [SerializeField, Min(0.1f)] private float hpFillWidth = 1.65f;
        [SerializeField] private TextMesh hpText;
        [SerializeField] private TextMesh damageText;

        private int currentHp;
        private Vector3 restPosition;

        public int CurrentHp => currentHp;
        public int LastDamage { get; private set; }
        public int HitCount { get; private set; }

        private void Awake()
        {
            restPosition = transform.position;
            ResetDummy();
        }

        public void Configure(SpriteRenderer visual, Transform fill, TextMesh hpLabel, TextMesh damageLabel)
        {
            spriteRenderer = visual;
            hpFill = fill;
            hpText = hpLabel;
            damageText = damageLabel;
        }

        public void TakeDamage(int amount, RhythmClock.TimingGrade grade)
        {
            if (currentHp <= 0)
                return;

            LastDamage = Mathf.Max(0, amount);
            HitCount++;
            currentHp = Mathf.Max(0, currentHp - LastDamage);
            UpdateReadout();

            if (damageText != null)
            {
                damageText.text = $"{LastDamage}  {grade.ToString().ToUpperInvariant()}";
                damageText.color = grade == RhythmClock.TimingGrade.Perfect
                    ? new Color(1f, 0.88f, 0.2f, 1f)
                    : Color.white;
            }

            StopAllCoroutines();
            StartCoroutine(PlayHitFeedback(currentHp <= 0));
        }

        public void ResetDummy()
        {
            StopAllCoroutines();
            currentHp = maxHp;
            LastDamage = 0;
            HitCount = 0;
            transform.position = restPosition;
            if (spriteRenderer != null)
                spriteRenderer.enabled = true;
            if (damageText != null)
                damageText.text = string.Empty;
            UpdateReadout();
        }

        private IEnumerator PlayHitFeedback(bool knockedOut)
        {
            Vector3 start = restPosition;
            for (int i = 0; i < 2; i++)
            {
                if (spriteRenderer != null)
                    spriteRenderer.enabled = false;
                transform.position = start + (Vector3)Random.insideUnitCircle * 0.08f;
                yield return new WaitForSecondsRealtime(0.05f);
                if (spriteRenderer != null)
                    spriteRenderer.enabled = true;
                yield return new WaitForSecondsRealtime(0.05f);
            }

            transform.position = start;
            yield return new WaitForSecondsRealtime(0.45f);
            if (damageText != null)
                damageText.text = string.Empty;

            if (!knockedOut)
                yield break;

            if (spriteRenderer != null)
                spriteRenderer.enabled = false;
            yield return new WaitForSecondsRealtime(respawnDelay);
            ResetDummy();
        }

        private void UpdateReadout()
        {
            if (hpText != null)
                hpText.text = $"TRAINING DUMMY  {currentHp}/{maxHp}";
            if (hpFill != null)
            {
                Vector3 scale = hpFill.localScale;
                scale.x = hpFillWidth * Mathf.Max(0.001f, currentHp / (float)maxHp);
                hpFill.localScale = scale;
            }
        }
    }
}
