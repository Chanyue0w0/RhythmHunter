using System.Collections;
using UnityEngine;

namespace RhythmHunter.RhythmArena
{
    public sealed class CombatResolver : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private RhythmClock rhythmClock;
        [SerializeField] private PlayerCombatController player;
        [SerializeField] private EnemyPatternController enemyPattern;

        [Header("Prototype HP")]
        [SerializeField, Min(1)] private int heroMaxHp = 5;
        [SerializeField, Min(1)] private int enemyMaxHp = 100;
        [SerializeField, Min(0.1f)] private float resetDelaySeconds = 1f;

        [Header("World-space Feedback")]
        [SerializeField] private Transform enemyVisual;
        [SerializeField] private Renderer[] heroRenderers;
        [SerializeField] private Renderer[] enemyRenderers;
        [SerializeField] private Transform heroHpFill;
        [SerializeField] private Transform enemyHpFill;
        [SerializeField] private TextMesh statusText;
        [SerializeField] private TextMesh rhythmText;

        private int heroHp;
        private int enemyHp;
        private bool combatActive = true;
        private Vector3 enemyRestPosition;
        private float messageVisibleUntil;

        public int HeroHp => heroHp;
        public int EnemyHp => enemyHp;
        public bool CombatActive => combatActive;

        private void Awake()
        {
            heroHp = heroMaxHp;
            enemyHp = enemyMaxHp;
            enemyRestPosition = enemyVisual != null ? enemyVisual.localPosition : Vector3.zero;
            UpdateHpBars();
        }

        private void Update()
        {
            if (rhythmText != null && rhythmClock != null)
            {
                string source = rhythmClock.IsUsingFmod ? "FMOD" : "WAITING / FALLBACK";
                rhythmText.text = $"{rhythmClock.Bpm:0} BPM   BEAT {rhythmClock.AbsoluteBeatTime:0.00}   {source}";
            }

            if (statusText != null && Time.unscaledTime > messageVisibleUntil && combatActive)
                statusText.text = "READ THE RING. RED IS DANGER.";
        }

        public void Configure(
            RhythmClock clock,
            PlayerCombatController combatPlayer,
            EnemyPatternController pattern,
            Transform enemy,
            Renderer[] heroParts,
            Renderer[] enemyParts,
            Transform heroFill,
            Transform enemyFill,
            TextMesh status,
            TextMesh rhythmReadout)
        {
            rhythmClock = clock;
            player = combatPlayer;
            enemyPattern = pattern;
            enemyVisual = enemy;
            heroRenderers = heroParts;
            enemyRenderers = enemyParts;
            heroHpFill = heroFill;
            enemyHpFill = enemyFill;
            statusText = status;
            rhythmText = rhythmReadout;
            enemyRestPosition = enemyVisual != null ? enemyVisual.localPosition : Vector3.zero;
            UpdateHpBars();
        }

        public void ResolveEnemyAttack(double attackBeat, int damage)
        {
            if (!combatActive)
                return;

            StartCoroutine(PlayEnemyLunge());
            if (player != null && player.TryGuardAgainst(attackBeat, out bool perfectParry))
            {
                if (perfectParry)
                {
                    enemyPattern.ShiftNextAttack(0.5f);
                    ShowMessage("PERFECT PARRY  NEXT ATTACK +0.5 BEAT");
                }
                else
                {
                    ShowMessage("BLOCK");
                }

                return;
            }

            heroHp = Mathf.Max(0, heroHp - Mathf.Max(1, damage));
            UpdateHpBars();
            StartCoroutine(FlashRenderers(heroRenderers));
            ShowMessage($"HERO HIT  HP {heroHp}/{heroMaxHp}");

            if (heroHp <= 0)
                StartCoroutine(ResetCombatAfterDelay("HERO DOWN — RESET"));
        }

        public void DamageEnemy(
            int damage,
            PlayerCombatController.ActionType actionType,
            RhythmClock.TimingGrade timingGrade)
        {
            if (!combatActive)
                return;

            enemyHp = Mathf.Max(0, enemyHp - Mathf.Max(0, damage));
            UpdateHpBars();
            StartCoroutine(FlashRenderers(enemyRenderers));
            ShowMessage($"{actionType.ToString().ToUpperInvariant()}  {timingGrade.ToString().ToUpperInvariant()}  {damage} DMG");

            if (enemyHp <= 0)
                StartCoroutine(ResetCombatAfterDelay("ENEMY DOWN — RESET"));
        }

        public void ShowMessage(string message, float seconds = 1.25f)
        {
            if (statusText == null)
                return;

            statusText.text = message;
            messageVisibleUntil = Time.unscaledTime + seconds;
        }

        private IEnumerator ResetCombatAfterDelay(string message)
        {
            if (!combatActive)
                yield break;

            combatActive = false;
            ShowMessage(message, resetDelaySeconds);
            yield return new WaitForSecondsRealtime(resetDelaySeconds);

            rhythmClock.ResetCombatTimeline();
            enemyPattern.ResetPattern();
            player.ResetPlayer();
            heroHp = heroMaxHp;
            enemyHp = enemyMaxHp;
            combatActive = true;
            UpdateHpBars();
            ShowMessage("COMBAT RESET", 0.8f);
        }

        private IEnumerator PlayEnemyLunge()
        {
            if (enemyVisual == null)
                yield break;

            Vector3 start = enemyRestPosition;
            const float duration = 0.16f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                enemyVisual.localPosition = start + Vector3.right * (Mathf.Sin(normalized * Mathf.PI) * 0.42f);
                yield return null;
            }

            enemyVisual.localPosition = start;
        }

        private static IEnumerator FlashRenderers(Renderer[] renderers)
        {
            if (renderers == null)
                yield break;

            for (int flash = 0; flash < 2; flash++)
            {
                SetRenderersEnabled(renderers, false);
                yield return new WaitForSecondsRealtime(0.06f);
                SetRenderersEnabled(renderers, true);
                yield return new WaitForSecondsRealtime(0.06f);
            }
        }

        private static void SetRenderersEnabled(Renderer[] renderers, bool enabled)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = enabled;
            }
        }

        private void UpdateHpBars()
        {
            if (heroHpFill != null)
                heroHpFill.localScale = new Vector3(Mathf.Max(0.001f, heroHp / (float)heroMaxHp), 0.07f, 1f);
            if (enemyHpFill != null)
                enemyHpFill.localScale = new Vector3(Mathf.Max(0.001f, enemyHp / (float)enemyMaxHp), 0.07f, 1f);
        }
    }
}
