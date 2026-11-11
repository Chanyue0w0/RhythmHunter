using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCharacterDefinition
    {
        public enum AimedApplicationMode { GoldOnShotHit, GreenOnPreparation }
        public enum AimedActivationSource { OwnerShotOnly, AnyHeroDirectHit }
        [Header("Aimed / Automatic Shooter Skill")]
        [SerializeField] private bool enableAimedFollowUp;
        [SerializeField] private AimedApplicationMode aimedApplication;
        [SerializeField] private AimedActivationSource aimedActivation;
        [SerializeField, Min(1)] private int aimedGreenBeats = 4;
        [SerializeField, Min(1)] private int aimedGoldBeats = 8;
        [SerializeField, Min(.5f)] private float aimedFollowUpDamage = 1;
        [SerializeField] private GameObject aimedPreparationEffect;
        [SerializeField, Min(0)] private float aimedPreparationEffectLifetime = .5f;
        [SerializeField] private GameObject aimedGoldProjectile;
        [SerializeField] private GameObject aimedTargetEffect;
        public bool EnableAimedFollowUp => enableAimedFollowUp;
        public AimedApplicationMode AimedApplication => aimedApplication;
        public AimedActivationSource AimedActivation => aimedActivation;
        public int AimedGreenBeats => Mathf.Max(1, aimedGreenBeats);
        public int AimedGoldBeats => Mathf.Max(1, aimedGoldBeats);
        public float AimedFollowUpDamage => Mathf.Max(.5f, aimedFollowUpDamage);
        public GameObject AimedPreparationEffect => aimedPreparationEffect;
        public float AimedPreparationEffectLifetime => aimedPreparationEffectLifetime;
        public GameObject AimedGoldProjectile => aimedGoldProjectile;
        public GameObject AimedTargetEffect => aimedTargetEffect;
    }
}
