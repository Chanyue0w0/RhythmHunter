using System;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightUnitSlot
    {
        private bool bossPhaseOneCleared;
        private bool finishCurrentBossPressure;
        private bool bossPhaseOneProtectionReleased;
        public bool BossPhaseOneProtected => !bossPhaseOneProtectionReleased && team==UnitTeam.Enemy &&
            characterDefinition != null && characterDefinition.ProtectBossPhaseOne &&
            CombatController != null && CombatController.RosterManager != null && CombatController.RosterManager.IsBossStage;
        public bool BossPhaseOneTired => BossPhaseOneProtected && bossPhaseOneCleared;
        public bool BossFinishingCurrentPressure => BossPhaseOneTired && finishCurrentBossPressure &&
            CombatController != null && !CombatController.StageOvertimeActive && CombatController.PressurePhaseActive;
        public bool BossPhaseOneActionsStopped => BossPhaseOneTired && !BossFinishingCurrentPressure;
        public double BossPhaseOneExtraDamage { get; private set; }
        public event Action<FightUnitSlot> BossPhaseOneCompleted;
        // Future coin rewards subscribe here. This is resolved damage, NOT additional HP loss.
        public event Action<FightUnitSlot,float,DamageKind> BossPhaseOneExtraDamageReceived;
        private void ResetBossPhaseOne()
        {
            bossPhaseOneCleared=false;bossPhaseOneProtectionReleased=false;BossPhaseOneExtraDamage=0;
            finishCurrentBossPressure=false;
            combatAnimator?.SetIdleOverride(null);
        }
        private void CompleteBossPhaseOneIfNeeded()
        {
            if(!BossPhaseOneProtected || bossPhaseOneCleared || currentHp>FightCombatUnits.BossHpFloor)return;
            bossPhaseOneCleared=true;
            finishCurrentBossPressure = CombatController != null && CombatController.PressurePhaseActive && !CombatController.StageOvertimeActive;
            if (!finishCurrentBossPressure) CombatController?.CancelEnemyActionForPhaseOneCompletion(this);
            RefreshBossRestPresentation();
            BossPhaseOneCompleted?.Invoke(this);
        }
        internal void RefreshBossRestPresentation()
        {
            if (characterDefinition == null || !characterDefinition.ProtectBossPhaseOne ||
                team != UnitTeam.Enemy || CombatController == null || CombatController.BossPhaseTwoActive) return;
            if (finishCurrentBossPressure && !BossFinishingCurrentPressure)
            {
                finishCurrentBossPressure = false;
                CombatController.CancelEnemyActionForPhaseOneCompletion(this);
            }
            bool tired = BossPhaseOneActionsStopped || CombatController.PressureRestActive;
            combatAnimator?.SetIdleOverride(tired ? characterDefinition.TiredIdleAnimationName : null);
        }
        private void RecordBossExtraDamage(float resolvedDamage,DamageKind kind)
        {
            if(resolvedDamage<=0)return;
            BossPhaseOneExtraDamage+=resolvedDamage;
            BossPhaseOneExtraDamageReceived?.Invoke(this,resolvedDamage,kind);
        }
        public bool DebugCompleteBossPhaseOne()
        {
            if(!BossPhaseOneProtected || IsTransitionInvulnerable)return false;
            currentHp=FightCombatUnits.BossHpFloor;CompleteBossPhaseOneIfNeeded();RefreshHealthVisuals();
            HealthChanged?.Invoke(this,currentHp,maxHp);return true;
        }
        // Called by the future phase-two controller; ordinary healing must never clear success.
        public void ReleaseBossPhaseOneProtection()
        {
            bossPhaseOneProtectionReleased=true;combatAnimator?.SetIdleOverride(null);
        }
        // Phase two is survival: its health display follows song progress, without damage/reward events.
        public void SetBossSurvivalHealth(float progress)
        {
            if (!BossPhaseOneProtected || CombatController == null || !CombatController.BossPhaseTwoActive) return;
            float value = FightCombatUnits.Positive(Mathf.Lerp(maxHp, FightCombatUnits.BossHpFloor, Mathf.Clamp01(progress)));
            if (Mathf.Abs(currentHp - value) < .001f) return;
            currentHp = value; RefreshHealthVisuals(); HealthChanged?.Invoke(this, currentHp, maxHp);
        }
    }
}
