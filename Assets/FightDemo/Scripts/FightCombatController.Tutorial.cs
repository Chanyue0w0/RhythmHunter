using System;
namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        // One event per confirmed primary sword contact. Delayed damage never counts twice.
        public event Action<FightUnitSlot, bool, int> SwordPrimaryHit;
        internal bool TutorialChargeSuppressed { get; private set; }
        internal void SetTutorialChargeSuppressed(bool suppressed)
        {
            if (TutorialChargeSuppressed == suppressed) return;
            TutorialChargeSuppressed = suppressed;
            if (suppressed) ResetSwordCharges();
        }
    }
}
