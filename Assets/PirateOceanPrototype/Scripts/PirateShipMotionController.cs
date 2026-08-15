using UnityEngine;

namespace RhythmHunter.PirateOceanPrototype
{
    /// <summary>
    /// Applies 2D ship motion to a visual-only root. Combat slot transforms stay
    /// under a separate stable root so gameplay coordinates never drift.
    /// </summary>
    public sealed class PirateShipMotionController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Transform motionVisualRoot;
        [SerializeField] private Transform stableCombatRoot;

        [Header("Ship Motion")]
        [SerializeField, Range(0f, 2f)] private float motionIntensity = 1f;
        [SerializeField, Range(0f, 0.75f)] private float heaveAmplitude = 0.14f;
        [SerializeField, Range(0f, 0.6f)] private float swayAmplitude = 0.08f;
        [SerializeField, Range(0f, 12f)] private float rollDegrees = 2.8f;
        [Tooltip("Simulates 2D pitch by subtly compressing and stretching the visual root vertically.")]
        [SerializeField, Range(0f, 0.12f)] private float pitchScaleAmount = 0.025f;
        [SerializeField, Range(0.05f, 3f)] private float motionSpeed = 0.78f;
        [SerializeField, Range(0f, 20f)] private float smoothing = 8f;

        [Header("Edit Mode Preview")]
        [Tooltip("Change this value to preview a static ship pose without entering Play Mode.")]
        [SerializeField, Range(0f, 12f)] private float previewPhase;

        [SerializeField, HideInInspector] private Vector3 baseLocalPosition;
        [SerializeField, HideInInspector] private Quaternion baseLocalRotation = Quaternion.identity;
        [SerializeField, HideInInspector] private Vector3 baseLocalScale = Vector3.one;
        [SerializeField, HideInInspector] private bool baselineCaptured;

        private float runtimeTime;

        public Transform MotionVisualRoot => motionVisualRoot;
        public Transform StableCombatRoot => stableCombatRoot;
        public float MotionIntensity => motionIntensity;
        public float HeaveAmplitude => heaveAmplitude;
        public float SwayAmplitude => swayAmplitude;
        public float RollDegrees => rollDegrees;
        public float PitchScaleAmount => pitchScaleAmount;
        public float MotionSpeed => motionSpeed;
        public float Smoothing => smoothing;

        public void Configure(Transform visualRoot, Transform logicRoot)
        {
            motionVisualRoot = visualRoot;
            stableCombatRoot = logicRoot;
            CaptureCurrentPoseAsBaseline();
            ApplyMotion(previewPhase, true);
        }

        private void Awake()
        {
            EnsureBaseline();
            runtimeTime = previewPhase;
        }

        private void Update()
        {
            runtimeTime += Time.deltaTime;
            ApplyMotion(runtimeTime, false);
        }

        private void OnValidate()
        {
            EnsureBaseline();
            if (!Application.isPlaying)
                ApplyMotion(previewPhase, true);
        }

        public void SetMotionIntensity(float value)
        {
            motionIntensity = Mathf.Clamp(value, 0f, 2f);
            ApplyCurrentState();
        }

        public void SetHeaveAmplitude(float value)
        {
            heaveAmplitude = Mathf.Clamp(value, 0f, 0.75f);
            ApplyCurrentState();
        }

        public void SetSwayAmplitude(float value)
        {
            swayAmplitude = Mathf.Clamp(value, 0f, 0.6f);
            ApplyCurrentState();
        }

        public void SetRollDegrees(float value)
        {
            rollDegrees = Mathf.Clamp(value, 0f, 12f);
            ApplyCurrentState();
        }

        public void SetPitchScaleAmount(float value)
        {
            pitchScaleAmount = Mathf.Clamp(value, 0f, 0.12f);
            ApplyCurrentState();
        }

        public void SetMotionSpeed(float value)
        {
            motionSpeed = Mathf.Clamp(value, 0.05f, 3f);
        }

        [ContextMenu("Capture Current Pose As Motion Baseline")]
        public void CaptureCurrentPoseAsBaseline()
        {
            if (motionVisualRoot == null)
                return;

            baseLocalPosition = motionVisualRoot.localPosition;
            baseLocalRotation = motionVisualRoot.localRotation;
            baseLocalScale = motionVisualRoot.localScale;
            baselineCaptured = true;
        }

        [ContextMenu("Reset Ship Visuals To Baseline")]
        public void ResetToBaseline()
        {
            if (motionVisualRoot == null || !baselineCaptured)
                return;

            motionVisualRoot.localPosition = baseLocalPosition;
            motionVisualRoot.localRotation = baseLocalRotation;
            motionVisualRoot.localScale = baseLocalScale;
        }

        private void ApplyCurrentState()
        {
            ApplyMotion(Application.isPlaying ? runtimeTime : previewPhase, true);
        }

        private void ApplyMotion(float time, bool immediate)
        {
            if (motionVisualRoot == null)
                return;

            EnsureBaseline();
            if (!baselineCaptured)
                return;

            float phase = time * motionSpeed;
            float heaveWave = Mathf.Sin(phase) + Mathf.Sin(phase * 1.83f) * 0.22f;
            float swayWave = Mathf.Sin(phase * 0.61f) + Mathf.Sin(phase * 1.37f) * 0.15f;
            float rollWave = Mathf.Sin(phase * 0.78f) + Mathf.Sin(phase * 1.51f) * 0.18f;
            float pitchWave = Mathf.Sin(phase * 0.93f);

            Vector3 targetPosition = baseLocalPosition;
            targetPosition.x += swayWave * swayAmplitude * motionIntensity;
            targetPosition.y += heaveWave * heaveAmplitude * motionIntensity;

            Quaternion targetRotation = baseLocalRotation
                * Quaternion.Euler(0f, 0f, rollWave * rollDegrees * motionIntensity);

            Vector3 targetScale = baseLocalScale;
            targetScale.y *= 1f + pitchWave * pitchScaleAmount * motionIntensity;

            if (immediate || smoothing <= 0f)
            {
                motionVisualRoot.localPosition = targetPosition;
                motionVisualRoot.localRotation = targetRotation;
                motionVisualRoot.localScale = targetScale;
                return;
            }

            float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            motionVisualRoot.localPosition = Vector3.Lerp(motionVisualRoot.localPosition, targetPosition, blend);
            motionVisualRoot.localRotation = Quaternion.Slerp(motionVisualRoot.localRotation, targetRotation, blend);
            motionVisualRoot.localScale = Vector3.Lerp(motionVisualRoot.localScale, targetScale, blend);
        }

        private void EnsureBaseline()
        {
            if (!baselineCaptured && motionVisualRoot != null)
                CaptureCurrentPoseAsBaseline();
        }
    }
}
