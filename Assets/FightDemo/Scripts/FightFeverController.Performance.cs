using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightFeverController
    {
        const double HeroBeats = 10, IntroBeats = 2, SlashEnd = 7.5, IdleStart = 8;
        const int SlashLoops = 10;
        const double DelayedHitBeats = .25;
        public FightUnitSlot PerformingHero { get; private set; }
        public double PerformanceElapsedBeats { get; private set; }
        public int PrimarySlashCount { get; private set; }
        public int DelayedSlashCount { get; private set; }
        readonly List<(double beat, int frame, bool delayed, int strike)> slashEvents = new();
        readonly List<(GameObject effect, float until)> performanceEffects = new();
        readonly Dictionary<FightUnitSlot, double> enemyHitStarts = new();
        readonly Dictionary<FightUnitSlot, FightCharacterCombatAnimator.Sequence> enemyHitSequences = new();
        FightCharacterCombatAnimator.Sequence introSequence, slashSequence, idleSequence, sampledSequence;
        FightCharacterCombatAnimator.Sequence authoredSlash;
        FightFeverAfterimagePool afterimages;
        FightUnitSlot[] performanceEnemies = Array.Empty<FightUnitSlot>();
        int performerIndex, nextSlashEvent, sampledLoop = -1;
        double performanceStarted;
        float performanceEffectTime;
        bool echoesReturned;

        private void BeginPerformances()
        {
            fight.BeginFeverPerformance();
            performanceEnemies = fight.FeverEnemies;
            foreach (var enemy in performanceEnemies) AddActorCopies(enemy);
            afterimages = GetComponent<FightFeverAfterimagePool>();
            if (!afterimages) afterimages = gameObject.AddComponent<FightFeverAfterimagePool>();
            performerIndex = -1; PrimarySlashCount = DelayedSlashCount = 0;
            barRoot.gameObject.SetActive(false); gem.gameObject.SetActive(false);
            if (gemEffect) gemEffect.SetActive(false);
            SetPhase(Phase.Performing);
            SelectNextPerformer(BeatPosition());
        }
        private void SelectNextPerformer(double started)
        {
            StopPerformer();
            var heroes = fight.FeverHeroes;
            while (++performerIndex < heroes.Length)
            {
                var hero = heroes[performerIndex];
                var animator = hero ? hero.CombatAnimator : null;
                if (!hero || !hero.HasCharacter || animator == null || hero.CharacterDefinition?.EnableSwordCharge != true) continue;
                var intro = animator.FindSequence("FeverUlt");
                authoredSlash = animator.FindSequence("Multi-Slash");
                if (intro == null || authoredSlash == null || intro.Frames.Count == 0 || authoredSlash.Frames.Count == 0) continue;
                int marked = 0;
                for (int i = 0; i < authoredSlash.Frames.Count; i++) if (authoredSlash.PlaysAttackEffectAt(i)) marked++;
                if (marked != 2) { Debug.LogError("Fever Multi-Slash requires two authored slash frames.", hero); continue; }
                PerformingHero = hero; fight.SetFeverWarrior(hero); echoesReturned = false;
                animator.InterruptPlayback(); animator.SetPlaybackPaused(false);
                introSequence = FitSequence(intro, IntroBeats);
                slashSequence = FitSequence(authoredSlash, (SlashEnd - IntroBeats) / SlashLoops);
                var idle = animator.FindSequence("Idle");
                if (idle == null)
                {
                    idle = new FightCharacterCombatAnimator.Sequence();
                    idle.Configure(FightCharacterCombatAnimator.CombatAnimation.OpeningIdle, animator.Frames, 16, -1, -1, -1);
                }
                idleSequence = FitSequence(idle, HeroBeats - IdleStart);
                slashEvents.Clear(); nextSlashEvent = 0;
                double cycle = (SlashEnd - IntroBeats) / SlashLoops;
                int strike = 0;
                for (int loop = 0; loop < SlashLoops; loop++)
                {
                    double at = IntroBeats + loop * cycle;
                    for (int frame = 0; frame < slashSequence.Frames.Count; frame++)
                    {
                        if (authoredSlash.PlaysAttackEffectAt(frame))
                        { slashEvents.Add((at, frame, false, strike)); slashEvents.Add((at + DelayedHitBeats, frame, true, strike)); strike++; }
                        at += slashSequence.GetFrameBeats(frame);
                    }
                }
                slashEvents.Sort((a, b) => a.beat.CompareTo(b.beat));
                performanceStarted = started; PerformanceElapsedBeats = 0; sampledSequence = null; sampledLoop = -1;
                hint.text = "";
                return;
            }
            BeginReturn();
        }
        private static FightCharacterCombatAnimator.Sequence FitSequence(FightCharacterCombatAnimator.Sequence source, double beats)
        {
            var sequence = JsonUtility.FromJson<FightCharacterCombatAnimator.Sequence>(JsonUtility.ToJson(source));
            double total = 0;
            for (int i = 0; i < source.Frames.Count; i++) total += source.UseFrameBeatDurations ? source.GetFrameBeats(i) : 1;
            var durations = new List<float>();
            for (int i = 0; i < source.Frames.Count; i++) durations.Add((float)(beats * (source.UseFrameBeatDurations ? source.GetFrameBeats(i) : 1) / Math.Max(.001, total)));
            sequence.ConfigureFrameBeats(durations); return sequence;
        }
        private void AdvancePerformance()
        {
            if (!PerformingHero || !PerformingHero.ActorInstance) { SelectNextPerformer(BeatPosition()); return; }
            double now = BeatPosition(); PerformanceElapsedBeats = Math.Max(0, now - performanceStarted);
            while (nextSlashEvent < slashEvents.Count && slashEvents[nextSlashEvent].beat <= PerformanceElapsedBeats + 1e-7)
            {
                var entry = slashEvents[nextSlashEvent++]; ResolveFeverSlash(entry.frame, entry.delayed, entry.beat, entry.strike);
            }
            var animator = PerformingHero.CombatAnimator;
            FightCharacterCombatAnimator.Sequence sequence;
            double local; int loopIndex;
            if (PerformanceElapsedBeats < IntroBeats) { sequence = introSequence; local = PerformanceElapsedBeats; loopIndex = -1; }
            else if (PerformanceElapsedBeats < IdleStart)
            {
                double cycle = (SlashEnd - IntroBeats) / SlashLoops;
                loopIndex = Math.Min(SlashLoops - 1, (int)((PerformanceElapsedBeats - IntroBeats) / cycle));
                local = Math.Min(cycle, PerformanceElapsedBeats - IntroBeats - loopIndex * cycle); sequence = slashSequence;
            }
            else { sequence = idleSequence; local = PerformanceElapsedBeats - IdleStart; loopIndex = SlashLoops; }
            if (sampledSequence != sequence || sampledLoop != loopIndex)
            {
                animator.PlayVisualSequence(animator.TargetRenderer, sequence, false, true, true);
                sampledSequence = sequence; sampledLoop = loopIndex;
            }
            animator.SampleVisualSequenceBeats(local);
            foreach (var pair in enemyHitStarts)
                if (pair.Key && pair.Key.CombatAnimator && enemyHitSequences.ContainsKey(pair.Key))
                    pair.Key.CombatAnimator.SampleVisualSequenceBeats(Math.Max(0, PerformanceElapsedBeats - pair.Value));
            if (PerformanceElapsedBeats >= IdleStart && !echoesReturned)
            {
                echoesReturned = true;
                if (afterimages.ActiveCount > 0)
                {
                    var data = PerformingHero.CharacterDefinition;
                    foreach (var target in performanceEnemies)
                    {
                        if (!target || !target.ActorInstance) continue;
                        Vector3 impact = target.ImpactEffectAnchor ? target.ImpactEffectAnchor.position : target.ActorInstance.transform.position + Vector3.up;
                        SpawnPerformanceEffect(data.SwordChargedOnHitEffect, impact, Quaternion.identity, .45f);
                        SpawnPerformanceEffect(data.SwordImmediateExplosionEffect, impact, Quaternion.identity, .55f);
                    }
                }
                afterimages.ReleaseAll();
            }
            if (PerformanceElapsedBeats >= HeroBeats) SelectNextPerformer(performanceStarted + HeroBeats);
        }
        private void ResolveFeverSlash(int frame, bool delayed, double at, int strike)
        {
            if (delayed) DelayedSlashCount++; else PrimarySlashCount++;
            foreach (var target in performanceEnemies)
            {
                // Combat damage is integer-quantized: alternate primary 2/1 to total 30.
                if (!fight.ApplyFeverDamage(PerformingHero, target, delayed ? 1 : (strike % 2 == 0 ? 2 : 1))) continue;
                var data = PerformingHero.CharacterDefinition;
                Vector3 impact = target.ImpactEffectAnchor ? target.ImpactEffectAnchor.position : target.ActorInstance.transform.position + Vector3.up;
                // Golden-angle spacing keeps consecutive cuts apart without moving the enemy.
                // A delayed hit shares its primary cut's position even if other cuts have occurred.
                float angle = (strike * 137.50776f + target.SlotIndex * 43f) * Mathf.Deg2Rad;
                float radius = .18f + (strike % 3) * .06f;
                impact += battleCamera.transform.right * (Mathf.Cos(angle) * radius)
                    + battleCamera.transform.up * (Mathf.Sin(angle) * radius);
                if (!delayed)
                {
                    var frameEffects = authoredSlash.EffectsAt(frame);
                    if (frameEffects != null) foreach (var effect in frameEffects)
                        if (effect != null && effect.prefab) SpawnPerformanceEffect(effect.prefab, impact + effect.positionOffset, Quaternion.Euler(effect.rotationOffset), effect.limitLifetime ? effect.lifetimeSeconds : 1, true);
                    afterimages.Strike(target);
                }
                SpawnPerformanceEffect(data.SwordChargedOnHitEffect, impact, Quaternion.identity, .45f);
                SpawnPerformanceEffect(data.SwordImmediateExplosionEffect, impact, Quaternion.identity, .55f);
                var animator = target.CombatAnimator;
                var hit = animator ? animator.FindSequence(FightCharacterCombatAnimator.CombatAnimation.Hit) : null;
                if (hit != null && hit.Frames.Count > 0)
                {
                    if (!enemyHitSequences.TryGetValue(target, out var fitted)) { fitted = FitSequence(hit, .3); enemyHitSequences[target] = fitted; }
                    animator.SetPlaybackPaused(false);
                    animator.PlayVisualSequence(animator.TargetRenderer, fitted, false, true, true);
                    enemyHitStarts[target] = at;
                }
            }
            pulse = delayed ? .25f : .65f;
        }
        private void SpawnPerformanceEffect(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime, bool centerSlash = false)
        {
            if (!prefab) return;
            var effect = Instantiate(prefab, stage.transform);
            effect.transform.SetPositionAndRotation(position + stageOffset,
                (centerSlash ? Quaternion.identity : battleCamera.transform.rotation) * rotation * prefab.transform.localRotation);
            if (centerSlash)
            {
                var emitter = effect.GetComponentInChildren<ParticleSystem>(true);
                if (emitter) effect.transform.position += position + stageOffset - emitter.transform.position;
            }
            foreach (var t in effect.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            foreach (var cfx in effect.GetComponentsInChildren<CartoonFX.CFXR_Effect>(true)) cfx.enabled = false;
            foreach (var light in effect.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
            { ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); var main = ps.main; main.loop = false; main.stopAction = ParticleSystemStopAction.None; ps.Play(false); }
            effects.Add(effect); performanceEffects.Add((effect, performanceEffectTime + Mathf.Max(.05f, lifetime)));
        }
        private void AdvancePerformanceEffects(float seconds)
        {
            performanceEffectTime += seconds;
            for (int i = performanceEffects.Count - 1; i >= 0; i--)
                if (performanceEffects[i].until <= performanceEffectTime)
                {
                    var effect = performanceEffects[i].effect; effects.Remove(effect);
                    if (effect) Destroy(effect); performanceEffects.RemoveAt(i);
                }
        }
        private void StopPerformer()
        {
            if (PerformingHero && PerformingHero.CombatAnimator)
            {
                PerformingHero.CombatAnimator.InterruptPlayback();
                PerformingHero.CombatAnimator.SetPlaybackPaused(true);
            }
            PerformingHero = null; fight?.SetFeverWarrior(null); afterimages?.ReleaseAll();
            foreach (var pair in enemyHitStarts)
                if (pair.Key && pair.Key.CombatAnimator)
                { pair.Key.CombatAnimator.InterruptPlayback(); pair.Key.CombatAnimator.SetPlaybackPaused(true); }
            enemyHitStarts.Clear(); enemyHitSequences.Clear();
        }
        private void CleanupPerformance()
        {
            StopPerformer();
            if (fight && fight.FeverPerformanceRunning) fight.EndFeverPerformance();
            performanceEffects.Clear(); performanceEnemies = Array.Empty<FightUnitSlot>();
        }
    }
}
