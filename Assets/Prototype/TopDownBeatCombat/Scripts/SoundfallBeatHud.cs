using RhythmHunter.RhythmArena;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.TopDownBeatCombat
{
    public sealed class SoundfallBeatHud : MonoBehaviour
    {
        [SerializeField] private RhythmClock rhythmClock;
        [SerializeField] private TopDownBeatPlayer player;
        [SerializeField] private RectTransform[] beatTicks;
        [SerializeField] private Image[] beatTickImages;
        [SerializeField] private RectTransform hitLine;
        [SerializeField] private Text beatReadout;
        [SerializeField] private Text resultReadout;
        [SerializeField, Min(20f)] private float pixelsPerBeat = 92f;

        private float resultHideAt;
        private float feedbackPulse;
        private bool resultVisible;
        private Image hitLineImage;
        private Image trackImage;
        private Outline resultOutline;
        private Color trackBaseColor;
        private Color hitLineBaseColor;
        private Color feedbackColor;

        public int BeatPairCount => beatTicks == null ? 0 : beatTicks.Length / 2;

        private void Awake()
        {
            CacheVisuals();
            ResetPrompt();
        }

        private void OnEnable()
        {
            if (player != null)
                player.AttackPerformed += OnAttackPerformed;
        }

        private void OnDisable()
        {
            if (player != null)
                player.AttackPerformed -= OnAttackPerformed;
        }

        private void Update()
        {
            if (rhythmClock == null || !rhythmClock.IsReady)
                return;

            CacheVisuals();
            double now = rhythmClock.AbsoluteBeatTime;
            long currentBeat = (long)System.Math.Floor(now);
            long nextBeat = (long)System.Math.Ceiling(now);
            int pairCount = BeatPairCount;
            for (int pairIndex = 0; pairIndex < pairCount; pairIndex++)
            {
                long absoluteBeat = nextBeat + pairIndex;
                float distance = (float)(absoluteBeat - now) * pixelsPerBeat;
                bool downbeat = Mod(absoluteBeat, 4) == 0;
                int leftIndex = pairIndex * 2;
                int rightIndex = leftIndex + 1;
                UpdateBeatPoint(leftIndex, -distance, downbeat, true);
                UpdateBeatPoint(rightIndex, distance, downbeat, false);
            }

            for (int i = pairCount * 2; i < beatTicks.Length; i++)
                beatTicks[i].gameObject.SetActive(false);

            float nearestDistance = Mathf.Abs((float)(now - System.Math.Round(now)));
            float beatPulse = Mathf.Clamp01(1f - nearestDistance * 5f);
            feedbackPulse = Mathf.MoveTowards(feedbackPulse, 0f, Time.unscaledDeltaTime * 2.4f);
            float pulse = 1f + beatPulse * 0.35f + feedbackPulse * 0.9f;
            if (hitLine != null)
                hitLine.localScale = new Vector3(pulse, pulse, 1f);
            if (hitLineImage != null)
                hitLineImage.color = Color.Lerp(hitLineBaseColor, feedbackColor, feedbackPulse);
            if (resultReadout != null)
                resultReadout.rectTransform.localScale = Vector3.one * (1f + feedbackPulse * 0.48f);
            if (trackImage != null)
                trackImage.color = Color.Lerp(trackBaseColor, feedbackColor, feedbackPulse * 0.78f);

            if (beatReadout != null)
            {
                int beatInBar = Mod(currentBeat, 4) + 1;
                beatReadout.text = $"FMOD  {rhythmClock.Bpm:0} BPM     BEAT {beatInBar}/4";
            }

            if (resultReadout != null && resultVisible && Time.unscaledTime >= resultHideAt)
                ResetPrompt();
        }

        public void Configure(
            RhythmClock clock,
            TopDownBeatPlayer controlledPlayer,
            RectTransform[] ticks,
            Image[] tickImages,
            RectTransform centerLine,
            Text beatText,
            Text resultText)
        {
            rhythmClock = clock;
            player = controlledPlayer;
            beatTicks = ticks;
            beatTickImages = tickImages;
            hitLine = centerLine;
            beatReadout = beatText;
            resultReadout = resultText;
        }

        private void OnAttackPerformed(TopDownBeatPlayer.AttackResult result)
        {
            if (resultReadout == null)
                return;

            CacheVisuals();
            string hit = result.Hit ? $"{result.Damage} DAMAGE" : "NO TARGET";
            bool successfulTiming = result.Hit && result.Grade != RhythmClock.TimingGrade.Offbeat;
            string emphasis = successfulTiming ? "◆" : "";
            resultReadout.text = $"{emphasis} {result.Grade.ToString().ToUpperInvariant()}!  //  {hit} {emphasis}";
            feedbackColor = result.Grade == RhythmClock.TimingGrade.Perfect
                ? new Color(1f, 0.82f, 0.12f, 1f)
                : result.Grade == RhythmClock.TimingGrade.Good
                    ? new Color(0.15f, 1f, 0.92f, 1f)
                    : new Color(1f, 0.35f, 0.35f, 1f);
            resultReadout.color = feedbackColor;
            resultReadout.fontSize = successfulTiming ? 32 : 24;
            if (resultOutline != null)
            {
                resultOutline.enabled = successfulTiming;
                resultOutline.effectColor = new Color(feedbackColor.r, feedbackColor.g, feedbackColor.b, 0.7f);
            }
            feedbackPulse = successfulTiming ? 1f : 0.35f;
            resultVisible = true;
            resultHideAt = Time.unscaledTime + (successfulTiming ? 1.15f : 0.8f);
        }

        private void UpdateBeatPoint(int index, float x, bool downbeat, bool leftSide)
        {
            if (beatTicks == null || index < 0 || index >= beatTicks.Length || beatTicks[index] == null)
                return;

            RectTransform point = beatTicks[index];
            if (!point.gameObject.activeSelf)
                point.gameObject.SetActive(true);
            point.anchoredPosition = new Vector2(x, 0f);
            float size = downbeat ? 18f : 12f;
            point.sizeDelta = new Vector2(size, size);
            point.localRotation = Quaternion.Euler(0f, 0f, 45f);

            if (beatTickImages == null || index >= beatTickImages.Length || beatTickImages[index] == null)
                return;
            beatTickImages[index].color = downbeat
                ? new Color(1f, 0.82f, 0.12f, 1f)
                : leftSide ? new Color(0.2f, 0.9f, 1f, 1f) : new Color(1f, 0.36f, 0.72f, 1f);
        }

        private void CacheVisuals()
        {
            if (hitLineImage == null && hitLine != null)
            {
                hitLineImage = hitLine.GetComponent<Image>();
                if (hitLineImage != null)
                    hitLineBaseColor = hitLineImage.color;
            }
            if (trackImage == null && hitLine != null && hitLine.parent != null)
            {
                trackImage = hitLine.parent.GetComponent<Image>();
                if (trackImage != null)
                    trackBaseColor = trackImage.color;
            }
            if (resultOutline == null && resultReadout != null)
            {
                resultOutline = resultReadout.GetComponent<Outline>();
                if (resultOutline == null)
                    resultOutline = resultReadout.gameObject.AddComponent<Outline>();
                resultOutline.effectDistance = new Vector2(2f, -2f);
                resultOutline.enabled = false;
            }
            if (feedbackColor.a <= 0f)
                feedbackColor = new Color(1f, 0.82f, 0.12f, 1f);
        }

        private void ResetPrompt()
        {
            if (resultReadout == null)
                return;

            resultVisible = false;
            resultReadout.text = "ATTACK WHEN BOTH BEAT POINTS MERGE";
            resultReadout.color = Color.white;
            resultReadout.fontSize = 22;
            if (resultOutline != null)
                resultOutline.enabled = false;
        }

        private static int Mod(long value, int modulo)
        {
            long result = value % modulo;
            return (int)(result < 0 ? result + modulo : result);
        }
    }
}
