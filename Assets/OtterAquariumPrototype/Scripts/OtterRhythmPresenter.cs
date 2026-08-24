using FMODUnity;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    public sealed class OtterRhythmPresenter : MonoBehaviour
    {
        private static readonly Color ReadyCyan = new(0.32f, 0.94f, 1f, 1f);
        private static readonly Color PerfectGreen = new(0.35f, 1f, 0.55f, 1f);
        private static readonly Color GoodGold = new(1f, 0.82f, 0.25f, 1f);
        private static readonly Color MissRed = new(1f, 0.3f, 0.35f, 1f);

        [Header("Dependencies")]
        [SerializeField] private OtterRhythmLevelRunner levelRunner;

        [Header("Stage")]
        [SerializeField] private Transform otterRoot;
        [SerializeField] private Transform leftPaw;
        [SerializeField] private Transform rightPaw;
        [SerializeField] private Transform shellRoot;
        [SerializeField] private Transform shellLeft;
        [SerializeField] private Transform shellRight;
        [SerializeField] private SpriteRenderer shellRenderer;
        [SerializeField] private Transform crabHammer;
        [SerializeField] private SpriteRenderer cueRipple;
        [SerializeField] private SpriteRenderer beatGlow;

        [Header("Readouts")]
        [SerializeField] private TextMesh instructionText;
        [SerializeField] private TextMesh resultText;
        [SerializeField] private TextMesh detailText;
        [SerializeField] private TextMesh statisticsText;

        private Vector3 otterBasePosition;
        private Vector3 leftPawBasePosition;
        private Vector3 rightPawBasePosition;
        private Vector3 shellBaseScale;
        private float cuePulse;
        private float hitPulse;
        private float missPulse;
        private float beatPulse;
        private int currentBeat = 1;
        private int beatsPerBar = 4;
        private string lastTiming = "WAITING FOR FMOD";

        public void Configure(
            OtterRhythmLevelRunner runner,
            Transform configuredOtterRoot,
            Transform configuredLeftPaw,
            Transform configuredRightPaw,
            Transform configuredShellRoot,
            Transform configuredShellLeft,
            Transform configuredShellRight,
            SpriteRenderer configuredShellRenderer,
            Transform configuredCrabHammer,
            SpriteRenderer configuredCueRipple,
            SpriteRenderer configuredBeatGlow,
            TextMesh configuredInstructionText,
            TextMesh configuredResultText,
            TextMesh configuredDetailText,
            TextMesh configuredStatisticsText)
        {
            levelRunner = runner;
            otterRoot = configuredOtterRoot;
            leftPaw = configuredLeftPaw;
            rightPaw = configuredRightPaw;
            shellRoot = configuredShellRoot;
            shellLeft = configuredShellLeft;
            shellRight = configuredShellRight;
            shellRenderer = configuredShellRenderer;
            crabHammer = configuredCrabHammer;
            cueRipple = configuredCueRipple;
            beatGlow = configuredBeatGlow;
            instructionText = configuredInstructionText;
            resultText = configuredResultText;
            detailText = configuredDetailText;
            statisticsText = configuredStatisticsText;
            CacheBasePose();
        }

        private void Awake()
        {
            CacheBasePose();
        }

        private void OnEnable()
        {
            if (levelRunner == null)
                return;
            levelRunner.BeatObserved += OnBeat;
            levelRunner.CountInChanged += OnCountIn;
            levelRunner.PhraseStarted += OnPhraseStarted;
            levelRunner.CueTriggered += OnCue;
            levelRunner.Judged += OnJudged;
            levelRunner.PhraseCompleted += OnPhraseCompleted;
            levelRunner.LevelCompleted += OnLevelCompleted;
            levelRunner.LevelError += OnLevelError;
        }

        private void OnDisable()
        {
            if (levelRunner == null)
                return;
            levelRunner.BeatObserved -= OnBeat;
            levelRunner.CountInChanged -= OnCountIn;
            levelRunner.PhraseStarted -= OnPhraseStarted;
            levelRunner.CueTriggered -= OnCue;
            levelRunner.Judged -= OnJudged;
            levelRunner.PhraseCompleted -= OnPhraseCompleted;
            levelRunner.LevelCompleted -= OnLevelCompleted;
            levelRunner.LevelError -= OnLevelError;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            cuePulse = Mathf.MoveTowards(cuePulse, 0f, dt * 4.5f);
            hitPulse = Mathf.MoveTowards(hitPulse, 0f, dt * 5.5f);
            missPulse = Mathf.MoveTowards(missPulse, 0f, dt * 3.5f);
            beatPulse = Mathf.MoveTowards(beatPulse, 0f, dt * 4f);

            if (otterRoot != null)
            {
                float bob = Mathf.Sin(Time.time * 2.4f) * 0.04f;
                otterRoot.localPosition = otterBasePosition + Vector3.up * (bob + hitPulse * 0.08f);
                otterRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.3f) * 1.2f);
            }

            if (leftPaw != null)
                leftPaw.localPosition = leftPawBasePosition + new Vector3(hitPulse * 0.18f, -hitPulse * 0.22f, 0f);
            if (rightPaw != null)
                rightPaw.localPosition = rightPawBasePosition + new Vector3(-hitPulse * 0.18f, -hitPulse * 0.22f, 0f);
            if (shellRoot != null)
                shellRoot.localScale = shellBaseScale * (1f + hitPulse * 0.2f - missPulse * 0.08f);

            if (crabHammer != null)
                crabHammer.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(10f, -52f, cuePulse));

            if (cueRipple != null)
            {
                cueRipple.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2.2f, 1f - cuePulse);
                Color color = cueRipple.color;
                color.a = cuePulse * 0.72f;
                cueRipple.color = color;
            }

            if (beatGlow != null)
            {
                beatGlow.transform.localScale = Vector3.one * (1f + beatPulse * 0.15f);
                Color color = beatGlow.color;
                color.a = 0.08f + beatPulse * 0.24f;
                beatGlow.color = color;
            }
        }

        private void OnBeat(FmodBeatClock.BeatSnapshot beat)
        {
            currentBeat = beat.Beat;
            beatsPerBar = beat.TimeSignatureUpper;
            beatPulse = 1f;
            UpdateStatistics();
        }

        private void OnCountIn(int beat, int total)
        {
            if (instructionText != null)
                instructionText.text = $"GET READY   {beat} / {total}";
            if (resultText != null)
            {
                resultText.text = beat.ToString();
                resultText.color = ReadyCyan;
            }
        }

        private void OnPhraseStarted(int phraseNumber, string label, OtterRhythmLevelRunner.AdaptiveTier tier)
        {
            if (shellLeft != null)
                shellLeft.localRotation = Quaternion.identity;
            if (shellRight != null)
                shellRight.localRotation = Quaternion.identity;
            if (instructionText != null)
                instructionText.text = $"{label}   •   {tier.ToString().ToUpperInvariant()}";
            if (detailText != null)
                detailText.text = "LISTEN TO THE CRAB";
            if (resultText != null)
            {
                resultText.text = "LISTEN";
                resultText.color = ReadyCyan;
            }
        }

        private void OnCue(int cueNumber, int cueCount, string patternId)
        {
            cuePulse = 1f;
            if (resultText != null)
            {
                resultText.text = $"KNOCK {cueNumber}";
                resultText.color = ReadyCyan;
            }
            if (detailText != null)
                detailText.text = $"REMEMBER {cueNumber} / {cueCount}   •   {patternId}";
            PlayOptional(levelRunner.LevelData.CueSoundEventPath);
        }

        private void OnJudged(OtterRhythmLevelRunner.JudgementResult result)
        {
            if (result.Judgement == OtterRhythmLevelRunner.Grade.NotReady)
                return;

            switch (result.Judgement)
            {
                case OtterRhythmLevelRunner.Grade.Perfect:
                    hitPulse = 1f;
                    lastTiming = FormatTiming(result.DeltaMs);
                    SetResult("PERFECT", PerfectGreen, lastTiming);
                    PlayOptional(levelRunner.LevelData.HitSoundEventPath);
                    break;

                case OtterRhythmLevelRunner.Grade.Good:
                    hitPulse = 0.75f;
                    lastTiming = FormatTiming(result.DeltaMs);
                    SetResult("GOOD", GoodGold, lastTiming);
                    PlayOptional(levelRunner.LevelData.HitSoundEventPath);
                    break;

                default:
                    missPulse = 1f;
                    lastTiming = result.ExtraInput ? "EXTRA INPUT" : "TOO LATE";
                    SetResult("MISS", MissRed, lastTiming);
                    PlayOptional(levelRunner.LevelData.MissSoundEventPath);
                    break;
            }
            UpdateStatistics();
        }

        private void OnPhraseCompleted(int hits, int targets)
        {
            bool clean = hits == targets;
            if (shellLeft != null)
                shellLeft.localRotation = Quaternion.Euler(0f, 0f, clean ? 18f : 4f);
            if (shellRight != null)
                shellRight.localRotation = Quaternion.Euler(0f, 0f, clean ? -18f : -4f);
            if (detailText != null)
                detailText.text = clean ? "SHELL OPENED!" : $"SHELL CRACKED   {hits} / {targets}";
            if (clean)
                PlayOptional(levelRunner.LevelData.SuccessSoundEventPath);
        }

        private void OnLevelCompleted(OtterRhythmLevelRunner.LevelSummary summary)
        {
            if (instructionText != null)
                instructionText.text = "PLAYTEST COMPLETE   •   PRESS PLAY AGAIN TO RETRY";
            if (resultText != null)
            {
                resultText.text = $"{summary.Accuracy * 100f:0}%";
                resultText.color = summary.Accuracy >= 0.8f ? PerfectGreen : GoodGold;
            }
            if (detailText != null)
                detailText.text = $"MEAN ERROR   {summary.MeanAbsoluteDeltaMs:0.0} ms";
            UpdateStatistics();
        }

        private void OnLevelError(string message)
        {
            SetResult("FMOD ERROR", MissRed, message);
        }

        private void SetResult(string title, Color color, string detail)
        {
            if (resultText != null)
            {
                resultText.text = title;
                resultText.color = color;
            }
            if (detailText != null)
                detailText.text = detail;
        }

        private void UpdateStatistics()
        {
            if (statisticsText == null || levelRunner == null)
                return;
            OtterRhythmLevelRunner.LevelSummary summary = levelRunner.GetSummary();
            statisticsText.text =
                $"BEAT {currentBeat}/{beatsPerBar}   •   PHRASE {levelRunner.CurrentPhraseNumber:00}   •   "
                + $"P {summary.Perfect:00}  G {summary.Good:00}  M {summary.Miss:00}  EXTRA {summary.Extra:00}\n"
                + $"PATTERN {levelRunner.CurrentPatternId.ToUpperInvariant()}   •   {lastTiming}";
        }

        private void CacheBasePose()
        {
            if (otterRoot != null)
                otterBasePosition = otterRoot.localPosition;
            if (leftPaw != null)
                leftPawBasePosition = leftPaw.localPosition;
            if (rightPaw != null)
                rightPawBasePosition = rightPaw.localPosition;
            if (shellRoot != null)
                shellBaseScale = shellRoot.localScale;
        }

        private static string FormatTiming(double deltaMs)
        {
            if (Mathf.Abs((float)deltaMs) < 1f)
                return "ON TIME";
            return deltaMs < 0.0
                ? $"EARLY {Mathf.Abs((float)deltaMs):0} ms"
                : $"LATE {deltaMs:0} ms";
        }

        private static void PlayOptional(string eventPath)
        {
            if (!string.IsNullOrWhiteSpace(eventPath))
                RuntimeManager.PlayOneShot(eventPath);
        }
    }
}
