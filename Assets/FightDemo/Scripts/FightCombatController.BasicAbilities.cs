using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        private FightUnitSlot aimedShotActor;
        private int aimedShotProgress;
        private long lastAimedShotBeat = long.MinValue;

        public int GetAimedShotProgress(FightUnitSlot actor) => actor != null && actor == aimedShotActor ? aimedShotProgress : 0;
        public bool GuardingCurrentBeat => UsesEqualBeats && guardedBeats.Contains(latestCombatBeat);

        /// <summary>Also used by battle/wave teardown; never modifies shared MP.</summary>
        public void ResetBasicAbilityProgress()
        {
            aimedShotActor = null;
            aimedShotProgress = 0;
            lastAimedShotBeat = long.MinValue;
        }

        private void TrackAimedShotJudgement(FightUnitSlot actor, FmodRhythmJudge.Result result)
        {
            if (!UsesEqualBeats || TeamSkillRunning || aimedShotProgress == 0) return;
            // A duplicate is not another beat. Ignore it, rather than advancing or
            // destroying already accepted preparation on that same musical beat.
            if (result.DuplicateBeat) return;
            if (result.Judgement == FmodRhythmJudge.Grade.Miss && actor == aimedShotActor)
                ResetBasicAbilityProgress();
            else if (result.Judgement == FmodRhythmJudge.Grade.Perfect && actor != aimedShotActor &&
                     result.NearestBeat.GlobalBeat > lastAimedShotBeat)
                ResetBasicAbilityProgress(); // The existing shared judge gave this beat to another hero.
        }

        private string PerformAimedShot(FightUnitSlot actor, float power, long beat)
        {
            if (FindFrontLivingEnemy() == null)
            {
                ResetBasicAbilityProgress();
                return "Aimed Shot found no target";
            }
            if (actor == aimedShotActor && beat <= lastAimedShotBeat)
                return "Aimed Shot already accepted this beat";
            if (actor != aimedShotActor || beat != lastAimedShotBeat + 1)
                ResetBasicAbilityProgress();
            aimedShotActor = actor;
            lastAimedShotBeat = beat;
            aimedShotProgress++;
            int required = actor.CharacterDefinition != null ? actor.CharacterDefinition.ConsecutiveInputBeats : 3;
            if (aimedShotProgress < required)
            {
                actor.PlayAttackFrameWarning(false);
                return $"Aimed Shot {aimedShotProgress}/{required} — {(aimedShotProgress == 1 ? "prepare" : "aim")}; input again next beat";
            }

            ResetBasicAbilityProgress();
            // Reacquire on the third input; never retain a target from preparation.
            var target = FindFrontLivingEnemy();
            if (target == null) return "Aimed Shot found no target";
            bool interrupted = actor.CharacterDefinition != null && actor.CharacterDefinition.BasicInterruptsEnemy &&
                TryInterruptEnemyAttack(target);
            if (interrupted) power += actor.CharacterDefinition.SuccessfulInterruptDamageBonus;
            if (actor.CharacterDefinition != null)
                ApplyBossBreak(actor.CharacterDefinition.BasicBreakPower, beat);
            PlayAnimatedHeroAction(actor, target, ActionType.LightAttack, power);
            return $"Aimed Shot fires for {power:0.#} base damage" + (interrupted ? " — INTERRUPTED" : "");
        }

        private void UpdateBasicAbilityProgress()
        {
            if (aimedShotProgress == 0) return;
            if (battleEnded || aimedShotActor == null || !aimedShotActor.HasCharacter || FindFrontLivingEnemy() == null)
            {
                ResetBasicAbilityProgress();
                return;
            }
            if (TeamSkillRunning || EnemyActionsPaused || TimingCalibrationActive || IsPaused) return;
            if (beatClock != null && beatClock.TryGetTimelinePositionMs(out int timelineMs))
                ExpireAimedShotWindow(timelineMs);
        }

        private void ExpireAimedShotWindow(double timelineMs)
        {
            if (aimedShotProgress == 0 || TeamSkillRunning || EnemyActionsPaused || TimingCalibrationActive || IsPaused ||
                rhythmJudge == null || beatClock == null || !beatClock.HasTimingAnchor) return;
            // Derive the expected beat from the one FMOD anchor, including calibration.
            // Do not clear at OnBeat: a valid late input is still possible after it.
            var anchor = beatClock.LatestBeat;
            double expectedMs = anchor.TimelinePositionMs +
                (lastAimedShotBeat + 1 - anchor.GlobalBeat) * beatClock.MillisecondsPerBeat;
            double deadline = expectedMs + rhythmJudge.PerfectWindowMs - rhythmJudge.JudgementOffsetMs + resolutionSafetyMs;
            if (timelineMs > deadline) ResetBasicAbilityProgress();
        }
    }
}
