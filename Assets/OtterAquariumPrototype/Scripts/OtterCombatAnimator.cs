using System;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    [DefaultExecutionOrder(100)]
    [ExecuteAlways]
    public sealed class OtterCombatAnimator : MonoBehaviour
    {
        private enum State
        {
            Intro,
            Idle,
            Rubbing,
            Cracking
        }

        [Header("Dependencies")]
        [SerializeField] private OtterGoblinDemo1Runner runner;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Frames (non-old assets only)")]
        [SerializeField] private Sprite[] swimmingFrames;
        [SerializeField] private Sprite[] rollingFrames;
        [SerializeField] private Sprite idleFrame;
        [SerializeField] private Sprite[] crackingFrames;
        [SerializeField] private Sprite[] rubbingFrames;

        [Header("Intro Timing (beats before first phrase)")]
        [SerializeField, Min(0.25f)] private float rollingDurationBeats = 2f;
        [SerializeField, Min(0f)] private float idleReadyBeats = 2f;
        [SerializeField, Min(0.25f)] private float swimmingFramesPerBeat = 4f;

        [Header("Intro Movement (world-space offset from the final pose)")]
        [SerializeField] private Vector3 swimmingEntryWorldOffset = new(5.5f, 0.2f, 0f);

        [Header("State Pose")]
        [SerializeField] private Vector3 swimmingLocalOffset = Vector3.zero;
        [SerializeField] private float swimmingScale = 1.15f;
        [SerializeField] private Vector3 rollingLocalOffset = Vector3.zero;
        [SerializeField] private float rollingScale = 0.72f;
        [SerializeField] private Vector3 idleLocalOffset = Vector3.zero;
        [SerializeField] private float idleScale = 1f;
        [SerializeField] private Vector3 crackingLocalOffset = Vector3.zero;
        [SerializeField] private float crackingScale = 1f;

        [Header("Beat Float")]
        [SerializeField, Min(0f)] private float idleBobAmplitude = 0.09f;
        [SerializeField, Min(0f)] private float idleRockDegrees = 0.8f;

        [Header("Cracking Timing (beats)")]
        [Tooltip("How long the visible raised-hand catch pose remains on cracking frame 2.")]
        [SerializeField, Min(0.01f)] private float catchFrameBeats = 0.5f;
        [Tooltip("Fast transition from the catch pose to the striking pose on cracking frame 3.")]
        [SerializeField, Min(0.01f)] private float liftFrameBeats = 0.08f;
        [Tooltip("Duration of the final strike on cracking frame 4. Shattering happens halfway through.")]
        [SerializeField, Min(0.01f)] private float impactFrameBeats = 0.08f;

        [Header("Occasional Rubbing Idle")]
        [SerializeField, Min(0.05f)] private float rubbingFrameSeconds = 0.16f;
        [SerializeField] private Vector2 rubbingIntervalSeconds = new(6f, 12f);

        [Header("Held Item Anchors (local to Visual Root)")]
        [Tooltip("Projectile arrival and raised-hand hold position. Edit this on the Otter's OtterCombatAnimator component.")]
        [SerializeField] private Vector3 catchAnchor = new(1.3f, 0.65f, -0.2f);
        [SerializeField] private Vector3 upperBellyAnchor = new(0.42f, 0.05f, -0.2f);
        [SerializeField] private Vector3 impactAnchor = new(0.08f, -0.13f, -0.2f);

        private State state = State.Intro;
        private RhythmTimelineProjectile heldProjectile;
        private double crackingStartedTimelineMs;
        private double crackingStartedFallbackTime;
        private bool crackImpactSent;
        private bool cinematicControl;
        private bool skipTimelineIntro;
        private float activeCrackingTimingScale = 1f;
        private float activeCrackingCatchHoldBeats;
        private double rubbingStartedTime;
        private double nextRubbingTime;

        public event Action<RhythmTimelineProjectile, Vector3> CrackImpact;

        public Transform CatchTargetTransform => visualRoot != null ? visualRoot : transform;
        public Vector3 CatchAnchorLocal => catchAnchor;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public Vector3 SwimmingEntryWorldOffset => swimmingEntryWorldOffset;
        public bool HasRequiredFrames => swimmingFrames is { Length: 4 }
            && rollingFrames is { Length: 9 }
            && idleFrame != null
            && crackingFrames is { Length: 4 }
            && rubbingFrames is { Length: 4 };
        public bool IsCracking => state == State.Cracking;

        public void BeginCinematicControl()
        {
            cinematicControl = true;
            state = State.Intro;
            SetCinematicSwimming(0f, 0f);
        }

        public void SetCinematicSwimming(float journeyProgress, float elapsedSeconds)
        {
            state = State.Intro;
            ApplySwimmingPose(
                Mathf.Max(0f, elapsedSeconds) * swimmingFramesPerBeat * 2f,
                Mathf.Clamp01(journeyProgress));
        }

        public void SetCinematicSwimmingPose(float elapsedSeconds, bool faceRight)
        {
            state = State.Intro;
            ApplySwimmingPose(
                Mathf.Max(0f, elapsedSeconds) * swimmingFramesPerBeat * 2f,
                1f);
            if (visualRoot == null)
                return;

            Vector3 scale = visualRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * (faceRight ? -1f : 1f);
            visualRoot.localScale = scale;
        }

        public void SetCinematicRolling(float progress)
        {
            state = State.Intro;
            ApplyRollingPose(Mathf.Clamp01(progress));
        }

        public void SetCinematicIdle()
        {
            state = State.Idle;
            ApplyIdlePose();
            ScheduleNextRubbing();
        }

        public void EndCinematicControl(bool shouldSkipTimelineIntro)
        {
            cinematicControl = false;
            skipTimelineIntro = shouldSkipTimelineIntro;
            state = State.Idle;
            ApplyIdlePose();
        }

        public void ConfigureRunner(OtterGoblinDemo1Runner configuredRunner)
        {
            runner = configuredRunner;
        }

        public void ConfigureVisuals(
            Transform configuredVisualRoot,
            SpriteRenderer configuredRenderer,
            Sprite[] configuredSwimmingFrames,
            Sprite[] configuredRollingFrames,
            Sprite configuredIdleFrame,
            Sprite[] configuredCrackingFrames,
            Sprite[] configuredRubbingFrames)
        {
            visualRoot = configuredVisualRoot;
            spriteRenderer = configuredRenderer;
            swimmingFrames = configuredSwimmingFrames;
            rollingFrames = configuredRollingFrames;
            idleFrame = configuredIdleFrame;
            crackingFrames = configuredCrackingFrames;
            rubbingFrames = configuredRubbingFrames;
            HideLegacyRenderers();
            ShowIdlePreview();
        }

        public void PlayCracking(RhythmTimelineProjectile projectile)
        {
            BeginCracking(projectile, 1f, 0f);
        }

        public void PlayCinematicCracking(
            RhythmTimelineProjectile projectile,
            float timingScale,
            float catchHoldBeats)
        {
            BeginCracking(
                projectile,
                Mathf.Max(1f, timingScale),
                Mathf.Max(0f, catchHoldBeats));
        }

        private void BeginCracking(
            RhythmTimelineProjectile projectile,
            float timingScale,
            float catchHoldBeats)
        {
            FinishHeldProjectile();
            heldProjectile = projectile;
            if (heldProjectile != null)
            {
                heldProjectile.CaptureForOtter();
            }

            state = State.Cracking;
            activeCrackingTimingScale = Mathf.Max(1f, timingScale);
            activeCrackingCatchHoldBeats = Mathf.Max(0f, catchHoldBeats);
            crackingStartedFallbackTime = Time.unscaledTimeAsDouble;
            crackingStartedTimelineMs = TryGetTimelineMs(out double timelineMs) ? timelineMs : 0.0;
            crackImpactSent = false;
            ApplyCrackingPose(0f);
        }

        public void ResetToIdle()
        {
            FinishHeldProjectile();
            activeCrackingTimingScale = 1f;
            activeCrackingCatchHoldBeats = 0f;
            state = State.Idle;
            if (visualRoot != null && spriteRenderer != null)
                ApplyIdlePose();
            ScheduleNextRubbing();
        }

        public void InterruptIdleVariation()
        {
            if (state != State.Rubbing)
                return;
            state = State.Idle;
            ApplyIdlePose();
            ScheduleNextRubbing();
        }

        private void Awake()
        {
            ResolveReferences();
            HideLegacyRenderers();
            state = State.Intro;
            ScheduleNextRubbing();
        }

        private void OnEnable()
        {
            ResolveReferences();
            HideLegacyRenderers();
            if (!Application.isPlaying)
                ShowIdlePreview();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
                FinishHeldProjectile();
        }

        private void OnValidate()
        {
            rollingDurationBeats = Mathf.Max(0.25f, rollingDurationBeats);
            swimmingFramesPerBeat = Mathf.Max(0.25f, swimmingFramesPerBeat);
            swimmingScale = Mathf.Max(0.01f, swimmingScale);
            rollingScale = Mathf.Max(0.01f, rollingScale);
            idleScale = Mathf.Max(0.01f, idleScale);
            crackingScale = Mathf.Max(0.01f, crackingScale);
            catchFrameBeats = Mathf.Max(0.01f, catchFrameBeats);
            liftFrameBeats = Mathf.Max(0.01f, liftFrameBeats);
            impactFrameBeats = Mathf.Max(0.01f, impactFrameBeats);
            rubbingFrameSeconds = Mathf.Max(0.05f, rubbingFrameSeconds);
            rubbingIntervalSeconds.x = Mathf.Max(0.5f, rubbingIntervalSeconds.x);
            rubbingIntervalSeconds.y = Mathf.Max(
                rubbingIntervalSeconds.x,
                rubbingIntervalSeconds.y);
            ResolveReferences();
            if (!Application.isPlaying)
                ShowIdlePreview();
        }

        private void OnDrawGizmosSelected()
        {
            ResolveReferences();
            if (visualRoot == null)
                return;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = visualRoot.localToWorldMatrix;

            Gizmos.color = new Color(0.2f, 1f, 1f, 0.95f);
            Gizmos.DrawWireSphere(catchAnchor, 0.11f);
            Gizmos.DrawLine(catchAnchor, upperBellyAnchor);
            Gizmos.color = new Color(1f, 0.8f, 0.15f, 0.95f);
            Gizmos.DrawWireSphere(upperBellyAnchor, 0.09f);
            Gizmos.DrawLine(upperBellyAnchor, impactAnchor);
            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.95f);
            Gizmos.DrawWireSphere(impactAnchor, 0.1f);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private void Update()
        {
            ResolveReferences();
            if (visualRoot == null || spriteRenderer == null)
                return;

            if (!Application.isPlaying)
            {
                ShowIdlePreview();
                return;
            }

            if (state == State.Cracking)
            {
                UpdateCracking();
                return;
            }

            if (cinematicControl)
                return;

            if (TryApplyIntro())
                return;

            if (state == State.Rubbing)
            {
                UpdateRubbing();
                return;
            }

            state = State.Idle;
            ApplyIdlePose();
            TryStartRubbing();
        }

        private bool TryApplyIntro()
        {
            if (skipTimelineIntro)
                return false;

            if (runner == null || runner.LevelData == null || runner.LevelData.Phrases.Count == 0)
            {
                ApplySwimmingPose((float)(Time.unscaledTimeAsDouble * 8.0), 0f);
                return true;
            }

            if (runner.BeatClock == null || !runner.BeatClock.HasTimingAnchor)
            {
                ApplySwimmingPose((float)(Time.unscaledTimeAsDouble * 8.0), 0f);
                return true;
            }

            OtterGoblinDemo1LevelData.AttackPhrase firstPhrase = runner.LevelData.Phrases[0];
            long firstPhraseTick = (long)(firstPhrase.StartBar - 1) * runner.LevelData.TicksPerBar
                + firstPhrase.StartOffsetTicks;
            long rollDurationTicks = (long)Math.Round(rollingDurationBeats * runner.LevelData.Ppq);
            long idleReadyTicks = (long)Math.Round(idleReadyBeats * runner.LevelData.Ppq);
            long rollStartTick = Math.Max(0L, firstPhraseTick - idleReadyTicks - rollDurationTicks);
            long rollEndTick = Math.Max(rollStartTick + 1L, firstPhraseTick - idleReadyTicks);
            long currentTick = runner.CurrentSongTick;

            if (currentTick < rollStartTick)
            {
                float beatPosition = currentTick / (float)runner.LevelData.Ppq;
                float journeyProgress = rollStartTick > 0L
                    ? Mathf.Clamp01(currentTick / (float)rollStartTick)
                    : 1f;
                ApplySwimmingPose(beatPosition * swimmingFramesPerBeat, journeyProgress);
                return true;
            }

            if (currentTick < rollEndTick)
            {
                float progress = Mathf.InverseLerp(rollStartTick, rollEndTick, currentTick);
                ApplyRollingPose(progress);
                return true;
            }

            if (currentTick < firstPhraseTick)
            {
                ApplyIdlePose();
                return true;
            }

            return false;
        }

        private void UpdateCracking()
        {
            float elapsedBeats = GetCrackingElapsedBeats();
            float liftStart = catchFrameBeats * activeCrackingTimingScale
                + activeCrackingCatchHoldBeats;
            float impactStart = (catchFrameBeats + liftFrameBeats) * activeCrackingTimingScale
                + activeCrackingCatchHoldBeats;
            float end = (catchFrameBeats + liftFrameBeats + impactFrameBeats)
                * activeCrackingTimingScale
                + activeCrackingCatchHoldBeats;

            if (elapsedBeats < liftStart)
            {
                ApplyCrackingPose(0f);
                SetHeldItemPose(catchAnchor, 0f);
                return;
            }

            if (elapsedBeats < impactStart)
            {
                ApplyCrackingPose(1f);
                float progress = Mathf.InverseLerp(liftStart, impactStart, elapsedBeats);
                SetHeldItemPose(Vector3.Lerp(catchAnchor, upperBellyAnchor, Smooth(progress)), -18f * progress);
                return;
            }

            if (elapsedBeats < end)
            {
                ApplyCrackingPose(2f);
                float progress = Mathf.InverseLerp(impactStart, end, elapsedBeats);
                float strikeProgress = Smooth(Mathf.Clamp01(progress * 2f));
                Vector3 anchor = Vector3.Lerp(upperBellyAnchor, impactAnchor, strikeProgress);
                SetHeldItemPose(anchor, Mathf.Lerp(-18f, 24f, strikeProgress));
                if (!crackImpactSent && progress >= 0.5f)
                {
                    crackImpactSent = true;
                    Vector3 impactWorld = CatchTargetTransform.TransformPoint(impactAnchor);
                    heldProjectile?.ShatterAt(impactWorld, runner != null
                        ? runner.CurrentMillisecondsPerBeat
                        : 500.0);
                    CrackImpact?.Invoke(heldProjectile, impactWorld);
                }
                return;
            }

            FinishHeldProjectile();
            activeCrackingTimingScale = 1f;
            activeCrackingCatchHoldBeats = 0f;
            state = State.Idle;
            ApplyIdlePose();
            ScheduleNextRubbing();
        }

        private void TryStartRubbing()
        {
            if (rubbingFrames is not { Length: > 0 }
                || Time.unscaledTimeAsDouble < nextRubbingTime
                || runner == null
                || runner.BeatClock == null
                || !runner.BeatClock.IsPlaying
                || runner.HasEnded
                || runner.Phase is not (OtterGoblinDemo1Runner.CombatPhase.Intro
                    or OtterGoblinDemo1Runner.CombatPhase.Rest))
            {
                return;
            }

            state = State.Rubbing;
            rubbingStartedTime = Time.unscaledTimeAsDouble;
            ApplyRubbingPose(0);
        }

        private void UpdateRubbing()
        {
            double elapsed = Time.unscaledTimeAsDouble - rubbingStartedTime;
            int frameIndex = Mathf.FloorToInt((float)(elapsed / rubbingFrameSeconds));
            if (rubbingFrames == null || frameIndex >= rubbingFrames.Length)
            {
                state = State.Idle;
                ApplyIdlePose();
                ScheduleNextRubbing();
                return;
            }

            ApplyRubbingPose(frameIndex);
        }

        private void ApplyRubbingPose(int frameIndex)
        {
            ApplyPose(
                GetFrame(rubbingFrames, frameIndex, idleFrame),
                idleLocalOffset,
                idleScale,
                true);
        }

        private void ScheduleNextRubbing()
        {
            float minimum = Mathf.Max(0.5f, rubbingIntervalSeconds.x);
            float maximum = Mathf.Max(minimum, rubbingIntervalSeconds.y);
            nextRubbingTime = Time.unscaledTimeAsDouble + UnityEngine.Random.Range(minimum, maximum);
        }

        private float GetCrackingElapsedBeats()
        {
            double elapsedMs;
            if (crackingStartedTimelineMs > 0.0 && TryGetTimelineMs(out double timelineMs))
                elapsedMs = Math.Max(0.0, timelineMs - crackingStartedTimelineMs);
            else
                elapsedMs = Math.Max(0.0, Time.unscaledTimeAsDouble - crackingStartedFallbackTime) * 1000.0;

            double millisecondsPerBeat = runner != null
                ? Math.Max(1.0, runner.CurrentMillisecondsPerBeat)
                : 500.0;
            return (float)(elapsedMs / millisecondsPerBeat);
        }

        private void ApplySwimmingPose(float framePosition, float journeyProgress)
        {
            int index = LoopIndex(Mathf.FloorToInt(framePosition), swimmingFrames);
            ApplyPose(GetFrame(swimmingFrames, index, idleFrame), swimmingLocalOffset, swimmingScale, false);

            Vector3 entryLocalOffset = visualRoot.parent != null
                ? visualRoot.parent.InverseTransformVector(swimmingEntryWorldOffset)
                : swimmingEntryWorldOffset;
            visualRoot.localPosition += entryLocalOffset * (1f - Smooth(journeyProgress));
        }

        private void ApplyRollingPose(float progress)
        {
            int index = rollingFrames == null || rollingFrames.Length == 0
                ? 0
                : Mathf.Clamp(Mathf.FloorToInt(progress * rollingFrames.Length), 0, rollingFrames.Length - 1);
            ApplyPose(GetFrame(rollingFrames, index, idleFrame), rollingLocalOffset, rollingScale, false);
        }

        private void ApplyIdlePose()
        {
            ApplyPose(idleFrame, idleLocalOffset, idleScale, true);
        }

        private void ApplyCrackingPose(float phase)
        {
            int requestedIndex = phase <= 0f ? 1 : phase < 2f ? 2 : 3;
            ApplyPose(GetFrame(crackingFrames, requestedIndex, idleFrame), crackingLocalOffset, crackingScale, true);
        }

        private void ApplyPose(Sprite sprite, Vector3 baseOffset, float scale, bool includeBeatFloat)
        {
            if (sprite != null)
                spriteRenderer.sprite = sprite;

            float bob = 0f;
            float rock = 0f;
            if (includeBeatFloat && runner != null && runner.LevelData != null && runner.LevelData.Ppq > 0)
            {
                float beatPhase = Mathf.Repeat(runner.CurrentSongTick / (float)runner.LevelData.Ppq, 1f);
                // The float reaches its highest point exactly on every authored beat.
                bob = Mathf.Cos(beatPhase * Mathf.PI * 2f) * idleBobAmplitude;
                rock = Mathf.Sin(beatPhase * Mathf.PI * 2f) * idleRockDegrees;
            }

            visualRoot.localPosition = baseOffset + Vector3.up * bob;
            visualRoot.localScale = Vector3.one * scale;
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, rock);
        }

        private void SetHeldItemPose(Vector3 localAnchor, float rotationDegrees)
        {
            if (heldProjectile == null || visualRoot == null)
                return;
            heldProjectile.SetCapturedPose(
                visualRoot.TransformPoint(localAnchor),
                rotationDegrees,
                heldProjectile.HeldScaleMultiplier);
        }

        private void FinishHeldProjectile()
        {
            if (heldProjectile != null)
                heldProjectile.FinishCaptured();
            heldProjectile = null;
        }

        private bool TryGetTimelineMs(out double timelineMs)
        {
            timelineMs = 0.0;
            if (runner == null || runner.BeatClock == null
                || !runner.BeatClock.TryGetTimelinePositionMs(out int value))
            {
                return false;
            }
            timelineMs = value;
            return true;
        }

        private void ResolveReferences()
        {
            if (visualRoot == null && spriteRenderer != null)
                visualRoot = spriteRenderer.transform;
            if (spriteRenderer == null && visualRoot != null)
                spriteRenderer = visualRoot.GetComponent<SpriteRenderer>();
            if (runner == null)
                runner = GetComponentInParent<OtterGoblinDemo1Runner>();
        }

        private void HideLegacyRenderers()
        {
            string[] legacyNames =
            {
                "Belly", "Head", "Muzzle", "EarL", "EarR", "EyeL", "EyeR", "Nose", "GuardPaw", "ShellGuard"
            };
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == spriteRenderer || renderer.name == "Shield")
                    continue;
                if (Array.IndexOf(legacyNames, renderer.name) >= 0)
                    renderer.enabled = false;
            }
        }

        private void ShowIdlePreview()
        {
            if (visualRoot == null || spriteRenderer == null)
                return;
            ApplyPose(idleFrame, idleLocalOffset, idleScale, false);
        }

        private static int LoopIndex(int index, Sprite[] frames)
        {
            return frames == null || frames.Length == 0 ? 0 : Mathf.Abs(index) % frames.Length;
        }

        private static Sprite GetFrame(Sprite[] frames, int index, Sprite fallback)
        {
            if (frames == null || frames.Length == 0)
                return fallback;
            Sprite frame = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
            return frame != null ? frame : fallback;
        }

        private static float Smooth(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
