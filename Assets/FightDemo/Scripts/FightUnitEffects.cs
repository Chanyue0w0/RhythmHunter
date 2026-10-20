using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemo
{
    [DisallowMultipleComponent]
    public sealed class FightUnitEffects : MonoBehaviour
    {
        private void TrackEffect(GameObject effect, float lifetime = 0)
        {
            var slot = GetComponent<FightUnitSlot>();
            if (slot == null) slot = GetComponentInParent<FightUnitSlot>();
            var owner = effect.GetComponent<FightBattleEffect>();
            if (owner == null) owner = effect.AddComponent<FightBattleEffect>();
            owner.Configure(slot != null ? slot.CombatController : null,
                slot != null && slot.Team == FightUnitSlot.UnitTeam.Enemy, lifetime);
        }
        public void SpawnConfiguredAbility(
            GameObject effectPrefab,
            Transform anchor,
            float lifetime,
            FightAttackEffect.VisualStyle style,
            Color color)
        {
            if (effectPrefab == null)
                return;

            Transform spawn = anchor != null ? anchor : transform;
            GameObject effect = Instantiate(effectPrefab, spawn.position, spawn.rotation);
            SceneManager.MoveGameObjectToScene(effect, gameObject.scene);
            effect.SetActive(true);

            FightAttackEffect attackEffect = effect.GetComponent<FightAttackEffect>();
            if (attackEffect != null)
            {
                attackEffect.PlayInPlace(lifetime, style, color);
                TrackEffect(effect);
                return;
            }

            // Particle/VFX-pack prefabs are presentation only. They never report hits.
            // A zero lifetime lets a self-destroying prefab own its cleanup.
            TrackEffect(effect, lifetime);
        }

        public void SpawnFrameEffect(FightCharacterCombatAnimator.FrameEffect entry, Transform anchor)
        {
            if (entry == null || entry.prefab == null || anchor == null) return;
            var effect = Instantiate(entry.prefab, anchor.TransformPoint(entry.positionOffset),
                anchor.rotation * Quaternion.Euler(entry.rotationOffset));
            SceneManager.MoveGameObjectToScene(effect, gameObject.scene);
            if (entry.followAnchor) effect.transform.SetParent(anchor, true);
            effect.SetActive(true);
            float lifetime = entry.limitLifetime
                ? (float.IsNaN(entry.lifetimeSeconds) || float.IsInfinity(entry.lifetimeSeconds)
                    ? 1.5f : Mathf.Max(0.01f, entry.lifetimeSeconds))
                : 0f;
            var attack = effect.GetComponent<FightAttackEffect>();
            // Legacy procedural visuals need initialization and own their animation's cleanup.
            if (attack != null) attack.PlayInPlace(entry.limitLifetime ? lifetime : 1.5f,
                FightAttackEffect.VisualStyle.Light, Color.white);
            TrackEffect(effect, lifetime);
        }

        public void SpawnAttack(
            string unitName,
            FightUnitSlot.UnitTeam team,
            Transform spawnPoint,
            Vector3 localOffset,
            Sprite fallbackSprite,
            GameObject effectPrefab,
            float lifetime,
            FightAttackEffect.VisualStyle style,
            Color color,
            float verticalOffset)
        {
            Transform spawn = spawnPoint != null ? spawnPoint : transform;
            Vector3 position = spawn.position + localOffset + Vector3.up * verticalOffset;
            GameObject effect;
            if (effectPrefab != null)
            {
                effect = Instantiate(effectPrefab, position, spawn.rotation);
            }
            else
            {
                effect = new GameObject($"{unitName}_AttackVFX");
                effect.transform.position = position;
                SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
                renderer.sprite = fallbackSprite;
                renderer.color = color;
                renderer.sortingOrder = 30;
            }

            SceneManager.MoveGameObjectToScene(effect, gameObject.scene);
            effect.SetActive(true);
            Vector3 direction = team == FightUnitSlot.UnitTeam.Hero ? Vector3.left : Vector3.right;
            FightAttackEffect attackEffect = effect.GetComponent<FightAttackEffect>();
            if (attackEffect == null)
                attackEffect = effect.AddComponent<FightAttackEffect>();
            attackEffect.Play(direction, lifetime, style, color);
            TrackEffect(effect);
        }

        public void SpawnGuard(
            string unitName,
            Transform actorRoot,
            Sprite fallbackSprite,
            Color color,
            float duration,
            float fromScale,
            float toScale,
            float rotationSpeed,
            float startingRotation)
        {
            Transform root = actorRoot != null ? actorRoot : transform;
            GameObject effect = new($"{unitName}_GuardVFX", typeof(SpriteRenderer), typeof(FightGuardEffect));
            effect.transform.SetParent(root, false);
            effect.transform.localPosition = new Vector3(0f, 0f, -0.45f);
            effect.transform.localRotation = Quaternion.Euler(0f, 0f, startingRotation);
            SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
            renderer.sprite = fallbackSprite;
            renderer.color = color;
            renderer.sortingOrder = 31;
            effect.GetComponent<FightGuardEffect>().Play(color, duration, fromScale, toScale, rotationSpeed);
            TrackEffect(effect);
        }

        public void SpawnAttackCharge(
            string unitName,
            Transform actorRoot,
            Sprite fallbackSprite,
            Color color,
            bool attackBeat,
            bool enemy,
            float startingRotation)
        {
            Transform root = actorRoot != null ? actorRoot : transform;
            GameObject effect = new($"{unitName}_{(enemy ? "Enemy" : "Hero")}BeatVFX", typeof(SpriteRenderer), typeof(FightGuardEffect));
            effect.transform.SetParent(root, false);
            effect.transform.localPosition = new Vector3(0f, 0f, 0.35f);
            effect.transform.localRotation = Quaternion.Euler(0f, 0f, startingRotation);
            SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
            renderer.sprite = fallbackSprite;
            renderer.color = color;
            renderer.sortingOrder = attackBeat ? 29 : 9;
            effect.GetComponent<FightGuardEffect>().Play(
                color,
                attackBeat ? 0.5f : 0.28f,
                attackBeat ? 0.72f : 0.42f,
                attackBeat ? 2.2f : 1.15f,
                attackBeat ? 260f : 90f);
            TrackEffect(effect);
        }
    }
}
