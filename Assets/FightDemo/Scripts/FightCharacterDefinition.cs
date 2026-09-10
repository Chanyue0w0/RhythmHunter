using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Authoring data stored directly on every hero/enemy prefab.
    /// The roster manager reads this component when it spawns the battlefield lineup.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FightCharacterDefinition : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string characterId = "character";
        [SerializeField] private string displayName = "CHARACTER";
        [SerializeField] private FightUnitSlot.UnitTeam team;
        [SerializeField] private FightUnitSlot.UnitRole role;

        [Header("Base Combat Information")]
        [SerializeField, Min(1)] private int maxHp = 100;
        [SerializeField, Min(0)] private int attackPower = 10;
        [SerializeField] private string skillName = "Beat Skill";
        [SerializeField, Min(0)] private int skillDamage = 30;
        [Tooltip("Optional replaceable VFX prefab spawned when this character uses its skill.")]
        [SerializeField] private GameObject skillEffectPrefab;
        [Tooltip("Number of musical beats between automatic attacks.")]
        [SerializeField, Min(1)] private int attackIntervalBeats = 4;
        [SerializeField, Min(1)] private int maxMana = 4;

        [Header("Visuals")]
        [SerializeField] private Color accentColor = Color.white;
        [SerializeField] private SpriteRenderer characterRenderer;
        [SerializeField] private BeatSyncedIdleAnimator idleAnimator;
        [SerializeField] private List<Sprite> idleFrames = new();
        [SerializeField, Min(0.01f)] private float idleCyclesPerBeat = 1f;
        [SerializeField] private bool idlePingPong;

        public string CharacterId => characterId;
        public string DisplayName => displayName;
        public FightUnitSlot.UnitTeam Team => team;
        public FightUnitSlot.UnitRole Role => role;
        public int MaxHp => maxHp;
        public int AttackPower => attackPower;
        public string SkillName => skillName;
        public int SkillDamage => skillDamage;
        public GameObject SkillEffectPrefab => skillEffectPrefab;
        public int AttackIntervalBeats => attackIntervalBeats;
        public int MaxMana => maxMana;
        public Color AccentColor => accentColor;
        public SpriteRenderer CharacterRenderer => characterRenderer;
        public BeatSyncedIdleAnimator IdleAnimator => idleAnimator;
        public IReadOnlyList<Sprite> IdleFrames => idleFrames;
        public float IdleCyclesPerBeat => idleCyclesPerBeat;
        public bool IdlePingPong => idlePingPong;

        public void Configure(
            string id,
            string shownName,
            FightUnitSlot.UnitTeam unitTeam,
            FightUnitSlot.UnitRole unitRole,
            int hp,
            int attack,
            string abilityName,
            int abilityDamage,
            GameObject abilityEffect,
            int intervalBeats,
            int manaCapacity,
            Color color,
            SpriteRenderer renderer,
            BeatSyncedIdleAnimator animator,
            IEnumerable<Sprite> frames,
            float cyclesPerBeat = 1f,
            bool pingPong = false)
        {
            characterId = id;
            displayName = shownName;
            team = unitTeam;
            role = unitRole;
            maxHp = Mathf.Max(1, hp);
            attackPower = Mathf.Max(0, attack);
            skillName = abilityName;
            skillDamage = Mathf.Max(0, abilityDamage);
            skillEffectPrefab = abilityEffect;
            attackIntervalBeats = Mathf.Max(1, intervalBeats);
            maxMana = Mathf.Max(1, manaCapacity);
            accentColor = color;
            characterRenderer = renderer;
            idleAnimator = animator;
            idleFrames = frames != null ? new List<Sprite>(frames) : new List<Sprite>();
            idleCyclesPerBeat = Mathf.Max(0.01f, cyclesPerBeat);
            idlePingPong = pingPong;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxHp = Mathf.Max(1, maxHp);
            attackPower = Mathf.Max(0, attackPower);
            skillDamage = Mathf.Max(0, skillDamage);
            attackIntervalBeats = Mathf.Max(1, attackIntervalBeats);
            maxMana = Mathf.Max(1, maxMana);
            idleCyclesPerBeat = Mathf.Max(0.01f, idleCyclesPerBeat);
        }
#endif
    }
}
