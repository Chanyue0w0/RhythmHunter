using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightUnitSlot
    {
        private long aimedUntil = long.MinValue;
        private bool aimedGold;
        private FightUnitSlot aimedOwner;
        public bool HasAimed => CanHaveStatus && currentHp > 0 && StatusBeat <= aimedUntil &&
            aimedOwner != null && aimedOwner.HasCharacter;
        public bool HasGoldAimed => HasAimed && aimedGold;
        public FightUnitSlot AimedOwner => HasAimed ? aimedOwner : null;
        public int AimedBeatsRemaining => HasAimed ? (int)Mathf.Max(0, aimedUntil - StatusBeat) : 0;

        public bool ApplyAimed(FightUnitSlot owner, bool gold)
        {
            if (!CanHaveStatus || currentHp <= 0 || owner == null || owner.CharacterDefinition == null) return false;
            var data = owner.CharacterDefinition;
            // Reapplication refreshes the duration, never downgrades an active gold mark.
            aimedGold = gold || HasGoldAimed;
            aimedOwner = owner;
            aimedUntil = StatusBeat + (aimedGold ? data.AimedGoldBeats : data.AimedGreenBeats);
            CombatController?.PulseAimedIcon(this);
            return true;
        }

        internal void NotifyAimedDirectHit(FightUnitSlot attacker)
        {
            if (!HasAimed || aimedGold || attacker == null) return;
            if (attacker == aimedOwner || aimedOwner.CharacterDefinition.AimedActivation ==
                FightCharacterDefinition.AimedActivationSource.AnyHeroDirectHit)
                ApplyAimed(aimedOwner, true);
        }

        private void ExpireAimed(long beat)
        {
            if (beat <= aimedUntil && aimedOwner != null && aimedOwner.HasCharacter) return;
            ClearAimed();
        }
        private void ClearAimed()
        {
            aimedUntil = long.MinValue; aimedOwner = null; aimedGold = false;
            CombatController?.RefreshStatusIcons(this);
        }
    }
}
