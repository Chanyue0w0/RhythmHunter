using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        [Header("Stage Deadline Runtime")]
        [SerializeField] private bool stageTimeExpired;
        [SerializeField] private bool stageOvertimeActive;
        [SerializeField] private float stageSecondsRemaining;
        private bool stageDeadlineErrorReported;
        private int stageDeadlineMs;
        private double stageMusicSeconds;
        public bool StageDeadlineEnabled => rosterManager != null && rosterManager.UsesStageDeadline;
        public bool StageTimeExpired => stageTimeExpired;
        public bool StageOvertimeActive => stageOvertimeActive;
        public double StageMusicSeconds => stageMusicSeconds;
        public float StageSecondsRemaining => stageSecondsRemaining;
        public float StageTimeLimitSeconds => stageDeadlineMs / 1000f;

        private void ResetStageDeadline()
        {
            stageTimeExpired = stageOvertimeActive = stageDeadlineErrorReported = false;
            stageDeadlineMs = 0; stageMusicSeconds = 0; stageSecondsRemaining = 0;
        }
        private void UpdateStageDeadline()
        {
            if (!StageDeadlineEnabled || beatClock == null || !beatClock.IsPlaying || IsPaused ||
                TimingCalibrationActive || battleEnded || AwaitingBattleStart) return;
            if (stageDeadlineMs <= 0)
            {
                if (!beatClock.TryGetMarkerPosition("Normal_End", out stageDeadlineMs) || stageDeadlineMs <= 0 || !beatClock.OvertimeMusicEnabled)
                {
                    stageDeadlineMs = 0;
                    if (!stageDeadlineErrorReported)
                    {
                        stageDeadlineErrorReported = true;
                        Debug.LogError("[Stage Deadline] Normal_End or native Overtime loop unavailable. Check song markers and rebuild FMOD banks.", this);
                    }
                    return;
                }
            }
            if (beatClock.TryGetTimelinePositionMs(out int position)) EvaluateStageDeadline(position);
        }
        // Shared by Update and beat boundaries, so expiry always wins over spawning the next Wave.
        private void EvaluateStageDeadline(int continuousMusicMs)
        {
            if (!StageDeadlineEnabled || IsPaused || TimingCalibrationActive || battleEnded || AwaitingBattleStart || stageDeadlineMs <= 0) return;
            stageMusicSeconds = System.Math.Max(stageMusicSeconds, continuousMusicMs / 1000d);
            stageSecondsRemaining = Mathf.Max(0, stageDeadlineMs / 1000f - (float)stageMusicSeconds);
            if (continuousMusicMs >= stageDeadlineMs) stageTimeExpired = true;
            if (!stageTimeExpired) return;
            // An already-started Fever may finish; no subsequent Wave can be spawned.
            if (TeamSkillRunning || TeamUltimateRunning) return;
            if (endBattleOnZeroHp && partyHp <= 0) { FinishBattle(false); return; }
            bool treasure = rosterManager.GetWaveKind(CurrentWave - 1) == FightStageDefinition.WaveKind.Treasure;
            if (treasure || AwaitingNextWave) { FinishBattle(true); return; }
            if (FindFrontLivingEnemy() != null) { stageOvertimeActive = true; return; }
            foreach (var enemy in fightScene2Enemies)
                if (enemy != null && enemy.DeathPresentationPending) return;
            CheckWaveCompletion(); // Award the current Wave clear once; retain true cleared-wave statistics.
            FinishBattle(true);
        }
    }
}
