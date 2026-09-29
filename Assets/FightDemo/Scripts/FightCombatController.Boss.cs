using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        public enum BossPhase { Neutral, Attack, Armor, Charge, Vulnerable, Rage, Break }
        private FightUnitSlot bossActor;
        private long bossPhaseStart, bossPhaseEnd, lastBossBeat = long.MinValue;
        private long lastBossAttackBeat = long.MinValue;
        private int bossBreakProgress;
        private float pendingBossDamage;
        public BossPhase CurrentBossPhase { get; private set; }
        public int BossBreakProgress => bossBreakProgress;
        public int BossBreakThreshold => Mathf.Max(1, BossPattern?.breakThreshold ?? 1);
        private FightBossPattern BossPattern => bossActor != null && bossActor.CharacterDefinition != null
            ? bossActor.CharacterDefinition.BossPattern : null;
        public bool BossActive => UsesEqualBeats && bossActor != null && bossActor.HasCharacter &&
            bossActor.CurrentHp > 0 && BossPattern != null && BossPattern.enabled;
        public int BossBeatsRemaining => BossActive ? (int)System.Math.Max(0, bossPhaseEnd - latestCombatBeat) : 0;
        public string BossHint => CurrentBossPhase switch
        {
            BossPhase.Attack => "GUARD on this beat",
            BossPhase.Armor => "Armor reduces damage — use Break abilities",
            BossPhase.Charge => "Interrupt before the last beat, or GUARD the heavy hit",
            BossPhase.Rage => "Break abilities + successful GUARD stop the finisher",
            BossPhase.Break => "BREAK — enemy cannot attack; bonus damage",
            BossPhase.Vulnerable => "Bonus damage window",
            _ => "Recover HP / build mana; attack follows"
        };
        public event System.Action BossBroken;

        private void ResetBossState()
        {
            bossActor = null; lastBossBeat = long.MinValue;
            lastBossAttackBeat = long.MinValue;
            bossBreakProgress = 0; pendingBossDamage = 0;
            bossPhaseStart = bossPhaseEnd = 0; CurrentBossPhase = BossPhase.Neutral;
        }

        private void AdvanceBossState(long beat)
        {
            if (!UsesEqualBeats) return;
            var target = FindFrontLivingEnemy();
            if (target != bossActor)
            {
                bossActor = target;
                lastBossBeat = long.MinValue;
                if (BossActive) EnterBossPhase(BossPhase.Neutral, beat);
            }
            if (!BossActive || beat <= lastBossBeat) return;
            lastBossBeat = beat;
            if (beat < bossPhaseEnd) return;
            EnterBossPhase(CurrentBossPhase switch
            {
                BossPhase.Neutral => BossPhase.Attack,
                BossPhase.Attack => BossPhase.Armor,
                BossPhase.Armor => BossPhase.Charge,
                BossPhase.Charge => BossPhase.Vulnerable,
                BossPhase.Vulnerable => BossPhase.Rage,
                _ => BossPhase.Neutral
            }, beat);
        }

        private void EnterBossPhase(BossPhase phase, long beat)
        {
            CurrentBossPhase = phase; bossPhaseStart = beat; bossBreakProgress = 0;
            var data = BossPattern;
            int duration = phase switch
            {
                BossPhase.Neutral => data.neutralBeats,
                BossPhase.Attack => 1,
                BossPhase.Armor => data.armorBeats,
                BossPhase.Charge => data.chargeBeats,
                BossPhase.Vulnerable => data.vulnerableBeats,
                BossPhase.Rage => Mathf.Max(1, data.rageWindupBeats) + Mathf.Max(2, data.rageAttackBeats),
                _ => data.breakBeats
            };
            bossPhaseEnd = beat + Mathf.Max(1, duration);
            if (phase == BossPhase.Break)
            {
                CancelBossPendingAttack();
                bossActor.PlayCombatAnimation(FightCharacterCombatAnimator.CombatAnimation.Hit, null, null, null);
                BossBroken?.Invoke();
            }
        }

        private void CancelBossPendingAttack()
        {
            if (bossActor == null || pendingEnemyAttacker != bossActor) return;
            pendingEnemyAttack = false; pendingEnemyAnimationDriven = false;
            pendingEnemyAttacker = null; pendingEnemyActionId = long.MinValue;
            pendingBossDamage = 0;
        }

        private void CancelDefeatedBossAttack(FightUnitSlot target)
        {
            if (target == bossActor && target.CurrentHp <= 0) CancelBossPendingAttack();
        }

        private bool IsBossAttackBeat(long beat)
        {
            if (!BossActive || beat < bossPhaseStart || beat >= bossPhaseEnd) return false;
            if (CurrentBossPhase == BossPhase.Attack) return beat == bossPhaseStart;
            if (CurrentBossPhase == BossPhase.Charge) return beat == bossPhaseEnd - 1;
            if (CurrentBossPhase != BossPhase.Rage) return false;
            long offset = beat - bossPhaseStart - Mathf.Max(1, BossPattern.rageWindupBeats);
            return beat == bossPhaseEnd - 1 || (offset >= 0 && offset % Mathf.Max(1, BossPattern.rageAttackInterval) == 0);
        }

        private int BossBeatsUntilAttack(long beat)
        {
            if (CurrentBossPhase == BossPhase.Neutral) return (int)System.Math.Max(0, bossPhaseEnd - beat);
            for (long next = System.Math.Max(beat, bossPhaseStart); next < bossPhaseEnd; next++)
                if (IsBossAttackBeat(next)) return (int)(next - beat);
            return -1;
        }

        private float BossAttackDamage(long beat)
        {
            if (!BossActive) return 0;
            if (CurrentBossPhase == BossPhase.Charge) return QuantizeCombatValue(BossPattern.chargeDamage);
            if (CurrentBossPhase == BossPhase.Rage && beat == bossPhaseEnd - 1)
                return QuantizeCombatValue(BossPattern.rageFinisherDamage);
            return bossActor.AttackPower;
        }

        private void ApplyBossBreak(int power, long beat)
        {
            if (!BossActive || power <= 0 || beat < bossPhaseStart || beat >= bossPhaseEnd ||
                (CurrentBossPhase != BossPhase.Armor && CurrentBossPhase != BossPhase.Charge && CurrentBossPhase != BossPhase.Rage)) return;
            bossBreakProgress = Mathf.Min(BossBreakThreshold, bossBreakProgress + power);
            if (bossBreakProgress >= BossBreakThreshold) EnterBossPhase(BossPhase.Break, System.Math.Max(latestCombatBeat, beat));
        }

        private float ModifyBossDamage(FightUnitSlot target, float damage)
        {
            if (!BossActive || target != bossActor || damage <= 0) return damage;
            if (CurrentBossPhase == BossPhase.Armor) return QuantizeCombatValue(Mathf.Max(0, damage - BossPattern.armorReduction));
            if (CurrentBossPhase == BossPhase.Break || CurrentBossPhase == BossPhase.Vulnerable)
                return QuantizeCombatValue(damage + Mathf.Max(0, BossPattern.vulnerableBonus));
            return damage;
        }
    }
}
