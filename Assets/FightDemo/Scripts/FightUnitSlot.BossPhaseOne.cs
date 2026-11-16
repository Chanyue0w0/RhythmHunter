using System;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightUnitSlot
    {
        private bool bossPhaseOneCleared;
        private bool bossPhaseOneProtectionReleased;
        public bool BossPhaseOneProtected => !bossPhaseOneProtectionReleased && team==UnitTeam.Enemy &&
            characterDefinition != null && characterDefinition.ProtectBossPhaseOne &&
            CombatController != null && CombatController.RosterManager != null && CombatController.RosterManager.IsBossStage;
        public bool BossPhaseOneTired => BossPhaseOneProtected && bossPhaseOneCleared;
        public double BossPhaseOneExtraDamage { get; private set; }
        public event Action<FightUnitSlot> BossPhaseOneCompleted;
        // Future coin rewards subscribe here. This is resolved damage, NOT additional HP loss.
        public event Action<FightUnitSlot,float,DamageKind> BossPhaseOneExtraDamageReceived;
        private void ResetBossPhaseOne()
        {
            bossPhaseOneCleared=false;bossPhaseOneProtectionReleased=false;BossPhaseOneExtraDamage=0;
            combatAnimator?.SetIdleOverride(null);
        }
        private void CompleteBossPhaseOneIfNeeded()
        {
            if(!BossPhaseOneProtected || bossPhaseOneCleared || currentHp>1)return;
            bossPhaseOneCleared=true;
            combatAnimator?.SetIdleOverride(characterDefinition.TiredIdleAnimationName);
            CombatController?.CancelEnemyActionForPhaseOneCompletion(this);
            BossPhaseOneCompleted?.Invoke(this);
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
            currentHp=1;CompleteBossPhaseOneIfNeeded();RefreshHealthVisuals();
            HealthChanged?.Invoke(this,currentHp,maxHp);return true;
        }
        // Called by the future phase-two controller; ordinary healing must never clear success.
        public void ReleaseBossPhaseOneProtection()
        {
            bossPhaseOneProtectionReleased=true;combatAnimator?.SetIdleOverride(null);
        }
    }
}
