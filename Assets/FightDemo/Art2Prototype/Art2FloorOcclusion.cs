using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    // Scene-only opt-in. No transforms, game layers, combat state or shared materials are changed.
    [ExecuteAlways]
    public sealed class Art2FloorOcclusion : MonoBehaviour
    {
        public bool enableCorrection = true;
        public Camera targetCamera;
        public bool previewInSceneView = true;
        public Renderer[] floors = System.Array.Empty<Renderer>();
        [Tooltip("Solid scenery only. Sprite transparency is respected. Exclude halos, fog and particles.")]
        public Renderer[] sceneryOccluders = System.Array.Empty<Renderer>();
        public FightUnitSlot[] battleSlots = System.Array.Empty<FightUnitSlot>();
        [Tooltip("Optional scene preview warrior roots; generated warriors are discovered through Battle Slots.")]
        public FightCharacterDefinition[] previewWarriors = System.Array.Empty<FightCharacterDefinition>();
        internal static readonly List<Art2FloorOcclusion> Active = new();
        private readonly List<SpriteRenderer> scratch = new();
        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() => Active.Remove(this);
        internal bool Matches(Camera camera) => enableCorrection && isActiveAndEnabled &&
            (camera == targetCamera || previewInSceneView && camera.cameraType == CameraType.SceneView);
        internal void Collect(List<Renderer> occluders, List<Renderer> warriors)
        {
            occluders.Clear(); warriors.Clear();
            foreach (var renderer in sceneryOccluders) Add(occluders, renderer);
            foreach (var slot in battleSlots)
            {
                if (slot == null || slot.ActorInstance == null) continue;
                CollectActor(slot.ActorInstance, slot.CharacterDefinition != null && slot.CharacterDefinition.EnableSwordCharge, occluders, warriors);
            }
            foreach (var warrior in previewWarriors)
                if (warrior != null) CollectActor(warrior.gameObject, true, occluders, warriors);
        }
        private void CollectActor(GameObject actor, bool selected, List<Renderer> occluders, List<Renderer> warriors)
        {
            scratch.Clear(); actor.GetComponentsInChildren(false, scratch);
            foreach (var renderer in scratch)
            {
                if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader.name != "RhythmHunter/Art2/SpriteCardLit") continue;
                Add(occluders, renderer);
                if (selected) Add(warriors, renderer);
            }
        }
        internal static void Add(List<Renderer> list, Renderer renderer)
        {
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && !list.Contains(renderer)) list.Add(renderer);
        }
    }
}
