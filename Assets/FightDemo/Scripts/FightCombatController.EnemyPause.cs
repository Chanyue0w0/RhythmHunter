using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        private readonly List<FightCharacterCombatAnimator> frozenEnemyAnimators = new();
        private long enemyPausedBeats, lastEnemyFreezeBeat;
        private long lastEnemyQueuedBeat = long.MinValue;
        public bool EnemyActionsPaused { get; private set; }

        private void BeginEnemyPause()
        {
            if (EnemyActionsPaused) return;
            EnemyActionsPaused = true;
            lastEnemyFreezeBeat = latestCombatBeat;
            foreach (var enemy in fightScene2Enemies)
            {
                var animator = enemy != null ? enemy.CombatAnimator : null;
                if (animator == null) continue;
                animator.SetPlaybackPaused(true);
                frozenEnemyAnimators.Add(animator);
            }
        }

        private void AdvanceEnemyPause(long beat)
        {
            if (!EnemyActionsPaused || beat <= lastEnemyFreezeBeat) return;
            long elapsed = beat - lastEnemyFreezeBeat;
            lastEnemyFreezeBeat = beat;
            enemyPausedBeats += elapsed;
            var retainedGuards = new List<long>(guardedBeats);
            guardedBeats.Clear();
            foreach (long guard in retainedGuards) guardedBeats.Add(guard + elapsed);
            ShiftScheduledEnemyHits(elapsed);
            if (lastEnemyQueuedBeat != long.MinValue) lastEnemyQueuedBeat += elapsed;
            if (BossActive)
            {
                bossPhaseStart += elapsed; bossPhaseEnd += elapsed;
                if (lastBossBeat != long.MinValue) lastBossBeat += elapsed;
                if (lastBossAttackBeat != long.MinValue) lastBossAttackBeat += elapsed;
            }
            if (pendingEnemyAttack)
            {
                pendingAttackGlobalBeat += elapsed;
            }
        }

        private void EndEnemyPause(bool resumeAttack)
        {
            if (!EnemyActionsPaused) return;
            EnemyActionsPaused = false;
            if (resumeAttack && aimedShotProgress > 0)
                lastAimedShotBeat = latestCombatBeat - 1;
            if (resumeAttack) ResumeScheduledEnemyHits();
            foreach (var animator in frozenEnemyAnimators)
                if (animator != null) animator.SetPlaybackPaused(false);
            frozenEnemyAnimators.Clear();
            if (resumeAttack && pendingEnemyAttack && beatClock != null && beatClock.HasTimingAnchor)
            {
                // Resume on this musical beat with its FULL judgement window, rather
                // than instantly applying a damage deadline that passed while frozen.
                pendingAttackGlobalBeat = latestCombatBeat;
                pendingAttackTimelineMs = beatClock.LatestBeat.TimelinePositionMs;
                pendingAttackBar = beatClock.LatestBeat.Bar;
            }
        }

        private void ResetEnemyPause()
        {
            if (EnemyActionsPaused) InterruptEnemyAttack(pendingEnemyAttacker);
            // Reset cancels suspended actions instead of briefly resuming their callbacks.
            foreach (var animator in frozenEnemyAnimators)
                if (animator != null) animator.InterruptPlayback();
            EndEnemyPause(false);
            enemyPausedBeats = 0;
            lastEnemyQueuedBeat = long.MinValue;
        }

        /// <summary>Use for an ability that interrupts an enemy, including during Team Skill.</summary>
        public bool TryInterruptEnemyAttack(FightUnitSlot enemy)
        {
            if (enemy == null || !enemy.HasCharacter || enemy.CurrentHp <= 0 || !fightScene2Enemies.Contains(enemy) ||
                (enemy.CharacterDefinition != null && !enemy.CharacterDefinition.AttackInterruptible)) return false;
            bool pending = pendingEnemyAttack && pendingEnemyAttacker == enemy;
            bool activeBossAction = BossActive && enemy == bossActor && latestCombatBeat < bossPhaseEnd &&
                (CurrentBossPhase == BossPhase.Attack || CurrentBossPhase == BossPhase.Charge || CurrentBossPhase == BossPhase.Rage);
            bool scheduled = InterruptScheduledEnemyAction(enemy);
            if (!pending && !activeBossAction && !scheduled) return false;
            InterruptEnemyAttack(enemy);
            // Cancels the remaining scheduled hits of the current test-boss sequence.
            // Already emitted visual/projectile objects are not owned or deleted here.
            if (activeBossAction) EnterBossPhase(BossPhase.Neutral, latestCombatBeat);
            return true;
        }

        /// <summary>Unconditional cancellation for death, Break and teardown; not evidence of a successful player interrupt.</summary>
        public void InterruptEnemyAttack(FightUnitSlot enemy)
        {
            if (enemy == null || !fightScene2Enemies.Contains(enemy)) return;
            enemy.CombatAnimator?.InterruptPlayback();
            if (pendingEnemyAttacker != enemy) return;
            pendingEnemyAttack = false; pendingEnemyAnimationDriven = false;
            pendingEnemyAttacker = null; pendingEnemyActionId = long.MinValue;
            pendingBossDamage = 0;
        }
    }
}
