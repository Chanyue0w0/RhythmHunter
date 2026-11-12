using System;
using System.Collections.Generic;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        private FightEnemyPressureSettings wavePressure = new();
        private sealed class EnemyCadence
        {
            public double actionEnd;
            public double pressureReady;
            public double normalReady = -1;
        }
        private readonly Dictionary<FightUnitSlot, EnemyCadence> enemyCadences = new();
        private bool pressureCadenceActive;
        public long WaveCombatBeatCount { get; private set; }
        public bool PressurePhaseEnabled => wavePressure.enablePressurePhase;
        public bool PressurePhaseActive => !battleEnded && !AwaitingBattleStart && !WaveTransitionActive &&
            wavePressure.IsActiveAt(WaveCombatBeatCount);
        public long PressurePhaseRemainingBeats => PressurePhaseActive ? wavePressure.RemainingAt(WaveCombatBeatCount) : 0;
        public long PressureNextStartBeat => battleEnded || AwaitingNextWave ? -1 : wavePressure.NextStartAt(WaveCombatBeatCount);
        public int PressureRecoveryBeats => wavePressure.recoveryBeats;
        public IReadOnlyList<FightUnitSlot> PressureEnemySlots => fightScene2Enemies;

        private void ResetWavePressure()
        {
            ClearPressureCadence();
            WaveCombatBeatCount = 0;
            wavePressure = rosterManager != null
                ? rosterManager.GetWavePressureSettings(CurrentWave - 1) : new FightEnemyPressureSettings();
        }

        // Called only when the existing normal-battle clock accepts a new beat.
        private void AdvanceWavePressureBeat() => WaveCombatBeatCount++;

        private EnemyCadence CadenceFor(FightUnitSlot enemy)
        {
            if (!enemyCadences.TryGetValue(enemy, out var cadence))
                enemyCadences.Add(enemy, cadence = new EnemyCadence());
            return cadence;
        }

        private bool EnemyActionBusy(FightUnitSlot enemy) =>
            enemyHits.Exists(hit => hit.source == enemy && !hit.launched) ||
            (enemy.CombatAnimator != null && enemy.CombatAnimator.IsPlaying);

        private void RefreshPressureCadence()
        {
            bool active = PressurePhaseActive;
            if (active == pressureCadenceActive) return;
            pressureCadenceActive = active;
            foreach (var enemy in fightScene2Enemies)
            {
                if (enemy == null || !enemy.HasCharacter || enemy.CurrentHp <= 0) continue;
                var cadence = CadenceFor(enemy);
                if (active)
                    cadence.pressureReady = EnemyActionBusy(enemy)
                        ? Math.Max(NormalBattleBeatCount, cadence.actionEnd) + PressureRecoveryBeats
                        : NormalBattleBeatCount;
                else
                    // Exit never cuts a telegraphed action short or catches up an old modulo beat.
                    cadence.normalReady = Math.Max(NormalBattleBeatCount, cadence.actionEnd) + enemy.AttackIntervalBeats;
            }
        }

        private bool EnemyCadenceReady(FightUnitSlot enemy, long globalBeat)
        {
            if (PressurePhaseEnabled)
            {
                var cadence = CadenceFor(enemy);
                if (PressurePhaseActive) return NormalBattleBeatCount + .00001 >= cadence.pressureReady;
                if (cadence.normalReady >= 0) return NormalBattleBeatCount + .00001 >= cadence.normalReady;
            }
            return IsScheduledAttackBeat(globalBeat - enemyPausedBeats, enemy.AttackIntervalBeats);
        }

        private void RecordEnemyActionCadence(FightUnitSlot enemy)
        {
            if (!PressurePhaseEnabled) return;
            var sequence = enemy.CombatAnimator?.FindSequence(FightCharacterCombatAnimator.CombatAnimation.NormalAttack);
            double duration = 0;
            if (sequence != null)
                for (int i = 0; i < sequence.Frames.Count; i++)
                    duration += sequence.UseFrameBeatDurations ? sequence.GetFrameBeats(i)
                        : 1000d / Math.Max(1, sequence.FramesPerSecond) / (beatClock != null ? Math.Max(1, beatClock.MillisecondsPerBeat) : 500d);
            if (!enemy.CharacterDefinition.EnemyAttackUsesEffectFrames)
                duration = Math.Max(duration, enemy.CharacterDefinition.AttackWindupBeats);
            var cadence = CadenceFor(enemy);
            // Use the complete authored animation, including recovery frames, not the last damage frame.
            cadence.actionEnd = NormalBattleBeatCount + duration;
            cadence.pressureReady = cadence.actionEnd + PressureRecoveryBeats;
            if (cadence.normalReady >= 0) cadence.normalReady = NormalBattleBeatCount + enemy.AttackIntervalBeats;
        }

        private void EndEnemyCadenceEarly(FightUnitSlot enemy)
        {
            if (!enemyCadences.TryGetValue(enemy, out var cadence)) return;
            double now = NormalBattleBeatCount;
            if (beatClock != null && !EnemyActionsPaused && beatClock.HasTimingAnchor && beatClock.TryGetTimelinePositionMs(out int ms))
                now += Math.Max(0, Math.Min(1, (ms - beatClock.LatestBeat.TimelinePositionMs) / Math.Max(1d, beatClock.MillisecondsPerBeat)));
            cadence.actionEnd = Math.Min(cadence.actionEnd, now);
            cadence.pressureReady = now + PressureRecoveryBeats;
            if (!PressurePhaseActive && cadence.normalReady >= 0) cadence.normalReady = now + enemy.AttackIntervalBeats;
        }

        private void ClearPressureCadence()
        {
            enemyCadences.Clear();
            pressureCadenceActive = false;
        }
    }
}
