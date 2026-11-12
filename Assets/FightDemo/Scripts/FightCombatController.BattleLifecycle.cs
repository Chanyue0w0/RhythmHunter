using System;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        [Header("Wave Transition")]
        [Tooltip("Number of subsequent music beats to wait before the next wave enters.")]
        [SerializeField, Min(0)] private int waveTransitionWaitBeats = 2;
        [Tooltip("Optional shared-armor recovery VFX, spawned at the front hero when a wave restores armor.")]
        [SerializeField] private GameObject waveArmorRecoveryEffect;
        [Tooltip("Zero lets the prefab control its own lifetime, like other battle VFX.")]
        [SerializeField, Min(0)] private float waveArmorRecoveryEffectLifetime;
        private int waveWaitBeatsRemaining;
        private long lastWaveWaitBeat = long.MinValue;
        public int WaveWaitBeatsRemaining => AwaitingNextWave ? waveWaitBeatsRemaining : 0;
        public event Action<int> WaveCleared;
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
            if (endBattleOnZeroHp && partyHp <= 0) { FinishBattle(false); return; }
            // Zero HP still means defeated; keep this wave available for burst overkill presentation.
            if (TeamSkillRunning) return;
            if (FindFrontLivingEnemy() != null) return;
            AwaitingNextWave = true;
            waveWaitBeatsRemaining = Mathf.Max(0, waveTransitionWaitBeats);
            lastWaveWaitBeat = latestCombatBeat;
            ResetBasicAbilityProgress();
            BreakSwordStreaks();
            CancelEnemyCombatWork();
            float restored = RecoverPartyArmor(FightCharacterDefinition.ArmorRestoreMode.Full);
            if (restored > 0 && waveArmorRecoveryEffect != null && tankSlot != null)
                tankSlot.EffectPlayer.SpawnConfiguredAbility(waveArmorRecoveryEffect, tankSlot.ImpactEffectAnchor,
                    waveArmorRecoveryEffectLifetime, FightAttackEffect.VisualStyle.Normal, Color.white);
            WaveCleared?.Invoke(CurrentWave);
        }

        private bool AdvanceWaveBoundary()
        {
            CheckWaveCompletion();
            if (battleEnded) return true;
            if (!AwaitingNextWave || TeamSkillRunning || TeamUltimateRunning) return false;
            if (CurrentWave >= WaveCount) { FinishBattle(true); return true; }
            // Count unique musical beats, not elapsed seconds or normal-combat status beats.
            // Pausing/calibration never consumes the gap; duplicate callbacks cannot shorten it.
            if (latestCombatBeat > lastWaveWaitBeat)
            {
                lastWaveWaitBeat = latestCombatBeat;
                waveWaitBeatsRemaining = Mathf.Max(0, waveWaitBeatsRemaining - 1);
            }
            if (waveWaitBeatsRemaining > 0) return false;
            CancelEnemyCombatWork();
            rosterVersion++; // Invalidates all old hero animation callbacks before slots are reused.
            if (!rosterManager.SpawnNextEnemyWave(CurrentWave)) { FinishBattle(true); return true; }
            CurrentWave++;
            ResetWavePressure();
            fightScene2Enemies.Clear();
            fightScene2Enemies.AddRange(rosterManager.ActiveEnemies);
            fightScene2Enemies.Sort((left, right) => right.SlotIndex.CompareTo(left.SlotIndex));
            activeEnemySlot = FindFrontLivingEnemy();
            AwaitingNextWave = false;
            waveWaitBeatsRemaining = 0;
            // Start the new wave's attack countdown from this boundary.
            enemyPausedBeats = latestCombatBeat + 1;
            RosterRebuilt?.Invoke();
            WaveStarted?.Invoke(CurrentWave);
            return true; // No enemy attack on the spawn boundary.
        }

        private void AdvanceNormalBattleBeat(FmodBeatClock.BeatSnapshot beat)
        {
            if (battleEnded || AwaitingBattleStart || WaveEntranceRunning || IsPaused || TimingCalibrationActive ||
                AwaitingNextWave || EnemyActionsPaused || TeamSkillRunning || TeamUltimateRunning ||
                beat.GlobalBeat <= lastNormalBattleBeat) return;
            lastNormalBattleBeat = beat.GlobalBeat;
            NormalBattleBeatCount++;
            AdvanceSwordChargeBeat();
            AdvanceWavePressureBeat();
            foreach (var enemy in fightScene2Enemies.ToArray())
            {
                if (enemy != null) enemy.AdvanceTimedStatuses(NormalBattleBeatCount);
                if (battleEnded || AwaitingNextWave) return;
            }
            CheckWaveCompletion();
            if (battleEnded || AwaitingNextWave) return;
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
                if (enemy != null) { enemy.ClearFragile(); enemy.ClearTimedStatuses(); enemy.CombatAnimator?.InterruptPlayback(); }
            ClearCombatEffectsKeepingDamageNumbers();
        }

        private void FinishBattle(bool won)
        {
            if (battleEnded) return;
            battleEnded = true;
            ResetSwordCharges();
            BattleWon = won;
            AwaitingNextWave = false;
            waveWaitBeatsRemaining = 0;
            rosterVersion++;
            int mana = teamSkillGauge;
            float manaFraction = teamSkillGaugeFraction;
            ResetTeamSkill();
            teamSkillGauge = mana;
            teamSkillGaugeFraction = manaFraction;
            CancelEnemyCombatWork();
            foreach (var hero in new[] { frontHero.UnitSlot, secondHero.UnitSlot, thirdHero.UnitSlot })
                if (hero != null) hero.CombatAnimator?.InterruptPlayback();
            if (won) BattleVictory?.Invoke(); else BattleLost?.Invoke();
        }
    }
}
