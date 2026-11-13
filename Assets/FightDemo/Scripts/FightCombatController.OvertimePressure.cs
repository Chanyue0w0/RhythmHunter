using System;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        private long overtimePressureStartBeat;
        private int lastOvertimeRecoveryBeats;
        public long OvertimeElapsedBeats => StageOvertimeActive ? Math.Max(0, NormalBattleBeatCount - overtimePressureStartBeat) : 0;
        public int OvertimeAccelerationSteps => (int)Math.Min(int.MaxValue,
            OvertimeElapsedBeats / Math.Max(1, rosterManager != null ? rosterManager.OvertimeAccelerationIntervalBeats : 20));
        private int OvertimeRecoveryBeats => (int)Math.Max(
            rosterManager != null ? rosterManager.OvertimeMinimumRecoveryBeats : 0,
            (long)Math.Max(0, wavePressure.recoveryBeats) - (long)OvertimeAccelerationSteps *
            (rosterManager != null ? rosterManager.OvertimeRecoveryReductionBeats : 1));

        private void BeginOvertimePressure()
        {
            overtimePressureStartBeat = NormalBattleBeatCount;
            pressureRemaining = PressureDurationBeats; // Stable full-bar presentation; never consumed in overtime.
            pressureRestUntil = 0;
            pressureNextStart = -1;
            pressureRewardPaid = true;
            pressureDamageProgress = 0;
            pressureBlockProgress = 0;
            PressureBreakRevision = 0;
            PressureRemovedBeats = 0;
            PressureLastBreakSongBeat = long.MinValue;
            lastOvertimeRecoveryBeats = PressureRecoveryBeats;
            // Retain running attacks, but discard a prior rest/ordinary cadence delay.
            pressureCadenceActive = false;
            RefreshPressureCadence();
        }

        private void AdvanceOvertimePressureBeat()
        {
            int recovery = PressureRecoveryBeats;
            int reduction = lastOvertimeRecoveryBeats - recovery;
            if (reduction > 0)
                foreach (var cadence in enemyCadences.Values)
                    cadence.pressureReady = Math.Max(cadence.actionEnd, cadence.pressureReady - reduction);
            lastOvertimeRecoveryBeats = recovery;
            // Scheduling remains once per accepted whole beat in StartAuthoredEnemyTurns.
            // EnemyActionBusy prevents overlapping animations even when recovery reaches zero.
        }
    }
}
