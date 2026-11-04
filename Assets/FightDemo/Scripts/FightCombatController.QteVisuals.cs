using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        private sealed class SkillVisual
        {
            public GameObject prefab, instance;
            public ParticleSystem[] particles;
            public float remaining = -1;
        }
        private sealed class SkillFlight
        {
            public FightUnitSlot actor, target;
            public GameObject visual;
            public Vector3 start;
            public double startBeat, duration;
            public float height;
            public int roster, playback;
            public bool slime;
        }
        private Transform skillVisualRoot;
        private readonly Dictionary<GameObject, Stack<SkillVisual>> idleSkillVisuals = new();
        private readonly Dictionary<GameObject, SkillVisual> skillVisualLookup = new();
        private readonly List<SkillVisual> activeSkillVisuals = new();
        private readonly List<SkillFlight> skillFlights = new();
        private Material qteRingMaterial;
        private readonly List<FightQteRing> qteRings = new();
        private bool skillParticlesPaused;

        private void UpdateSkillVisualPause()
        {
            bool paused = IsPaused || TimingCalibrationActive;
            if (paused == skillParticlesPaused) return;
            skillParticlesPaused = paused;
            foreach (var item in activeSkillVisuals)
                foreach (var particle in item.particles)
                    if (particle != null) { if (paused) particle.Pause(false); else particle.Play(false); }
        }

        private Transform EnsureSkillVisualRoot()
        {
            if (skillVisualRoot == null)
            {
                skillVisualRoot = new GameObject("TeamSkillVisualPool").transform;
                skillVisualRoot.SetParent(transform, false);
            }
            return skillVisualRoot;
        }

        public GameObject RentSkillVisual(GameObject prefab, Vector3 position, float lifetime = -1)
        {
            if (prefab == null || this == null) return null;
            if (!idleSkillVisuals.TryGetValue(prefab, out var pool)) idleSkillVisuals[prefab] = pool = new Stack<SkillVisual>();
            SkillVisual item = null;
            while (pool.Count > 0 && item == null) { var candidate = pool.Pop(); if (candidate.instance != null) item = candidate; }
            if (item == null)
            {
                var root = Instantiate(prefab, EnsureSkillVisualRoot());
                item = new SkillVisual { prefab = prefab, instance = root, particles = root.GetComponentsInChildren<ParticleSystem>(true) };
                skillVisualLookup[root] = item;
                // Cartoon FX normally destroys completed child effects. The pool
                // owns cleanup here so every explosion remains reusable.
                foreach (var effect in root.GetComponentsInChildren<CartoonFX.CFXR_Effect>(true))
                    effect.clearBehavior = CartoonFX.CFXR_Effect.ClearBehavior.None;
                foreach (var particle in item.particles) { var main = particle.main; main.stopAction = ParticleSystemStopAction.None; }
            }
            item.remaining = lifetime;
            item.instance.transform.SetParent(EnsureSkillVisualRoot(), false);
            item.instance.transform.localScale = prefab.transform.localScale;
            item.instance.transform.SetPositionAndRotation(position, prefab.transform.rotation);
            item.instance.SetActive(true);
            foreach (var particle in item.particles) { particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); particle.Play(false); }
            activeSkillVisuals.Add(item);
            return item.instance;
        }

        public void ReturnSkillVisual(GameObject instance)
        {
            if (instance == null || !skillVisualLookup.TryGetValue(instance, out var item) || !activeSkillVisuals.Remove(item)) return;
            foreach (var particle in item.particles) if (particle != null) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            instance.SetActive(false); instance.transform.SetParent(skillVisualRoot, false);
            item.remaining = -1;
            idleSkillVisuals[item.prefab].Push(item);
        }

        internal Vector3 SlimeFloorPosition(FightUnitSlot target, Vector3 offset)
        {
            var renderer = target.CombatAnimator != null ? target.CombatAnimator.TargetRenderer : null;
            var position = renderer != null ? new Vector3(renderer.bounds.center.x, renderer.bounds.min.y, renderer.bounds.center.z) : target.transform.position;
            return position + offset;
        }

        private FightQteRing GetQteRing(int index)
        {
            if (qteRingMaterial == null) qteRingMaterial = new Material(Shader.Find("Sprites/Default"));
            while (qteRings.Count <= index)
            {
                var root = new GameObject("QTE Ring"); root.transform.SetParent(EnsureSkillVisualRoot(), false);
                var ring = root.AddComponent<FightQteRing>(); ring.Initialize(qteRingMaterial);
                root.SetActive(false); qteRings.Add(ring);
            }
            return qteRings[index];
        }

        private void HideQteRings()
        { foreach (var ring in qteRings) if (ring != null) ring.gameObject.SetActive(false); }

        private void LaunchQteProjectile(FightUnitSlot actor, FightUnitSlot target, bool slime, double now)
        {
            if (target == null || !target.HasCharacter || target.CurrentHp <= 0 || !fightScene2Enemies.Contains(target)) return;
            var data = actor.CharacterDefinition;
            Vector3 start = actor.CastEffectAnchor.position;
            skillFlights.Add(new SkillFlight { actor = actor, target = target, start = start, startBeat = now,
                duration = data.QteProjectileFlightBeats, height = data.QteProjectileArcHeight, slime = slime,
                roster = rosterVersion, playback = teamSkillPlaybackVersion,
                visual = RentSkillVisual(data.QteProjectilePrefab, start) });
        }

        private void AdvanceQteProjectiles(double now)
        {
            for (int i = skillFlights.Count - 1; i >= 0; i--)
            {
                var flight = skillFlights[i];
                bool valid = flight.playback == teamSkillPlaybackVersion && IsCurrentHeroAction(flight.roster, flight.actor, flight.target)
                    && flight.target.CurrentHp > 0 && !battleEnded;
                float t = Mathf.Clamp01((float)((now - flight.startBeat) / flight.duration));
                if (valid && flight.visual != null)
                    flight.visual.transform.position = Vector3.Lerp(flight.start, flight.target.ImpactEffectAnchor.position, t)
                        + Vector3.up * (4 * flight.height * t * (1 - t));
                if (valid && t < 1) continue;
                skillFlights.RemoveAt(i); ReturnSkillVisual(flight.visual);
                if (!valid || !HealthSystemEnabled) continue;
                var data = flight.actor.CharacterDefinition;
                if (flight.slime) flight.target.AttachSlime(data.FragmentDamage, data.FragmentBeats, flight.actor);
                else
                {
                    RentSkillVisual(data.QteImpactEffectPrefab, flight.target.ImpactEffectAnchor.position,
                        data.QteImpactEffectLifetime > 0 ? data.QteImpactEffectLifetime : 2);
                    ApplyHeroDamage(flight.roster, flight.actor, flight.target, data.SkillPower);
                    if (IsCurrentHeroAction(flight.roster, flight.actor, flight.target)) flight.target.ApplyStun(data.StunBeats);
                }
                if (flight.roster != rosterVersion || flight.playback != teamSkillPlaybackVersion) return;
            }
        }

        private void AdvanceSkillVisualLifetimes(float seconds)
        {
            for (int i = activeSkillVisuals.Count - 1; i >= 0; i--)
            {
                var item = activeSkillVisuals[i];
                if (item.instance == null) { activeSkillVisuals.RemoveAt(i); continue; }
                if (item.remaining < 0) continue;
                item.remaining -= seconds;
                if (item.remaining <= 0) ReturnSkillVisual(item.instance);
            }
        }

        private void ClearSkillVisuals()
        {
            skillFlights.Clear(); HideQteRings();
            while (activeSkillVisuals.Count > 0)
            {
                var item = activeSkillVisuals[activeSkillVisuals.Count - 1];
                if (item.instance != null) ReturnSkillVisual(item.instance); else activeSkillVisuals.RemoveAt(activeSkillVisuals.Count - 1);
            }
        }
    }
}
