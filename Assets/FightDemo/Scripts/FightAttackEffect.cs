using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Optional behaviour for a normal-attack VFX prefab. Prefabs without this component
    /// are still spawned and cleaned up by FightUnitSlot.
    /// </summary>
    public sealed class FightAttackEffect : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float travelDistance = 2.2f;
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0.55f, 1f, 0f);

        private Vector3 origin;
        private Vector3 direction;
        private float lifetime;
        private float elapsed;

        public void Play(Vector3 travelDirection, float duration)
        {
            origin = transform.position;
            direction = travelDirection.sqrMagnitude > 0f ? travelDirection.normalized : Vector3.left;
            lifetime = Mathf.Max(0.05f, duration);
            elapsed = 0f;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / lifetime);
            transform.position = origin + direction * (travelDistance * progress);
            transform.localScale = Vector3.one * Mathf.Max(0f, scaleCurve.Evaluate(progress));

            if (progress >= 1f)
                Destroy(gameObject);
        }
    }
}
