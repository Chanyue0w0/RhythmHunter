using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    public enum AquariumSurfaceType
    {
        Land,
        ShallowWater,
        Water
    }

    [RequireComponent(typeof(Collider2D))]
    public sealed class AquariumSurfaceZone : MonoBehaviour
    {
        [SerializeField] private AquariumSurfaceType surfaceType = AquariumSurfaceType.Water;
        [SerializeField] private int priority = 50;
        [SerializeField, Min(0.1f)] private float speedMultiplier = 1f;

        public AquariumSurfaceType SurfaceType => surfaceType;
        public int Priority => priority;
        public float SpeedMultiplier => speedMultiplier;

        public void Configure(AquariumSurfaceType type, int zonePriority, float zoneSpeedMultiplier)
        {
            surfaceType = type;
            priority = zonePriority;
            speedMultiplier = Mathf.Max(0.1f, zoneSpeedMultiplier);

            Collider2D zoneCollider = GetComponent<Collider2D>();
            zoneCollider.isTrigger = true;
        }
    }
}
