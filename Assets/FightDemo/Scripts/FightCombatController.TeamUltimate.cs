using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        [Header("Team Ultimate (Equal Beat)")]
        [SerializeField, Range(1, 8)] private int teamUltimateRequiredCharges = 3;
        [SerializeField, Min(1)] private int teamUltimatePerformanceBeats = 4;
        [Tooltip("One-based impact beat within the performance; clamped to its length.")]
        [SerializeField, Min(1)] private int teamUltimateImpactBeat = 4;
        [SerializeField, Min(.5f)] private float teamUltimateDamage = 3;

        private int teamUltimateCharge;
        private long ultimateStartBeat = long.MaxValue, ultimateEndBeat = long.MaxValue, ultimateImpactBeat;
        private long lastTeamUltimateBeat = long.MinValue;
        private int ultimateDuration, ultimateImpactOffset;
        private float ultimateDamage;
        private bool ultimateStarted, ultimateImpacted;
        private readonly FightUnitSlot[] ultimateActors = new FightUnitSlot[3];
        public int TeamUltimateCharge => teamUltimateCharge;
        public int TeamUltimateRequiredCharges => Mathf.Clamp(teamUltimateRequiredCharges, 1, 8);
        public bool TeamUltimateRunning => ultimateStartBeat != long.MaxValue;
        public bool TeamUltimatePerforming => TeamUltimateRunning && ultimateStarted;
        public bool TeamUltimateReady => UsesEqualBeats && !TeamSkillRunning && !TeamUltimateRunning &&
            teamUltimateCharge >= TeamUltimateRequiredCharges && TeamSkillGaugeMax > 0;
        public int TeamUltimateBeatsRemaining => !TeamUltimateRunning ? 0 : (int)System.Math.Max(0,
            (ultimateStarted ? ultimateEndBeat : ultimateStartBeat) - latestCombatBeat);
        public int TeamUltimateActivationCount { get; private set; }
        public int TeamUltimateImpactCount { get; private set; }
        public string TeamUltimateStatus { get; private set; } = "Complete Team Skills to charge";

        private void GainTeamUltimateCharge()
        {
            if (!UsesEqualBeats) return;
            teamUltimateCharge = Mathf.Min(TeamUltimateRequiredCharges, teamUltimateCharge + 1);
            TeamUltimateStatus = teamUltimateCharge >= TeamUltimateRequiredCharges ? "READY  RB + A / SHIFT + R" : "Complete Team Skills to charge";
        }

        public bool TryStartTeamUltimate()
        {
            if (!TeamUltimateReady || IsPaused || TimingCalibrationActive || battleEnded || latestCombatBeat < 0) return false;
            ultimateActors[0] = frontHero.UnitSlot; ultimateActors[1] = secondHero.UnitSlot; ultimateActors[2] = thirdHero.UnitSlot;
            ultimateDuration = Mathf.Max(1, teamUltimatePerformanceBeats);
            ultimateImpactOffset = Mathf.Clamp(teamUltimateImpactBeat, 1, ultimateDuration) - 1;
            ultimateDamage = QuantizeCombatValue(Mathf.Max(.5f, teamUltimateDamage));
            teamUltimateCharge = 0;
            ultimateStarted = ultimateImpacted = false;
            ultimateStartBeat = latestCombatBeat + 1;
            TeamUltimateStatus = "QUEUED — next beat";
            return true;
        }

        private void AdvanceTeamUltimate(long beat)
        {
            if (!UsesEqualBeats || !TeamUltimateRunning || beat < ultimateStartBeat) return;
            if (!ultimateStarted)
            {
                ultimateStarted = true;
                ultimateStartBeat = beat;
                ultimateEndBeat = beat + ultimateDuration;
                ultimateImpactBeat = beat + ultimateImpactOffset;
                lastTeamUltimateBeat = ultimateEndBeat - 1;
                TeamUltimateActivationCount++;
                TeamUltimateStatus = "TEAM ULTIMATE — charging";
                foreach (var actor in ultimateActors)
                {
                    if (actor == null || !actor.HasCharacter) continue;
                    actor.PlayAttackFrameWarning(false);
                    actor.PlayCombatAnimation(FightCharacterCombatAnimator.CombatAnimation.Skill, null, null, null);
                }
            }
            if (!ultimateImpacted && beat >= ultimateImpactBeat)
            {
                ultimateImpacted = true;
                // One team damage packet, never one packet per participating hero.
                foreach (var actor in ultimateActors)
                {
                    if (actor == null || !actor.HasCharacter) continue;
                    ApplyAreaDamage(rosterVersion, actor, ultimateDamage);
                    break;
                }
                foreach (var actor in ultimateActors)
                {
                    if (actor == null || !actor.HasCharacter) continue;
                    bool animated = actor.PlayCombatAnimation(FightCharacterCombatAnimator.CombatAnimation.HeavyAttack,
                        null, actor.PlayHeavyAttack, null);
                    if (!animated) actor.PlayHeavyAttack();
                }
                TeamUltimateImpactCount++;
                TeamUltimateStatus = $"FINISHER — {ultimateDamage:0.#} damage to all enemies";
            }
            if (beat >= ultimateEndBeat)
            {
                ultimateStartBeat = ultimateEndBeat = long.MaxValue;
                ultimateStarted = false;
                System.Array.Clear(ultimateActors, 0, ultimateActors.Length);
                TeamUltimateStatus = "Complete Team Skills to charge";
            }
        }

        private void ResetTeamUltimate()
        {
            teamUltimateCharge = 0;
            ultimateStartBeat = ultimateEndBeat = long.MaxValue;
            lastTeamUltimateBeat = long.MinValue;
            ultimateStarted = ultimateImpacted = false;
            TeamUltimateActivationCount = TeamUltimateImpactCount = 0;
            System.Array.Clear(ultimateActors, 0, ultimateActors.Length);
            TeamUltimateStatus = "Complete Team Skills to charge";
        }
    }
}
