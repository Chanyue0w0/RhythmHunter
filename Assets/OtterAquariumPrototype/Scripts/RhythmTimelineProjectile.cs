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
        [SerializeField, Min(0f)] private float arcHeight = 0.8f;
        [SerializeField, Min(0f)] private float rotationTurns = 2.5f;
        [SerializeField, Min(0.01f)] private float resolveFadeSeconds = 0.14f;

        private FmodBeatClock beatClock;
        private SpriteRenderer spriteRenderer;
        private Vector3 launchPosition;
        private Vector3 arrivalPosition;
        private Vector3 baseScale;
        private double launchTimelineMs;
        private double fallbackLaunchTime;
        private double fallbackDurationSeconds;
        private float configuredArcHeight;
        private float resolvedAt;
        private bool launched;
        private bool resolved;
        private bool caught;

        public double ArrivalTimelineMs { get; private set; }
        public bool IsResolved => resolved;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
        }

        private void Update()
        {
            if (!launched)
                return;

            if (resolved)
            {
                UpdateResolveEffect();
                return;
            }

            double timelineMs = GetTimelineMs();
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
            beatClock = configuredBeatClock;
            launchPosition = from;
            arrivalPosition = to;
            launchTimelineMs = configuredLaunchTimelineMs;
            ArrivalTimelineMs = System.Math.Max(configuredLaunchTimelineMs + 1.0, configuredArrivalTimelineMs);
            configuredArcHeight = Mathf.Max(0f, arcHeight + laneArcOffset);
            fallbackLaunchTime = Time.unscaledTimeAsDouble;
            fallbackDurationSeconds = Mathf.Max(0.001f, (float)((ArrivalTimelineMs - launchTimelineMs) / 1000.0));
            launched = true;
            resolved = false;
            caught = false;
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.color = Color.white;
            transform.localScale = baseScale;
            UpdateFlightPose(0f);
        }

        public void Resolve(bool wasCaught)
        {
            if (resolved)
                return;

            UpdateFlightPose(1f);
            resolved = true;
            caught = wasCaught;
            resolvedAt = Time.unscaledTime;
            spriteRenderer.color = wasCaught
                ? new Color(0.45f, 1f, 0.72f, 1f)
                : new Color(1f, 0.35f, 0.3f, 1f);
        }

        private double GetTimelineMs()
        {
            if (beatClock != null && beatClock.TryGetTimelinePositionMs(out int timelineMs))
                return timelineMs;
            return launchTimelineMs + (Time.unscaledTimeAsDouble - fallbackLaunchTime)
                / fallbackDurationSeconds * (ArrivalTimelineMs - launchTimelineMs);
        }

        private void UpdateFlightPose(float progress)
        {
            Vector3 position = Vector3.LerpUnclamped(launchPosition, arrivalPosition, progress);
            position.y += Mathf.Sin(progress * Mathf.PI) * configuredArcHeight;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f, -360f * rotationTurns * progress);
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
