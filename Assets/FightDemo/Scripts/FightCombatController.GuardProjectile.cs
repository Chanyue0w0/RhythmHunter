using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        private void PlayGuardProjectileVisual(FightUnitSlot actor, bool skill, FightUnitSlot receiver)
        {
            var data = actor.CharacterDefinition;
            if (skill || data == null || data.GuardProjectilePrefab == null || beatClock == null || !beatClock.HasTimingAnchor)
            { actor.PlayGuardAt(skill, receiver); return; }
            receiver = receiver != null ? receiver : actor;
            var from = actor.CastEffectAnchor;
            var root = receiver.ActorInstance != null ? receiver.ActorInstance.transform : receiver.ActorRoot;
            Transform to = null;
            if (!string.IsNullOrWhiteSpace(data.GuardProjectileTargetPath))
                to = ResolveGuardProjectileAnchor(root, data.GuardProjectileTargetPath);
            if (to == null)
                switch (data.GuardProjectileTargetAnchor)
                {
                    case FightCharacterDefinition.GuardProjectileAnchor.Feet:
                        to = ResolveGuardProjectileAnchor(root, "FootAnchor"); break;
                    case FightCharacterDefinition.GuardProjectileAnchor.Cast: to = receiver.CastEffectAnchor; break;
                    case FightCharacterDefinition.GuardProjectileAnchor.Impact: to = receiver.ImpactEffectAnchor; break;
                    case FightCharacterDefinition.GuardProjectileAnchor.ActorRoot: to = root; break;
                }
            if (to == null) to = root;
            if (from == null || to == null) { actor.PlayGuardAt(skill, receiver); return; }
            int version = rosterVersion;
            var prefab = data.GuardProjectilePrefab;
            // Keep the authored 2.5D orientation/scale, independent of actor-card scale and flips.
            var effect = Instantiate(prefab, from.position + from.rotation * data.GuardProjectileSpawnOffset, prefab.transform.rotation);
            SceneManager.MoveGameObjectToScene(effect, gameObject.scene);
            effect.SetActive(true);
            effect.GetComponent<FightCameraFacingEffect>()?.Bind(statusIconCamera != null ? statusIconCamera : Camera.main);
            var projectile = effect.GetComponent<FightBeatProjectile>() ?? effect.AddComponent<FightBeatProjectile>();
            projectile.Initialize(this, to, data.GuardProjectileFlightBeats,
                () => IsCurrentHero(version, actor) && receiver != null && receiver.HasCharacter && !battleEnded,
                () => actor.PlayGuardAt(false, receiver), data.GuardProjectileFadeBeats, data.GuardProjectileFinalScale,
                data.GuardProjectileTargetOffset, data.GuardEffectDelayAfterImpactBeats);
        }

        private static Transform ResolveGuardProjectileAnchor(Transform root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path)) return null;
            var direct = root.Find(path);
            if (direct != null) return direct;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == path) return child;
            return null;
        }
    }
}
