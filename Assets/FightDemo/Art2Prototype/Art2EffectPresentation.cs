using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    // Visual-only adapter on effect prefabs. Inactive in scenes without an Art2 camera.
    [DefaultExecutionOrder(200), DisallowMultipleComponent]
    public sealed class Art2EffectPresentation : MonoBehaviour
    {
        [SerializeField] private bool faceSpritesToCamera = true;
        [SerializeField] private bool faceBillboardParticlesToCamera = true;
        [SerializeField] private bool facePlanarMeshParticlesToCamera = true;
        [SerializeField] private bool keepAboveGround;
        [SerializeField] private float groundHeight;
        [SerializeField, Min(0)] private float groundClearance = .03f;
        private Camera view;
        private SpriteRenderer[] sprites;
        private Quaternion[] applied, rolls;
        private Renderer[] renderers;
        private ParticleSystemRenderer[] particles;
        private bool initialized;
        private Vector3 appliedGroundLift;

        private void OnEnable() { initialized = false; view = null; appliedGroundLift = Vector3.zero; }
        private void OnDisable()
        {
            transform.localPosition -= appliedGroundLift;
            appliedGroundLift = Vector3.zero;
        }
        private void LateUpdate()
        {
            if (!initialized) Initialize();
            if (!view || !view.isActiveAndEnabled) return;
            if (faceSpritesToCamera)
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (!sprites[i]) continue;
                    // Preserve authored roll and procedural Z spin between frames.
                    var t = sprites[i].transform;
                    var delta = Quaternion.Inverse(applied[i]) * t.rotation;
                    rolls[i] *= Quaternion.Euler(0, 0, Mathf.DeltaAngle(0, delta.eulerAngles.z));
                    applied[i] = view.transform.rotation * rolls[i];
                    t.rotation = applied[i];
                }
            if (keepAboveGround)
            {
                // Recompute from the authored position, rather than accumulating height as the camera moves.
                transform.localPosition -= appliedGroundLift;
                appliedGroundLift = Vector3.zero;
                float bottom = float.PositiveInfinity;
                foreach (var renderer in renderers)
                {
                    if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    if (renderer is ParticleSystemRenderer pr && (pr.renderMode == ParticleSystemRenderMode.None || pr.GetComponent<ParticleSystem>().particleCount == 0)) continue;
                    if (renderer.bounds.size.sqrMagnitude > .00001f) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
                }
                float lift = groundHeight + groundClearance - bottom;
                if (lift > 0 && !float.IsInfinity(lift))
                {
                    appliedGroundLift = transform.parent ? transform.parent.InverseTransformVector(Vector3.up * lift) : Vector3.up * lift;
                    transform.localPosition += appliedGroundLift;
                }
            }
        }
        private void Initialize()
        {
            initialized = true;
            foreach (var cameraRig in FindObjectsByType<Art2CameraController>(FindObjectsSortMode.None))
                if (cameraRig.gameObject.scene == gameObject.scene && cameraRig.TryGetComponent<Camera>(out var camera) && camera.isActiveAndEnabled)
                { view = camera; break; }
            if (!view) return;
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            particles = GetComponentsInChildren<ParticleSystemRenderer>(true);
            applied = new Quaternion[sprites.Length]; rolls = new Quaternion[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                applied[i] = sprites[i].transform.rotation;
                rolls[i] = Quaternion.Euler(0, 0, (Quaternion.Inverse(view.transform.rotation) * applied[i]).eulerAngles.z);
            }
            foreach (var renderer in particles)
            {
                if (facePlanarMeshParticlesToCamera && renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh != null)
                {
                    var size = renderer.mesh.bounds.size;
                    if (size.z <= Mathf.Max(size.x, size.y) * .05f)
                        renderer.alignment = ParticleSystemRenderSpace.View;
                }
                if (faceBillboardParticlesToCamera && (renderer.renderMode == ParticleSystemRenderMode.Billboard || renderer.renderMode == ParticleSystemRenderMode.VerticalBillboard || renderer.renderMode == ParticleSystemRenderMode.Stretch))
                {
                    if (renderer.renderMode == ParticleSystemRenderMode.VerticalBillboard) renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    renderer.alignment = ParticleSystemRenderSpace.View;
                }
                if (keepAboveGround)
                {
                    var main = renderer.GetComponent<ParticleSystem>().main;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                }
            }
        }
    }
}
