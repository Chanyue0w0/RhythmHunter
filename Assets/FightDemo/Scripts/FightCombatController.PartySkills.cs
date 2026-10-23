using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        [Header("Character Passives")]
        [Tooltip("Prototype passives are disabled for the current design: frontline armor bonus, after-skill armor recovery and interrupt bonus damage.")]
        [SerializeField] private bool enableCharacterPassives;
        private void AdvanceSlimeDetonations(float deltaTime)
        {
            if (beatClock == null || !beatClock.HasTimingAnchor || IsPaused || TimingCalibrationActive || battleEnded || AwaitingNextWave) return;
            float elapsedBeats = deltaTime * beatClock.MusicPitch / (float)System.Math.Max(.001, beatClock.MillisecondsPerBeat / 1000d);
            int version = rosterVersion;
            foreach (var enemy in fightScene2Enemies)
            {
                if (enemy != null) enemy.AdvanceFragmentDetonations(elapsedBeats);
                if (version != rosterVersion || battleEnded) return;
            }
        }
        private void RestorePartyArmor()
        {
            RecoverPartyArmor(FightCharacterDefinition.ArmorRestoreMode.Full);
        }

        public float RecoverPartyArmor(FightCharacterDefinition.ArmorRestoreMode mode, float amount = 0)
        {
            if (!HealthSystemEnabled || battleEnded || AwaitingBattleStart || mode == FightCharacterDefinition.ArmorRestoreMode.None) return 0;
            if (mode != FightCharacterDefinition.ArmorRestoreMode.Full &&
                (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0)) return 0;
            float previous = partyArmor;
            partyArmor = mode == FightCharacterDefinition.ArmorRestoreMode.Full ? maxPartyArmor
                : Mathf.Min(maxPartyArmor, partyArmor + Mathf.Max(0, Mathf.Round(amount * 2f) * .5f));
            PartyHealthChanged?.Invoke(partyHp, maxPartyHp);
            return partyArmor - previous;
        }

        private void ApplyAbilityArmorRecovery(FightCharacterDefinition definition, bool skill)
        {
            if (definition == null || (!enableCharacterPassives &&
                (skill ? definition.SkillType : definition.BasicAbilityType) != FightCharacterDefinition.AbilityBehavior.RestoreArmor)) return;
            RecoverPartyArmor(skill ? definition.TeamSkillArmorRecovery : definition.BasicArmorRecovery,
                skill ? definition.TeamSkillArmorRecoveryAmount : definition.BasicArmorRecoveryAmount);
        }

        private string PerformPartySkill(FightUnitSlot actor, FightCharacterDefinition.AbilityBehavior behavior, float power)
        {
            int version = rosterVersion;
            var target = FindFrontLivingEnemy();
            float alternative = actor.CharacterDefinition != null ? actor.CharacterDefinition.ConditionalSkillPower : power;
            switch (behavior)
            {
                case FightCharacterDefinition.AbilityBehavior.SlimeFragments:
                case FightCharacterDefinition.AbilityBehavior.SmallBombs:
                    var targets = GetTeamSkillEffectTargets(actor);
                    PlayAnimatedHeroUtility(actor, FightCharacterCombatAnimator.CombatAnimation.Skill,
                        () =>
                        {
                            foreach (var enemy in targets)
                                if (IsCurrentHeroAction(version, actor, enemy)) actor.PlaySkillAttackAt(enemy.ImpactEffectAnchor);
                        },
                        () =>
                        {
                            foreach (var enemy in targets)
                            {
                                if (!IsCurrentHeroAction(version, actor, enemy) || enemy.CurrentHp <= 0 || !HealthSystemEnabled) continue;
                                var data = actor.CharacterDefinition;
                                if (behavior == FightCharacterDefinition.AbilityBehavior.SlimeFragments)
                                    enemy.AttachSlime(data.FragmentDamage, data.FragmentBeats, actor);
                                else
                                {
                                    ApplyHeroDamage(version, actor, enemy, power);
                                    if (IsCurrentHeroAction(version, actor, enemy)) enemy.ApplyStun(data.StunBeats);
                                }
                            }
                        });
                    return behavior == FightCharacterDefinition.AbilityBehavior.SlimeFragments
                        ? "Slime Fragments — attached; direct damage detonates" : "Small Bombs — damage and stun";
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
