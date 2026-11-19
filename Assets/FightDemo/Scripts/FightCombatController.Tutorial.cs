using System;
namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        private bool tutorialControlsEnemies, tutorialEnemiesEnabled;
        private int tutorialAttackInterval = 4;
        private long tutorialAttackStart;
        public bool TutorialPreventsDeath { get; private set; }
        public bool TutorialEnemyDeathProtected(FightUnitSlot target) => target != null && target.Team == FightUnitSlot.UnitTeam.Enemy &&
            GetComponent<FightLevelPlanner>()?.ActiveStage?.TutorialLesson?.preventEnemyDeath == true;
        public event Action<FightUnitSlot, FightUnitSlot, int> AimedFollowUpHit;
        internal bool TutorialControlsEnemies => tutorialControlsEnemies;
        internal void ConfigureTutorialEnemies(bool control, bool enabled, int interval, bool preventDeath)
        {
            bool changed = control != tutorialControlsEnemies || enabled != tutorialEnemiesEnabled;
            tutorialControlsEnemies = control; tutorialEnemiesEnabled = enabled;
            tutorialAttackInterval = UnityEngine.Mathf.Max(1, interval); TutorialPreventsDeath = preventDeath;
            if (!changed) return;
            tutorialAttackStart = NormalBattleBeatCount;
            if (control && !enabled)
            {
                enemyHits.Clear(); pendingEnemyAttack = pendingEnemyAnimationDriven = false;
                foreach (var enemy in fightScene2Enemies) enemy?.CombatAnimator?.InterruptPlayback();
                ClearMageAbility();
            }
        }
        private bool TutorialEnemyReady(FightUnitSlot enemy) => tutorialEnemiesEnabled &&
            NormalBattleBeatCount > tutorialAttackStart &&
            (NormalBattleBeatCount - tutorialAttackStart) % tutorialAttackInterval == 0;
        // One event per confirmed primary sword contact. Delayed damage never counts twice.
        public event Action<FightUnitSlot, bool, int> SwordPrimaryHit;
        public bool FeverBlockedByTutorial
        {
            get
            {
                var stage = GetComponent<FightLevelPlanner>()?.ActiveStage;
                return stage != null ? stage.Tutorial && !stage.TeachesFever : rosterManager != null && rosterManager.IsTutorialStage;
            }
        }
        internal bool TutorialChargeSuppressed { get; private set; }
        internal void SetTutorialChargeSuppressed(bool suppressed)
        {
            if (TutorialChargeSuppressed == suppressed) return;
            TutorialChargeSuppressed = suppressed;
            if (suppressed) ResetSwordCharges();
        }
    }
}
