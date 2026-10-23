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
            public bool launched, interruptible;
        }
        private readonly List<ScheduledEnemyHit> enemyHits = new();
        private long lastAuthoredEnemyBeat = long.MinValue;
        public int ScheduledEnemyHitCount => enemyHits.Count;

        private void StartAuthoredEnemyTurns(long globalBeat)
        {
            if (globalBeat == lastAuthoredEnemyBeat || battleEnded || AwaitingBattleStart ||
                AwaitingNextWave || EnemyActionsPaused || IsPaused || TimingCalibrationActive) return;
            lastAuthoredEnemyBeat = globalBeat;
            foreach (var enemy in fightScene2Enemies)
            {
                if (enemy == null || !enemy.HasCharacter || enemy.CurrentHp <= 0 || enemy.IsStunned ||
                    enemy.CharacterDefinition == null || !enemy.CharacterDefinition.EnemyAttackUsesEffectFrames ||
                    (BossActive && enemy == bossActor) ||
                    !IsScheduledAttackBeat(globalBeat - enemyPausedBeats, enemy.AttackIntervalBeats) ||
                    enemyHits.Exists(hit => hit.source == enemy && !hit.launched) ||
                    (enemy.CombatAnimator != null && enemy.CombatAnimator.IsPlaying)) continue;
                TryStartAuthoredEnemyAttack(enemy);
            }
        }

        /// <summary>Future behavior entry point: offsets are ordinary combat beats from now.
        /// A launched projectile survives interruption of its source. No enemy prefab uses this automatically.</summary>
        public bool ScheduleEnemyHits(FightUnitSlot source, int[] beatOffsets, float damage,
            bool interruptible = true, bool launchedProjectile = false)
        {
            if (battleEnded || AwaitingBattleStart || AwaitingNextWave || EnemyActionsPaused || IsPaused || TimingCalibrationActive ||
                source == null || source.CurrentHp <= 0 || source.IsStunned || !fightScene2Enemies.Contains(source) ||
                beatOffsets == null || beatOffsets.Length == 0 || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return false;
            foreach (int offset in beatOffsets) if (offset < 1) return false;
            foreach (int offset in beatOffsets)
                enemyHits.Add(new ScheduledEnemyHit { source = source, due = NormalBattleBeatCount + offset,
                    version = rosterVersion, damage = damage, launched = launchedProjectile, interruptible = interruptible });
            source.PlayAttackFrameWarning(true);
            return true;
        }

        // Read frame timing once per action; animation callbacks remain presentation only.
        private bool TryStartAuthoredEnemyAttack(FightUnitSlot source)
        {
            var definition = source != null ? source.CharacterDefinition : null;
            if (definition == null || source.IsStunned || !definition.EnemyAttackUsesEffectFrames) return false;
            var animation = FightCharacterCombatAnimator.CombatAnimation.NormalAttack;
            var sequence = source.CombatAnimator != null ? source.CombatAnimator.FindSequence(animation) : null;
            if (sequence == null || !sequence.UseFrameBeatDurations) return false;
            double offset = 0;
            int count = 0;
            for (int i = 0; i < sequence.Frames.Count; i++)
            {
                if (sequence.PlaysAttackEffectAt(i))
                {
                    enemyHits.Add(new ScheduledEnemyHit { source = source, due = NormalBattleBeatCount + offset,
                        version = rosterVersion, damage = source.AttackPower, interruptible = definition.AttackInterruptible });
                    count++;
                }
                offset += sequence.GetFrameBeats(i);
            }
            if (count == 0) return false;
            int version = rosterVersion;
            bool Valid() => version == rosterVersion && !battleEnded && source != null && source.CurrentHp > 0;
            source.PlayCombatAnimation(animation,
                () => { if (Valid()) source.PlayAttackFrameWarning(true); },
                () => { if (Valid()) source.PlayLightAttackAt(tankSlot != null ? tankSlot.ImpactEffectAnchor : null); }, null);
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
                if (hit.hitBeat == long.MaxValue || timelineMs < hit.timeline + window) continue;
                if (!enemyHits.Remove(hit) || hit.version != rosterVersion) continue;
                if (!hit.launched && (hit.source == null || hit.source.CurrentHp <= 0)) continue;
                bool blocked = guardedBeats.Contains(hit.hitBeat);
                float damage = blocked || !HealthSystemEnabled ? 0 : QuantizeCombatValue(hit.damage);
                if (blocked) blockedAttackCount++; else receivedAttackCount++;
                ApplyPartyDamage(damage, hit.hitBeat);
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
            lastAuthoredEnemyBeat = long.MinValue;
        }
    }
}
