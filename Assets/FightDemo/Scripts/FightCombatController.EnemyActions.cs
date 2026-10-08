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
            public long due, hitBeat = long.MaxValue;
            public int timeline, bar, version;
            public float damage;
            public bool launched, interruptible;
        }
        private readonly List<ScheduledEnemyHit> enemyHits = new();
        public int ScheduledEnemyHitCount => enemyHits.Count;

        /// <summary>Future behavior entry point: offsets are ordinary combat beats from now.
        /// A launched projectile survives interruption of its source. No enemy prefab uses this automatically.</summary>
        public bool ScheduleEnemyHits(FightUnitSlot source, int[] beatOffsets, float damage,
            bool interruptible = true, bool launchedProjectile = false)
        {
            if (battleEnded || AwaitingBattleStart || AwaitingNextWave || EnemyActionsPaused || IsPaused || TimingCalibrationActive ||
                source == null || source.CurrentHp <= 0 || !fightScene2Enemies.Contains(source) ||
                beatOffsets == null || beatOffsets.Length == 0 || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return false;
            foreach (int offset in beatOffsets) if (offset < 1) return false;
            foreach (int offset in beatOffsets)
                enemyHits.Add(new ScheduledEnemyHit { source = source, due = NormalBattleBeatCount + offset,
                    version = rosterVersion, damage = damage, launched = launchedProjectile, interruptible = interruptible });
            source.PlayAttackFrameWarning(true);
            return true;
        }

        private bool InterruptScheduledEnemyAction(FightUnitSlot source)
        {
            return enemyHits.RemoveAll(hit => hit.source == source && !hit.launched && hit.interruptible) > 0;
        }

        private void AdvanceEnemyCombatWork(FmodBeatClock.BeatSnapshot beat)
        {
            enemyHits.RemoveAll(hit => hit.version != rosterVersion || (!hit.launched && (hit.source == null || hit.source.CurrentHp <= 0)));
            foreach (var hit in enemyHits)
            {
                if (hit.hitBeat != long.MaxValue || hit.due > NormalBattleBeatCount) continue;
                hit.hitBeat = beat.GlobalBeat; hit.timeline = beat.TimelinePositionMs; hit.bar = beat.Bar;
            }
        }

        private void ResolveEnemyCombatWork(int timelineMs)
        {
            if (battleEnded || AwaitingNextWave || EnemyActionsPaused || IsPaused || TimingCalibrationActive || rhythmJudge == null) return;
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

        private void ClearEnemyScheduledActions() => enemyHits.Clear();
    }
}
