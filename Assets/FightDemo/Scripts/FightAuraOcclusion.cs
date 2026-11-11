using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>Temporary silhouette depth for transparent hero cards. Owned by the aura, not the actor.</summary>
    [DefaultExecutionOrder(230)]
    public sealed class FightAuraOcclusion : MonoBehaviour
    {
        [Tooltip("Depth-only alpha-clipped sprite material; leaves the character's own material unchanged.")]
        [SerializeField] private Material silhouetteDepthMaterial;
        [SerializeField] private bool enableOcclusion = true;
        private SpriteRenderer source, mask;
        public void Bind(SpriteRenderer renderer)
        {
            source = renderer;
            if (source == null || silhouetteDepthMaterial == null || !enableOcclusion) return;
            var go = new GameObject("Aura_CharacterDepth"); go.transform.SetParent(transform, false);
            mask = go.AddComponent<SpriteRenderer>(); mask.sharedMaterial = silhouetteDepthMaterial;
            mask.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mask.receiveShadows = false;
            LateUpdate();
        }
        private void LateUpdate()
        {
            if (mask == null) return;
            mask.enabled = enableOcclusion && source != null && source.enabled && source.gameObject.activeInHierarchy;
            if (!mask.enabled) return;
            mask.sprite = source.sprite; mask.flipX = source.flipX; mask.flipY = source.flipY; mask.color = source.color;
            mask.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            // Root effect is unparented and unit scale; preserve authored child dimensions.
            var scale = transform.lossyScale; var desired = source.transform.lossyScale;
            mask.transform.localScale = new Vector3(desired.x / Safe(scale.x), desired.y / Safe(scale.y), desired.z / Safe(scale.z));
        }
        private static float Safe(float value) => Mathf.Abs(value) < .0001f ? 1 : value;
    }
}
