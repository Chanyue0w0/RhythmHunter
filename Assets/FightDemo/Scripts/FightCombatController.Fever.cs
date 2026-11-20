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
        public FightUnitSlot[] FeverEnemies => fightScene2Enemies.ToArray();
        public bool FeverPerformanceRunning { get; private set; }
        private SwordCharge feverSword;
        private int feverSavedSwordStage;
        internal void BeginFeverPerformance()
        {
            FeverPerformanceRunning = true;
            BeginBurstTargets();
            // Hit presentations replace enemy animations, so discard their unfinished attack callbacks.
            foreach (var enemy in fightScene2Enemies) if (enemy) InterruptEnemyAttack(enemy);
        }
        internal void SetFeverWarrior(FightUnitSlot actor)
        {
            RestoreFeverSword();
            if (!actor) return;
            feverSword = SwordState(actor); feverSavedSwordStage = feverSword.stage;
            feverSword.stage = 3; ShowSwordStage(feverSword);
        }
        private void RestoreFeverSword()
        {
            if (feverSword == null) return;
            feverSword.stage = feverSavedSwordStage; ShowSwordStage(feverSword); feverSword = null;
        }
        internal bool ApplyFeverDamage(FightUnitSlot actor, FightUnitSlot target, float amount)
        {
            if (!FeverPerformanceRunning || !FeverBattleSuspended || IsPaused || battleEnded ||
                actor == null || !actor.HasCharacter || !CanHitBurstTarget(target) || !fightScene2Enemies.Contains(target)) return false;
            target.TakeDamage(ModifyBossDamage(target, amount), FightUnitSlot.DamageKind.Secondary, true);
            return true;
        }
        internal void EndFeverPerformance()
        {
            RestoreFeverSword(); FeverPerformanceRunning = false; ClearBurstTargets();
        }
        internal void SetFeverMana(bool full)
        { teamSkillGauge = full ? TeamSkillGaugeMax : 0; teamSkillGaugeFraction = 0; }
        internal void CompleteFeverTutorial()
        {
            if (GetComponent<FightLevelPlanner>()?.ActiveStage?.TeachesFever == true)
                FinishBattle(true);
        }
    }
}
