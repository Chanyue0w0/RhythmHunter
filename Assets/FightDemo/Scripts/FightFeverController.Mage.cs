using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightFeverController
    {
        sealed class MageShot
        {
            public GameObject visual;
            public double launched;
            public Vector3 start, direction;
            public float distance;
            public readonly HashSet<FightUnitSlot> hit = new();
        }
        readonly List<MageShot> mageShots = new();
        readonly HashSet<FightUnitSlot> mageSlimeTargets = new();
        readonly List<(Transform source, GameObject copy)> mageFloors = new();
        bool magePerforming, mageAuraCreated;
        double mageAuraBeat, mageSkillBeats;
        Vector3 mageOrigin;
        GameObject mageAura;
        public int MageShotsFired { get; private set; }
        public int MageHitCount { get; private set; }
        public double MageFireStartBeat => mageAuraBeat + 3;

        private bool TryBeginMage(FightUnitSlot hero, double started)
        {
            var data = hero.CharacterDefinition;
            var animator = hero.CombatAnimator;
            var skill = animator.FindSequence(FightCharacterCombatAnimator.CombatAnimation.Skill);
            if (!data.FeverMagicAura || !data.FeverEnergyStrike || !data.FeverBlueExplosion || skill == null || skill.Frames.Count == 0) return false;
            int effectFrame = -1;
            for (int i = 0; i < skill.Frames.Count; i++)
                if (skill.PlaysAttackEffectAt(i)) { effectFrame = i; break; }
            if (effectFrame < 0) { Debug.LogError("Mage Fever Skill needs an attack-effect frame.", hero); return false; }
            PerformingHero = hero; magePerforming = true; mageAuraCreated = false;
            MageShotsFired = MageHitCount = 0; mageSkillBeats = 2;
            mageSlimeTargets.Clear();
            introSequence = FitSequence(skill, mageSkillBeats);
            mageAuraBeat = 0;
            for (int i = 0; i < effectFrame; i++) mageAuraBeat += introSequence.GetFrameBeats(i);
            var idle = animator.FindSequence("Idle");
            if (idle == null)
            {
                idle = new FightCharacterCombatAnimator.Sequence();
                idle.Configure(FightCharacterCombatAnimator.CombatAnimation.OpeningIdle, animator.Frames, 16, -1, -1, -1);
            }
            idleSequence = FitSequence(idle, HeroBeats - mageSkillBeats);
            animator.InterruptPlayback(); animator.SetPlaybackPaused(false);
            animator.PlayVisualSequence(animator.TargetRenderer, introSequence, false, true, true);
            sampledSequence = introSequence;
            performanceStarted = started; PerformanceElapsedBeats = 0; hint.text = "";
            return true;
        }

        private void AdvanceMagePerformance()
        {
            var animator = PerformingHero.CombatAnimator;
            var data = PerformingHero.CharacterDefinition;
            if (PerformanceElapsedBeats >= mageSkillBeats && sampledSequence != idleSequence)
            {
                animator.PlayVisualSequence(animator.TargetRenderer, idleSequence, true, true, true);
                sampledSequence = idleSequence;
            }
            animator.SampleVisualSequenceBeats(sampledSequence == introSequence ? PerformanceElapsedBeats : PerformanceElapsedBeats - mageSkillBeats);
            if (!mageAuraCreated && PerformanceElapsedBeats >= mageAuraBeat)
            {
                mageAuraCreated = true;
                mageOrigin = (PerformingHero.CastEffectAnchor ? PerformingHero.CastEffectAnchor.position : PerformingHero.ActorInstance.transform.position + Vector3.up)
                    - battleCamera.transform.right * .9f;
                mageAura = SpawnPerformanceEffect(data.FeverMagicAura, mageOrigin, Quaternion.identity, 60);
                SetMageEffectLoop(mageAura, true);
            }
            while (MageShotsFired < 20 && PerformanceElapsedBeats + 1e-7 >= MageFireStartBeat + MageShotsFired * .2)
            {
                double launched = MageFireStartBeat + MageShotsFired * .2;
                // Alternate above/below the aura centre with varied heights for each volley.
                float height = (.12f + ((MageShotsFired * 7) % 10) * .035f) * (MageShotsFired % 2 == 0 ? 1 : -1);
                Vector3 shotOrigin = mageOrigin + battleCamera.transform.up * height;
                var direction = -battleCamera.transform.right;
                float distance = 4;
                foreach (var enemy in performanceEnemies)
                    if (enemy && enemy.ActorInstance) distance = Mathf.Max(distance, Vector3.Dot(enemy.ActorInstance.transform.position - mageOrigin, direction) + 2);
                // The authored thruster emits its trail toward local +X, behind a leftward shot.
                var visual = SpawnPerformanceEffect(data.FeverEnergyStrike, shotOrigin, Quaternion.identity, 60);
                if (visual) visual.transform.localScale *= .45f;
                SetMageEffectLoop(visual, true);
                mageShots.Add(new MageShot { visual = visual, launched = launched, start = shotOrigin, direction = direction, distance = distance });
                MageShotsFired++; pulse = .3f;
            }
            for (int i = mageShots.Count - 1; i >= 0; i--)
            {
                var shot = mageShots[i];
                float travel = Mathf.Clamp01((float)(PerformanceElapsedBeats - shot.launched));
                if (shot.visual) shot.visual.transform.position = shot.start + shot.direction * (shot.distance * travel) + stageOffset;
                foreach (var target in performanceEnemies)
                {
                    if (!target || !target.ActorInstance || shot.hit.Contains(target)) continue;
                    Vector3 impact = target.ImpactEffectAnchor ? target.ImpactEffectAnchor.position : target.ActorInstance.transform.position + Vector3.up;
                    if (Vector3.Dot(impact - shot.start, shot.direction) > shot.distance * travel) continue;
                    shot.hit.Add(target);
                    if (!fight.ApplyFeverDamage(PerformingHero, target, 1)) continue;
                    MageHitCount++;
                    SpawnPerformanceEffect(data.FeverBlueExplosion, impact, Quaternion.identity, .45f);
                    var hitAnimator = target.CombatAnimator;
                    var hit = hitAnimator ? hitAnimator.FindSequence(FightCharacterCombatAnimator.CombatAnimation.Hit) : null;
                    if (hit != null && hit.Frames.Count > 0)
                    {
                        if (!enemyHitSequences.TryGetValue(target, out var fitted)) { fitted = FitSequence(hit, .2); enemyHitSequences[target] = fitted; }
                        hitAnimator.SetPlaybackPaused(false);
                        hitAnimator.PlayVisualSequence(hitAnimator.TargetRenderer, fitted, false, true, true);
                        enemyHitStarts[target] = PerformanceElapsedBeats;
                    }
                    if (mageSlimeTargets.Add(target))
                    {
                        var floor = fight.ApplyFeverMageSlime(PerformingHero, target);
                        if (floor)
                        {
                            var copy = SpawnPerformanceEffect(data.SlimeFloorPrefab, floor.transform.position, Quaternion.identity, 3600);
                            copy.transform.rotation = floor.transform.rotation; copy.transform.localScale = floor.transform.lossyScale;
                            foreach (var sprite in copy.GetComponentsInChildren<SpriteRenderer>(true))
                            { var color = sprite.color; color.a *= data.RetaliationFloorOpacity; sprite.color = color; }
                            SetMageEffectLoop(copy, true); mageFloors.Add((floor.transform, copy));
                        }
                    }
                }
                if (travel >= 1) { RemoveMageEffect(shot.visual); mageShots.RemoveAt(i); }
            }
            foreach (var pair in enemyHitStarts)
                if (pair.Key && pair.Key.CombatAnimator) pair.Key.CombatAnimator.SampleVisualSequenceBeats(PerformanceElapsedBeats - pair.Value);
            if (PerformanceElapsedBeats >= MageFireStartBeat + 4 && mageAura) { RemoveMageEffect(mageAura); mageAura = null; }
            if (PerformanceElapsedBeats >= HeroBeats) SelectNextPerformer(performanceStarted + HeroBeats);
        }
        private void SetMageEffectLoop(GameObject visual, bool loop)
        {
            if (!visual) return;
            foreach (var particle in visual.GetComponentsInChildren<ParticleSystem>(true))
            { var main = particle.main; main.loop = loop; main.simulationSpace = ParticleSystemSimulationSpace.Local; particle.Play(false); }
        }
        private void RemoveMageEffect(GameObject visual)
        {
            if (!visual) return;
            effects.Remove(visual); performanceEffects.RemoveAll(e => e.effect == visual); Destroy(visual);
        }
        private void StopMagePerformance()
        {
            magePerforming = false;
            foreach (var shot in mageShots) RemoveMageEffect(shot.visual);
            mageShots.Clear(); RemoveMageEffect(mageAura); mageAura = null;
        }
        private void PresentMageFloors()
        {
            foreach (var floor in mageFloors)
                if (floor.copy)
                {
                    floor.copy.SetActive(floor.source && floor.source.gameObject.activeInHierarchy);
                    if (floor.source) floor.copy.transform.position = floor.source.position + stageOffset;
                }
        }
    }
}
