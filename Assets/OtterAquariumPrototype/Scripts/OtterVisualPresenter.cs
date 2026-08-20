using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    public sealed class OtterVisualPresenter : MonoBehaviour
    {
        [SerializeField] private OtterMovementController movement;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private SpriteRenderer shadow;
        [SerializeField] private SpriteRenderer[] sortedRenderers;
        [SerializeField] private float bobAmount = 0.08f;
        [SerializeField] private float bobSpeed = 3.5f;

        private Vector3 visualBaseScale = Vector3.one;
        private Vector3 bodyBasePosition;

        public void Configure(
            OtterMovementController movementController,
            Transform configuredVisualRoot,
            Transform configuredBodyRoot,
            SpriteRenderer configuredShadow,
            SpriteRenderer[] renderers)
        {
            movement = movementController;
            visualRoot = configuredVisualRoot;
            bodyRoot = configuredBodyRoot;
            shadow = configuredShadow;
            sortedRenderers = renderers;
            CacheBasePose();
        }

        private void Awake()
        {
            CacheBasePose();
        }

        private void LateUpdate()
        {
            if (movement == null || visualRoot == null || bodyRoot == null)
                return;

            Vector2 facing = movement.FacingDirection;
            float flip = Mathf.Abs(facing.x) > 0.08f ? Mathf.Sign(facing.x) : Mathf.Sign(visualRoot.localScale.x);
            if (Mathf.Approximately(flip, 0f))
                flip = 1f;

            float targetTilt = movement.IsSliding ? -8f * flip : -movement.Velocity.x * 1.4f;
            float scaleX = movement.IsSliding ? 1.18f : 1f;
            float scaleY = movement.IsSliding ? 0.82f : 1f;
            visualRoot.localScale = Vector3.Lerp(
                visualRoot.localScale,
                new Vector3(visualBaseScale.x * flip * scaleX, visualBaseScale.y * scaleY, visualBaseScale.z),
                Time.deltaTime * 12f);
            visualRoot.localRotation = Quaternion.Lerp(
                visualRoot.localRotation,
                Quaternion.Euler(0f, 0f, targetTilt),
                Time.deltaTime * 10f);

            float bob = movement.CurrentSurface == AquariumSurfaceType.Water
                ? Mathf.Sin(Time.time * bobSpeed) * bobAmount
                : 0f;
            bodyRoot.localPosition = Vector3.Lerp(
                bodyRoot.localPosition,
                bodyBasePosition + Vector3.up * bob,
                Time.deltaTime * 10f);

            if (shadow != null)
            {
                Color color = shadow.color;
                color.a = movement.CurrentSurface == AquariumSurfaceType.Water ? 0.12f : 0.28f;
                shadow.color = color;
            }

            int baseOrder = Mathf.RoundToInt(-transform.position.y * 20f) + 200;
            if (sortedRenderers != null)
            {
                for (int i = 0; i < sortedRenderers.Length; i++)
                {
                    if (sortedRenderers[i] != null)
                        sortedRenderers[i].sortingOrder = baseOrder + i;
                }
            }
        }

        private void CacheBasePose()
        {
            if (visualRoot != null)
                visualBaseScale = new Vector3(Mathf.Abs(visualRoot.localScale.x), visualRoot.localScale.y, visualRoot.localScale.z);
            if (bodyRoot != null)
                bodyBasePosition = bodyRoot.localPosition;
        }
    }
}
