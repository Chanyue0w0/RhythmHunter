using System;
using System.Collections.Generic;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        // One runtime state per warrior, never stored on the shared prefab asset.
        private sealed class SwordCharge
        {
            public int stage = 1, hits, generation;
            public long lastCall = long.MinValue, fullUntil = long.MinValue;
            public FightWeaponAttachment attachment;
            public FightCharacterDefinition definition;
            public FightCharacterCombatAnimator animator;
            public void BreakStreak() { hits = 0; lastCall = long.MinValue; generation++; }
        }
        private readonly Dictionary<FightUnitSlot, SwordCharge> swordCharges = new();
        public int GetSwordChargeStage(FightUnitSlot actor) => actor != null && swordCharges.TryGetValue(actor, out var state) ? state.stage : 1;
        public int GetSwordChargeHits(FightUnitSlot actor) => actor != null && swordCharges.TryGetValue(actor, out var state) ? state.hits : 0;
        public int GetSwordChargeBeatsRemaining(FightUnitSlot actor) => actor != null && swordCharges.TryGetValue(actor, out var state) && state.stage == 3
            ? (int)Math.Max(0, state.fullUntil - NormalBattleBeatCount) : 0;

        private SwordCharge SwordState(FightUnitSlot actor)
        {
            if (!swordCharges.TryGetValue(actor, out var state))
            {
                state = new SwordCharge { definition = actor.CharacterDefinition, animator = actor.CombatAnimator,
                    attachment = actor.ActorInstance != null ? actor.ActorInstance.GetComponentInChildren<FightWeaponAttachment>(true) : null };
                swordCharges.Add(actor, state);
                ShowSwordStage(state);
            }
            return state;
        }
        private void ShowSwordStage(SwordCharge state)
        {
            if (state.animator != null) state.animator.SetChargedIdle(state.stage == 3);
            state.attachment?.SetWeaponChargeStage(state.stage == 3 ? FightWeaponVisual.ChargeStage.FullCharged : state.stage == 2
                ? FightWeaponVisual.ChargeStage.HalfCharged : FightWeaponVisual.ChargeStage.Uncharged);
            if (state.definition != null) state.definition.SetSwordChargeAura(state.stage == 3, this);
        }

        private void TrackSwordJudgement(FightUnitSlot actor, FmodRhythmJudge.Result result)
        {
            if (!UsesEqualBeats || TeamSkillRunning || result.DuplicateBeat) return;
            foreach (var pair in swordCharges)
            {
                var state = pair.Value;
                if (state.stage == 3) continue;
                if (result.Judgement == FmodRhythmJudge.Grade.Miss && pair.Key == actor ||
                    result.Judgement == FmodRhythmJudge.Grade.Perfect && pair.Key != actor && result.NearestBeat.GlobalBeat > state.lastCall)
                    state.BreakStreak();
            }
        }

        private void UpdateSwordStreaks()
        {
            if (battleEnded || TeamSkillRunning || EnemyActionsPaused || TimingCalibrationActive || IsPaused || WaveTransitionActive ||
                rhythmJudge == null || beatClock == null || !beatClock.HasTimingAnchor || !beatClock.TryGetTimelinePositionMs(out int now)) return;
            var anchor = beatClock.LatestBeat;
            foreach (var state in swordCharges.Values)
            {
                if (state.stage == 3 || state.lastCall == long.MinValue) continue;
                double deadline = anchor.TimelinePositionMs + (state.lastCall + 1 - anchor.GlobalBeat) * beatClock.MillisecondsPerBeat
                    + rhythmJudge.PerfectWindowMs - rhythmJudge.JudgementOffsetMs + resolutionSafetyMs;
                if (now > deadline) state.BreakStreak();
            }
        }

        private void AdvanceSwordChargeBeat()
        {
            foreach (var state in swordCharges.Values)
                if (state.stage == 3 && NormalBattleBeatCount >= state.fullUntil)
                {
                    state.stage = 1; state.BreakStreak(); state.fullUntil = long.MinValue;
                    ShowSwordStage(state);
                }
        }
        private void BreakSwordStreaks()
        {
            foreach (var state in swordCharges.Values) state.BreakStreak();
        }
        private void ResetSwordCharges()
        {
            foreach (var state in swordCharges.Values) { state.stage = 1; ShowSwordStage(state); }
            swordCharges.Clear();
        }

        private string PerformSwordAttack(FightUnitSlot actor, float power, long beat)
        {
            var data = actor.CharacterDefinition;
            var state = SwordState(actor);
            if (state.lastCall != long.MinValue && beat <= state.lastCall) return "Sword input already accepted";
            if (state.stage < 3 && state.lastCall != long.MinValue && beat != state.lastCall + 1) state.BreakStreak();
            state.lastCall = beat;
            var target = FindFrontLivingEnemy();
            bool charged = state.stage == 3;
            var animation = charged
                ? FightCharacterCombatAnimator.CombatAnimation.HeavyAttack
                : FightCharacterCombatAnimator.CombatAnimation.NormalAttack;
            if (target == null || WaveTransitionActive)
            {
                state.BreakStreak();
                PlayTransitionAttackWithoutTarget(actor, animation);
                if (target != null) target.ShowTransitionImmuneHit();
                return "Sword — no vulnerable target";
            }
            int version = rosterVersion, generation = state.generation;
            bool primaryContact = false;
            ScheduleNormalImpact(actor, target, () =>
            {
                if (WaveTransitionActive || !IsCurrentHeroAction(version, actor, target) || target.CurrentHp <= 0)
                { if (state.generation == generation) state.BreakStreak(); return; }
                primaryContact = true;
                ApplyHeroDamage(version, actor, target, charged ? data.SwordChargedDamage : power, false, false,
                    charged ? data.SwordChargedOnHitEffect : null,
                    charged ? data.SwordImmediateExplosionEffect : null);
                // A killing contact counts. Follow-up/secondary damage never reaches this boundary.
                if (!charged && !battleEnded && state.generation == generation && state.stage < 3)
                {
                    state.hits++;
                    if (state.hits >= data.SwordHitsPerStage)
                    {
                        state.hits = 0; state.stage++;
                        if (state.stage == 3)
                        {
                            state.fullUntil = NormalBattleBeatCount + data.SwordFullChargeBeats;
                        }
                        ShowSwordStage(state);
                    }
                }
            }, onCancelled: () => { if (!charged && state.generation == generation) state.BreakStreak(); });
            // Queue against the original contact time: not VFX lifetime, not a second input.
            // Dead targets and roster changes cancel this tick rather than retargeting another enemy.
            if (charged)
                ScheduleNormalImpact(actor, target, () =>
                {
                    if (primaryContact) ApplyHeroDamage(version, actor, target, data.SwordDelayedDamage, false, false);
                }, delayBeats: normalAttackImpactBeats + data.SwordDelayedHitBeats);

            Action visual = () =>
            {
                if (!IsCurrentHeroAction(version, actor, target)) return;
                if (charged && data.SwordChargedSlashEffect != null)
                    actor.EffectPlayer.SpawnConfiguredAbility(data.SwordChargedSlashEffect, target.ImpactEffectAnchor,
                        data.AbilityEffectLifetime, FightAttackEffect.VisualStyle.Light, Color.white);
                else actor.PlayLightAttackAt(target.ImpactEffectAnchor);
            };
            bool animated = actor.PlayCombatAnimation(animation, () => PlayHeroAttackWarning(version, actor), visual, null,
                effect => { if (IsCurrentHeroAction(version, actor, target)) actor.PlayFrameEffect(effect, target.ImpactEffectAnchor); });
            if (!animated) visual();
            return charged ? "Resonance slash: 2 + delayed damage" : $"Sword charge {state.stage}: {state.hits}/{data.SwordHitsPerStage}";
        }
    }
}
