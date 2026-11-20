using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        public bool FeverBattleSuspended { get; private set; }
        private readonly List<(FightCharacterCombatAnimator animator, bool paused)> feverAnimators = new();
        internal void SetFeverSuspended(bool value)
        {
            if (FeverBattleSuspended == value) return;
            FeverBattleSuspended = value;
            if (value)
            {
                BeginEnemyPause();
                foreach (var slot in FeverHeroes)
                {
                    var animator = slot != null ? slot.CombatAnimator : null;
                    if (animator == null) continue;
                    feverAnimators.Add((animator, animator.PlaybackPaused)); animator.SetPlaybackPaused(true);
                }
            }
            else
            {
                foreach (var item in feverAnimators) if (item.animator) item.animator.SetPlaybackPaused(item.paused);
                feverAnimators.Clear();
                EndEnemyPause(true);
            }
        }
        public FightUnitSlot[] FeverHeroes => new[] { frontHero?.UnitSlot, secondHero?.UnitSlot, thirdHero?.UnitSlot };
        internal void SetFeverMana(bool full)
        { teamSkillGauge = full ? TeamSkillGaugeMax : 0; teamSkillGaugeFraction = 0; }
    }
}
