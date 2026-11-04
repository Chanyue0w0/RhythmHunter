using System;
using UnityEngine;
using QteMode = RhythmHunter.FightDemo.FightCharacterDefinition.TeamQteMode;
using Result = RhythmHunter.FightDemo.FightQteNote.Result;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        [Header("Team Skill QTE Presentation")]
        [SerializeField] private Vector3 teamQteOffset = new(0, .25f, -1);
        [SerializeField, Min(.1f)] private float teamQteInnerRadius = .45f;
        private sealed class TeamQte
        {
            public HeroBeatSettings hero;
            public FightUnitSlot[] targets;
            public FightQteNote[] notes;
            public bool[] handled;
            public QteMode mode;
            public double start, animationBeat;
            public bool animationStarted, impact, resolved;
        }
        private TeamQte teamQte;
        private double qtePresentationBeat;
        private bool suppressQteAdditionalEffects;
        public bool TeamQteActive => teamQte != null && !teamQte.resolved;

        private bool TryGetQteBeat(out double now)
        {
            now = latestCombatBeat;
            if (beatClock == null || !beatClock.HasTimingAnchor || !beatClock.TryGetTimelinePositionMs(out int timeline)) return false;
            var anchor = beatClock.LatestBeat;
            now = anchor.GlobalBeat + (timeline - anchor.TimelinePositionMs) / beatClock.MillisecondsPerBeat;
            return true;
        }

        private double SkillImpactOffset(FightUnitSlot actor)
        {
            var sequence = actor.CombatAnimator != null ? actor.CombatAnimator.FindSequence(FightCharacterCombatAnimator.CombatAnimation.Skill) : null;
            double offset = 0;
            if (sequence != null)
                for (int i = 0; i < sequence.Frames.Count; i++)
                {
                    if (sequence.PlaysAttackEffectAt(i)) return offset;
                    offset += sequence.UseFrameBeatDurations ? sequence.GetFrameBeats(i)
                        : 1d / sequence.FramesPerSecond / (beatClock.MillisecondsPerBeat / 1000d);
                }
            return actor.CharacterDefinition.TeamSkillEffectBeatOffset;
        }

        private bool BeginTeamSkillQte(HeroBeatSettings hero, long beat)
        {
            HideQteRings(); teamQte = null;
            var data = hero.UnitSlot.CharacterDefinition;
            if (data == null || data.QteMode == QteMode.None) return false;
            var targets = GetTeamSkillEffectTargets(hero.UnitSlot);
            int count = data.QteMode == QteMode.Slash ? Math.Min(1, targets.Length) : targets.Length;
            double interval = data.RapidQteIntervalBeats;
            double window = rhythmJudge != null && beatClock != null ? rhythmJudge.PerfectWindowMs / beatClock.MillisecondsPerBeat : .2;
            // Consecutive QTE windows must not overlap. Calibration remains the same as ordinary inputs.
            if (data.QteMode != QteMode.Slash) window = Math.Min(window, interval * .45);
            var qte = new TeamQte { hero = hero, mode = data.QteMode, targets = targets, start = beat,
                notes = new FightQteNote[count], handled = new bool[count], animationBeat = beat };
            for (int i = 0; i < count; i++)
            {
                double offset = data.QteMode == QteMode.Slash ? SkillImpactOffset(hero.UnitSlot) : interval * (i + 1);
                qte.notes[i] = new FightQteNote(beat + offset, data.QteMode == QteMode.Slash ? Math.Max(.5, offset) : interval, window);
            }
            teamQte = qte;
            if (data.QteMode == QteMode.Bomb && count > 0)
            {
                qte.animationBeat = beat + Math.Ceiling(interval * count + window);
                nextTeamSkillBeat = (long)qte.animationBeat + data.TeamSkillPerformanceBeats;
                lastTeamSkillBeat = nextTeamSkillBeat - 1;
                hero.UnitSlot.PlayCombatAnimation(FightCharacterCombatAnimator.CombatAnimation.Aim, null, null, null);
            }
            else StartQteAnimation(qte);
            if (count == 0) CompleteQteSkill(qte);
            return true;
        }

        private void StartQteAnimation(TeamQte qte)
        {
            qte.animationStarted = true;
            pendingTeamSkillEffectBeat = (long)qte.animationBeat + qte.hero.UnitSlot.CharacterDefinition.TeamSkillEffectBeatOffset;
            StartTeamSkillPresentation(qte.hero);
        }

        private void SubmitTeamSkillQte()
        {
            if (!TeamQteActive || rhythmJudge == null || !TryGetQteBeat(out double visualBeat)) return;
            if (inputRouter != null) rhythmJudge.SetInputProfile(inputRouter.CurrentInputProfile);
            if (!rhythmJudge.TryGetEvaluatedBeat(inputRouter != null ? inputRouter.CurrentInputAgeMs : 0, out double evaluated)) return;
            SubmitTeamSkillQteAt(evaluated, visualBeat);
        }

        private void SubmitTeamSkillQteAt(double evaluated, double visualBeat)
        {
            if (!TeamQteActive || IsPaused || TimingCalibrationActive || battleEnded) return;
            // Do not expire prompts before timestamped input has had its chance to resolve.
            for (int i = 0; i < teamQte.notes.Length; i++)
            {
                if (teamQte.notes[i].Expire(evaluated)) { HandleQteNote(teamQte, i, visualBeat); continue; }
                if (teamQte.notes[i].Outcome == Result.Pending && teamQte.notes[i].Press(evaluated, visualBeat))
                { HandleQteNote(teamQte, i, visualBeat); break; }
            }
            UpdateTeamSkillQteAt(visualBeat, evaluated);
        }

        private void HandleQteNote(TeamQte qte, int index, double now)
        {
            if (qte.handled[index] || qte.notes[index].Outcome == Result.Pending) return;
            qte.handled[index] = true;
            bool hit = qte.notes[index].Outcome == Result.Hit;
            TeamSkillStatus = $"{qte.hero.HeroLabel}: QTE {index + 1}/{qte.notes.Length} — {(hit ? "HIT" : "MISS")}";
            if (hit && qte.mode == QteMode.Slime) LaunchQteProjectile(qte.hero.UnitSlot, qte.targets[index], true, now);
        }

        private void UpdateTeamSkillQte()
        {
            if (!TeamSkillRunning || teamQte == null || IsPaused || TimingCalibrationActive || battleEnded || !TryGetQteBeat(out double now)) return;
            double evaluated = now;
            if (rhythmJudge != null) evaluated += rhythmJudge.JudgementOffsetMs / beatClock.MillisecondsPerBeat;
            UpdateTeamSkillQteAt(now, evaluated);
        }

        private void UpdateTeamSkillQteAt(double now, double evaluated)
        {
            if (!TeamSkillRunning || teamQte == null || IsPaused || TimingCalibrationActive || battleEnded) return;
            qtePresentationBeat = now;
            var qte = teamQte;
            bool all = true;
            for (int i = 0; i < qte.notes.Length; i++)
            {
                var note = qte.notes[i];
                // Same safety margin as normal enemy/input ordering.
                note.Expire(evaluated - resolutionSafetyMs / beatClock.MillisecondsPerBeat);
                HandleQteNote(qte, i, now);
                all &= note.Outcome != Result.Pending;
                var ring = GetQteRing(i);
                var target = qte.targets[i];
                bool visible = target != null && target.CurrentHp > 0 && now >= note.ShowBeat && now <= note.DueBeat + note.WindowBeats + .2;
                if (visible) ring.Present(target.ImpactEffectAnchor.position + teamQteOffset,
                    (float)((now - note.ShowBeat) / Math.Max(.01, note.DueBeat - note.ShowBeat)), teamQteInnerRadius, note.Outcome);
                else ring.gameObject.SetActive(false);
            }
            if (!qte.animationStarted && all && now >= qte.animationBeat) StartQteAnimation(qte);
            AdvanceQteProjectiles(now);
            if (teamQte != qte || qte.resolved) return;
            if (all && qte.animationStarted && !qte.impact && qte.mode != QteMode.Slime &&
                teamSkillAnimator != null && !teamSkillAnimator.IsPlaying && now >= nextTeamSkillBeat)
            {
                // An authored sequence without an impact marker must not strand the battle.
                CompleteQteSkill(qte);
                return;
            }
            if (qte.mode == QteMode.Slash && qte.impact && all) ResolveQteSlash(qte);
            else if (all && skillFlights.Count == 0 && (qte.mode == QteMode.Slime || qte.impact)) CompleteQteSkill(qte);
        }

        private void OnQteImpact()
        {
            var qte = teamQte;
            if (qte == null || qte.resolved || qte.impact) return;
            qte.impact = true;
            if (qte.mode == QteMode.Slash)
            {
                if (qte.notes.Length > 0 && qte.notes[0].Outcome == Result.Pending) teamSkillAnimator?.SetPlaybackPaused(true);
                else ResolveQteSlash(qte);
            }
            else if (qte.mode == QteMode.Bomb)
            {
                if (!TryGetQteBeat(out double now)) now = qtePresentationBeat;
                for (int i = 0; i < qte.notes.Length; i++)
                    if (qte.notes[i].Outcome == Result.Hit) LaunchQteProjectile(qte.hero.UnitSlot, qte.targets[i], false, now);
            }
        }

        private void ResolveQteSlash(TeamQte qte)
        {
            var actor = qte.hero.UnitSlot;
            var data = actor.CharacterDefinition;
            int version = rosterVersion;
            bool hit = qte.notes.Length > 0 && qte.notes[0].Outcome == Result.Hit;
            float damage = data.SkillPower * (hit ? 1 : data.QteFailureDamageMultiplier);
            teamSkillAnimator?.SetPlaybackPaused(false);
            resolvingTeamSkillFrame = true;
            suppressQteAdditionalEffects = !hit && !data.QteFailureAppliesStatus;
            try
            {
                // Keep the displayed target fixed: no strike jumps to a different enemy mid-QTE.
                var target = qte.targets.Length > 0 ? qte.targets[0] : null;
                if (target != null && target.CurrentHp > 0 && IsCurrentHeroAction(version, actor, target))
                {
                    PlayAnimatedHeroAction(actor, target, ActionType.Skill, damage);
                    if (HealthSystemEnabled && !suppressQteAdditionalEffects && IsCurrentHeroAction(version, actor, target) && target.CurrentHp > 0) target.ApplyFragile();
                }
            }
            finally { resolvingTeamSkillFrame = false; suppressQteAdditionalEffects = false; }
            CompleteQteSkill(qte);
        }

        private void CompleteQteSkill(TeamQte qte)
        {
            if (teamQte != qte || qte.resolved) return;
            qte.resolved = true; pendingTeamSkillHero = null; pendingTeamSkillEffectBeat = long.MaxValue;
            int version = rosterVersion;
            ApplyAbilityArmorRecovery(qte.hero.UnitSlot.CharacterDefinition, true);
            if (teamQte != qte || !TeamSkillRunning || rosterVersion != version) return;
            activeEnemySlot = FindFrontLivingEnemy();
            TeamSkillStepResolved?.Invoke(qte.hero.UnitSlot);
        }

        private void ResetTeamSkillQte()
        {
            if (teamSkillAnimator != null) teamSkillAnimator.SetPlaybackPaused(false);
            teamQte = null; HideQteRings(); ClearSkillVisuals();
        }
    }
}
