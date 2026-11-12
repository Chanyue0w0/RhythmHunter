using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    // Scene-only rendering adaptation. Slot order, damage, timing and prefab data are untouched.
    [DefaultExecutionOrder(90)]
    public sealed class Art2ActorPresentation : MonoBehaviour
    {
        [SerializeField] private FightUnitSlot[] slots;
        [SerializeField] private Material actorMaterial;
        [Header("Character Information")]
        [SerializeField] private Camera informationCamera;
        [SerializeField] private bool alignSlotInformation = true;
        [SerializeField] private float healthBarHeadGap = .25f;
        private readonly List<(Transform target, FightUnitSlot slot, Vector3 position, Quaternion rotation, bool health)> overlays = new();
        [Tooltip("Cast the current animation sprite silhouette into the Art2 main-light shadow map.")]
        [SerializeField] private bool castSpriteShadows = true;
        [Tooltip("Align the authored FootAnchor once on spawn; animation jumps remain free to move.")]
        [SerializeField] private bool alignFeetToGround = true;
        [SerializeField] private float actorGroundHeight = 0;
        private readonly Dictionary<Transform, Vector3> originalActorPositions = new();
        [SerializeField] private bool showContactShadows = true;
        [SerializeField] private Material contactShadowMaterial;
        [SerializeField] private Vector2 shadowSize = new Vector2(1.25f, 0.65f);
        [SerializeField] private Vector2 shadowGroundOffset;
        [Header("Contact Shadow Lighting")]
        [SerializeField] private bool contactShadowFollowsLight = true;
        [Tooltip("Main light driving the soft foot shadow. Directional lights use their direction, not their position.")]
        [SerializeField] private Light contactShadowLight;
        [SerializeField, Min(0)] private float contactShadowProjectionHeight = .25f;
        [SerializeField, Min(0)] private float contactShadowMaxOffset = .45f;
        [SerializeField] private float groundHeight = 0.025f;
        private GameObject[] actors;
        private GameObject[] shadows;
        private Transform[] feet;
        private Mesh shadowMesh;
        private readonly List<(SpriteRenderer renderer, int layer, int order, Material material,
            UnityEngine.Rendering.ShadowCastingMode casting, bool receiving)> originalSorting = new();

        private void OnEnable()
        {
            actors = new GameObject[slots != null ? slots.Length : 0];
            shadows = new GameObject[actors.Length];
            feet = new Transform[actors.Length];
            overlays.Clear();
            if (slots != null)
                foreach (var slot in slots)
                    if (slot != null)
                        foreach (Transform child in slot.transform)
                            if (child.name == "HealthFill" || child.name == "HealthBackground" || child.GetComponent<TextMesh>() != null)
                                overlays.Add((child, slot, child.localPosition, child.localRotation, child.name.StartsWith("Health")));
        }

        // Weapon replacement does not replace the actor root, so register its renderer explicitly.
        public void RegisterWeaponRenderer(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.gameObject.scene != gameObject.scene) return;
            originalSorting.RemoveAll(entry => entry.renderer == null);
            if (!originalSorting.Exists(entry => entry.renderer == renderer))
                originalSorting.Add((renderer, renderer.sortingLayerID, renderer.sortingOrder,
                    renderer.sharedMaterial, renderer.shadowCastingMode, renderer.receiveShadows));
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = 0;
            if (actorMaterial != null) renderer.sharedMaterial = actorMaterial;
            renderer.shadowCastingMode = castSpriteShadows ? UnityEngine.Rendering.ShadowCastingMode.TwoSided : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            RegisterFill(renderer);
        }

        private void RegisterFill(SpriteRenderer renderer)
        {
            var fill = GetComponent<Art2CharacterFillLighting>();
            var slot = renderer.GetComponentInParent<FightUnitSlot>();
            if (fill != null && slot != null) fill.Register(renderer, slot.Team);
        }

        private void LateUpdate()
        {
            if (slots == null) return;
            if (actors.Length != slots.Length) { OnDisable(); OnEnable(); }
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = slots[i] != null ? slots[i].ActorInstance : null;
                if (actors[i] == actor) continue;
                actors[i] = actor;
                feet[i] = actor != null ? actor.transform.Find("FootAnchor") : null;
                if (actor == null) continue;
                if (alignFeetToGround)
                {
                    Transform foot = feet[i];
                    if (foot != null)
                    {
                        originalActorPositions[actor.transform] = actor.transform.localPosition;
                        actor.transform.position += Vector3.up * (actorGroundHeight - foot.position.y);
                    }
                }
                originalSorting.RemoveAll(entry => entry.renderer == null);
                foreach (var renderer in actor.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!originalSorting.Exists(entry => entry.renderer == renderer))
                        originalSorting.Add((renderer, renderer.sortingLayerID, renderer.sortingOrder,
                            renderer.sharedMaterial, renderer.shadowCastingMode, renderer.receiveShadows));
                    renderer.sortingLayerID = 0;
                    renderer.sortingOrder = 0;
                    if (actorMaterial != null) renderer.sharedMaterial = actorMaterial;
                    RegisterFill(renderer);
                }
            }
            // Run after initial grounding so even the first shadow frame uses the final feet.
            for (int i = 0; i < actors.Length; i++) UpdateShadow(i, actors[i]);
            // Also apply Inspector changes to actors that have already spawned.
            foreach (var entry in originalSorting)
                if (entry.renderer != null)
                {
                    entry.renderer.shadowCastingMode = castSpriteShadows
                        ? UnityEngine.Rendering.ShadowCastingMode.TwoSided
                        : UnityEngine.Rendering.ShadowCastingMode.Off;
                    entry.renderer.receiveShadows = true;
                }
            foreach (var overlay in overlays)
            {
                if (overlay.target == null) continue;
                if (!alignSlotInformation || informationCamera == null)
                {
                    overlay.target.localPosition = overlay.position;
                    overlay.target.localRotation = overlay.rotation;
                    continue;
                }
                Transform view = informationCamera.transform;
                Vector3 offset = Vector3.Scale(overlay.position, overlay.target.parent.lossyScale);
                Vector3 origin = overlay.slot.transform.position;
                if (overlay.health && overlay.slot.HeadAnchor != null)
                {
                    origin = overlay.slot.HeadAnchor.position;
                    offset.y = healthBarHeadGap;
                }
                overlay.target.SetPositionAndRotation(origin + view.TransformDirection(offset), view.rotation * overlay.rotation);
            }
        }

        private void UpdateShadow(int index, GameObject actor)
        {
            var settings = actor != null ? actor.GetComponent<Art2CharacterShadowSettings>() : null;
            bool custom = settings != null && settings.enabled;
            Vector2 size = custom ? settings.size : shadowSize;
            Vector2 offset = custom ? settings.groundOffset : shadowGroundOffset;
            bool followsLight = custom ? settings.followsLight : contactShadowFollowsLight;
            float projectionHeight = custom ? settings.projectionHeight : contactShadowProjectionHeight;
            float maxOffset = custom ? settings.maxLightOffset : contactShadowMaxOffset;
            bool visible = showContactShadows && (!custom || settings.showShadow) && contactShadowMaterial != null && actor != null && actor.activeInHierarchy;
            if (visible && shadows[index] == null)
            {
                if (shadowMesh == null)
                {
                    shadowMesh = new Mesh { name = "Art2 Shared Contact Shadow Quad" };
                    shadowMesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
                    shadowMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                    shadowMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                    shadowMesh.RecalculateBounds();
                }
                var shadow = new GameObject("ContactShadow_" + slots[index].name);
                shadow.transform.SetParent(transform, false);
                shadow.AddComponent<MeshFilter>().sharedMesh = shadowMesh;
                var renderer = shadow.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = contactShadowMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                shadows[index] = shadow;
            }
            if (shadows[index] == null) return;
            shadows[index].SetActive(visible);
            if (!visible) return;
            Vector3 position = feet[index] != null ? feet[index].position : actor.transform.position;
            Vector3 lightOffset = Vector3.zero;
            if (followsLight && contactShadowLight != null && contactShadowLight.isActiveAndEnabled && contactShadowLight.intensity > 0)
            {
                float height = Mathf.Max(0, projectionHeight) + Mathf.Max(0, position.y - actorGroundHeight);
                Vector3 ray = contactShadowLight.type == LightType.Directional
                    ? contactShadowLight.transform.forward
                    : position + Vector3.up * Mathf.Max(0, projectionHeight) - contactShadowLight.transform.position;
                lightOffset = ProjectContactOffset(ray, height, maxOffset);
            }
            position += lightOffset;
            shadows[index].transform.SetPositionAndRotation(
                new Vector3(position.x + offset.x, groundHeight, position.z + offset.y), Quaternion.Euler(90, 0, 0));
            shadows[index].transform.localScale = new Vector3(Mathf.Max(.01f, size.x), Mathf.Max(.01f, size.y), 1);
        }

        private static Vector3 ProjectContactOffset(Vector3 lightRay, float height, float maxOffset)
        {
            lightRay.Normalize();
            if (lightRay.y >= -.001f) return Vector3.zero;
            var offset = new Vector3(lightRay.x, 0, lightRay.z) * (Mathf.Max(0, height) / -lightRay.y);
            return Vector3.ClampMagnitude(offset, Mathf.Max(0, maxOffset));
        }

        private void OnDisable()
        {
            foreach (var entry in originalActorPositions)
                if (entry.Key != null) entry.Key.localPosition = entry.Value;
            originalActorPositions.Clear();
            foreach (var overlay in overlays)
                if (overlay.target != null)
                {
                    overlay.target.localPosition = overlay.position;
                    overlay.target.localRotation = overlay.rotation;
                }
            overlays.Clear();
            foreach (var entry in originalSorting)
                if (entry.renderer != null)
                {
                    entry.renderer.sortingLayerID = entry.layer;
                    entry.renderer.sortingOrder = entry.order;
                    entry.renderer.sharedMaterial = entry.material;
                    entry.renderer.shadowCastingMode = entry.casting;
                    entry.renderer.receiveShadows = entry.receiving;
                }
            originalSorting.Clear();
            if (shadows != null)
                foreach (var shadow in shadows) if (shadow != null) Destroy(shadow);
            if (shadowMesh != null) Destroy(shadowMesh);
            shadowMesh = null;
        }
    }
}
