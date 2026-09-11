using UnityEngine;

namespace RhythmHunter.FightDemo
{
    [DisallowMultipleComponent]
    public sealed class FightUnitEffects : MonoBehaviour
    {
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

            effect.SetActive(true);
            Vector3 direction = team == FightUnitSlot.UnitTeam.Hero ? Vector3.left : Vector3.right;
            FightAttackEffect attackEffect = effect.GetComponent<FightAttackEffect>();
            if (attackEffect == null)
                attackEffect = effect.AddComponent<FightAttackEffect>();
            attackEffect.Play(direction, lifetime, style, color);
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
        }
    }
}
