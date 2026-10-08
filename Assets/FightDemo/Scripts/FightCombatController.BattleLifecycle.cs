using System;
using RhythmHunter.RhythmDemo;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        public int CurrentWave { get; private set; } = 1;
        public int WaveCount => rosterManager != null ? rosterManager.WaveCount : 1;
        public bool BattleWon { get; private set; }
        public bool AwaitingNextWave { get; private set; }
        public event Action BattleVictory;
        public event Action<int> WaveStarted;
        // Buff durations use this event/count, never the musical performance beat.
        public event Action<long> NormalBattleBeat;
        public long NormalBattleBeatCount { get; private set; }
        private long lastNormalBattleBeat = long.MinValue;

        private void CheckWaveCompletion()
        {
            if (!UsesBattlePreparation || AwaitingBattleStart || battleEnded || AwaitingNextWave || !HealthSystemEnabled) return;
            if (partyHp <= 0) { FinishBattle(false); return; }
            if (FindFrontLivingEnemy() != null) return;
            AwaitingNextWave = true;
            ResetBasicAbilityProgress();
            CancelEnemyCombatWork();
            RestorePartyArmor();
        }

        private bool AdvanceWaveBoundary()
        {
            CheckWaveCompletion();
            if (battleEnded) return true;
            if (!AwaitingNextWave || TeamSkillRunning || TeamUltimateRunning) return false;
            if (CurrentWave >= WaveCount) { FinishBattle(true); return true; }
            CancelEnemyCombatWork();
            rosterVersion++; // Invalidates all old hero animation callbacks before slots are reused.
            if (!rosterManager.SpawnNextEnemyWave(CurrentWave)) { FinishBattle(true); return true; }
            CurrentWave++;
            fightScene2Enemies.Clear();
            fightScene2Enemies.AddRange(rosterManager.ActiveEnemies);
            fightScene2Enemies.Sort((left, right) => right.SlotIndex.CompareTo(left.SlotIndex));
            activeEnemySlot = FindFrontLivingEnemy();
            AwaitingNextWave = false;
            // Start the new wave's attack countdown from this boundary.
            enemyPausedBeats = latestCombatBeat + 1;
            RosterRebuilt?.Invoke();
            WaveStarted?.Invoke(CurrentWave);
            return true; // No enemy attack on the spawn boundary.
        }

        private void AdvanceNormalBattleBeat(FmodBeatClock.BeatSnapshot beat)
        {
            if (battleEnded || AwaitingNextWave || EnemyActionsPaused || TeamSkillRunning || TeamUltimateRunning ||
                beat.GlobalBeat <= lastNormalBattleBeat) return;
            lastNormalBattleBeat = beat.GlobalBeat;
            NormalBattleBeatCount++;
            NormalBattleBeat?.Invoke(NormalBattleBeatCount);
            AdvanceEnemyCombatWork(beat);
        }

        private void CancelEnemyCombatWork()
        {
            pendingEnemyAttack = pendingEnemyAnimationDriven = false;
            pendingEnemyAttacker = null;
            pendingEnemyActionId = long.MinValue;
            guardedBeats.Clear();
            ResetBossState();
            ClearEnemyScheduledActions();
            foreach (var enemy in fightScene2Enemies)
                if (enemy != null) { enemy.ClearFragile(); enemy.CombatAnimator?.InterruptPlayback(); }
            ClearBattleEffects();
        }

        private void FinishBattle(bool won)
        {
            if (battleEnded) return;
            battleEnded = true;
            BattleWon = won;
            AwaitingNextWave = false;
            rosterVersion++;
            int mana = teamSkillGauge;
            ResetTeamSkill();
            teamSkillGauge = mana;
            CancelEnemyCombatWork();
            foreach (var hero in new[] { frontHero.UnitSlot, secondHero.UnitSlot, thirdHero.UnitSlot })
                if (hero != null) hero.CombatAnimator?.InterruptPlayback();
            if (won) BattleVictory?.Invoke(); else BattleLost?.Invoke();
        }
    }
}
