using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    /// <summary>
    /// Reusable projectile motion driven by absolute FMOD timeline positions.
    /// Author rhythm in beats and inject launch/arrival times; never author a fixed travel duration.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RhythmTimelineProjectile : MonoBehaviour
    {
        public enum OtterReactionKind
        {
            Crack,
            Food
        }

        [SerializeField, Min(0f)] private float arcHeight = 0.8f;
        [SerializeField] private bool rotateDuringFlight = true;
        [SerializeField, Min(0f)] private float rotationTurns = 2.5f;
        [SerializeField, Min(0.01f)] private float resolveFadeSeconds = 0.14f;

        [Header("Otter Interaction")]
        [SerializeField] private OtterReactionKind otterReaction = OtterReactionKind.Crack;
        [SerializeField, Min(0.01f)] private float heldScaleMultiplier = 1f;

        private FmodBeatClock beatClock;
        private SpriteRenderer spriteRenderer;
        private Vector3 launchPosition;
        private Vector3 arrivalPosition;
        private Transform arrivalTarget;
        private Vector3 arrivalTargetLocalOffset;
        private Vector3 baseScale;
        private double launchTimelineMs;
        private double fallbackLaunchTime;
        private float configuredArcHeight;
        private float resolvedAt;
        private bool configured;
        private bool flightStarted;
        private bool resolved;
        private bool caught;
        private bool captured;
        private bool shattered;

        public double ArrivalTimelineMs { get; private set; }
        public bool IsResolved => resolved;
        public bool RotateDuringFlight => rotateDuringFlight;
        public OtterReactionKind OtterReaction => otterReaction;
        public float HeldScaleMultiplier => heldScaleMultiplier;

        public void ConfigureAppearance(bool shouldRotate)
        {
            rotateDuringFlight = shouldRotate;
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
        }

        private void Update()
        {
            if (!configured)
                return;

            if (captured)
                return;

            if (resolved)
            {
                UpdateResolveEffect();
                return;
            }

            double timelineMs = GetTimelineMs();
            if (!flightStarted)
            {
                if (timelineMs < launchTimelineMs)
                    return;
                flightStarted = true;
                spriteRenderer.enabled = true;
                UpdateFlightPose(0f);
            }

            float progress = Mathf.Clamp01((float)((timelineMs - launchTimelineMs)
                / Mathf.Max(1f, (float)(ArrivalTimelineMs - launchTimelineMs))));
            UpdateFlightPose(progress);

            if (timelineMs > ArrivalTimelineMs + 650.0)
                Resolve(false);
        }

        public void Launch(
            FmodBeatClock configuredBeatClock,
            Vector3 from,
            Vector3 to,
            double configuredLaunchTimelineMs,
            double configuredArrivalTimelineMs,
            float laneArcOffset)
        {
            LaunchInternal(
                configuredBeatClock,
                from,
                to,
                null,
                Vector3.zero,
                configuredLaunchTimelineMs,
                configuredArrivalTimelineMs,
                laneArcOffset);
        }

        public void LaunchToTarget(
            FmodBeatClock configuredBeatClock,
            Vector3 from,
            Transform configuredArrivalTarget,
            Vector3 configuredArrivalLocalOffset,
            double configuredLaunchTimelineMs,
            double configuredArrivalTimelineMs,
            float laneArcOffset)
        {
            Vector3 fallbackArrival = configuredArrivalTarget != null
                ? configuredArrivalTarget.TransformPoint(configuredArrivalLocalOffset)
                : from;
            LaunchInternal(
                configuredBeatClock,
                from,
                fallbackArrival,
                configuredArrivalTarget,
                configuredArrivalLocalOffset,
                configuredLaunchTimelineMs,
                configuredArrivalTimelineMs,
                laneArcOffset);
        }

        private void LaunchInternal(
            FmodBeatClock configuredBeatClock,
            Vector3 from,
            Vector3 to,
            Transform configuredArrivalTarget,
            Vector3 configuredArrivalLocalOffset,
            double configuredLaunchTimelineMs,
            double configuredArrivalTimelineMs,
            float laneArcOffset)
        {
            beatClock = configuredBeatClock;
            launchPosition = from;
            arrivalPosition = to;
            arrivalTarget = configuredArrivalTarget;
            arrivalTargetLocalOffset = configuredArrivalLocalOffset;
            launchTimelineMs = configuredLaunchTimelineMs;
            ArrivalTimelineMs = System.Math.Max(configuredLaunchTimelineMs + 1.0, configuredArrivalTimelineMs);
            configuredArcHeight = Mathf.Max(0f, arcHeight + laneArcOffset);
            double currentTimelineMs = configuredLaunchTimelineMs;
            if (beatClock != null && beatClock.TryGetTimelinePositionMs(out int timelineMs))
                currentTimelineMs = timelineMs;
            fallbackLaunchTime = Time.unscaledTimeAsDouble
                + System.Math.Max(0.0, configuredLaunchTimelineMs - currentTimelineMs) / 1000.0;
            configured = true;
            flightStarted = currentTimelineMs >= configuredLaunchTimelineMs;
            resolved = false;
            caught = false;
            captured = false;
            shattered = false;
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.enabled = flightStarted;
            spriteRenderer.color = Color.white;
            transform.localScale = baseScale;
            UpdateFlightPose(0f);
        }

        public void Resolve(bool wasCaught)
        {
            if (resolved || captured)
                return;

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.enabled = true;
            UpdateFlightPose(1f);
            resolved = true;
            caught = wasCaught;
            resolvedAt = Time.unscaledTime;
            spriteRenderer.color = wasCaught
                ? new Color(0.45f, 1f, 0.72f, 1f)
                : new Color(1f, 0.35f, 0.3f, 1f);
        }

        public void CaptureForOtter()
        {
            if (captured)
                return;
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            captured = true;
            configured = true;
            flightStarted = true;
            resolved = true;
            caught = true;
            spriteRenderer.enabled = true;
            spriteRenderer.color = Color.white;
        }

        public void SetCapturedPose(Vector3 worldPosition, float rotationDegrees, float scaleMultiplier)
        {
            if (!captured)
                return;
            transform.position = worldPosition;
            transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            transform.localScale = baseScale * Mathf.Max(0.01f, scaleMultiplier);
        }

        public bool ShatterAt(Vector3 impactWorldPosition, double millisecondsPerBeat)
        {
            if (shattered)
                return false;
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null)
                return false;

            float lifetimeSeconds = Mathf.Clamp((float)(millisecondsPerBeat / 1000.0 * 1.25), 0.45f, 1.2f);
            bool spawned = ProjectileSpriteShatter.Spawn(
                spriteRenderer,
                transform,
                impactWorldPosition,
                lifetimeSeconds);
            if (!spawned)
                return false;

            shattered = true;
            spriteRenderer.enabled = false;
            return true;
        }

        public void FinishCaptured()
        {
            if (this != null)
                Destroy(gameObject);
        }

        private double GetTimelineMs()
        {
            if (beatClock != null && beatClock.TryGetTimelinePositionMs(out int timelineMs))
                return timelineMs;
            return launchTimelineMs + (Time.unscaledTimeAsDouble - fallbackLaunchTime) * 1000.0;
        }

        private void UpdateFlightPose(float progress)
        {
            Vector3 currentArrival = arrivalTarget != null
                ? arrivalTarget.TransformPoint(arrivalTargetLocalOffset)
                : arrivalPosition;
            Vector3 position = Vector3.LerpUnclamped(launchPosition, currentArrival, progress);
            position.y += Mathf.Sin(progress * Mathf.PI) * configuredArcHeight;
            transform.position = position;
            transform.rotation = rotateDuringFlight
                ? Quaternion.Euler(0f, 0f, -360f * rotationTurns * progress)
                : Quaternion.identity;
        }

        private void UpdateResolveEffect()
        {
            float progress = Mathf.Clamp01((Time.unscaledTime - resolvedAt) / resolveFadeSeconds);
            float pulse = caught ? Mathf.Sin(progress * Mathf.PI) * 0.35f : -progress * 0.2f;
            transform.localScale = baseScale * (1f + pulse);
            Color color = spriteRenderer.color;
            color.a = 1f - progress;
            spriteRenderer.color = color;
            if (progress >= 1f)
                Destroy(gameObject);
        }
    }
}
