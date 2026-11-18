using UnityEngine;
using RhythmHunter.RhythmDemo;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        private FightBossRhythmBattle bossRhythm;
        public FightBossRhythmBattle BossRhythm => bossRhythm ? bossRhythm : bossRhythm = GetComponent<FightBossRhythmBattle>();
        public FmodRhythmJudge BossRhythmJudge => rhythmJudge;
        public string BossHitSoundPath => successfulInputSoundEventPath;
        public float BossHitSoundVolume => enableSuccessfulInputSound ? successfulInputSoundVolume : 0;
        public float BossPhaseTwoDurationSeconds => beatClock != null && beatClock.TryGetMarkerPosition("Normal_End", out int start) &&
            beatClock.TryGetMarkerPosition("Boss_End", out int end) ? Mathf.Max(0, (end-start)/1000f) : 0;
        public void ReportBossGuard(FightUnitSlot hero)
        {
            blockedAttackCount++;
            if (showStatusIcons && hero) RentStatusIcon(0, hero);
            // Phase-two sound is requested on successful input, independently of impact settlement.
        }
        public void CompleteBossRhythmBattle()
        {
            if (!BossPhaseTwoActive || battleEnded || DefeatSlowMotionActive) return;
            FinishBattle(partyHp > 0 || !endBattleOnZeroHp);
        }
    }
}
