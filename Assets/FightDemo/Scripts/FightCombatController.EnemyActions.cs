using System.Collections.Generic;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        private sealed class ScheduledEnemyHit
        {
            public FightUnitSlot source;
            public double due;
            public long hitBeat = long.MaxValue;
            public int timeline, bar, version;
            public float damage;
            public float guardChip;
            public bool launched, interruptible;
        }
        private readonly List<ScheduledEnemyHit> enemyHits = new();
        private long lastAuthoredEnemyBeat = long.MinValue;
        public int ScheduledEnemyHitCount => enemyHits.Count;

        private void StartAuthoredEnemyTurns(long globalBeat)
        {
            if (globalBeat == lastAuthoredEnemyBeat || battleEnded || AwaitingBattleStart ||
                AwaitingNextWave || WaveEntranceRunning || EnemyActionsPaused || TeamSkillRunning || IsPaused || TimingCalibrationActive) return;
            lastAuthoredEnemyBeat = globalBeat;
            RefreshPressureCadence();
            foreach (var enemy in fightScene2Enemies)
            {
                if (enemy == null || !enemy.HasCharacter || enemy.CurrentHp <= 0 || enemy.IsStunned || enemy.BossPhaseOneActionsStopped ||
                    enemy.CharacterDefinition == null || !enemy.CharacterDefinition.EnemyCanAttack ||
                    (BossActive && enemy == bossActor) ||
                    !EnemyCadenceReady(enemy, globalBeat) || EnemyActionBusy(enemy)) continue;
                if (enemy.CharacterDefinition.EnemyAttackUsesEffectFrames)
                    TryStartAuthoredEnemyAttack(enemy);
                else
                    StartIndependentEnemyAttack(enemy);
            }
        }

        // Windup-based enemies use the same per-source queue as frame-authored enemies.
        // Their timing choice must never restrict them to the front formation slot.
        private void StartIndependentEnemyAttack(FightUnitSlot source)
        {
            if(source==null || source.BossPhaseOneActionsStopped || source.CharacterDefinition == null || !source.CharacterDefinition.EnemyCanAttack)return;
            var definition = source.CharacterDefinition;
            RecordEnemyActionCadence(source);
            enemyHits.Add(new ScheduledEnemyHit { source = source,
                due = NormalBattleBeatCount + definition.AttackWindupBeats,
                version = rosterVersion, guardChip = CurrentPressureGuardChip, damage = source.AttackPower, interruptible = definition.AttackInterruptible });
            int version = rosterVersion;
            bool Valid() => version == rosterVersion && !battleEnded && source != null && source.CurrentHp > 0;
            bool started = source.PlayCombatAnimation(FightCharacterCombatAnimator.CombatAnimation.NormalAttack,
                () => { if (Valid()) source.PlayAttackFrameWarning(true); },
                () => { if (Valid()) source.PlayLightAttackAt(tankSlot != null ? tankSlot.ImpactEffectAnchor : null); }, null);
            if (!started) source.PlayLightAttackAt(tankSlot != null ? tankSlot.ImpactEffectAnchor : null);
        }

        /// <summary>Future behavior entry point: offsets are ordinary combat beats from now.
        /// A launched projectile survives interruption of its source. No enemy prefab uses this automatically.</summary>
        public bool ScheduleEnemyHits(FightUnitSlot source, int[] beatOffsets, float damage,
            bool interruptible = true, bool launchedProjectile = false)
        {
            if (PressureRestActive || battleEnded || AwaitingBattleStart || AwaitingNextWave || EnemyActionsPaused || IsPaused || TimingCalibrationActive ||
                source == null || source.CharacterDefinition == null || !source.CharacterDefinition.EnemyCanAttack || source.CurrentHp <= 0 || source.IsStunned || source.BossPhaseOneActionsStopped || !fightScene2Enemies.Contains(source) ||
                beatOffsets == null || beatOffsets.Length == 0 || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return false;
            foreach (int offset in beatOffsets) if (offset < 1) return false;
            foreach (int offset in beatOffsets)
                enemyHits.Add(new ScheduledEnemyHit { source = source, due = NormalBattleBeatCount + offset,
                    version = rosterVersion, guardChip = CurrentPressureGuardChip, damage = damage, launched = launchedProjectile, interruptible = interruptible });
            source.PlayAttackFrameWarning(true);
            return true;
        }

        // Read frame timing once per action; animation callbacks remain presentation only.
        private bool TryStartAuthoredEnemyAttack(FightUnitSlot source)
        {
            var definition = source != null ? source.CharacterDefinition : null;
            if (definition == null || !definition.EnemyCanAttack || source.IsStunned || source.BossPhaseOneActionsStopped || !definition.EnemyAttackUsesEffectFrames) return false;
            var animation = FightCharacterCombatAnimator.CombatAnimation.NormalAttack;
            float damage = source.AttackPower;
            float healing = 0;
            GameObject healingEffect = null;
            if (definition.EnemyAbilities.Count > 0)
            {
                var ability = definition.SelectEnemyAbility(Random.value, PressurePhaseActive);
                if (ability == null) return false;
                animation = ability.Animation;
                damage = Mathf.Max(0, ability.damagePerHit);
                healing = Mathf.Max(0, ability.healOnEffectFrame);
                healingEffect = ability.healingEffectPrefab;
            }
            var sequence = source.CombatAnimator != null ? source.CombatAnimator.FindSequence(animation) : null;
            if (sequence == null || sequence.Frames.Count == 0 || !sequence.UseFrameBeatDurations) return false;
            double offset = 0;
            int count = 0;
            for (int i = 0; i < sequence.Frames.Count; i++)
            {
                if (damage > 0 && sequence.PlaysAttackEffectAt(i))
                {
                    enemyHits.Add(new ScheduledEnemyHit { source = source, due = NormalBattleBeatCount + offset,
                        version = rosterVersion, guardChip = CurrentPressureGuardChip, damage = damage, interruptible = definition.AttackInterruptible });
                    count++;
                }
                offset += sequence.GetFrameBeats(i);
            }
            if (count == 0 && damage > 0) return false;
            RecordEnemyAnimationCadence(source, animation);
            int version = rosterVersion;
            bool Valid() => version == rosterVersion && !battleEnded && source != null && source.CurrentHp > 0;
            bool healed = false;
            source.PlayCombatAnimation(animation,
                () => { if (Valid()) source.PlayAttackFrameWarning(true); },
                () =>
                {
                    if (!Valid()) return;
                    // Only an actually entered effect frame heals, never a legacy callback flush.
                    var animator = source.CombatAnimator;
                    if (healingEffect == null && !healed && healing > 0 && animator != null && animator.IsPlaying &&
                        animator.ActiveAnimation == animation && sequence.PlaysAttackEffectAt(animator.ActiveFrame))
                    { healed = true; source.Heal(healing); }
                    if (damage > 0) source.PlayLightAttackAt(tankSlot != null ? tankSlot.ImpactEffectAnchor : null);
                }, null, effect =>
                {
                    if (!Valid()) return;
                    source.PlayFrameEffect(effect, tankSlot != null ? tankSlot.ImpactEffectAnchor : null);
                    if (!healed && healing > 0 && healingEffect != null && effect.prefab == healingEffect)
                    { healed = true; source.Heal(healing); }
                });
            return true;
        }

        private bool InterruptScheduledEnemyAction(FightUnitSlot source)
        {
            return enemyHits.RemoveAll(hit => hit.source == source && !hit.launched && hit.interruptible) > 0;
        }

        private void AdvanceEnemyCombatWork(FmodBeatClock.BeatSnapshot beat)
        {
            if (pendingEnemyAttack && pendingEnemyWindup && NormalBattleBeatCount >= pendingEnemyHitCombatBeat)
            {
                pendingEnemyWindup = false;
                pendingAttackGlobalBeat = beat.GlobalBeat;
                pendingAttackTimelineMs = beat.TimelinePositionMs;
                pendingAttackBar = beat.Bar;
            }
            enemyHits.RemoveAll(hit => hit.version != rosterVersion || (!hit.launched && (hit.source == null || hit.source.CurrentHp <= 0)));
            foreach (var hit in enemyHits)
            {
                if (hit.hitBeat != long.MaxValue || hit.due > NormalBattleBeatCount + 0.00001) continue;
                double relative = hit.due - NormalBattleBeatCount;
                hit.hitBeat = beat.GlobalBeat + (long)System.Math.Floor(relative + 0.00001);
                float beatMs = beat.Tempo > 0 ? 60000f / beat.Tempo : 500f;
                hit.timeline = beat.TimelinePositionMs + Mathf.RoundToInt((float)relative * beatMs);
                hit.bar = beat.Bar;
            }
        }

        private void ResolveEnemyCombatWork(int timelineMs)
        {
            if (battleEnded || AwaitingNextWave || EnemyActionsPaused || IsPaused || TimingCalibrationActive || rhythmJudge == null) return;
            // Fractional hits use the containing beat's guard and the existing late input window.
            if (beatClock != null)
            {
                var beat = beatClock.LatestBeat;
                double duration = beatClock.MillisecondsPerBeat;
                double now = NormalBattleBeatCount + System.Math.Max(0, timelineMs - beat.TimelinePositionMs) / duration;
                foreach (var hit in enemyHits)
                {
                    if (hit.hitBeat != long.MaxValue || hit.due > now + 0.00001) continue;
                    double relative = hit.due - NormalBattleBeatCount;
                    hit.hitBeat = beat.GlobalBeat + (long)System.Math.Floor(relative + 0.00001);
                    hit.timeline = beat.TimelinePositionMs + Mathf.RoundToInt((float)(relative * duration));
                    hit.bar = beat.Bar;
                }
            }
            float window = Mathf.Max(0, rhythmJudge.PerfectWindowMs - rhythmJudge.JudgementOffsetMs) + resolutionSafetyMs;
            foreach (var hit in enemyHits.ToArray())
            {
                if (hit.hitBeat == long.MaxValue || timelineMs < hit.timeline) continue;
                // A confirmed guard cannot become a miss. Resolve it at impact instead of
                // holding its sound/visual feedback until the late-input window closes.
                bool blocked = guardedBeats.Contains(hit.hitBeat);
                if (!blocked && timelineMs < hit.timeline + window) continue;
                if (!enemyHits.Remove(hit) || hit.version != rosterVersion) continue;
                if (!hit.launched && (hit.source == null || hit.source.CurrentHp <= 0)) continue;
                float damage = ResolveEnemyHitDamage(GetSlimeReducedDamage(hit.source, hit.damage), blocked, hit.guardChip);
                if (blocked) RecordMageGuardBlock(hit.hitBeat);
                if (blocked) { blockedAttackCount++; PlaySuccessfulBlockFeedback(); } else receivedAttackCount++;
                damage = ApplyEnemyPartyDamage(damage, blocked, hit.hitBeat);
                if (blocked) RecordPressureBlock();
                if (damage > 0 && hit.source != null && tankSlot != null)
                    hit.source.PlayOnHitEffect(false, tankSlot.ImpactEffectAnchor);
                if (!blocked && !battleEnded) tankSlot?.PlayCombatAnimation(FightCharacterCombatAnimator.CombatAnimation.Hit, null, null, null);
                EnemyAttackResolved?.Invoke(new EnemyAttackResult(hit.hitBeat, hit.bar, blocked, damage, partyHp));
                if (battleEnded) break;
            }
        }

        private void ResumeScheduledEnemyHits()
        {
            foreach (var hit in enemyHits)
            {
                if (hit.hitBeat == long.MaxValue) continue;
                hit.hitBeat = latestCombatBeat;
                if (beatClock != null) { hit.timeline = beatClock.LatestBeat.TimelinePositionMs; hit.bar = beatClock.LatestBeat.Bar; }
            }
        }

        private void ShiftScheduledEnemyHits(long elapsed)
        {
            foreach (var hit in enemyHits)
            {
                if (hit.hitBeat == long.MaxValue) continue;
                hit.hitBeat += elapsed;
            }
        }

        private void ClearEnemyScheduledActions()
        {
            enemyHits.Clear();
            ClearPressureCadence();
            lastAuthoredEnemyBeat = long.MinValue;
        }
    }
}
