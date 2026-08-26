using System.Collections.Generic;
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
        [SerializeField] private GameObject axeProjectilePrefab;
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
        private bool enemyHoldingAxe;
        private int idleFrame;
        private int currentBeat = 1;
        private int currentBar = 1;
        private readonly List<double> pendingTargetTimes = new();
        private readonly List<RhythmTimelineProjectile> flyingAxes = new();

        public GameObject AxeProjectilePrefab => axeProjectilePrefab;

        public void Configure(
            OtterGoblinDemo1Runner configuredRunner,
            Transform configuredEnemyRoot,
            SpriteRenderer configuredEnemyRenderer,
            Sprite[] configuredIdleFrames,
            Sprite[] configuredAttackFrames,
            Sprite configuredAttackedFrame,
            GameObject configuredAxeProjectilePrefab,
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
            axeProjectilePrefab = configuredAxeProjectilePrefab;
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
            runner.WaitCue += OnWaitCue;
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
            runner.WaitCue -= OnWaitCue;
            runner.AttackCue -= OnAttackCue;
            runner.Judged -= OnJudged;
            runner.HealthChanged -= OnHealthChanged;
            runner.BattleWon -= OnBattleWon;
            runner.BattleLost -= OnBattleLost;
            runner.BattleError -= OnBattleError;
            ClearFlyingAxes();
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
                timingText.text = phrase.Kind switch
                {
                    OtterGoblinDemo1LevelData.AttackKind.Single =>
                        "ONE WARNING • ONE ATTACK BEAT • CATCH ONCE",
                    OtterGoblinDemo1LevelData.AttackKind.Triple =>
                        "THREE QUICK WARNINGS • CATCH THREE AXES ON ALTERNATING BEATS",
                    OtterGoblinDemo1LevelData.AttackKind.DoubleSingle =>
                        "TWO X _ ATTACKS • THE NEXT WARNING FOLLOWS YOUR CATCH",
                    _ => "CATCH THREE AXES ON ALTERNATING BEATS • THEN PREPARE FOR X _"
                };
        }

        private void OnWarningCue(int index, int count, string patternId)
        {
            warningPulse = 1f;
            enemyHoldingAxe = true;
            if (judgementText != null)
            {
                judgementText.text = $"WARNING  {index}/{count}";
                judgementText.color = Cyan;
            }
            PlayOptional(runner.LevelData.WarningSoundEventPath);
        }

        private void OnWaitCue(int index, int count, int projectileCount)
        {
            attackPulse = 1f;
            enemyHoldingAxe = false;
            if (judgementText != null)
            {
                judgementText.text = $"AXE  {index}/{count}";
                judgementText.color = GoodGold;
            }
            LaunchAxesForPendingTargets(projectileCount);
            PlayOptional(runner.LevelData.AttackSoundEventPath);
        }

        private void OnAttackCue(int index, int count, string patternId)
        {
            if (judgementText != null)
            {
                judgementText.text = $"DEFEND  {index}/{count}";
                judgementText.color = Color.white;
            }
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

            ResolveClosestAxe(result);

            switch (result.Judgement)
            {
                case OtterGoblinDemo1Runner.Grade.Perfect:
                    shieldPulse = 1f;
                    counterPulse = 1f;
                    if (shield != null)
                        shield.color = new Color(PerfectGreen.r, PerfectGreen.g, PerfectGreen.b, shield.color.a);
                    SetJudgement("PERFECT", PerfectGreen, FormatDelta(result.DeltaMs) + " • COUNTER");
                    PlayOptional(runner.LevelData.BlockSoundEventPath);
                    PlayOptional(runner.LevelData.PerfectSoundEventPath);
                    break;

                case OtterGoblinDemo1Runner.Grade.Good:
                    shieldPulse = 0.75f;
                    if (shield != null)
                        shield.color = new Color(GoodGold.r, GoodGold.g, GoodGold.b, shield.color.a);
                    SetJudgement("GOOD", GoodGold, FormatDelta(result.DeltaMs) + " • BLOCKED");
                    PlayOptional(runner.LevelData.BlockSoundEventPath);
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
            ClearFlyingAxes();
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
            ClearFlyingAxes();
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
            ClearFlyingAxes();
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
            if (enemyHoldingAxe && enemyAttackFrames != null && enemyAttackFrames.Length > 0)
            {
                enemyRenderer.sprite = enemyAttackFrames[Mathf.Min(2, enemyAttackFrames.Length - 1)];
                return;
            }
            if (attackPulse > 0f && enemyAttackFrames != null && enemyAttackFrames.Length > 0)
            {
                int index = attackPulse > 0.55f
                    ? enemyAttackFrames.Length - 1
                    : Mathf.Min(1, enemyAttackFrames.Length - 1);
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
                $"BAR {currentBar:000}/{runner.LevelData.TotalBars}  BEAT {currentBeat}/4   •   {runner.LevelData.AuthoredBpm:0.#} BPM\n"
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

        private void LaunchAxesForPendingTargets(int projectileCount)
        {
            if (axeProjectilePrefab == null || runner == null || runner.BeatClock == null
                || enemyRoot == null || otterRoot == null
                || !runner.BeatClock.TryGetTimelinePositionMs(out int launchTimelineMs))
            {
                return;
            }

            runner.CopyPendingTargetTimelineMs(pendingTargetTimes);
            Vector3 start = enemyRoot.position + new Vector3(1.15f, 0.62f, -0.45f);
            Vector3 end = otterRoot.position + new Vector3(-1.05f, 0.38f, -0.45f);
            int spawnCount = Mathf.Min(Mathf.Max(1, projectileCount), pendingTargetTimes.Count);
            for (int i = 0; i < spawnCount; i++)
            {
                GameObject instance = Instantiate(axeProjectilePrefab);
                instance.name = $"FlyingAxe_{i + 1}";
                RhythmTimelineProjectile axe = instance.GetComponent<RhythmTimelineProjectile>();
                if (axe == null)
                {
                    Destroy(instance);
                    continue;
                }

                float laneOffset = i * 0.22f;
                axe.Launch(
                    runner.BeatClock,
                    start + Vector3.up * (i * 0.08f),
                    end + Vector3.up * (i * 0.06f),
                    launchTimelineMs,
                    pendingTargetTimes[i],
                    laneOffset);
                flyingAxes.Add(axe);
            }
        }

        private void ResolveClosestAxe(OtterGoblinDemo1Runner.JudgementResult result)
        {
            if (flyingAxes.Count == 0)
                return;

            double targetTimelineMs = 0.0;
            if (runner.BeatClock.TryGetTimelinePositionMs(out int timelineMs))
                targetTimelineMs = timelineMs + runner.LevelData.JudgementOffsetMs - result.DeltaMs;

            RhythmTimelineProjectile closest = null;
            double closestDistance = double.MaxValue;
            for (int i = flyingAxes.Count - 1; i >= 0; i--)
            {
                RhythmTimelineProjectile axe = flyingAxes[i];
                if (axe == null || axe.IsResolved)
                {
                    flyingAxes.RemoveAt(i);
                    continue;
                }

                double distance = targetTimelineMs == 0.0
                    ? i
                    : System.Math.Abs(axe.ArrivalTimelineMs - targetTimelineMs);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = axe;
                }
            }

            if (closest == null)
                return;
            closest.Resolve(result.Judgement is OtterGoblinDemo1Runner.Grade.Perfect
                or OtterGoblinDemo1Runner.Grade.Good);
            flyingAxes.Remove(closest);
        }

        private void ClearFlyingAxes()
        {
            foreach (RhythmTimelineProjectile axe in flyingAxes)
            {
                if (axe != null)
                    Destroy(axe.gameObject);
            }
            flyingAxes.Clear();
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
