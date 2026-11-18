using System;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        public enum BossMusicPhase { PhaseOne, Overtime, PhaseTwo, PhaseTwoMusicEnded }
        [Header("Boss Music Stage (runtime)")]
        [SerializeField] private BossMusicPhase bossMusicPhase;
        [SerializeField] private float bossPhaseTwoSecondsRemaining;
        private bool bossMusicErrorReported;
        public bool IsBossMusicStage => rosterManager != null && rosterManager.IsBossStage;
        public BossMusicPhase CurrentBossMusicPhase => bossMusicPhase;
        public bool BossPhaseTwoActive => IsBossMusicStage && bossMusicPhase >= BossMusicPhase.PhaseTwo;
        public float BossPhaseTwoSecondsRemaining => bossPhaseTwoSecondsRemaining;
        public event Action BossPhaseTwoStarted;
        public event Action BossPhaseTwoMusicEnded;

        private void ResetBossMusicStage()
        {
            bossMusicPhase = BossMusicPhase.PhaseOne;
            bossPhaseTwoSecondsRemaining = 0;
            bossMusicErrorReported = false;
        }

        private bool BossPhaseOneSucceeded()
        {
            bool found = false;
            foreach (var enemy in rosterManager.ActiveEnemies)
            {
                if (enemy == null || !enemy.BossPhaseOneProtected) continue;
                found = true;
                if (!enemy.BossPhaseOneTired) return false;
            }
            return found;
        }

        private void UpdateBossMusicStage()
        {
            if (!IsBossMusicStage || beatClock == null || !beatClock.IsPlaying || IsPaused ||
                TimingCalibrationActive || battleEnded || AwaitingBattleStart) return;
            if (!beatClock.TryGetMarkerPosition("Normal_End", out int normalEnd) ||
                !beatClock.TryGetMarkerPosition("Boss_End", out int bossEnd) || bossEnd <= normalEnd)
            {
                if (!bossMusicErrorReported)
                { bossMusicErrorReported = true; Debug.LogError("[Boss Music] Requires Normal_End and a later Boss_End marker.", this); }
                return;
            }
            if (!beatClock.TryGetRawTimelinePositionMs(out int raw)) return;
            if (BossPhaseTwoActive)
            {
                bossPhaseTwoSecondsRemaining = beatClock.BossSectionJumpInProgress
                    ? (bossEnd - normalEnd) / 1000f : Mathf.Max(0, (bossEnd - raw) / 1000f);
                if (!beatClock.BossSectionJumpInProgress && raw >= bossEnd && bossMusicPhase == BossMusicPhase.PhaseTwo)
                {
                    bossMusicPhase = BossMusicPhase.PhaseTwoMusicEnded;
                    BossPhaseTwoMusicEnded?.Invoke();
                    beatClock.FadeOutAndStop();
                }
                return;
            }
            var debug = GetComponent<FightBossStageDebug>();
            bool skip = debug != null && debug.PhaseTwoSkipRequested;
            bool succeeded = BossPhaseOneSucceeded();
            // Finish the already active finite Pressure before transitioning, even if the native loop wraps.
            // Overtime is intentionally excluded: a successful overtime clear still jumps immediately.
            if (!skip && succeeded && !stageOvertimeActive)
                foreach (var enemy in rosterManager.ActiveEnemies)
                    if (enemy != null && enemy.BossFinishingCurrentPressure) return;
            bool endpointPassed = raw >= normalEnd || beatClock.CompletedMusicLoops > 0 ||
                beatClock.TryGetTimelinePositionMs(out int continuous) && continuous >= normalEnd;
            if (skip || succeeded && (endpointPassed || stageOvertimeActive))
            {
                bool needsJump = skip || stageOvertimeActive || raw < normalEnd;
                if (needsJump ? !beatClock.JumpToBossSection() : !beatClock.DisableOvertimeMusic()) return;
                if (skip) debug.ConsumePhaseTwoSkipRequest();
                EnterBossPhaseTwoMusic((bossEnd - normalEnd) / 1000f);
                return;
            }
            if (succeeded)
            {
                // Disarm before the native endpoint so an early win continues without a seek or fade.
                if (beatClock.OvertimeMusicEnabled) beatClock.DisableOvertimeMusic();
            }
            else if (endpointPassed && !stageOvertimeActive && !TeamSkillRunning && !TeamUltimateRunning)
            {
                stageOvertimeActive = true;
                bossMusicPhase = BossMusicPhase.Overtime;
                BeginOvertimePressure();
            }
        }

        private void EnterBossPhaseTwoMusic(float duration)
        {
            bossMusicPhase = BossMusicPhase.PhaseTwo;
            bossPhaseTwoSecondsRemaining = duration;
            stageOvertimeActive = false;
            pressureRemaining = 0;
            pressureRestUntil = 0;
            pressureNextStart = -1;
            pressureRewardPaid = true;
            rosterVersion++; // Invalidate first-phase attack callbacks before the timeline seek.
            int mana = teamSkillGauge;
            float fraction = teamSkillGaugeFraction;
            ResetTeamSkill();
            teamSkillGauge = mana;
            teamSkillGaugeFraction = fraction;
            ClearNormalImpacts();
            CancelEnemyCombatWork();
            foreach (var hero in rosterManager.ActiveHeroes) hero?.CombatAnimator?.InterruptPlayback();
            BossPhaseTwoStarted?.Invoke();
            // Keep the 1HP safety floor until the later phase-two/outro implementation owns Boss defeat.
        }
    }
}
