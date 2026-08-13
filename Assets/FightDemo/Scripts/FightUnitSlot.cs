using System;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// A world-space battlefield slot and the data hook for a future hero/enemy prefab.
    /// Assign Actor Prefab later; the scene placeholder is hidden automatically at runtime.
    /// </summary>
    public sealed class FightUnitSlot : MonoBehaviour
    {
        public enum UnitTeam
        {
            Enemy,
            Hero
        }

        public enum UnitRole
        {
            Enemy,
            Tank,
            Support,
            Damage
        }

        [Header("Identity")]
        [SerializeField] private string slotId = "Unit";
        [SerializeField] private string displayName = "UNIT";
        [SerializeField] private UnitTeam team;
        [SerializeField] private UnitRole role;
        [SerializeField, Min(0)] private int slotIndex;

        [Header("Combat Data")]
        [SerializeField, Min(1)] private int maxHp = 100;
        [SerializeField, Min(0)] private int attackPower = 10;

        [Header("Replaceable Prefab Hooks")]
        [Tooltip("Drop the final hero/enemy prefab here. It is instantiated under Actor Root at runtime.")]
        [SerializeField] private GameObject actorPrefab;
        [Tooltip("Spawned when this unit performs a normal attack. A FightAttackEffect component is optional.")]
        [SerializeField] private GameObject normalAttackEffectPrefab;
        [SerializeField] private Transform actorRoot;
        [SerializeField] private Transform normalAttackEffectSpawnPoint;
        [SerializeField] private Vector3 actorLocalOffset;
        [SerializeField] private Vector3 attackEffectLocalOffset;
        [SerializeField, Min(0.05f)] private float attackEffectLifetime = 0.45f;

        [Header("Prototype World Visuals")]
        [SerializeField] private GameObject placeholderVisual;
        [SerializeField] private Sprite fallbackEffectSprite;
        [SerializeField] private Color accentColor = Color.white;
        [SerializeField] private SpriteRenderer hpFill;
        [SerializeField] private TextMesh hpLabel;

        private GameObject actorInstance;
        private int currentHp;
        private float pulse;
        private int normalAttackPlayCount;

        public event Action<FightUnitSlot, int, int> HealthChanged;

        public string SlotId => slotId;
        public string DisplayName => displayName;
        public UnitTeam Team => team;
        public UnitRole Role => role;
        public int SlotIndex => slotIndex;
        public int MaxHp => maxHp;
        public int CurrentHp => currentHp;
        public int AttackPower => attackPower;
        public GameObject ActorPrefab => actorPrefab;
        public GameObject NormalAttackEffectPrefab => normalAttackEffectPrefab;
        public Transform ActorRoot => actorRoot;
        public Transform NormalAttackEffectSpawnPoint => normalAttackEffectSpawnPoint;
        public int NormalAttackPlayCount => normalAttackPlayCount;

        public void Configure(
            string id,
            string unitName,
            UnitTeam unitTeam,
            UnitRole unitRole,
            int index,
            int hp,
            int power,
            Color color,
            Transform prefabRoot,
            Transform effectSpawnPoint,
            GameObject placeholder,
            Sprite fallbackSprite,
            SpriteRenderer healthFill,
            TextMesh healthLabel)
        {
            slotId = id;
            displayName = unitName;
            team = unitTeam;
            role = unitRole;
            slotIndex = Mathf.Max(0, index);
            maxHp = Mathf.Max(1, hp);
            attackPower = Mathf.Max(0, power);
            accentColor = color;
            actorRoot = prefabRoot;
            normalAttackEffectSpawnPoint = effectSpawnPoint;
            placeholderVisual = placeholder;
            fallbackEffectSprite = fallbackSprite;
            hpFill = healthFill;
            hpLabel = healthLabel;
            currentHp = maxHp;
            RefreshHealthVisuals();
        }

        private void Awake()
        {
            currentHp = maxHp;
            SpawnActorPrefab();
            RefreshHealthVisuals();
        }

        private void Update()
        {
            pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 4f);
            Transform visualRoot = actorInstance != null ? actorInstance.transform : placeholderVisual != null ? placeholderVisual.transform : null;
            if (visualRoot != null)
                visualRoot.localScale = Vector3.one * Mathf.Lerp(1f, 1.14f, pulse);
        }

        public void RestoreFullHealth()
        {
            currentHp = maxHp;
            RefreshHealthVisuals();
            HealthChanged?.Invoke(this, currentHp, maxHp);
        }

        public int TakeDamage(int amount)
        {
            int applied = Mathf.Clamp(amount, 0, currentHp);
            currentHp -= applied;
            RefreshHealthVisuals();
            HealthChanged?.Invoke(this, currentHp, maxHp);
            Pulse();
            return applied;
        }

        public void Pulse()
        {
            pulse = 1f;
        }

        public void PlayNormalAttack()
        {
            normalAttackPlayCount++;
            Pulse();
            Transform spawn = normalAttackEffectSpawnPoint != null ? normalAttackEffectSpawnPoint : transform;
            Vector3 position = spawn.position + attackEffectLocalOffset;
            GameObject effect;

            if (normalAttackEffectPrefab != null)
            {
                effect = Instantiate(normalAttackEffectPrefab, position, spawn.rotation);
            }
            else
            {
                effect = new GameObject($"{displayName}_NormalAttackVFX");
                effect.transform.position = position;
                SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
                renderer.sprite = fallbackEffectSprite;
                renderer.color = accentColor;
                renderer.sortingOrder = 30;
                effect.AddComponent<FightAttackEffect>();
            }

            effect.SetActive(true);
            Vector3 direction = team == UnitTeam.Hero ? Vector3.left : Vector3.right;
            if (effect.TryGetComponent(out FightAttackEffect attackEffect))
                attackEffect.Play(direction, attackEffectLifetime);
            else
                Destroy(effect, attackEffectLifetime);
        }

        private void SpawnActorPrefab()
        {
            if (actorPrefab == null || actorInstance != null)
                return;

            Transform root = actorRoot != null ? actorRoot : transform;
            actorInstance = Instantiate(actorPrefab, root);
            actorInstance.name = $"{actorPrefab.name} (Runtime)";
            actorInstance.transform.localPosition = actorLocalOffset;
            actorInstance.transform.localRotation = Quaternion.identity;

            if (placeholderVisual != null)
                placeholderVisual.SetActive(false);
        }

        private void RefreshHealthVisuals()
        {
            float ratio = maxHp > 0 ? (float)currentHp / maxHp : 0f;
            if (hpFill != null)
            {
                Vector3 scale = hpFill.transform.localScale;
                scale.x = ratio;
                hpFill.transform.localScale = scale;
            }

            if (hpLabel != null)
                hpLabel.text = $"HP {currentHp}/{maxHp}  ATK {attackPower}";
        }
    }
}
