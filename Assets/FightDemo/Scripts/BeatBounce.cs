using System;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Gives a decorative scene layer a subtle scale pulse on every combat beat.
    /// </summary>
    public sealed class BeatBounce : MonoBehaviour
    {
        private enum BeatParity
        {
            Every,
            Odd,
            Even
        }

        [SerializeField] private FightCombatController beatSource;
        [SerializeField, Min(1f)] private float fallbackBpm = 120f;
        [SerializeField, Min(0f)] private float scaleAmount = 0.03f;
        [SerializeField, Range(0.05f, 1f)] private float durationInBeats = 0.45f;
        [SerializeField] private BeatParity beatParity;

        private Vector3 restLocalScale;
        private double beatAnchorTime;
        private long beatAnchorIndex;
        private double secondsPerBeat;
        private bool hasBeatAnchor;

        private void Configure(
            FightCombatController source,
            float amount,
            float duration,
            BeatParity parity)
        {
            Unsubscribe();
            beatSource = source;
            fallbackBpm = 120f;
            scaleAmount = Mathf.Max(0f, amount);
            durationInBeats = Mathf.Clamp(duration, 0.05f, 1f);
            beatParity = parity;
            restLocalScale = transform.localScale;
            secondsPerBeat = 60d / fallbackBpm;
            Subscribe();
        }

        public static void EnsureSceneAnimation(FightCombatController source)
        {
            if (source == null)
                return;

            Transform[] sceneTransforms = FindObjectsByType<Transform>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (Transform sceneTransform in sceneTransforms)
            {
                if (sceneTransform.gameObject.scene != source.gameObject.scene)
                    continue;

                string objectName = sceneTransform.name;
                if (objectName == "StoneLayer (Beat Pulse)")
                {
                    Ensure(sceneTransform, source, 0.015f, 0.45f, BeatParity.Every);
                    continue;
                }

                if (objectName == "SlotGround")
                {
                    Ensure(sceneTransform, source, 0.04f, 0.6f, BeatParity.Every);
                    continue;
                }

                if (!objectName.StartsWith("Grass_", StringComparison.Ordinal) ||
                    !int.TryParse(objectName.Substring("Grass_".Length), out int zeroBasedSlice))
                {
                    continue;
                }

                BeatParity sliceParity = zeroBasedSlice % 2 == 0
                    ? BeatParity.Odd
                    : BeatParity.Even;
                Ensure(sceneTransform, source, 0.03f, 0.45f, sliceParity);
            }
        }

        private void Awake()
        {
            restLocalScale = transform.localScale;
            secondsPerBeat = 60d / Mathf.Max(1f, fallbackBpm);
        }

        private void OnEnable()
        {
            restLocalScale = transform.localScale;
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            transform.localScale = restLocalScale;
        }

        private void Update()
        {
            double now = Time.unscaledTimeAsDouble;
            double beatPosition = hasBeatAnchor
                ? beatAnchorIndex + Math.Max(0d, now - beatAnchorTime) / Math.Max(0.001d, secondsPerBeat)
                : now / Math.Max(0.001d, secondsPerBeat);
            long beatIndex = (long)Math.Floor(beatPosition);
            float beatProgress = (float)(beatPosition - beatIndex);

            bool isOddBeat = PositiveModulo(beatIndex, 2) != 0;
            bool shouldPulse = beatParity == BeatParity.Every ||
                               (beatParity == BeatParity.Odd && isOddBeat) ||
                               (beatParity == BeatParity.Even && !isOddBeat);
            if (!shouldPulse || beatProgress >= durationInBeats)
            {
                transform.localScale = restLocalScale;
                return;
            }

            float pulseProgress = Mathf.Clamp01(beatProgress / Math.Max(0.05f, durationInBeats));
            float multiplier = 1f + Mathf.Sin(pulseProgress * Mathf.PI * 2f) * scaleAmount;
            transform.localScale = restLocalScale * multiplier;
        }

        private void OnBeat(FmodBeatClock.BeatSnapshot beat)
        {
            beatAnchorTime = Time.unscaledTimeAsDouble;
            beatAnchorIndex = beat.GlobalBeat;
            secondsPerBeat = 60d / Math.Max(1d, beat.Tempo);
            hasBeatAnchor = true;
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || beatSource == null)
                return;

            beatSource.FightBeat -= OnBeat;
            beatSource.FightBeat += OnBeat;
        }

        private void Unsubscribe()
        {
            if (beatSource != null)
                beatSource.FightBeat -= OnBeat;
        }

        private static void Ensure(
            Transform target,
            FightCombatController source,
            float amount,
            float duration,
            BeatParity parity)
        {
            BeatBounce pulse = target.GetComponent<BeatBounce>();
            if (pulse == null)
                pulse = target.gameObject.AddComponent<BeatBounce>();

            pulse.Configure(source, amount, duration, parity);
        }

        private static long PositiveModulo(long value, long modulus)
        {
            if (modulus <= 0)
                return 0;

            long result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            fallbackBpm = Mathf.Max(1f, fallbackBpm);
            scaleAmount = Mathf.Max(0f, scaleAmount);
            durationInBeats = Mathf.Clamp(durationInBeats, 0.05f, 1f);
        }
#endif
    }
}
