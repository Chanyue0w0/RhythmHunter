using System;
namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
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
