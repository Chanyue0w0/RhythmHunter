using UnityEngine;
namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCharacterDefinition
    {
        [Header("Mage — successful guard retaliation")]
        [Tooltip("Successful normal shield blocks launch one slime on the next beat. Serialized name retained for existing prefabs.")]
        [SerializeField] private bool enableUnusedGuardSlime;
        [SerializeField, Min(0)] private float slimeAttackReduction = 1;
        [SerializeField, Min(1)] private int slimeAttackReductionBeats = 8;
        [SerializeField, Min(0)] private float slimeMinimumAttackDamage;
        [Header("Mage slime visuals (independent of Fever)")]
        [SerializeField, Min(.01f)] private float retaliationProjectileScale = .75f;
        [SerializeField, Range(0, 1)] private float retaliationFloorOpacity = .5f;
        [SerializeField] private Vector3 retaliationFloorOffset = new(0, .025f, -.1f);
        public float RetaliationProjectileScale => Mathf.Max(.01f, retaliationProjectileScale);
        public float RetaliationFloorOpacity => Mathf.Clamp01(retaliationFloorOpacity);
        public Vector3 RetaliationFloorOffset => retaliationFloorOffset;
        public bool EnableUnusedGuardSlime => enableUnusedGuardSlime;
        public float SlimeAttackReduction => Mathf.Max(0, slimeAttackReduction);
        public int SlimeAttackReductionBeats => Mathf.Max(1, slimeAttackReductionBeats);
        public float SlimeMinimumAttackDamage => Mathf.Max(0, slimeMinimumAttackDamage);
    }
}
