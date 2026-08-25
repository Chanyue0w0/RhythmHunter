using FMODUnity;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    public sealed class OtterGoblinDemo1Presenter : MonoBehaviour
    {
        private static readonly Color Cyan = new(0.25f, 0.95f, 1f, 1f);
        private static readonly Color PerfectGreen = new(0.3f, 1f, 0.48f, 1f);
        private static readonly Color GoodGold = new(1f, 0.78f, 0.2f, 1f);
        private static readonly Color MissRed = new(1f, 0.25f, 0.28f, 1f);

        [Header("Dependencies")]
        [SerializeField] private OtterGoblinDemo1Runner runner;

        [Header("Characters")]
        [SerializeField] private Transform enemyRoot;
        [SerializeField] private SpriteRenderer enemyRenderer;
        [SerializeField] private Sprite[] enemyIdleFrames;
        [SerializeField] private Sprite[] enemyAttackFrames;
        [SerializeField] private Sprite enemyAttackedFrame;
        [SerializeField] private Transform otterRoot;
        [SerializeField] private SpriteRenderer otterBody;
        [SerializeField] private SpriteRenderer shield;
        [SerializeField] private SpriteRenderer dangerFlash;

        [Header("HUD")]
        [SerializeField] private TextMesh phaseText;
        [SerializeField] private TextMesh phraseText;
        [SerializeField] private TextMesh patternText;
        [SerializeField] private TextMesh judgementText;
        [SerializeField] private TextMesh timingText;
        [SerializeField] private TextMesh healthText;
        [SerializeField] private TextMesh statusText;

        private Vector3 enemyBasePosition;
        private Vector3 otterBasePosition;
        private Vector3 shieldBaseScale = Vector3.one;
        private float warningPulse;
        private float attackPulse;
        private float shieldPulse;
        private float hurtPulse;
        private float counterPulse;
        private int idleFrame;
        private int currentBeat = 1;
        private int currentBar = 1;

        public void Configure(
            OtterGoblinDemo1Runner configuredRunner,
            Transform configuredEnemyRoot,
            SpriteRenderer configuredEnemyRenderer,
            Sprite[] configuredIdleFrames,
            Sprite[] configuredAttackFrames,
            Sprite configuredAttackedFrame,
            Transform configuredOtterRoot,
            SpriteRenderer configuredOtterBody,
            SpriteRenderer configuredShield,
            SpriteRenderer configuredDangerFlash,
            TextMesh configuredPhaseText,
            TextMesh configuredPhraseText,
            TextMesh configuredPatternText,
            TextMesh configuredJudgementText,
            TextMesh configuredTimingText,
            TextMesh configuredHealthText,
            TextMesh configuredStatusText)
        {
            runner = configuredRunner;
            enemyRoot = configuredEnemyRoot;
            enemyRenderer = configuredEnemyRenderer;
            enemyIdleFrames = configuredIdleFrames;
            enemyAttackFrames = configuredAttackFrames;
            enemyAttackedFrame = configuredAttackedFrame;
            otterRoot = configuredOtterRoot;
            otterBody = configuredOtterBody;
            shield = configuredShield;
            dangerFlash = configuredDangerFlash;
            phaseText = configuredPhaseText;
            phraseText = configuredPhraseText;
            patternText = configuredPatternText;
            judgementText = configuredJudgementText;
            timingText = configuredTimingText;
            healthText = configuredHealthText;
            statusText = configuredStatusText;
            CachePose();
            RefreshHud();
        }

        private void Awake()
        {
            CachePose();
        }

        private void Start()
        {
            RefreshHud();
            SetPhase(OtterGoblinDemo1Runner.CombatPhase.Intro);
        }

        private void OnEnable()
        {
            if (runner == null)
                return;
            runner.BeatObserved += OnBeat;
            runner.PhaseChanged += SetPhase;
            runner.PhraseStarted += OnPhraseStarted;
            runner.WarningCue += OnWarningCue;
            runner.AttackCue += OnAttackCue;
            runner.Judged += OnJudged;
            runner.HealthChanged += OnHealthChanged;
            runner.BattleWon += OnBattleWon;
            runner.BattleLost += OnBattleLost;
            runner.BattleError += OnBattleError;
        }

        private void OnDisable()
        {
            if (runner == null)
                return;
            runner.BeatObserved -= OnBeat;
            runner.PhaseChanged -= SetPhase;
            runner.PhraseStarted -= OnPhraseStarted;
            runner.WarningCue -= OnWarningCue;
            runner.AttackCue -= OnAttackCue;
            runner.Judged -= OnJudged;
            runner.HealthChanged -= OnHealthChanged;
            runner.BattleWon -= OnBattleWon;
            runner.BattleLost -= OnBattleLost;
            runner.BattleError -= OnBattleError;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            warningPulse = Mathf.MoveTowards(warningPulse, 0f, dt * 4.8f);
            attackPulse = Mathf.MoveTowards(attackPulse, 0f, dt * 5.5f);
            shieldPulse = Mathf.MoveTowards(shieldPulse, 0f, dt * 4.2f);
            hurtPulse = Mathf.MoveTowards(hurtPulse, 0f, dt * 3.6f);
            counterPulse = Mathf.MoveTowards(counterPulse, 0f, dt * 3.2f);

            if (enemyRoot != null)
            {
                float lunge = attackPulse * 0.55f;
                float knockback = counterPulse * 0.38f;
                float attackDirection = Mathf.Sign(otterBasePosition.x - enemyBasePosition.x);
                enemyRoot.localPosition = enemyBasePosition
                    + Vector3.right * (attackDirection * lunge)
                    - Vector3.right * (attackDirection * knockback)
                    + Vector3.up * Mathf.Sin(Time.time * 3f) * 0.025f;
                enemyRoot.localRotation = Quaternion.Euler(0f, 0f, warningPulse * 8f - counterPulse * 11f);
            }

            if (enemyRenderer != null)
                UpdateEnemyFrame();

            if (otterRoot != null)
            {
                float shake = hurtPulse > 0f ? Mathf.Sin(Time.unscaledTime * 72f) * hurtPulse * 0.13f : 0f;
                otterRoot.localPosition = otterBasePosition + Vector3.right * shake + Vector3.up * shieldPulse * 0.06f;
            }

            if (otterBody != null)
                otterBody.color = Color.Lerp(Color.white, MissRed, hurtPulse * 0.65f);

            if (shield != null)
            {
                shield.transform.localScale = shieldBaseScale * (1f + shieldPulse * 0.25f);
                Color color = shield.color;
                color.a = shieldPulse * 0.72f;
                shield.color = color;
            }

            if (dangerFlash != null)
            {
                Color color = dangerFlash.color;
                color.a = warningPulse * 0.13f + hurtPulse * 0.22f;
                dangerFlash.color = color;
            }
        }

        private void OnBeat(FmodBeatClock.BeatSnapshot beat)
        {
            currentBeat = beat.Beat;
            currentBar = runner != null ? runner.CurrentBar : beat.Bar;
            idleFrame++;
            RefreshStatus();
        }

        private void OnPhraseStarted(int number, OtterGoblinDemo1LevelData.AttackPhrase phrase)
        {
            if (phraseText != null)
                phraseText.text = $"#{number:00}  {phrase.Label}";
            if (patternText != null)
                patternText.text = runner.GetCurrentPatternDisplay();
            if (judgementText != null)
            {
                judgementText.text = "LISTEN";
                judgementText.color = Cyan;
            }
            if (timingText != null)
                timingText.text = phrase.GapBeats > 0
                    ? $"REMEMBER IT • THEN WAIT {phrase.GapBeats} BEAT{(phrase.GapBeats == 1 ? string.Empty : "S")}" 
                    : "REMEMBER IT • ATTACK WILL ECHO";
        }

        private void OnWarningCue(int index, int count, string patternId)
        {
            warningPulse = 1f;
            if (judgementText != null)
            {
                judgementText.text = $"WATCH  {index}/{count}";
                judgementText.color = Cyan;
            }
            PlayOptional(runner.LevelData.WarningSoundEventPath);
        }

        private void OnAttackCue(int index, int count, string patternId)
        {
            attackPulse = 1f;
            if (judgementText != null)
            {
                judgementText.text = $"DEFEND  {index}/{count}";
                judgementText.color = Color.white;
            }
            PlayOptional(runner.LevelData.AttackSoundEventPath);
        }

        private void OnJudged(OtterGoblinDemo1Runner.JudgementResult result)
        {
            if (result.Judgement == OtterGoblinDemo1Runner.Grade.NotReady)
                return;

            if (result.ExtraInput)
            {
                if (judgementText != null)
                {
                    judgementText.text = "EMPTY SWING";
                    judgementText.color = MissRed;
                }
                if (timingText != null)
                    timingText.text = "NO ATTACK IN RANGE • NO DAMAGE";
                RefreshStatus();
                return;
            }

            switch (result.Judgement)
            {
                case OtterGoblinDemo1Runner.Grade.Perfect:
                    shieldPulse = 1f;
                    counterPulse = 1f;
                    if (shield != null)
                        shield.color = new Color(PerfectGreen.r, PerfectGreen.g, PerfectGreen.b, shield.color.a);
                    SetJudgement("PERFECT", PerfectGreen, FormatDelta(result.DeltaMs) + " • COUNTER");
                    PlayOptional(runner.LevelData.PerfectSoundEventPath);
                    break;

                case OtterGoblinDemo1Runner.Grade.Good:
                    shieldPulse = 0.75f;
                    if (shield != null)
                        shield.color = new Color(GoodGold.r, GoodGold.g, GoodGold.b, shield.color.a);
                    SetJudgement("GOOD", GoodGold, FormatDelta(result.DeltaMs) + " • BLOCKED");
                    PlayOptional(runner.LevelData.GoodSoundEventPath);
                    break;

                default:
                    hurtPulse = 1f;
                    SetJudgement("MISS", MissRed, "-1 HP");
                    PlayOptional(runner.LevelData.MissSoundEventPath);
                    break;
            }
            RefreshHud();
        }

        private void OnHealthChanged(int health, int maxHealth)
        {
            RefreshHealth(health, maxHealth);
        }

        private void OnBattleWon(OtterGoblinDemo1Runner.CombatSummary summary)
        {
            if (phaseText != null)
            {
                phaseText.text = "DEMO1 CLEAR";
                phaseText.color = PerfectGreen;
            }
            if (judgementText != null)
                judgementText.text = $"ACCURACY  {summary.Accuracy * 100f:0}%";
            if (timingText != null)
                timingText.text = "PRESS R TO RESTART";
        }

        private void OnBattleLost()
        {
            if (phaseText != null)
            {
                phaseText.text = "OTTER DOWN";
                phaseText.color = MissRed;
            }
            if (judgementText != null)
                judgementText.text = "3 MISSES";
            if (timingText != null)
                timingText.text = "PRESS R TO RESTART";
        }

        private void OnBattleError(string message)
        {
            if (phaseText != null)
            {
                phaseText.text = "FMOD ERROR";
                phaseText.color = MissRed;
            }
            if (timingText != null)
                timingText.text = message;
        }

        private void SetPhase(OtterGoblinDemo1Runner.CombatPhase next)
        {
            if (phaseText == null)
                return;

            phaseText.color = next switch
            {
                OtterGoblinDemo1Runner.CombatPhase.Warning => Cyan,
                OtterGoblinDemo1Runner.CombatPhase.Gap => GoodGold,
                OtterGoblinDemo1Runner.CombatPhase.Defend => MissRed,
                OtterGoblinDemo1Runner.CombatPhase.Victory => PerfectGreen,
                OtterGoblinDemo1Runner.CombatPhase.Defeat => MissRed,
                _ => Color.white
            };
            phaseText.text = next switch
            {
                OtterGoblinDemo1Runner.CombatPhase.Intro => "GET READY",
                OtterGoblinDemo1Runner.CombatPhase.Warning => "LISTEN",
                OtterGoblinDemo1Runner.CombatPhase.Gap => "WAIT",
                OtterGoblinDemo1Runner.CombatPhase.Defend => "DEFEND",
                OtterGoblinDemo1Runner.CombatPhase.Rest => "BREATHE",
                OtterGoblinDemo1Runner.CombatPhase.Victory => "DEMO1 CLEAR",
                OtterGoblinDemo1Runner.CombatPhase.Defeat => "OTTER DOWN",
                _ => "FMOD ERROR"
            };
        }

        private void UpdateEnemyFrame()
        {
            if (counterPulse > 0.25f && enemyAttackedFrame != null)
            {
                enemyRenderer.sprite = enemyAttackedFrame;
                return;
            }
            if (attackPulse > 0f && enemyAttackFrames != null && enemyAttackFrames.Length > 0)
            {
                float progress = 1f - attackPulse;
                int index = Mathf.Clamp(Mathf.FloorToInt(progress * enemyAttackFrames.Length), 0, enemyAttackFrames.Length - 1);
                enemyRenderer.sprite = enemyAttackFrames[index];
                return;
            }
            if (enemyIdleFrames != null && enemyIdleFrames.Length > 0)
                enemyRenderer.sprite = enemyIdleFrames[Mathf.Abs(idleFrame) % enemyIdleFrames.Length];
        }

        private void SetJudgement(string title, Color color, string detail)
        {
            if (judgementText != null)
            {
                judgementText.text = title;
                judgementText.color = color;
            }
            if (timingText != null)
                timingText.text = detail;
        }

        private void RefreshHud()
        {
            if (runner == null || runner.LevelData == null)
                return;
            RefreshHealth(runner.Health, runner.LevelData.OtterMaxHealth);
            RefreshStatus();
        }

        private void RefreshHealth(int health, int maxHealth)
        {
            if (healthText == null)
                return;
            healthText.text = $"OTTER HP   {Blocks(health)}{EmptyBlocks(maxHealth - health)}   {health}/{maxHealth}";
            healthText.color = health <= 1 ? MissRed : Color.white;
        }

        private void RefreshStatus()
        {
            if (statusText == null || runner == null || runner.LevelData == null)
                return;
            OtterGoblinDemo1Runner.CombatSummary summary = runner.GetSummary();
            statusText.text =
                $"BAR {currentBar:000}/{runner.LevelData.TotalBars}  BEAT {currentBeat}/4   •   153.1 BPM\n"
                + $"P {summary.Perfect:00}   G {summary.Good:00}   M {summary.Miss:00}   EXTRA {summary.Extra:00}";
        }

        private void CachePose()
        {
            if (enemyRoot != null)
                enemyBasePosition = enemyRoot.localPosition;
            if (otterRoot != null)
                otterBasePosition = otterRoot.localPosition;
            if (shield != null)
                shieldBaseScale = shield.transform.localScale;
        }

        private static string FormatDelta(double deltaMs)
        {
            if (Mathf.Abs((float)deltaMs) < 1f)
                return "ON TIME";
            return deltaMs < 0.0
                ? $"EARLY {Mathf.Abs((float)deltaMs):0} ms"
                : $"LATE {deltaMs:0} ms";
        }

        private static string Blocks(int count) => new('■', Mathf.Max(0, count));
        private static string EmptyBlocks(int count) => new('□', Mathf.Max(0, count));

        private static void PlayOptional(string eventPath)
        {
            if (!string.IsNullOrWhiteSpace(eventPath))
                RuntimeManager.PlayOneShot(eventPath);
        }
    }
}
