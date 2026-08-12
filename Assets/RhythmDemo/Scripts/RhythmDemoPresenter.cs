using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.RhythmDemo
{
    public sealed class RhythmDemoPresenter : MonoBehaviour
    {
        private static readonly Color Cyan = new(0.18f, 0.9f, 1f, 1f);
        private static readonly Color DimCyan = new(0.08f, 0.28f, 0.34f, 1f);
        private static readonly Color PerfectGreen = new(0.25f, 1f, 0.48f, 1f);
        private static readonly Color MissRed = new(1f, 0.28f, 0.34f, 1f);
        private static readonly Color WaitingYellow = new(1f, 0.78f, 0.2f, 1f);

        [Header("Dependencies")]
        [SerializeField] private FmodBeatClock beatClock;
        [SerializeField] private FmodRhythmJudge rhythmJudge;

        [Header("UI")]
        [SerializeField] private Text statusText;
        [SerializeField] private Text resultText;
        [SerializeField] private Text deltaText;
        [SerializeField] private Text timingText;
        [SerializeField] private Text statisticsText;
        [SerializeField] private Image beatPulseImage;
        [SerializeField] private Slider beatProgressSlider;
        [SerializeField] private Image[] beatDots;

        private int perfectCount;
        private int missCount;
        private double totalPerfectAbsoluteDeltaMs;
        private float pulseStrength;
        private float resultVisibility;

        public void Configure(
            FmodBeatClock clock,
            FmodRhythmJudge judge,
            Text status,
            Text result,
            Text delta,
            Text timing,
            Text statistics,
            Image pulseImage,
            Slider progressSlider,
            Image[] dots)
        {
            beatClock = clock;
            rhythmJudge = judge;
            statusText = status;
            resultText = result;
            deltaText = delta;
            timingText = timing;
            statisticsText = statistics;
            beatPulseImage = pulseImage;
            beatProgressSlider = progressSlider;
            beatDots = dots;
        }

        private void OnEnable()
        {
            if (beatClock != null)
            {
                beatClock.Beat += OnBeat;
                beatClock.PlaybackError += OnPlaybackError;
            }

            if (rhythmJudge != null)
                rhythmJudge.Judged += OnJudged;
        }

        private void Start()
        {
            SetResult("READY", WaitingYellow, "Press on the beat", 0.8f);
            UpdateStatistics();
        }

        private void OnDisable()
        {
            if (beatClock != null)
            {
                beatClock.Beat -= OnBeat;
                beatClock.PlaybackError -= OnPlaybackError;
            }

            if (rhythmJudge != null)
                rhythmJudge.Judged -= OnJudged;
        }

        private void Update()
        {
            UpdateBeatProgress();
            UpdatePulseAnimation();
            UpdateResultFade();
            UpdateTimingReadout();
        }

        private void OnBeat(FmodBeatClock.BeatSnapshot beat)
        {
            pulseStrength = 1f;

            if (statusText != null)
            {
                statusText.text = $"PLAYING  |  BAR {beat.Bar}  BEAT {beat.Beat}/{beat.TimeSignatureUpper}";
                statusText.color = Cyan;
            }

            if (beatDots == null)
                return;

            for (int i = 0; i < beatDots.Length; i++)
            {
                if (beatDots[i] != null)
                    beatDots[i].color = i == beat.Beat - 1 ? Cyan : DimCyan;
            }
        }

        private void OnJudged(FmodRhythmJudge.Result result)
        {
            switch (result.Judgement)
            {
                case FmodRhythmJudge.Grade.Perfect:
                    perfectCount++;
                    totalPerfectAbsoluteDeltaMs += System.Math.Abs(result.DeltaMs);
                    SetResult("PERFECT", PerfectGreen, FormatDelta(result.DeltaMs), 1.2f);
                    break;

                case FmodRhythmJudge.Grade.Miss:
                    missCount++;
                    string detail = result.DuplicateBeat
                        ? "Beat already used"
                        : FormatDelta(result.DeltaMs);
                    SetResult("MISS", MissRed, detail, 1.2f);
                    break;

                default:
                    SetResult("WAIT", WaitingYellow, result.Message, 1.0f);
                    break;
            }

            UpdateStatistics();
        }

        private void OnPlaybackError(string message)
        {
            if (statusText != null)
            {
                statusText.text = "FMOD ERROR";
                statusText.color = MissRed;
            }

            SetResult("ERROR", MissRed, message, 5f);
        }

        private void UpdateBeatProgress()
        {
            if (beatProgressSlider == null || beatClock == null)
                return;

            if (beatClock.TryGetBeatPhase(out float phase))
                beatProgressSlider.SetValueWithoutNotify(phase);
        }

        private void UpdatePulseAnimation()
        {
            if (beatPulseImage == null)
                return;

            pulseStrength = Mathf.MoveTowards(pulseStrength, 0f, Time.unscaledDeltaTime * 4.5f);
            float easedPulse = 1f - Mathf.Pow(1f - pulseStrength, 3f);
            beatPulseImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.28f, easedPulse);
            beatPulseImage.color = Color.Lerp(DimCyan, Cyan, easedPulse);
        }

        private void UpdateResultFade()
        {
            if (resultText == null || deltaText == null)
                return;

            resultVisibility = Mathf.MoveTowards(resultVisibility, 0f, Time.unscaledDeltaTime);
            float alpha = Mathf.Clamp01(resultVisibility * 3f);

            Color resultColor = resultText.color;
            resultColor.a = alpha;
            resultText.color = resultColor;

            Color detailColor = deltaText.color;
            detailColor.a = alpha;
            deltaText.color = detailColor;
        }

        private void UpdateTimingReadout()
        {
            if (timingText == null || beatClock == null || rhythmJudge == null)
                return;

            string timeline = beatClock.TryGetTimelinePositionMs(out int timelineMs)
                ? $"{timelineMs / 1000f:0.000} s"
                : "--";

            float bpm = beatClock.HasTimingAnchor ? beatClock.LatestBeat.Tempo : 0f;
            timingText.text =
                $"EVENT   {beatClock.MusicEventPath}\n" +
                $"TIME    {timeline}     BPM {bpm:0.##}\n" +
                $"JUDGE   +/-{rhythmJudge.PerfectWindowMs:0} ms     OFFSET {rhythmJudge.JudgementOffsetMs:+0;-0;0} ms";
        }

        private void SetResult(string result, Color color, string detail, float visibleSeconds)
        {
            if (resultText != null)
            {
                resultText.text = result;
                resultText.color = color;
            }

            if (deltaText != null)
            {
                deltaText.text = detail;
                deltaText.color = new Color(color.r, color.g, color.b, 1f);
            }

            resultVisibility = visibleSeconds;
        }

        private void UpdateStatistics()
        {
            if (statisticsText == null)
                return;

            int total = perfectCount + missCount;
            float accuracy = total > 0 ? perfectCount * 100f / total : 100f;
            double averageDelta = perfectCount > 0
                ? totalPerfectAbsoluteDeltaMs / perfectCount
                : 0.0;

            statisticsText.text =
                $"PERFECT  {perfectCount:000}     MISS  {missCount:000}     " +
                $"ACCURACY  {accuracy:0.0}%     AVG  {averageDelta:0.0} ms";
        }

        private static string FormatDelta(double deltaMs)
        {
            string direction = deltaMs < 0.0 ? "EARLY" : "LATE";
            return $"{deltaMs:+0.0;-0.0;0.0} ms  {direction}";
        }
    }
}
