using System;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    [Serializable]
    public sealed class FightBossPattern
    {
        public bool enabled;
        [Min(1)] public int neutralBeats = 4;
        [Min(1)] public int armorBeats = 4;
        [Min(2)] public int chargeBeats = 4;
        [Min(1)] public int vulnerableBeats = 4;
        [Min(1)] public int rageWindupBeats = 2;
        [Min(2)] public int rageAttackBeats = 6;
        [Min(1)] public int rageAttackInterval = 2;
        [Min(1)] public int breakBeats = 4;
        [Min(1)] public int breakThreshold = 3;
        [Min(0)] public float armorReduction = .5f;
        [Min(0)] public float vulnerableBonus = .5f;
        [Min(.5f)] public float chargeDamage = 2;
        [Min(.5f)] public float rageFinisherDamage = 3;
        [Min(0)] public int successfulGuardBreakPower = 1;
    }

    public sealed partial class FightCharacterDefinition
    {
        [Header("Boss Interaction")]
        [Tooltip("Progress against the front boss's Armor, Charge or Rage on an accepted ability.")]
        [SerializeField, Min(0)] private int basicBreakPower;
        [SerializeField, Min(0)] private int skillBreakPower;
        [Header("Test Boss Pattern (Equal Beat Only)")]
        [SerializeField] private FightBossPattern bossPattern = new();
        public int BasicBreakPower => Mathf.Max(0, basicBreakPower);
        public int SkillBreakPower => Mathf.Max(0, skillBreakPower);
        public FightBossPattern BossPattern => bossPattern;
    }
}
