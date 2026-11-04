using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCharacterDefinition
    {
        public enum TeamQteMode { None, Slash, Slime, Bomb }
        [Header("Team Skill QTE")]
        [SerializeField] private TeamQteMode teamQteMode;
        [Tooltip("Slash QTE only. Rapid QTE misses skip that projectile entirely.")]
        [SerializeField, Range(0, 1)] private float qteFailureDamageMultiplier = .5f;
        [Tooltip("Slash QTE only: apply Fragile even after a failed input.")]
        [SerializeField] private bool qteFailureAppliesStatus = true;
        [SerializeField, Min(.1f)] private float rapidQteIntervalBeats = 1f;
        [SerializeField] private GameObject qteProjectilePrefab;
        [SerializeField, Min(.05f)] private float qteProjectileFlightBeats = .35f;
        [SerializeField, Min(0)] private float qteProjectileArcHeight = 1.2f;
        [SerializeField] private GameObject qteImpactEffectPrefab;
        [SerializeField, Min(0)] private float qteImpactEffectLifetime = 1.5f;
        [SerializeField] private GameObject slimeFloorPrefab;
        [SerializeField] private Vector3 slimeFloorOffset = new(0, 0, -.1f);
        public TeamQteMode QteMode => teamQteMode;
        public float QteFailureDamageMultiplier => Mathf.Clamp01(qteFailureDamageMultiplier);
        public bool QteFailureAppliesStatus => qteFailureAppliesStatus;
        public float RapidQteIntervalBeats => Mathf.Max(.1f, rapidQteIntervalBeats);
        public GameObject QteProjectilePrefab => qteProjectilePrefab;
        public float QteProjectileFlightBeats => Mathf.Max(.05f, qteProjectileFlightBeats);
        public float QteProjectileArcHeight => Mathf.Max(0, qteProjectileArcHeight);
        public GameObject QteImpactEffectPrefab => qteImpactEffectPrefab;
        public float QteImpactEffectLifetime => Mathf.Max(0, qteImpactEffectLifetime);
        public GameObject SlimeFloorPrefab => slimeFloorPrefab;
        public Vector3 SlimeFloorOffset => slimeFloorOffset;
    }
}
