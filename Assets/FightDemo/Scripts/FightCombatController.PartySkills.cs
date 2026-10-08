using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        private void RestorePartyArmor()
        {
            partyArmor = maxPartyArmor;
            nextArmorRecoveryBeat = long.MaxValue;
            PartyHealthChanged?.Invoke(partyHp, maxPartyHp);
        }

        private string PerformPartySkill(FightUnitSlot actor, FightCharacterDefinition.AbilityBehavior behavior, float power)
        {
            int version = rosterVersion;
            var target = FindFrontLivingEnemy();
            float alternative = actor.CharacterDefinition != null ? actor.CharacterDefinition.ConditionalSkillPower : power;
            switch (behavior)
            {
                case FightCharacterDefinition.AbilityBehavior.BreakingSlash:
                    if (target == null) return "Breaking Slash — no target";
                    PlayAnimatedHeroAction(actor, target, ActionType.Skill, power);
                    if (HealthSystemEnabled && IsCurrentHeroAction(version, actor, target) && target.CurrentHp > 0)
                        target.ApplyFragile();
                    return $"Breaking Slash — {power:0.#} base damage, then Fragile if target survives";

                case FightCharacterDefinition.AbilityBehavior.EnergySurge:
                    // Evaluate now, after earlier party members have resolved their effects.
                    foreach (var enemy in fightScene2Enemies)
                        if (enemy != null && enemy.IsFragile) { target = enemy; break; }
                    if (target != null && target.IsFragile)
                    {
                        PlayAnimatedHeroAction(actor, target, ActionType.Skill, alternative);
                        return $"Energy Surge — focused {alternative:0.#} base damage";
                    }
                    PlayAnimatedHeroAreaAction(actor, FightCharacterCombatAnimator.CombatAnimation.Skill, ActionType.Skill, power);
                    return $"Energy Surge — {power:0.#} base damage to each enemy";

                case FightCharacterDefinition.AbilityBehavior.FormationBreaker:
                    if (target == null) return "Formation Breaker — no target";
                    // Only the living front enemy selects the mode. The area helper
                    // resolves a snapshot; a death cannot change this cast's mode.
                    if (target.IsFragile)
                    {
                        PlayAnimatedHeroAreaAction(actor, FightCharacterCombatAnimator.CombatAnimation.Skill, ActionType.Skill, alternative);
                        return $"Formation Breaker — piercing {alternative:0.#} base damage to each enemy";
                    }
                    PlayAnimatedHeroAction(actor, target, ActionType.Skill, power);
                    return $"Formation Breaker — {power:0.#} base damage to front";
            }
            return "No skill effect";
        }
    }
}
