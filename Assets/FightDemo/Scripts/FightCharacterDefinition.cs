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
        public enum SkillBehavior
        {
            Damage,
            Guard,
            HealParty
        }

        [Header("Identity")]
        [SerializeField] private string characterId = "character";
        [SerializeField] private string displayName = "CHARACTER";
        [SerializeField] private FightUnitSlot.UnitTeam team;
        [SerializeField] private FightUnitSlot.UnitRole role;

        [Header("Base Combat Information")]
        [SerializeField, Min(1)] private int maxHp = 100;
        [SerializeField, Min(0)] private int attackPower = 10;
        [SerializeField] private string skillName = "Beat Skill";
        [Tooltip("Gameplay behavior activated when this character is called on beat 4.")]
        [SerializeField] private SkillBehavior skillBehavior;
        [FormerlySerializedAs("skillDamage")]
        [SerializeField, Min(0)] private int skillPower = 30;
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
        public int MaxHp => maxHp;
        public int AttackPower => attackPower;
        public string SkillName => skillName;
        public SkillBehavior SkillType => skillBehavior;
        public int SkillPower => skillPower;
        public int SkillDamage => skillPower;
        public GameObject SkillEffectPrefab => skillEffectPrefab;
        public int AttackIntervalBeats => attackIntervalBeats;
        public int MaxMana => maxMana;
        public Color AccentColor => accentColor;

        public void Configure(
            string id,
            string shownName,
            FightUnitSlot.UnitTeam unitTeam,
            FightUnitSlot.UnitRole unitRole,
            int hp,
            int attack,
            string abilityName,
            SkillBehavior abilityBehavior,
            int abilityDamage,
            GameObject abilityEffect,
            int intervalBeats,
            int manaCapacity,
            Color color)
        {
            characterId = id;
            displayName = shownName;
            team = unitTeam;
            role = unitRole;
            maxHp = Mathf.Max(1, hp);
            attackPower = Mathf.Max(0, attack);
            skillName = abilityName;
            skillBehavior = abilityBehavior;
            skillPower = Mathf.Max(0, abilityDamage);
            skillEffectPrefab = abilityEffect;
            attackIntervalBeats = Mathf.Max(1, intervalBeats);
            maxMana = Mathf.Max(1, manaCapacity);
            accentColor = color;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxHp = Mathf.Max(1, maxHp);
            attackPower = Mathf.Max(0, attackPower);
            skillPower = Mathf.Max(0, skillPower);
            attackIntervalBeats = Mathf.Max(1, attackIntervalBeats);
            maxMana = Mathf.Max(1, maxMana);
        }
#endif
    }
}
