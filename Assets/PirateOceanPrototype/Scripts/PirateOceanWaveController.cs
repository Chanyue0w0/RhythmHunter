using UnityEngine;

namespace RhythmHunter.PirateOceanPrototype
{
    /// <summary>
    /// Animates layered 2D wave segments without moving gameplay-space objects.
    /// Runtime UI can use the public setters later; for now every parameter is
    /// directly adjustable in the Inspector.
    /// </summary>
    public sealed class PirateOceanWaveController : MonoBehaviour
    {
        public enum TravelDirection
        {
            Left = -1,
            Right = 1
        }

        [Header("Sea State")]
        [SerializeField, Range(0f, 2f)] private float intensity = 1f;
        [SerializeField, Range(0f, 1.2f)] private float waveHeight = 0.34f;
        [SerializeField, Range(0f, 5f)] private float waveSpeed = 1.25f;
        [SerializeField, Range(0.1f, 3f)] private float frequency = 0.82f;
        [SerializeField, Range(0f, 0.8f)] private float horizontalDrift = 0.16f;
        [SerializeField, Range(0f, 18f)] private float crestTilt = 5f;
        [SerializeField] private TravelDirection direction = TravelDirection.Right;

        [Header("Foam")]
        [SerializeField, Range(0f, 1f)] private float foamAmount = 0.72f;
        [SerializeField, Range(0f, 2f)] private float foamPulse = 0.55f;

        [Header("Edit Mode Preview")]
        [Tooltip("Change this value to preview the wave shape without entering Play Mode.")]
        [SerializeField, Range(0f, 12f)] private float previewPhase;

        [Header("Generated Wave Segments")]
        [SerializeField] private PirateOceanSurface continuousSurface;
        [SerializeField] private Transform[] farWaveSegments;
        [SerializeField] private Transform[] midWaveSegments;
        [SerializeField] private Transform[] nearWaveSegments;
        [SerializeField] private Transform[] foamWaveSegments;
        [SerializeField] private SpriteRenderer[] foamSegments;

        [SerializeField, HideInInspector] private Vector3[] farBasePositions;
        [SerializeField, HideInInspector] private Vector3[] farBaseScales;
        [SerializeField, HideInInspector] private Vector3[] midBasePositions;
        [SerializeField, HideInInspector] private Vector3[] midBaseScales;
        [SerializeField, HideInInspector] private Vector3[] nearBasePositions;
        [SerializeField, HideInInspector] private Vector3[] nearBaseScales;
        [SerializeField, HideInInspector] private Vector3[] foamBasePositions;
        [SerializeField, HideInInspector] private Vector3[] foamBaseScales;
        [SerializeField, HideInInspector] private Color[] foamBaseColors;

        private float runtimeTime;

        public float Intensity => intensity;
        public float WaveHeight => waveHeight;
        public float WaveSpeed => waveSpeed;
        public float Frequency => frequency;
        public float FoamAmount => foamAmount;
        public TravelDirection Direction => direction;
        public PirateOceanSurface ContinuousSurface => continuousSurface;

        public void Configure(
            PirateOceanSurface generatedSurface,
            Transform[] farSegments,
            Transform[] midSegments,
            Transform[] nearSegments,
            SpriteRenderer[] generatedFoamSegments)
        {
            continuousSurface = generatedSurface;
            farWaveSegments = farSegments;
            midWaveSegments = midSegments;
            nearWaveSegments = nearSegments;
            foamSegments = generatedFoamSegments;
            foamWaveSegments = new Transform[foamSegments != null ? foamSegments.Length : 0];
            for (int i = 0; i < foamWaveSegments.Length; i++)
                foamWaveSegments[i] = foamSegments[i] != null ? foamSegments[i].transform : null;
            CaptureCurrentPoseAsBaseline();
            ApplyWave(previewPhase);
        }

        private void Awake()
        {
            EnsureBaselineData();
            runtimeTime = previewPhase;
        }

        private void Update()
        {
            runtimeTime += Time.deltaTime;
            ApplyWave(runtimeTime);
        }

        private void OnValidate()
        {
            EnsureBaselineData();
            if (!Application.isPlaying)
                ApplyWave(previewPhase);
        }

        public void SetIntensity(float value)
        {
            intensity = Mathf.Clamp(value, 0f, 2f);
            ApplyCurrentState();
        }

        public void SetWaveHeight(float value)
        {
            waveHeight = Mathf.Clamp(value, 0f, 1.2f);
            ApplyCurrentState();
        }

        public void SetWaveSpeed(float value)
        {
            waveSpeed = Mathf.Clamp(value, 0f, 5f);
        }

        public void SetFrequency(float value)
        {
            frequency = Mathf.Clamp(value, 0.1f, 3f);
            ApplyCurrentState();
        }

        public void SetHorizontalDrift(float value)
        {
            horizontalDrift = Mathf.Clamp(value, 0f, 0.8f);
            ApplyCurrentState();
        }

        public void SetFoamAmount(float value)
        {
            foamAmount = Mathf.Clamp01(value);
            ApplyCurrentState();
        }

        public void SetDirection(TravelDirection value)
        {
            direction = value;
            ApplyCurrentState();
        }

        [ContextMenu("Capture Current Pose As Wave Baseline")]
        public void CaptureCurrentPoseAsBaseline()
        {
            CapturePose(farWaveSegments, out farBasePositions, out farBaseScales);
            CapturePose(midWaveSegments, out midBasePositions, out midBaseScales);
            CapturePose(nearWaveSegments, out nearBasePositions, out nearBaseScales);

            CapturePose(foamWaveSegments, out foamBasePositions, out foamBaseScales);
            foamBaseColors = CaptureColors(foamSegments);
        }

        [ContextMenu("Reset Wave Segments To Baseline")]
        public void ResetSegmentsToBaseline()
        {
            ResetPose(farWaveSegments, farBasePositions, farBaseScales);
            ResetPose(midWaveSegments, midBasePositions, midBaseScales);
            ResetPose(nearWaveSegments, nearBasePositions, nearBaseScales);
            ResetPose(foamWaveSegments, foamBasePositions, foamBaseScales);

            if (foamSegments == null || foamBaseColors == null)
                return;

            int count = Mathf.Min(foamSegments.Length, foamBaseColors.Length);
            for (int i = 0; i < count; i++)
            {
                if (foamSegments[i] != null)
                    foamSegments[i].color = foamBaseColors[i];
            }
        }

        private void ApplyCurrentState()
        {
            ApplyWave(Application.isPlaying ? runtimeTime : previewPhase);
        }

        private void ApplyWave(float time)
        {
            if (!HasValidBaseline(farWaveSegments, farBasePositions, farBaseScales))
                return;

            float directionSign = (float)direction;
            ApplyBand(farWaveSegments, farBasePositions, farBaseScales, time, directionSign, 0.38f, 0.72f, 0.15f);
            ApplyBand(midWaveSegments, midBasePositions, midBaseScales, time, directionSign, 0.68f, 0.9f, 1.35f);
            ApplyBand(nearWaveSegments, nearBasePositions, nearBaseScales, time, directionSign, 1f, 1.08f, 2.6f);
            ApplyFoam(time, directionSign);
            if (continuousSurface != null)
                continuousSurface.ApplyWave(time, intensity, waveHeight, waveSpeed, frequency, directionSign);
        }

        private void ApplyBand(
            Transform[] segments,
            Vector3[] basePositions,
            Vector3[] baseScales,
            float time,
            float directionSign,
            float amplitudeMultiplier,
            float speedMultiplier,
            float phaseOffset)
        {
            if (!HasValidBaseline(segments, basePositions, baseScales))
                return;

            float amplitude = waveHeight * intensity * amplitudeMultiplier;
            for (int i = 0; i < segments.Length; i++)
            {
                Transform segment = segments[i];
                if (segment == null)
                    continue;

                float phase = i * frequency + time * waveSpeed * speedMultiplier * directionSign + phaseOffset;
                float wave = Mathf.Sin(phase);
                float crest = Mathf.Cos(phase);

                Vector3 position = basePositions[i];
                position.x += crest * horizontalDrift * intensity * amplitudeMultiplier;
                position.y += wave * amplitude;
                segment.localPosition = position;
                segment.localRotation = Quaternion.Euler(0f, 0f, crest * crestTilt * intensity * amplitudeMultiplier);

                Vector3 scale = baseScales[i];
                scale.y *= 1f + Mathf.Abs(wave) * 0.16f * intensity;
                segment.localScale = scale;
            }
        }

        private void ApplyFoam(float time, float directionSign)
        {
            if (!HasValidBaseline(foamWaveSegments, foamBasePositions, foamBaseScales))
                return;

            ApplyBand(foamWaveSegments, foamBasePositions, foamBaseScales, time, directionSign, 0.82f, 1.18f, 3.7f);

            if (foamSegments == null || foamBaseColors == null || foamBaseColors.Length != foamSegments.Length)
                return;

            for (int i = 0; i < foamSegments.Length; i++)
            {
                SpriteRenderer renderer = foamSegments[i];
                if (renderer == null)
                    continue;

                float phase = i * frequency + time * waveSpeed * 1.18f * directionSign + 3.7f;
                float pulse = Mathf.Lerp(1f - foamPulse * 0.5f, 1f, Mathf.Abs(Mathf.Sin(phase)));
                Color color = foamBaseColors[i];
                color.a *= foamAmount * pulse;
                renderer.color = color;
            }
        }

        private void EnsureBaselineData()
        {
            if (!HasValidBaseline(farWaveSegments, farBasePositions, farBaseScales)
                || !HasValidBaseline(midWaveSegments, midBasePositions, midBaseScales)
                || !HasValidBaseline(nearWaveSegments, nearBasePositions, nearBaseScales)
                || !HasValidBaseline(foamWaveSegments, foamBasePositions, foamBaseScales)
                || foamBaseColors == null
                || foamSegments == null
                || foamBaseColors.Length != foamSegments.Length)
            {
                CaptureCurrentPoseAsBaseline();
            }
        }

        private static void CapturePose(Transform[] segments, out Vector3[] positions, out Vector3[] scales)
        {
            int count = segments != null ? segments.Length : 0;
            positions = new Vector3[count];
            scales = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                if (segments[i] == null)
                    continue;

                positions[i] = segments[i].localPosition;
                scales[i] = segments[i].localScale;
            }
        }

        private static Color[] CaptureColors(SpriteRenderer[] renderers)
        {
            int count = renderers != null ? renderers.Length : 0;
            Color[] colors = new Color[count];
            for (int i = 0; i < count; i++)
                colors[i] = renderers[i] != null ? renderers[i].color : Color.white;
            return colors;
        }

        private static void ResetPose(Transform[] segments, Vector3[] positions, Vector3[] scales)
        {
            if (!HasValidBaseline(segments, positions, scales))
                return;

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null)
                    continue;

                segments[i].localPosition = positions[i];
                segments[i].localRotation = Quaternion.identity;
                segments[i].localScale = scales[i];
            }
        }

        private static bool HasValidBaseline(Transform[] segments, Vector3[] positions, Vector3[] scales)
        {
            return segments != null
                && positions != null
                && scales != null
                && segments.Length > 0
                && segments.Length == positions.Length
                && segments.Length == scales.Length;
        }
    }
}
