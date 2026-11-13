using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        [Header("Team Skill (Equal Beat)")]
        [Tooltip("Testing: start a new battle with full shared MP. Off starts at 0. Does not refill on wave changes or after casting.")]
        [SerializeField] private bool startWithFullTeamSkill = false;
        [SerializeField, Min(0)] private float teamSkillGaugeGain = 1f;
        private int teamSkillGauge, teamSkillStep;
        private float teamSkillGaugeFraction;
        public float TeamSkillGaugeExact => teamSkillGauge + teamSkillGaugeFraction;
        private long nextTeamSkillBeat = long.MaxValue;
        private long lastGaugeBeat = long.MinValue;
        private long lastTeamSkillBeat = long.MinValue;
        private readonly HeroBeatSettings[] teamSkillActors = new HeroBeatSettings[3];
        private readonly int[] teamSkillDurations = new int[3];
        private readonly int[] teamSkillEffectOffsets = new int[3];
        private HeroBeatSettings pendingTeamSkillHero;
        private long pendingTeamSkillEffectBeat = long.MaxValue;
        private bool pendingTeamSkillUsesAnimation;
        private bool resolvingTeamSkillFrame;
        private FightCharacterCombatAnimator teamSkillAnimator;
        private int teamSkillPlaybackVersion;
        public FightUnitSlot TeamSkillPerformingHero { get; private set; }
        public int TeamSkillBeatsRemaining => Mathf.CeilToInt(TeamSkillBurstRemaining);
        public int TeamSkillGauge => teamSkillGauge;
        public int TeamSkillGaugeMax => ManaCost(frontHero) + ManaCost(secondHero) + ManaCost(thirdHero);
        private static int ManaCost(HeroBeatSettings hero) => hero?.UnitSlot != null && hero.UnitSlot.HasCharacter
            ? hero.UnitSlot.CharacterDefinition?.TeamSkillManaCost ?? 0 : 0;
        public bool TeamSkillRunning => nextTeamSkillBeat != long.MaxValue;
        public bool TeamSkillReady => !FeverBlockedByPressure && UsesEqualBeats && !battleEnded && !AwaitingNextWave && !AwaitingBattleStart && !TeamSkillRunning && !TeamUltimateRunning && TeamSkillGaugeMax > 0 && teamSkillGauge >= TeamSkillGaugeMax;
        public string TeamSkillStatus { get; private set; } = "Build charge with Basic abilities";
        public event System.Action<FightUnitSlot> TeamSkillStepResolved;

        private void GainTeamSkill(FmodRhythmJudge.Result result)
        {
            if (TeamSkillRunning || TeamUltimateRunning || result.Judgement != FmodRhythmJudge.Grade.Perfect || result.DuplicateBeat ||
                result.NearestBeat.GlobalBeat <= lastTeamUltimateBeat ||
                result.NearestBeat.GlobalBeat <= lastGaugeBeat || result.NearestBeat.GlobalBeat <= lastTeamSkillBeat) return;
            lastGaugeBeat = result.NearestBeat.GlobalBeat;
            float gain = Mathf.Max(0, teamSkillGaugeGain) * (PressurePhaseActive ? pressureManaMultiplier : 1);
            float total = Mathf.Clamp(TeamSkillGaugeExact + gain, 0, TeamSkillGaugeMax);
            teamSkillGauge = Mathf.FloorToInt(total);
            teamSkillGaugeFraction = total - teamSkillGauge;
            TeamSkillStatus = TeamSkillReady ? "READY  A / R" : "Build charge with Basic abilities";
        }

        // A can be pressed at any time once ready; actual skills begin on the next
        // FMOD beat. This never consumes the rhythm judge's Basic input slot.
        public bool TryStartTeamSkill()
        {
            if (suspendFeverActivation || FeverBlockedByPressure) return false;
            if (!TeamSkillReady || WaveEntranceRunning || IsPaused || TimingCalibrationActive || battleEnded || latestCombatBeat < 0) return false;
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
            teamSkillGauge = 0; teamSkillGaugeFraction = 0; teamSkillStep = 0;
            teamSkillPlaybackVersion++;
            teamSkillWindow.Begin(latestCombatBeat + 1, teamSkillBurstBeats);
            burstFrameEvents.Clear();
            nextTeamSkillBeat = (long)teamSkillWindow.End;
            BeginBurstTargets();
            lastTeamSkillBeat = nextTeamSkillBeat - 1;
            BeginEnemyPause();
            TeamSkillStatus = "QUEUED — next beat";
            return true;
        }

        private void AdvanceTeamSkill(long beat)
        {
            if (UsesEqualBeats && TeamSkillRunning) UpdateTeamSkillBurst();
        }

        private void StartTeamSkillPresentation(HeroBeatSettings hero)
        {
            var actor = hero.UnitSlot;
            var animator = actor.CombatAnimator;
            var sequence = animator != null ? animator.FindSequence(FightCharacterCombatAnimator.CombatAnimation.Skill) : null;
            pendingTeamSkillUsesAnimation = sequence != null && sequence.Frames.Count > 0;
            if (!pendingTeamSkillUsesAnimation) return;
            teamSkillAnimator = animator;
            int version = rosterVersion, playback = teamSkillPlaybackVersion, step = teamSkillStep;
            FightUnitSlot[] hitTargets = null;
            bool Valid() => animator.IsPlaying && TeamSkillRunning && playback == teamSkillPlaybackVersion && step == teamSkillStep &&
                IsCurrentHero(version, actor) && !battleEnded && TeamSkillPerformingHero == actor;
            bool hasHitFrame = false;
            for (int i = 0; i < sequence.Frames.Count; i++) hasHitFrame |= sequence.PlaysAttackEffectAt(i);
            if (!hasHitFrame)
                Debug.LogWarning($"[TeamSkill] {actor.DisplayName}: no attack-effect frame selected. This animation will not deal damage.", actor);
            bool started = actor.PlayCombatAnimation(FightCharacterCombatAnimator.CombatAnimation.Skill,
                () => { if (Valid()) PlayHeroAttackWarning(version, actor); },
                () =>
                {
                    if (!Valid()) return;
                    if (pendingTeamSkillHero == hero)
                    {
                        hitTargets = GetTeamSkillEffectTargets(actor);
                        ResolvePendingTeamSkill(latestCombatBeat, true);
                    }
                    else if (hitTargets != null && teamQte == null)
                    {
                        // Later marked frames retain their VFX, without another damage event.
                        var anchors = new System.Collections.Generic.List<Transform>();
                        foreach (var target in hitTargets)
                            if (IsCurrentHeroAction(version, actor, target)) anchors.Add(target.ImpactEffectAnchor);
                        if (anchors.Count > 0) actor.PlaySkillAttackAt(anchors.ToArray());
                    }
                },
                null,
                effect =>
                {
                    if (!Valid()) return;
                    if (effect.location == FightCharacterCombatAnimator.EffectLocation.Self) actor.PlayFrameEffect(effect);
                    else
                    {
                        foreach (var target in hitTargets ?? GetTeamSkillEffectTargets(actor))
                            if (IsCurrentHeroAction(version, actor, target)) actor.PlayFrameEffect(effect, target.ImpactEffectAnchor);
                    }
                });
            if (!started)
            {
                pendingTeamSkillUsesAnimation = false;
                teamSkillAnimator = null;
            }
        }

        private FightUnitSlot[] GetTeamSkillEffectTargets(FightUnitSlot actor)
        {
            var living = fightScene2Enemies.FindAll(CanHitBurstTarget);
            var target = TeamSkillRunning ? FindBurstSingleTarget() : FindFrontLivingEnemy();
            var behavior = actor.SkillBehavior;
            if (behavior == FightCharacterDefinition.AbilityBehavior.SlimeFragments ||
                behavior == FightCharacterDefinition.AbilityBehavior.SmallBombs)
            {
                int limit = actor.CharacterDefinition != null ? actor.CharacterDefinition.StatusSkillTargetLimit : 3;
                return living.GetRange(0, Mathf.Min(limit, living.Count)).ToArray();
            }
            if (behavior == FightCharacterDefinition.AbilityBehavior.EnergySurge)
                target = living.Find(enemy => enemy.IsFragile) ?? target;
            bool all = behavior == FightCharacterDefinition.AbilityBehavior.DamageAll ||
                (behavior == FightCharacterDefinition.AbilityBehavior.EnergySurge && (target == null || !target.IsFragile)) ||
                (behavior == FightCharacterDefinition.AbilityBehavior.FormationBreaker && target != null && target.IsFragile);
            return all ? living.ToArray() : target != null ? new[] { target } : System.Array.Empty<FightUnitSlot>();
        }

        private void ResolvePendingTeamSkill(long beat, bool fromAttackEffect = false)
        {
            if (teamQte != null && teamQte.hero == pendingTeamSkillHero)
            {
                if (fromAttackEffect || (teamQte.animationStarted && !pendingTeamSkillUsesAnimation && beat >= pendingTeamSkillEffectBeat)) OnQteImpact();
                return;
            }
            if (pendingTeamSkillHero == null || (pendingTeamSkillUsesAnimation ? !fromAttackEffect : beat < pendingTeamSkillEffectBeat)) return;
            var hero = pendingTeamSkillHero;
            // Clear before callbacks so repeated beats and reentrant resets cannot double-cast.
            pendingTeamSkillHero = null;
            pendingTeamSkillEffectBeat = long.MaxValue;
            if (hero.UnitSlot == null || !hero.UnitSlot.HasCharacter) return;
            int expectedRosterVersion = rosterVersion;
            string effect;
            resolvingTeamSkillFrame = true;
            try { effect = PerformConfiguredAbility(hero, ActionType.Skill, hero.UnitSlot.SkillBehavior, hero.UnitSlot.SkillPower, beat); }
            finally { resolvingTeamSkillFrame = false; }
            if (!TeamSkillRunning || rosterVersion != expectedRosterVersion) return;
            ApplyAbilityArmorRecovery(hero.UnitSlot.CharacterDefinition, true);
            if (!TeamSkillRunning || rosterVersion != expectedRosterVersion) return;
            TeamSkillStatus = hero.HeroLabel + ": " + effect;
            activeEnemySlot = FindFrontLivingEnemy();
            while (teamSkillStep < teamSkillActors.Length &&
                (teamSkillActors[teamSkillStep]?.UnitSlot == null || !teamSkillActors[teamSkillStep].UnitSlot.HasCharacter)) teamSkillStep++;
            TeamSkillStepResolved?.Invoke(hero.UnitSlot);
        }

        private void ResetTeamSkill()
        {
            StopBattleAudio();
            CancelBurstAnimations();
            ResetTeamSkillQte();
            teamSkillPlaybackVersion++;
            if (teamSkillAnimator != null) teamSkillAnimator.InterruptPlayback();
            teamSkillAnimator = null;
            pendingTeamSkillUsesAnimation = false;
            ClearEnemyScheduledActions();
            foreach (var enemy in fightScene2Enemies) if (enemy != null) { enemy.ClearFragile(); enemy.ClearTimedStatuses(); }
            ResetBasicAbilityProgress();
            ResetEnemyPause();
            ResetBossState();
            ResetTeamUltimate();
            teamSkillGauge = 0; teamSkillGaugeFraction = 0; teamSkillStep = 0; lastGaugeBeat = long.MinValue;
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
