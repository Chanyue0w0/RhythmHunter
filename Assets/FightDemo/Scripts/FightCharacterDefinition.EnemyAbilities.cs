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
            [SerializeField, HideInInspector] private int animationSourceId = -1;
            public FightCharacterCombatAnimator.CombatAnimation Animation => animationSourceId >= 0
                ? (FightCharacterCombatAnimator.CombatAnimation)animationSourceId : animation;
            [Min(0)] public float damagePerHit = 1;
            [Min(0), Tooltip("Relative selection weight. Zero disables selection. 45 / 45 / 10 gives 45% / 45% / 10%.")]
            public float selectionWeight = 1;
            [Min(0), Tooltip("Heal self once on the first entered attack-effect frame. Zero disables healing. Interrupted actions do not heal.")]
            public float healOnEffectFrame;
            [Tooltip("When assigned, heal when this additional frame-effect prefab spawns instead of the attack-effect marker. Once per cast.")]
            public GameObject healingEffectPrefab;
        }
        [Header("Enemy Phase 1 Ability Pool")]
        [Tooltip("Optional frame-authored abilities. Choose by relative weight per action; repeats allowed. Damage 0 permits non-damaging animations. Empty preserves the existing Normal attack.")]
        [SerializeField] private List<EnemyAbility> enemyAbilities = new();
        public IReadOnlyList<EnemyAbility> EnemyAbilities => enemyAbilities;
        public EnemyAbility SelectEnemyAbility(float normalizedRoll)
        {
            double total = 0;
            foreach (var ability in enemyAbilities)
                if (ValidWeight(ability)) total += ability.selectionWeight;
            if (total <= 0 || float.IsNaN(normalizedRoll)) return null;
            double roll = Mathf.Clamp01(normalizedRoll) * total;
            EnemyAbility last = null;
            foreach (var ability in enemyAbilities)
            {
                if (!ValidWeight(ability)) continue;
                last = ability;
                if (roll < ability.selectionWeight) return ability;
                roll -= ability.selectionWeight;
            }
            return last;
        }
        private static bool ValidWeight(EnemyAbility ability) => ability != null && ability.selectionWeight > 0 &&
            !float.IsNaN(ability.selectionWeight) && !float.IsInfinity(ability.selectionWeight);
    }
}
