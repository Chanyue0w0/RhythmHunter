using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    // Scene-only rendering adaptation. Slot order, damage, timing and prefab data are untouched.
    public sealed class Art2ActorPresentation : MonoBehaviour
    {
        [SerializeField] private FightUnitSlot[] slots;
        private GameObject[] actors;
        private readonly List<(SpriteRenderer renderer, int layer, int order)> originalSorting = new();

        private void OnEnable() => actors = new GameObject[slots != null ? slots.Length : 0];

        private void LateUpdate()
        {
            if (slots == null) return;
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = slots[i] != null ? slots[i].ActorInstance : null;
                if (actors[i] == actor) continue;
                actors[i] = actor;
                if (actor == null) continue;
                originalSorting.RemoveAll(entry => entry.renderer == null);
                foreach (var renderer in actor.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    originalSorting.Add((renderer, renderer.sortingLayerID, renderer.sortingOrder));
                    renderer.sortingLayerID = 0;
                    renderer.sortingOrder = 0;
                }
            }
        }

        private void OnDisable()
        {
            foreach (var entry in originalSorting)
                if (entry.renderer != null)
                {
                    entry.renderer.sortingLayerID = entry.layer;
                    entry.renderer.sortingOrder = entry.order;
                }
            originalSorting.Clear();
        }
    }
}
