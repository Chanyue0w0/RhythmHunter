using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCharacterDefinition
    {
        [Serializable]
        public sealed class EnemyAbility
        {
            public string name;
            public FightCharacterCombatAnimator.CombatAnimation animation;
            [Min(0)] public float damagePerHit = 1;
        }
        [Header("Enemy Phase 1 Ability Pool")]
        [Tooltip("Optional frame-authored attacks. Choose one uniformly per attack; repeats allowed. Empty preserves the existing Normal attack.")]
        [SerializeField] private List<EnemyAbility> enemyAbilities = new();
        public IReadOnlyList<EnemyAbility> EnemyAbilities => enemyAbilities;
    }
}
