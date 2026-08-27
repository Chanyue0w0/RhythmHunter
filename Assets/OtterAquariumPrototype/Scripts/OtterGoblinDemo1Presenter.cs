using System.Collections.Generic;
using FMODUnity;
using RhythmHunter.RhythmDemo;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Header("Projectile Timing")]
        [SerializeField, Min(0.25f)] private float tripleAxeVisualFlightBeats = 1f;

        [Header("HUD")]
        [SerializeField] private TextMesh titleText;
        [SerializeField] private TextMesh phaseText;
        [SerializeField] private TextMesh phraseText;
        [SerializeField] private TextMesh patternText;
        [SerializeField] private TextMesh judgementText;
        [SerializeField] private TextMesh timingText;
        [FormerlySerializedAs("healthText")]
        [SerializeField] private TextMesh failureCountText;
        [SerializeField] private TextMesh statusText;

        [Header("HUD Visibility (Edit or Play Mode)")]
        [Tooltip("When disabled, the diagnostic HUD is hidden immediately in Edit Mode and starts hidden in Play Mode.")]
        [SerializeField] private bool showDiagnosticHudOnStart = true;

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
        private bool tripleAxesScheduled;
        private OtterGoblinDemo1LevelData.AttackPhrase activePhrase;
        private bool diagnosticHudVisible;
        private GameObject rhythmHudRoot;
        private GameObject resultHudRoot;
        private readonly List<double> pendingTargetTimes = new();
        private readonly List<RhythmTimelineProjectile> flyingAxes = new();

        public GameObject AxeProjectilePrefab => axeProjectilePrefab;
        public bool DiagnosticHudVisible => diagnosticHudVisible;

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
            TextMesh configuredTitleText,
            TextMesh configuredPhaseText,
            TextMesh configuredPhraseText,
            TextMesh configuredPatternText,
            TextMesh configuredJudgementText,
            TextMesh configuredTimingText,
            TextMesh configuredFailureCountText,
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
            titleText = configuredTitleText;
            phaseText = configuredPhaseText;
            phraseText = configuredPhraseText;
            patternText = configuredPatternText;
            judgementText = configuredJudgementText;
            timingText = configuredTimingText;
            failureCountText = configuredFailureCountText;
            statusText = configuredStatusText;
            CachePose();
            CacheDiagnosticHudRoots();
            SetDiagnosticHudVisible(showDiagnosticHudOnStart);
            RefreshLevelPresentation();
        }

        private void Awake()
        {
            ResolveTitleText();
            CachePose();
            CacheDiagnosticHudRoots();
            SetDiagnosticHudVisible(showDiagnosticHudOnStart);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall -= ApplyEditorHudVisibility;
            UnityEditor.EditorApplication.delayCall += ApplyEditorHudVisibility;
        }

        private void ApplyEditorHudVisibility()
        {
            if (this == null || Application.isPlaying)
                return;

            CacheDiagnosticHudRoots();
            SetDiagnosticHudVisible(showDiagnosticHudOnStart);
        }
#endif

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
            runner.FailureCountChanged += OnFailureCountChanged;
            runner.BattleWon += OnBattleWon;
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
            runner.FailureCountChanged -= OnFailureCountChanged;
            runner.BattleWon -= OnBattleWon;
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
            activePhrase = phrase;
            tripleAxesScheduled = false;
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

            if (phrase.Kind is OtterGoblinDemo1LevelData.AttackKind.Triple
                or OtterGoblinDemo1LevelData.AttackKind.TripleThenSingle)
            {
                tripleAxesScheduled = ScheduleAxesForPendingTargets(
                    3,
                    tripleAxeVisualFlightBeats,
                    "TripleFlyingProjectile",
                    0);
            }
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
            int projectileStartIndex = GetWaitProjectileStartIndex(index);
            if (projectileCount != 3 || !tripleAxesScheduled)
                LaunchProjectilesForPendingTargets(projectileCount, projectileStartIndex);
            if (projectileCount != 3)
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
                hurtPulse = 0.4f;
                SetJudgement(
                    "STAGGER",
                    MissRed,
                    $"LOCKED {runner.LevelData.ExtraInputStunBeats:0.##} BEAT • NO DAMAGE");
                PlayOptional(runner.LevelData.MissSoundEventPath);
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
                    if (result.ExtraInput)
                    {
                        SetJudgement(
                            "EXTRA INPUT",
                            MissRed,
                            $"STUN {runner.LevelData.ExtraInputStunBeats:0.##} BEAT");
                    }
                    else
                    {
                        SetJudgement("MISS", MissRed, $"FAILURES {result.FailureCount}");
                    }
                    PlayOptional(runner.LevelData.MissSoundEventPath);
                    break;
            }
            RefreshHud();
        }

        private void OnFailureCountChanged(int failureCount)
        {
            RefreshFailureCount(failureCount);
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
            RefreshTitle();
            RefreshFailureCount(runner.FailureCount);
            RefreshStatus();
        }

        public void RefreshLevelPresentation()
        {
            ResolveTitleText();
            RefreshHud();
        }

        private void RefreshTitle()
        {
            if (titleText == null || runner == null || runner.LevelData == null)
                return;

            string eventPath = runner.LevelData.MusicEventPath;
            int separator = string.IsNullOrWhiteSpace(eventPath) ? -1 : eventPath.LastIndexOf('/');
            string songName = separator >= 0 && separator + 1 < eventPath.Length
                ? eventPath.Substring(separator + 1)
                : runner.LevelData.DisplayName;
            titleText.text = $"DEMO1  •  {songName.ToUpperInvariant()}";
        }

        private void ResolveTitleText()
        {
            if (titleText != null)
                return;

            Transform searchRoot = transform.root;
            foreach (TextMesh textMesh in searchRoot.GetComponentsInChildren<TextMesh>(true))
            {
                if (textMesh.name != "Title")
                    continue;
                titleText = textMesh;
                break;
            }
        }

        private void RefreshFailureCount(int failureCount)
        {
            if (failureCountText == null)
                return;
            failureCountText.text = $"FAILURES   {failureCount}";
            failureCountText.color = failureCount > 0 ? MissRed : Color.white;
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

        public void ToggleDiagnosticHud()
        {
            SetDiagnosticHudVisible(!diagnosticHudVisible);
        }

        public void SetDiagnosticHudVisible(bool visible)
        {
            diagnosticHudVisible = visible;
            SetHudRootActive(rhythmHudRoot, visible);
            SetHudRootActive(resultHudRoot, visible);
        }

        private void CacheDiagnosticHudRoots()
        {
            rhythmHudRoot = FindSharedHudRoot(phaseText, phraseText, patternText);
            resultHudRoot = FindSharedHudRoot(judgementText, timingText, statusText);
        }

        private static GameObject FindSharedHudRoot(params TextMesh[] labels)
        {
            foreach (TextMesh label in labels)
            {
                if (label != null && label.transform.parent != null)
                    return label.transform.parent.gameObject;
            }
            return null;
        }

        private static void SetHudRootActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
                root.SetActive(active);
        }

        private int GetWaitProjectileStartIndex(int waitCueIndex)
        {
            if (activePhrase == null)
                return 0;
            return activePhrase.Kind switch
            {
                OtterGoblinDemo1LevelData.AttackKind.DoubleSingle => Mathf.Max(0, waitCueIndex - 1),
                OtterGoblinDemo1LevelData.AttackKind.TripleThenSingle when waitCueIndex > 1 => 3,
                _ => 0
            };
        }

        private void LaunchProjectilesForPendingTargets(int projectileCount, int projectileStartIndex)
        {
            ScheduleAxesForPendingTargets(
                projectileCount,
                null,
                "FlyingProjectile",
                projectileStartIndex);
        }

        private bool ScheduleAxesForPendingTargets(
            int projectileCount,
            float? fixedFlightBeats,
            string instancePrefix,
            int projectileStartIndex)
        {
            if (runner == null || runner.BeatClock == null
                || enemyRoot == null || otterRoot == null
                || !runner.BeatClock.TryGetTimelinePositionMs(out int launchTimelineMs))
            {
                return false;
            }

            runner.CopyPendingTargetTimelineMs(pendingTargetTimes);
            float enemyScale = Mathf.Max(0.01f, Mathf.Abs(enemyRoot.lossyScale.x));
            float otterScale = Mathf.Max(0.01f, Mathf.Abs(otterRoot.lossyScale.x));
            Vector3 start = enemyRoot.position
                + new Vector3(1.15f * enemyScale, 0.62f * enemyScale, -0.45f);
            Vector3 end = otterRoot.position
                + new Vector3(-1.05f * otterScale, 0.38f * otterScale, -0.45f);
            int spawnCount = Mathf.Min(Mathf.Max(1, projectileCount), pendingTargetTimes.Count);
            int spawnedCount = 0;
            for (int i = 0; i < spawnCount; i++)
            {
                int projectileIndex = projectileStartIndex + i;
                GameObject projectilePrefab = activePhrase != null
                    ? activePhrase.GetProjectilePrefab(projectileIndex, axeProjectilePrefab)
                    : axeProjectilePrefab;
                if (projectilePrefab == null)
                    continue;

                GameObject instance = Instantiate(projectilePrefab);
                instance.name = $"{instancePrefix}_{projectileIndex + 1}_{projectilePrefab.name}";
                RhythmTimelineProjectile axe = instance.GetComponent<RhythmTimelineProjectile>();
                if (axe == null)
                {
                    Destroy(instance);
                    continue;
                }

                float laneOffset = i * 0.22f;
                double scheduledLaunchTimelineMs = fixedFlightBeats.HasValue
                    ? pendingTargetTimes[i] - fixedFlightBeats.Value * runner.CurrentMillisecondsPerBeat
                    : launchTimelineMs;
                scheduledLaunchTimelineMs = System.Math.Max(launchTimelineMs, scheduledLaunchTimelineMs);
                axe.Launch(
                    runner.BeatClock,
                    start + Vector3.up * (i * 0.08f),
                    end + Vector3.up * (i * 0.06f),
                    scheduledLaunchTimelineMs,
                    pendingTargetTimes[i],
                    laneOffset);
                flyingAxes.Add(axe);
                spawnedCount++;
            }
            return spawnedCount > 0;
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
            tripleAxesScheduled = false;
        }

        private static string FormatDelta(double deltaMs)
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
