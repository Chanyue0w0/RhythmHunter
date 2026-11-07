using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    [ExecuteAlways, RequireComponent(typeof(Light)), DisallowMultipleComponent]
    public sealed class Art2TorchFlicker : MonoBehaviour
    {
        [Min(0)] [SerializeField] private float baseIntensity = 10f;
        [Tooltip("Maximum fractional brightness variation; 0.15 means +/-15%.")]
        [Range(0, 0.5f)] [SerializeField] private float flickerAmount = 0.15f;
        [Min(0)] [SerializeField] private float flickerSpeed = 2.5f;
        [SerializeField] private float noiseSeed = 17f;
        [SerializeField] private bool previewInEditor = true;
        private Light torch;
        private float originalIntensity;
        private double startTime;

        private void OnEnable()
        {
            torch = GetComponent<Light>();
            originalIntensity = torch.intensity;
            startTime = Time.unscaledTimeAsDouble;
#if UNITY_EDITOR
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += BeginPreview;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += EndPreview;
            UnityEditor.EditorApplication.update += RefreshPreview;
#endif
        }

        private void Update()
        {
            if (!Application.IsPlaying(gameObject)) return;
            // Visual ambience only: independent of combat beats and Team Skill pauses.
            float time = (float)(Time.unscaledTimeAsDouble - startTime) * flickerSpeed;
            ApplyFlicker(time);
        }

        private void ApplyFlicker(float time)
        {
            float noise = Mathf.PerlinNoise(noiseSeed, time) * 0.75f
                + Mathf.PerlinNoise(noiseSeed + 41.7f, time * 2.3f) * 0.25f;
            torch.intensity = Mathf.Max(0, baseIntensity) *
                (1 + (Mathf.Clamp01(noise) * 2 - 1) * Mathf.Clamp(flickerAmount, 0, 0.5f));
            if (TryGetComponent<Art2TorchHalo>(out var halo)) halo.SyncVisual();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= BeginPreview;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= EndPreview;
            UnityEditor.EditorApplication.update -= RefreshPreview;
            RestorePreview();
#endif
            if (torch != null) torch.intensity = originalIntensity;
        }

#if UNITY_EDITOR
        private bool previewing;
        private float previewIntensity;
        private static double nextPreviewRefresh;

        private void RefreshPreview()
        {
            // Recover if rendering was interrupted, and share one 30 Hz repaint across torches.
            RestorePreview();
            if (Application.isPlaying || !previewInEditor || UnityEditor.EditorApplication.isCompiling) return;
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now < nextPreviewRefresh) return;
            nextPreviewRefresh = now + 1.0 / 30.0;
            UnityEditor.SceneView.RepaintAll();
        }

        private void BeginPreview(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
        {
            if (Application.isPlaying || !previewInEditor || torch == null ||
                (camera.cameraType != CameraType.SceneView && camera.cameraType != CameraType.Game)) return;
            RestorePreview();
            previewIntensity = torch.intensity;
            previewing = true;
            ApplyFlicker((float)UnityEditor.EditorApplication.timeSinceStartup * flickerSpeed);
        }

        private void EndPreview(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera) => RestorePreview();

        private void RestorePreview()
        {
            if (previewing && torch != null) torch.intensity = previewIntensity;
            previewing = false;
        }
#endif
    }
}
