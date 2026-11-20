using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightFeverController
    {
        const double SkeletonFireStart = 3, SkeletonFireEnd = 7, SkeletonLandingStart = 7.5;
        const double BombFlightBeats = .65, SkeletonBulletBeats = .4;
        sealed class AirborneEnemy
        {
            public FightUnitSlot enemy;
            public Transform actor;
            public Vector3 ground, localGround, impact, lift;
            public bool bombHit;
        }
        sealed class SkeletonFlight
        {
            public GameObject visual;
            public AirborneEnemy target;
            public Vector3 start;
            public double launched;
            public bool bomb;
        }
        readonly List<AirborneEnemy> airborneEnemies = new();
        readonly List<SkeletonFlight> skeletonFlights = new();
        bool skeletonPerforming, skeletonBombsThrown;
        double skeletonThrowBeat, skeletonShotOffset;
        FightCharacterCombatAnimator.Sequence skeletonNormal;
        int skeletonAnimationLoop = -1;
        public int SkeletonBombsThrown { get; private set; }
        public int SkeletonVolleysFired { get; private set; }
        public int SkeletonBulletHits { get; private set; }

        private bool TryBeginSkeleton(FightUnitSlot hero, double started)
        {
            var data = hero.CharacterDefinition;
            var animator = hero.CombatAnimator;
            var skill = animator.FindSequence(FightCharacterCombatAnimator.CombatAnimation.Skill);
            var normal = animator.FindSequence(FightCharacterCombatAnimator.CombatAnimation.NormalAttack);
            if (!data.QteProjectilePrefab || !data.NormalAbilityEffectPrefab || skill == null || normal == null || skill.Frames.Count == 0 || normal.Frames.Count == 0) return false;
            introSequence = FitSequence(skill, 2);
            skeletonNormal = FitSequence(normal, .2);
            skeletonThrowBeat = FirstEffectBeat(introSequence);
            skeletonShotOffset = FirstEffectBeat(skeletonNormal);
            if (skeletonThrowBeat < 0 || skeletonShotOffset < 0)
            { Debug.LogError("Skeleton Fever needs attack-effect frames in Skill and NormalAttack.", hero); return false; }
            float height = float.NegativeInfinity;
            foreach (var enemy in performanceEnemies)
            {
                if (!enemy || !enemy.ActorInstance || (!enemy.HasCharacter) || airborneEnemies.Count == 3) continue;
                Vector3 impact = SkeletonImpact(enemy);
                height = Mathf.Max(height, Vector3.Dot(impact, battleCamera.transform.up));
                airborneEnemies.Add(new AirborneEnemy { enemy = enemy, actor = enemy.ActorInstance.transform,
                    ground = enemy.ActorInstance.transform.position, localGround = enemy.ActorInstance.transform.localPosition, impact = impact });
            }
            if (airborneEnemies.Count == 0) return false;
            foreach (var enemy in airborneEnemies)
                enemy.lift = battleCamera.transform.up * (height + 1.4f - Vector3.Dot(enemy.impact, battleCamera.transform.up));
            var idle = animator.FindSequence("Idle");
            if (idle == null)
            {
                idle = new FightCharacterCombatAnimator.Sequence();
                idle.Configure(FightCharacterCombatAnimator.CombatAnimation.OpeningIdle, animator.Frames, 16, -1, -1, -1);
            }
            idleSequence = FitSequence(idle, 2);
            PerformingHero = hero; skeletonPerforming = true; skeletonBombsThrown = false;
            SkeletonBombsThrown = SkeletonVolleysFired = SkeletonBulletHits = 0; skeletonAnimationLoop = -1;
            animator.InterruptPlayback(); animator.SetPlaybackPaused(false);
            animator.PlayVisualSequence(animator.TargetRenderer, introSequence, false, true, true); sampledSequence = introSequence;
            performanceStarted = started; PerformanceElapsedBeats = 0; hint.text = "";
            return true;
        }
        private static double FirstEffectBeat(FightCharacterCombatAnimator.Sequence sequence)
        {
            double at = 0;
            for (int i = 0; i < sequence.Frames.Count; i++)
            { if (sequence.PlaysAttackEffectAt(i)) return at; at += sequence.GetFrameBeats(i); }
            return -1;
        }
        private static Vector3 SkeletonImpact(FightUnitSlot target) => target.ImpactEffectAnchor ? target.ImpactEffectAnchor.position : target.ActorInstance.transform.position + Vector3.up;
        private Vector3 SkeletonGroundPosition(FightUnitSlot target)
        {
            var enemy = airborneEnemies.Find(e => e.enemy == target);
            return enemy != null ? enemy.ground : target.ActorInstance.transform.position;
        }
        private void AdvanceSkeletonPerformance()
        {
            var animator = PerformingHero.CombatAnimator;
            FightCharacterCombatAnimator.Sequence sequence;
            double local;
            int loop = -1;
            if (PerformanceElapsedBeats < 2) { sequence = introSequence; local = PerformanceElapsedBeats; }
            else if (PerformanceElapsedBeats >= SkeletonFireStart && PerformanceElapsedBeats < SkeletonFireEnd)
            {
                loop = Math.Min(19, (int)((PerformanceElapsedBeats - SkeletonFireStart) / .2));
                sequence = skeletonNormal; local = PerformanceElapsedBeats - SkeletonFireStart - loop * .2;
            }
            else { sequence = idleSequence; local = PerformanceElapsedBeats < SkeletonFireStart ? PerformanceElapsedBeats - 2 : PerformanceElapsedBeats - SkeletonFireEnd; }
            if (sampledSequence != sequence || skeletonAnimationLoop != loop)
            {
                animator.PlayVisualSequence(animator.TargetRenderer, sequence, sequence == idleSequence, true, true);
                sampledSequence = sequence; skeletonAnimationLoop = loop;
            }
            animator.SampleVisualSequenceBeats(local);
            if (!skeletonBombsThrown && PerformanceElapsedBeats >= skeletonThrowBeat)
            {
                skeletonBombsThrown = true;
                foreach (var target in airborneEnemies)
                { LaunchSkeletonProjectile(target, skeletonThrowBeat, true); SkeletonBombsThrown++; }
            }
            while (SkeletonVolleysFired < 20 && PerformanceElapsedBeats + 1e-7 >= SkeletonFireStart + SkeletonVolleysFired * .2 + skeletonShotOffset)
            {
                double launched = SkeletonFireStart + SkeletonVolleysFired * .2 + skeletonShotOffset;
                foreach (var target in airborneEnemies) LaunchSkeletonProjectile(target, launched, false);
                SkeletonVolleysFired++; pulse = .3f;
            }
            foreach (var enemy in airborneEnemies)
            {
                if (!enemy.actor || !enemy.bombHit) continue;
                float lift = Mathf.SmoothStep(0, 1, (float)((PerformanceElapsedBeats - skeletonThrowBeat - BombFlightBeats) / .35));
                float landing = 1 - Mathf.SmoothStep(0, 1, (float)((PerformanceElapsedBeats - SkeletonLandingStart) / .5));
                enemy.actor.position = enemy.ground + enemy.lift * (lift * landing);
            }
            for (int i = skeletonFlights.Count - 1; i >= 0; i--)
            {
                var flight = skeletonFlights[i];
                if (!flight.target.actor || !flight.target.enemy) { RemoveMageEffect(flight.visual); skeletonFlights.RemoveAt(i); continue; }
                float t = Mathf.Clamp01((float)((PerformanceElapsedBeats - flight.launched) / (flight.bomb ? BombFlightBeats : SkeletonBulletBeats)));
                Vector3 end = flight.bomb ? flight.target.impact : SkeletonImpact(flight.target.enemy);
                Vector3 position = Vector3.Lerp(flight.start, end, t);
                if (flight.bomb) position += battleCamera.transform.up * (4 * 1.6f * t * (1 - t));
                if (flight.visual)
                {
                    flight.visual.transform.position = position + stageOffset;
                    if (flight.bomb) flight.visual.transform.rotation = battleCamera.transform.rotation * Quaternion.Euler(0, 0, -360 * t);
                }
                if (t < 1) continue;
                var data = PerformingHero.CharacterDefinition;
                if (flight.bomb)
                {
                    flight.target.bombHit = true;
                    SpawnPerformanceEffect(data.QteImpactEffectPrefab, end, Quaternion.identity, .65f);
                    PlaySkeletonHit(flight.target.enemy, flight.launched + BombFlightBeats);
                }
                else if (fight.ApplyFeverDamage(PerformingHero, flight.target.enemy, 1))
                {
                    SkeletonBulletHits++;
                    var settings = data.NormalAbilityEffectPrefab.GetComponent<FightEffectSettings>();
                    SpawnPerformanceEffect(settings ? settings.onHitPrefab : data.QteImpactEffectPrefab, end, Quaternion.identity, .35f);
                    PlaySkeletonHit(flight.target.enemy, flight.launched + SkeletonBulletBeats);
                }
                RemoveMageEffect(flight.visual); skeletonFlights.RemoveAt(i);
            }
            foreach (var pair in enemyHitStarts)
                if (pair.Key && pair.Key.CombatAnimator) pair.Key.CombatAnimator.SampleVisualSequenceBeats(Math.Max(0, PerformanceElapsedBeats - pair.Value));
            if (PerformanceElapsedBeats >= HeroBeats) SelectNextPerformer(performanceStarted + HeroBeats);
        }
        private void LaunchSkeletonProjectile(AirborneEnemy target, double launched, bool bomb)
        {
            if (!target.actor) return;
            var data = PerformingHero.CharacterDefinition;
            Vector3 start = PerformingHero.CastEffectAnchor ? PerformingHero.CastEffectAnchor.position : PerformingHero.ActorInstance.transform.position + Vector3.up;
            var visual = SpawnPerformanceEffect(bomb ? data.QteProjectilePrefab : data.NormalAbilityEffectPrefab, start, Quaternion.identity, 60);
            // Presentation owns movement and lifetime; prefab auto-facing must not fight the arc.
            foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            if (!bomb)
            {
                visual.transform.localScale *= .65f;
                SetMageEffectLoop(visual, true);
                foreach (var particle in visual.GetComponentsInChildren<ParticleSystem>(true)) particle.Emit(1);
            }
            skeletonFlights.Add(new SkeletonFlight { visual = visual, target = target, start = start, launched = launched, bomb = bomb });
        }
        private void PlaySkeletonHit(FightUnitSlot target, double at)
        {
            var animator = target.CombatAnimator;
            var hit = animator ? animator.FindSequence(FightCharacterCombatAnimator.CombatAnimation.Hit) : null;
            if (hit == null || hit.Frames.Count == 0) return;
            if (!enemyHitSequences.TryGetValue(target, out var fitted)) { fitted = FitSequence(hit, .2); enemyHitSequences[target] = fitted; }
            animator.SetPlaybackPaused(false); animator.PlayVisualSequence(animator.TargetRenderer, fitted, false, true, true);
            enemyHitStarts[target] = at;
        }
        private void StopSkeletonPerformance()
        {
            skeletonPerforming = false;
            foreach (var enemy in airborneEnemies) if (enemy.actor) enemy.actor.localPosition = enemy.localGround;
            airborneEnemies.Clear();
            foreach (var flight in skeletonFlights) RemoveMageEffect(flight.visual);
            skeletonFlights.Clear();
        }
    }
}
