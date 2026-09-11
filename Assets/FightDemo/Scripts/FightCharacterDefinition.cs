using UnityEngine;
using UnityEngine.Serialization;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Authoring data stored directly on every hero/enemy prefab.
    /// The roster manager reads this component when it spawns the battlefield lineup.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FightCharacterDefinition : MonoBehaviour
    {
        public enum AbilityBehavior
        {
            DamageFront,
            Guard,
            HealParty,
            DamageAll,
            GuardAndDamageFront
        }

        [Header("Identity")]
        [SerializeField] private string characterId = "character";
        [SerializeField] private string displayName = "CHARACTER";
        [SerializeField] private FightUnitSlot.UnitTeam team;
        [SerializeField] private FightUnitSlot.UnitRole role;

        [Header("Base Combat Information")]
        [SerializeField, Min(0.5f)] private float maxHp = 4f;
        [Tooltip("Gameplay behavior activated on beats 1-3.")]
        [SerializeField] private AbilityBehavior normalAbilityBehavior;
        [SerializeField, Min(0)] private float normalAbilityPower = 1f;
        [SerializeField, Min(0.5f)] private float attackPower = 1f;
        [SerializeField] private string skillName = "Beat Skill";
        [Tooltip("Gameplay behavior activated when this character is called on beat 4.")]
        [SerializeField] private AbilityBehavior skillBehavior;
        [FormerlySerializedAs("skillDamage")]
        [SerializeField, Min(0)] private float skillPower = 1f;
        [Tooltip("Optional replaceable VFX prefab spawned when this character uses its skill.")]
        [SerializeField] private GameObject skillEffectPrefab;
        [Tooltip("AI attack interval. Player-controlled party members ignore this value.")]
        [SerializeField, Min(1)] private int attackIntervalBeats = 4;
        [SerializeField, Min(1)] private int maxMana = 4;

        [Header("Visuals")]
        [SerializeField] private Color accentColor = Color.white;

        public string CharacterId => characterId;
        public string DisplayName => displayName;
        public FightUnitSlot.UnitTeam Team => team;
        public FightUnitSlot.UnitRole Role => role;
        public float MaxHp => maxHp;
        public AbilityBehavior NormalAbilityType => normalAbilityBehavior;
        public float NormalAbilityPower => normalAbilityPower;
        public float AttackPower => attackPower;
        public string SkillName => skillName;
        public AbilityBehavior SkillType => skillBehavior;
        public float SkillPower => skillPower;
        public GameObject SkillEffectPrefab => skillEffectPrefab;
        public int AttackIntervalBeats => attackIntervalBeats;
        public int MaxMana => maxMana;
        public Color AccentColor => accentColor;

        public void Configure(
            string id,
            string shownName,
            FightUnitSlot.UnitTeam unitTeam,
            FightUnitSlot.UnitRole unitRole,
            float hp,
            AbilityBehavior normalBehavior,
            float normalPower,
            float attack,
            string abilityName,
            AbilityBehavior abilityBehavior,
            float abilityDamage,
            GameObject abilityEffect,
            int intervalBeats,
            int manaCapacity,
            Color color)
        {
            characterId = id;
            displayName = shownName;
            team = unitTeam;
            role = unitRole;
            maxHp = QuantizePositive(hp);
            normalAbilityBehavior = normalBehavior;
            normalAbilityPower = normalBehavior == AbilityBehavior.Guard
                ? 0f
                : QuantizePositive(normalPower);
            attackPower = QuantizePositive(attack);
            skillName = abilityName;
            skillBehavior = abilityBehavior;
            skillPower = abilityBehavior == AbilityBehavior.Guard
                ? 0f
                : QuantizePositive(abilityDamage);
            skillEffectPrefab = abilityEffect;
            attackIntervalBeats = Mathf.Max(1, intervalBeats);
            maxMana = Mathf.Max(1, manaCapacity);
            accentColor = color;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxHp = QuantizePositive(maxHp);
            normalAbilityPower = normalAbilityBehavior == AbilityBehavior.Guard
                ? 0f
                : QuantizePositive(normalAbilityPower);
            attackPower = QuantizePositive(attackPower);
            skillPower = skillBehavior == AbilityBehavior.Guard
                ? 0f
                : QuantizePositive(skillPower);
            attackIntervalBeats = Mathf.Max(1, attackIntervalBeats);
            maxMana = Mathf.Max(1, maxMana);
        }
#endif

        private static float QuantizePositive(float value)
        {
            return Mathf.Max(0.5f, Mathf.Round(value * 2f) * 0.5f);
        }
    }
}
