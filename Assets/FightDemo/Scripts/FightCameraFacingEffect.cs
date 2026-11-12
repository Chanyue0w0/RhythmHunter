using UnityEngine;

namespace RhythmHunter.FightDemo
{
    // Rotate after the Art2 camera; never inherit the actor's scale or change flight position.
    [DefaultExecutionOrder(1000), DisallowMultipleComponent]
    public sealed class FightCameraFacingEffect : MonoBehaviour
    {
        public bool faceCamera = true;
        public int sortingOrder = 31;
        private Camera view;
        private SpriteRenderer[] sprites;
        public void Bind(Camera camera)
        {
            view = camera;
            Apply();
        }
        private void OnEnable() => Apply();
        private void LateUpdate() => Apply();
        private void Apply()
        {
            if (view == null) view = Camera.main;
            if (faceCamera && view != null) transform.rotation = view.transform.rotation;
            sprites ??= GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sprite in sprites)
                if (sprite != null) { sprite.sortingLayerID = 0; sprite.sortingOrder = sortingOrder; }
        }
    }
}
