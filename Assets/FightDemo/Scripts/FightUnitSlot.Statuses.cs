using UnityEngine;
using System.Collections.Generic;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightUnitSlot
    {
        public enum DamageKind { Direct, DamageOverTime, Secondary }
        private long slimeUntil = long.MinValue, stunUntil = long.MinValue;
        private long lastBleedBeat = long.MinValue;
        private int bleedRemaining;
        private int slimeDuration, stunDuration;
        private float slimeDamage, bleedingDamage;
        private FightUnitSlot slimeCaster;
        private struct SlimeDetonation
        {
            public float remainingBeats, damage, lifetime;
            public FightUnitSlot caster;
            public GameObject effect;
        }
        private readonly List<SlimeDetonation> slimeDetonations = new();
        public bool SlimeDetonationPending => slimeDetonations.Count > 0;
        private long StatusBeat => CombatController != null ? CombatController.NormalBattleBeatCount : 0;
        private bool CanHaveStatus => team == UnitTeam.Enemy && hasCharacter && currentHp > 0;
        // The first ordinary beat starts duration 1. Expire at the boundary after
        // the fourth protected interval, never during Team Skill presentation.
        public bool HasSlimeFragment => CanHaveStatus && StatusBeat <= slimeUntil;
        public bool IsStunned => CanHaveStatus && StatusBeat <= stunUntil;
        public int BleedBeatsRemaining => CanHaveStatus ? bleedRemaining : 0;
        public int SlimeBeatsRemaining => HasSlimeFragment ? Mathf.Clamp((int)(slimeUntil - StatusBeat + 1), 1, slimeDuration) : 0;
        public int StunBeatsRemaining => IsStunned ? Mathf.Clamp((int)(stunUntil - StatusBeat + 1), 1, stunDuration) : 0;

        public bool AttachSlime(float damage, int beats, FightUnitSlot caster = null)
        {
            if (!CanHaveStatus) return false;
            slimeDamage = Mathf.Max(.5f, damage);
            slimeCaster = caster;
            slimeDuration = Mathf.Max(1, beats);
            slimeUntil = StatusBeat + slimeDuration;
            return true; // Reapplication refreshes one attachment, never stacks it.
        }

        private void ArmSlimeDetonation()
        {
            // Consume before callbacks so additional hits cannot arm the same fragment twice.
            slimeUntil = long.MinValue;
            var data = slimeCaster != null ? slimeCaster.CharacterDefinition : null;
            slimeDetonations.Add(new SlimeDetonation
            {
                remainingBeats = data != null ? data.FragmentDetonationDelayBeats : .5f,
                damage = slimeDamage, caster = slimeCaster,
                effect = data != null ? data.FragmentExplosionEffectPrefab : null,
                lifetime = data != null ? data.FragmentExplosionLifetime : 0
            });
        }

        // Uses song beat fractions like combat animations. Triggered explosions
        // advance during Team Skill; status duration counters remain frozen.
        public void AdvanceFragmentDetonations(float elapsedBeats)
        {
            if (!CanHaveStatus) { slimeDetonations.Clear(); return; }
            if (CombatController != null && (CombatController.IsPaused || CombatController.TimingCalibrationActive || CombatController.BattleEnded)) return;
            if (elapsedBeats < 0 || float.IsNaN(elapsedBeats) || float.IsInfinity(elapsedBeats)) return;
            for (int i = slimeDetonations.Count - 1; i >= 0; i--)
            {
                var hit = slimeDetonations[i];
                hit.remainingBeats -= elapsedBeats;
                if (hit.remainingBeats > .00001f) { slimeDetonations[i] = hit; continue; }
                slimeDetonations.RemoveAt(i);
                if (hit.caster != null && hit.caster.HasCharacter)
                {
                    if (hit.effect != null)
                        hit.caster.EffectPlayer.SpawnConfiguredAbility(hit.effect, ImpactEffectAnchor, hit.lifetime,
                            FightAttackEffect.VisualStyle.Skill, hit.caster.accentColor);
                    else hit.caster.PlaySkillAttackAt(ImpactEffectAnchor);
                }
                TakeDamage(hit.damage, DamageKind.Secondary);
                if (currentHp <= 0 && CombatController != null) CombatController.InterruptEnemyAttack(this);
                // Death/restart callbacks can clear the queue while a hit resolves.
                if (i > slimeDetonations.Count) return;
            }
        }

        public bool ApplyBleeding(float damage, int beats)
        {
            if (!CanHaveStatus) return false;
            bleedingDamage = Mathf.Max(.5f, damage);
            bleedRemaining = Mathf.Max(1, beats);
            lastBleedBeat = StatusBeat;
            if (CombatController != null) CombatController.RefreshStatusIcons(this);
            return true;
        }

        public bool ApplyStun(int beats)
        {
            if (!CanHaveStatus || (characterDefinition != null && characterDefinition.CrowdControlImmune)) return false;
            stunDuration = Mathf.Max(1, beats);
            stunUntil = StatusBeat + stunDuration;
            if (CombatController != null) CombatController.CancelEnemyActionForStun(this);
            return true;
        }

        public void AdvanceTimedStatuses(long beat)
        {
            if (!CanHaveStatus) { ClearTimedStatuses(); return; }
            if (CombatController != null && (CombatController.EnemyActionsPaused || CombatController.TeamSkillRunning)) return;
            if (beat > slimeUntil) slimeUntil = long.MinValue;
            if (beat > stunUntil) stunUntil = long.MinValue;
            if (bleedRemaining <= 0 || beat <= lastBleedBeat) return;
            lastBleedBeat = beat;
            if (CombatController != null) CombatController.PlayStatusIconTrigger(this, true, bleedRemaining == 1);
            bleedRemaining--;
            if (CombatController != null) CombatController.RefreshStatusIcons(this);
            // TakeDamage handles HP and Fragile; deliberately bypass boss armor/guard.
            TakeDamage(bleedingDamage, DamageKind.DamageOverTime);
            if (currentHp <= 0 && CombatController != null) CombatController.InterruptEnemyAttack(this);
        }

        public void ClearTimedStatuses()
        {
            slimeDetonations.Clear();
            slimeCaster = null;
            slimeUntil = stunUntil = lastBleedBeat = long.MinValue;
            bleedRemaining = 0;
            if (CombatController != null) CombatController.RefreshStatusIcons(this);
        }
    }
}
