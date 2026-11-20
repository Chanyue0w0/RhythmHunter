using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    // Render-stage ghosts survive between casts; actor transforms and original sorting stay untouched.
    public sealed class FightFeverAfterimagePool : MonoBehaviour
    {
        sealed class Echo
        {
            public FightUnitSlot enemy;
            public SpriteRenderer source, image;
            public int rank, hits;
        }
        readonly Stack<SpriteRenderer> available = new();
        readonly List<Echo> active = new();
        public int AllocatedCount { get; private set; }
        public int ActiveCount => active.Count;
        public void Strike(FightUnitSlot enemy)
        {
            if (!enemy || !enemy.ActorInstance) return;
            bool found = false;
            foreach (var echo in active)
                if (echo.enemy == enemy) { echo.hits++; found = true; }
            if (found) return;
            foreach (var source in enemy.ActorInstance.GetComponentsInChildren<SpriteRenderer>(true))
                for (int rank = 1; rank <= 3; rank++)
                {
                    SpriteRenderer image;
                    if (available.Count > 0) image = available.Pop();
                    else
                    {
                        image = new GameObject("Fever Enemy Echo").AddComponent<SpriteRenderer>();
                        image.transform.SetParent(transform, false); image.gameObject.layer = 31; AllocatedCount++;
                    }
                    image.gameObject.SetActive(true);
                    image.sharedMaterial = source.sharedMaterial;
                    active.Add(new Echo { enemy = enemy, source = source, image = image, rank = rank, hits = 1 });
                }
        }
        public void Present(Camera camera, Vector3 renderOffset, float returnProgress)
        {
            float away = 1 - Mathf.SmoothStep(0, 1, returnProgress);
            foreach (var echo in active)
            {
                if (!echo.source || !echo.image) continue;
                var source = echo.source;
                var image = echo.image;
                image.enabled = source.enabled && source.gameObject.activeInHierarchy;
                image.sprite = source.sprite; image.flipX = source.flipX; image.flipY = source.flipY;
                image.sortingLayerID = source.sortingLayerID; image.sortingOrder = source.sortingOrder - echo.rank;
                // The transparent render texture is composited over the black veil again.
                // Keep echoes bright enough after that second blend, especially the outer two.
                var color = source.color; color.a *= (1f - echo.rank * .15f) * away; image.color = color;
                float distance = (.24f + echo.hits * .045f) * echo.rank * away;
                image.transform.SetPositionAndRotation(source.transform.position + renderOffset - camera.transform.right * distance, source.transform.rotation);
                image.transform.localScale = source.transform.lossyScale;
            }
        }
        public void ReleaseAll()
        {
            foreach (var echo in active)
                if (echo.image) { echo.image.gameObject.SetActive(false); echo.image.sprite = null; available.Push(echo.image); }
            active.Clear();
        }
        private void OnDisable() => ReleaseAll();
    }
}
