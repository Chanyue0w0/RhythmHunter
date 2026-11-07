using System;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        [Header("Team Skill Rhythm Burst")]
        [SerializeField, Min(1)] private int teamSkillBurstBeats = 16;
        public event Action TeamSkillBeatAccepted;
        public event Action<double> TeamSkillPointAccepted;
        private readonly FightTeamSkillBeatWindow teamSkillWindow = new();
        private readonly System.Collections.Generic.List<(double beat, Action action)> burstFrameEvents = new();
        private double burstPresentationBeat;
        private readonly System.Collections.Generic.HashSet<FightUnitSlot> burstTargets = new();
        private FightUnitSlot lastBurstDefeatedEnemy;
        public bool IsBurstRetainedTarget(FightUnitSlot target) => TeamSkillRunning && target != null &&
            target.HasCharacter && burstTargets.Contains(target) && fightScene2Enemies.Contains(target);
        private bool CanHitBurstTarget(FightUnitSlot target) => target != null && target.HasCharacter &&
            (target.CurrentHp > 0 || IsBurstRetainedTarget(target));
        private FightUnitSlot FindBurstSingleTarget() => FindFrontLivingEnemy() ??
            (IsBurstRetainedTarget(lastBurstDefeatedEnemy) ? lastBurstDefeatedEnemy : null);
        public void RecordBurstDefeat(FightUnitSlot target)
        {
            if (IsBurstRetainedTarget(target)) lastBurstDefeatedEnemy = target;
        }
        private void BeginBurstTargets()
        {
            burstTargets.Clear(); lastBurstDefeatedEnemy = null;
            foreach (var enemy in fightScene2Enemies)
                if (enemy != null && enemy.HasCharacter && enemy.CurrentHp > 0) burstTargets.Add(enemy);
        }
        private void ClearBurstTargets()
        {
            foreach (var enemy in burstTargets)
                if (enemy != null && enemy.CurrentHp <= 0) { enemy.ClearFragile(); enemy.ClearTimedStatuses(); }
            burstTargets.Clear(); lastBurstDefeatedEnemy = null;
        }
        public bool TeamSkillBurstActive => TeamSkillRunning && TryGetQteBeat(out double now) && teamSkillWindow.IsOpen(now);
        public float TeamSkillBurstRemaining => TeamSkillRunning && TryGetQteBeat(out double now)
            ? (float)Math.Max(0, Math.Min(teamSkillBurstBeats, teamSkillWindow.End - now)) : 0;
        public float TeamSkillBurstProgress => TeamSkillBurstRemaining / Mathf.Max(1, teamSkillBurstBeats);

        private void SubmitTeamSkillBurst()
        {
            if (!TeamSkillRunning || rhythmJudge == null || !TryGetQteBeat(out double now) ||
                !rhythmJudge.TryGetEvaluatedBeat(inputRouter != null ? inputRouter.CurrentInputAgeMs : 0, out double evaluated)) return;
            SubmitTeamSkillBurstAt(evaluated, now);
        }

        private void SubmitTeamSkillBurstAt(double evaluated, double now)
        {
            if (!TeamSkillRunning || IsPaused || TimingCalibrationActive || battleEnded || AwaitingNextWave) return;
            if (FindBurstSingleTarget() == null) return;
            if (!teamSkillWindow.TryAccept(evaluated, now, rhythmJudge.PerfectWindowMs / beatClock.MillisecondsPerBeat)) return;
            int playback = teamSkillPlaybackVersion;
            AdvanceBurstFrames(now);
            if (!TeamSkillRunning || playback != teamSkillPlaybackVersion || battleEnded) return;
            TeamSkillBeatAccepted?.Invoke();
            double acceptedBeat = teamSkillWindow.Start + Math.Round((evaluated - teamSkillWindow.Start) * 2,
                MidpointRounding.AwayFromZero) * .5;
            TeamSkillPointAccepted?.Invoke(acceptedBeat);
            foreach (var hero in teamSkillActors)
                if (hero?.UnitSlot != null && hero.UnitSlot.HasCharacter) CastBurstSkill(hero, now);
            AdvanceBurstFrames(now);
            TeamSkillStatus = "ALL HEROES — A ON EVERY HALF BEAT";
        }
        private void CastBurstSkill(HeroBeatSettings hero, double startBeat)
        {
            var actor = hero.UnitSlot;
            int roster = rosterVersion, playback = teamSkillPlaybackVersion;
            bool resolved = false;
            FightUnitSlot[] targets = null;
            bool Valid() => TeamSkillRunning && playback == teamSkillPlaybackVersion && IsCurrentHero(roster, actor) && !battleEnded;
            void Impact()
            {
                if (resolved || !Valid()) return;
                resolved = true;
                targets = GetTeamSkillEffectTargets(actor);
                var behavior = actor.SkillBehavior;
                if (behavior == FightCharacterDefinition.AbilityBehavior.SlimeFragments || behavior == FightCharacterDefinition.AbilityBehavior.SmallBombs)
                {
                    double now = burstPresentationBeat;
                    foreach (var target in targets)
                        LaunchQteProjectile(actor, target, behavior == FightCharacterDefinition.AbilityBehavior.SlimeFragments, now);
                }
                else
                {
                    resolvingTeamSkillFrame = true;
                    try { PerformConfiguredAbility(hero, ActionType.Skill, behavior, actor.SkillPower, latestCombatBeat); }
                    finally { resolvingTeamSkillFrame = false; }
                }
                if (!Valid()) return;
                ApplyAbilityArmorRecovery(actor.CharacterDefinition, true);
                TeamSkillStepResolved?.Invoke(actor);
            }
            TeamSkillPerformingHero = actor;
            hero.RecordSkillActivation();
            var animator = actor.CombatAnimator;
            var sequence = animator != null ? animator.FindSequence(FightCharacterCombatAnimator.CombatAnimation.Skill) : null;
            if (sequence == null || sequence.Frames.Count == 0) { Impact(); return; }
            // Animation can restart on the next half beat, but each accepted cast
            // retains its own authored frame events on the existing musical clock.
            double offset = 0;
            bool impactScheduled = false;
            for (int frame = 0; frame < sequence.Frames.Count; frame++)
            {
                double due = startBeat + offset;
                if (frame == sequence.WarningFrame)
                    ScheduleBurstFrame(due, () => { if (Valid()) PlayHeroAttackWarning(roster, actor); });
                if (!impactScheduled && sequence.PlaysAttackEffectAt(frame))
                { ScheduleBurstFrame(due, Impact); impactScheduled = true; }
                var effects = sequence.EffectsAt(frame);
                if (effects != null) foreach (var entry in effects)
                {
                    var effect = entry;
                    if (effect == null) continue;
                    ScheduleBurstFrame(due, () =>
                    {
                        if (!Valid()) return;
                        if (effect.location == FightCharacterCombatAnimator.EffectLocation.Self) actor.PlayFrameEffect(effect);
                        else foreach (var target in targets ?? GetTeamSkillEffectTargets(actor))
                            if (IsCurrentHeroAction(roster, actor, target)) actor.PlayFrameEffect(effect, target.ImpactEffectAnchor);
                    });
                }
                offset += sequence.UseFrameBeatDurations ? sequence.GetFrameBeats(frame)
                    : 1000d / sequence.FramesPerSecond / beatClock.MillisecondsPerBeat;
            }
            if (!impactScheduled) Debug.LogWarning($"[TeamSkill] {actor.DisplayName}: no attack-effect frame selected.", actor);
            animator.InterruptPlayback();
            // The scheduled cast owns custom effects, too; suppress animator fallback spawning.
            actor.PlayCombatAnimation(FightCharacterCombatAnimator.CombatAnimation.Skill, null, null, null, _ => { });
        }

        private void ScheduleBurstFrame(double beat, Action action)
        {
            int index = burstFrameEvents.FindIndex(item => item.beat > beat);
            if (index < 0) burstFrameEvents.Add((beat, action));
            else burstFrameEvents.Insert(index, (beat, action));
        }

        private void AdvanceBurstFrames(double now)
        {
            while (burstFrameEvents.Count > 0 && burstFrameEvents[0].beat <= now)
            {
                var item = burstFrameEvents[0];
                burstFrameEvents.RemoveAt(0);
                burstPresentationBeat = item.beat;
                AdvanceQteProjectiles(item.beat);
                item.action();
            }
            burstPresentationBeat = now;
        }
        private void UpdateTeamSkillBurst()
        {
            if (!TeamSkillRunning || !TryGetQteBeat(out double now)) return;
            UpdateTeamSkillBurstAt(now);
        }

        private void UpdateTeamSkillBurstAt(double now)
        {
            if (!TeamSkillRunning || IsPaused || TimingCalibrationActive || battleEnded) return;
            int playback = teamSkillPlaybackVersion;
            AdvanceBurstFrames(now);
            AdvanceQteProjectiles(now);
            if (!TeamSkillRunning || playback != teamSkillPlaybackVersion || battleEnded) return;
            if (now < teamSkillWindow.End) return;
            TeamSkillStatus = "FINISHING LAST ATTACK";
            foreach (var hero in teamSkillActors)
            {
                var animator = hero?.UnitSlot?.CombatAnimator;
                if (animator != null && animator.IsPlaying && animator.ActiveAnimation == FightCharacterCombatAnimator.CombatAnimation.Skill) return;
            }
            if (skillFlights.Count > 0 || burstFrameEvents.Count > 0) return;
            nextTeamSkillBeat = long.MaxValue;
            ClearBurstTargets();
            TeamSkillPerformingHero = null;
            EndEnemyPause(true);
            TeamSkillStatus = "COMPLETE — build mana with Basic abilities";
        }

        private void CancelBurstAnimations()
        {
            foreach (var hero in teamSkillActors)
            {
                var animator = hero?.UnitSlot?.CombatAnimator;
                if (animator != null && animator.IsPlaying && animator.ActiveAnimation == FightCharacterCombatAnimator.CombatAnimation.Skill)
                    animator.InterruptPlayback();
            }
            burstFrameEvents.Clear();
            ClearBurstTargets();
        }
    }
}
