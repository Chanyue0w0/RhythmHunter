using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>Shared VFX configuration; gameplay reports hits, particles never deal damage.</summary>
    [DisallowMultipleComponent]
    public sealed class FightEffectSettings : MonoBehaviour
    {
        [Tooltip("Wait for all child particles to finish, then use the battle effect cleanup path.")]
        public bool destroyAfterParticles;
        public enum SpawnOrientation { Anchor, Prefab, WorldUpright }
        [Header("Placement (configured abilities)")]
        public SpawnOrientation spawnOrientation;
        [Tooltip("Use the owner's FootAnchor instead of the supplied cast/impact anchor. For self auras only.")]
        public bool useOwnerFeet;
        [Tooltip("Offset in the chosen effect orientation, independent of the character's scale.")]
        public Vector3 spawnOffset;
        public bool followPositionOnly;
        [Header("Normal attack projectile")]
        public bool flyToTarget;
        [Min(.01f)] public float flightBeats = .6f;
        [Min(0)] public float forwardSpawnDistance = .25f;
        [Tooltip("Spawned once on a confirmed direct hit, at the damaged target's impact anchor.")]
        public GameObject onHitPrefab;
        [Min(0)] public float onHitLifetime = 1.5f;

        private void Start()
        {
            // Standalone previews use the same cleanup as effects spawned by combat.
            if (destroyAfterParticles && !flyToTarget && !TryGetComponent<FightBattleEffect>(out _))
                gameObject.AddComponent<FightBattleEffect>().Configure(null, false, 0);
        }
    }
}
