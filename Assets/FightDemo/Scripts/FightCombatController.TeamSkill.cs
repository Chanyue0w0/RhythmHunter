using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        [Header("Team Skill (Equal Beat)")]
        [SerializeField, Min(1)] private int teamSkillGaugeGain = 1;
        private int teamSkillGauge, teamSkillStep;
        private long nextTeamSkillBeat = long.MaxValue;
        private long lastGaugeBeat = long.MinValue;
        private long lastTeamSkillBeat = long.MinValue;
        private readonly HeroBeatSettings[] teamSkillActors = new HeroBeatSettings[3];
        private readonly int[] teamSkillDurations = new int[3];
        private readonly int[] teamSkillEffectOffsets = new int[3];
        private HeroBeatSettings pendingTeamSkillHero;
        private long pendingTeamSkillEffectBeat = long.MaxValue;
        public FightUnitSlot TeamSkillPerformingHero { get; private set; }
        public int TeamSkillBeatsRemaining => TeamSkillRunning ? (int)System.Math.Max(0, nextTeamSkillBeat - latestCombatBeat) : 0;
        public int TeamSkillGauge => teamSkillGauge;
        public int TeamSkillGaugeMax => ManaCost(frontHero) + ManaCost(secondHero) + ManaCost(thirdHero);
        private static int ManaCost(HeroBeatSettings hero) => hero?.UnitSlot != null && hero.UnitSlot.HasCharacter
            ? hero.UnitSlot.CharacterDefinition?.TeamSkillManaCost ?? 0 : 0;
        public bool TeamSkillRunning => nextTeamSkillBeat != long.MaxValue;
        public bool TeamSkillReady => UsesEqualBeats && !AwaitingBattleStart && !TeamSkillRunning && !TeamUltimateRunning && TeamSkillGaugeMax > 0 && teamSkillGauge >= TeamSkillGaugeMax;
        public string TeamSkillStatus { get; private set; } = "Build charge with Basic abilities";
        public event System.Action<FightUnitSlot> TeamSkillStepResolved;

        private void GainTeamSkill(FmodRhythmJudge.Result result)
        {
            if (TeamSkillRunning || TeamUltimateRunning || result.Judgement != FmodRhythmJudge.Grade.Perfect || result.DuplicateBeat ||
                result.NearestBeat.GlobalBeat <= lastTeamUltimateBeat ||
                result.NearestBeat.GlobalBeat <= lastGaugeBeat || result.NearestBeat.GlobalBeat <= lastTeamSkillBeat) return;
            lastGaugeBeat = result.NearestBeat.GlobalBeat;
            teamSkillGauge = Mathf.Min(TeamSkillGaugeMax, teamSkillGauge + Mathf.Max(1, teamSkillGaugeGain));
            TeamSkillStatus = TeamSkillReady ? "READY  A / R" : "Build charge with Basic abilities";
        }

        // A can be pressed at any time once ready; actual skills begin on the next
        // FMOD beat. This never consumes the rhythm judge's Basic input slot.
        public bool TryStartTeamSkill()
        {
            if (!TeamSkillReady || IsPaused || TimingCalibrationActive || battleEnded || latestCombatBeat < 0) return false;
            if (frontHero.UnitSlot?.SkillBehavior == FightCharacterDefinition.AbilityBehavior.Unavailable ||
                secondHero.UnitSlot?.SkillBehavior == FightCharacterDefinition.AbilityBehavior.Unavailable ||
                thirdHero.UnitSlot?.SkillBehavior == FightCharacterDefinition.AbilityBehavior.Unavailable)
            {
                TeamSkillStatus = "Team Skills pending checkpoint 3";
                return false;
            }
            teamSkillActors[0] = frontHero; teamSkillActors[1] = secondHero; teamSkillActors[2] = thirdHero;
            bool any = false;
            foreach (var hero in teamSkillActors) any |= hero?.UnitSlot != null && hero.UnitSlot.HasCharacter;
            if (!any) return false;
            UpdateBasicAbilityProgress();
            for (int i = 0; i < teamSkillActors.Length; i++)
            {
                teamSkillDurations[i] = teamSkillActors[i]?.UnitSlot?.CharacterDefinition?.TeamSkillPerformanceBeats ?? 1;
                teamSkillEffectOffsets[i] = teamSkillActors[i]?.UnitSlot?.CharacterDefinition?.TeamSkillEffectBeatOffset ?? 0;
            }
            teamSkillGauge = 0; teamSkillStep = 0;
            nextTeamSkillBeat = latestCombatBeat + 1;
            BeginEnemyPause();
            TeamSkillStatus = "QUEUED — next beat";
            return true;
        }

        private void AdvanceTeamSkill(long beat)
        {
            if (!UsesEqualBeats || !TeamSkillRunning) return;
            ResolvePendingTeamSkill(beat);
            if (!TeamSkillRunning || beat < nextTeamSkillBeat) return;
            while (teamSkillStep < teamSkillActors.Length &&
                (teamSkillActors[teamSkillStep]?.UnitSlot == null || !teamSkillActors[teamSkillStep].UnitSlot.HasCharacter)) teamSkillStep++;
            if (teamSkillStep >= teamSkillActors.Length)
            {
                nextTeamSkillBeat = long.MaxValue;
                EndEnemyPause(true);
                TeamSkillPerformingHero = null;
                TeamSkillStatus = "CHAIN COMPLETE — build mana with Basic abilities";
                GainTeamUltimateCharge();
                return;
            }
            int duration = teamSkillDurations[teamSkillStep];
            int effectOffset = teamSkillEffectOffsets[teamSkillStep];
            var hero = teamSkillActors[teamSkillStep++];
            // The interval is [start, start + duration). Basic on the completion
            // beat may charge again, but every performance beat stays reserved.
            lastTeamSkillBeat = beat + duration - 1;
            TeamSkillPerformingHero = hero.UnitSlot;
            hero.RecordSkillActivation();
            nextTeamSkillBeat = beat + duration;
            pendingTeamSkillHero = hero;
            pendingTeamSkillEffectBeat = beat + effectOffset;
            TeamSkillStatus = hero.HeroLabel + ": PREPARING";
            ResolvePendingTeamSkill(beat);
        }

        private void ResolvePendingTeamSkill(long beat)
        {
            if (pendingTeamSkillHero == null || beat < pendingTeamSkillEffectBeat) return;
            var hero = pendingTeamSkillHero;
            // Clear before callbacks so repeated beats and reentrant resets cannot double-cast.
            pendingTeamSkillHero = null;
            pendingTeamSkillEffectBeat = long.MaxValue;
            if (hero.UnitSlot == null || !hero.UnitSlot.HasCharacter) return;
            int expectedRosterVersion = rosterVersion;
            string effect = PerformConfiguredAbility(hero, ActionType.Skill, hero.UnitSlot.SkillBehavior, hero.UnitSlot.SkillPower, beat);
            if (!TeamSkillRunning || rosterVersion != expectedRosterVersion) return;
            if (hero.UnitSlot.CharacterDefinition != null && hero.UnitSlot.CharacterDefinition.RestoreArmorAfterTeamSkill)
                RestorePartyArmor();
            if (!TeamSkillRunning || rosterVersion != expectedRosterVersion) return;
            TeamSkillStatus = hero.HeroLabel + ": " + effect;
            activeEnemySlot = FindFrontLivingEnemy();
            while (teamSkillStep < teamSkillActors.Length &&
                (teamSkillActors[teamSkillStep]?.UnitSlot == null || !teamSkillActors[teamSkillStep].UnitSlot.HasCharacter)) teamSkillStep++;
            TeamSkillStepResolved?.Invoke(hero.UnitSlot);
        }

        private void ResetTeamSkill()
        {
            foreach (var enemy in fightScene2Enemies) if (enemy != null) enemy.ClearFragile();
            ResetBasicAbilityProgress();
            ResetEnemyPause();
            ResetBossState();
            ResetTeamUltimate();
            teamSkillGauge = 0; teamSkillStep = 0; lastGaugeBeat = long.MinValue;
            lastTeamSkillBeat = long.MinValue;
            nextTeamSkillBeat = long.MaxValue;
            pendingTeamSkillHero = null;
            pendingTeamSkillEffectBeat = long.MaxValue;
            TeamSkillPerformingHero = null;
            System.Array.Clear(teamSkillActors, 0, teamSkillActors.Length);
            System.Array.Clear(teamSkillDurations, 0, teamSkillDurations.Length);
            System.Array.Clear(teamSkillEffectOffsets, 0, teamSkillEffectOffsets.Length);
            TeamSkillStatus = "Build charge with Basic abilities";
        }
    }
}
