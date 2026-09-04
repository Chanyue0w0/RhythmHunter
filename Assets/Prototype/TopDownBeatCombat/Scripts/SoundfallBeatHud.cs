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

            double now = rhythmClock.AbsoluteBeatTime;
            long centerBeat = (long)System.Math.Floor(now);
            for (int i = 0; i < beatTicks.Length; i++)
            {
                long absoluteBeat = centerBeat + i - beatTicks.Length / 2;
                float x = (float)(absoluteBeat - now) * pixelsPerBeat;
                beatTicks[i].anchoredPosition = new Vector2(x, 0f);
                bool downbeat = Mod(absoluteBeat, 4) == 0;
                beatTicks[i].sizeDelta = downbeat ? new Vector2(12f, 54f) : new Vector2(7f, 36f);
                if (i < beatTickImages.Length && beatTickImages[i] != null)
                    beatTickImages[i].color = downbeat ? new Color(0.2f, 0.9f, 1f, 1f) : Color.white;
            }

            float nearestDistance = Mathf.Abs((float)(now - System.Math.Round(now)));
            float pulse = 1f + Mathf.Clamp01(1f - nearestDistance * 5f) * 0.35f;
            if (hitLine != null)
                hitLine.localScale = new Vector3(pulse, pulse, 1f);

            if (beatReadout != null)
            {
                int beatInBar = Mod(centerBeat, 4) + 1;
                beatReadout.text = $"FMOD  {rhythmClock.Bpm:0} BPM     BEAT {beatInBar}/4";
            }

            if (resultReadout != null && Time.unscaledTime >= resultHideAt)
                resultReadout.text = "ATTACK ON THE CENTER LINE";
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

            string hit = result.Hit ? $"{result.Damage} DAMAGE" : "NO TARGET";
            resultReadout.text = $"{result.Grade.ToString().ToUpperInvariant()}  //  {hit}";
            resultReadout.color = result.Grade == RhythmClock.TimingGrade.Perfect
                ? new Color(1f, 0.88f, 0.2f, 1f)
                : Color.white;
            resultHideAt = Time.unscaledTime + 0.8f;
        }

        private static int Mod(long value, int modulo)
        {
            long result = value % modulo;
            return (int)(result < 0 ? result + modulo : result);
        }
    }
}
