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
        [SerializeField, Min(0)] private int skillDamage = 30;
        [SerializeField, Min(1)] private int attackIntervalBeats = 4;
        [SerializeField, Min(1)] private int maxMana = 4;

        [Header("Runtime Combat State")]
        [Tooltip("Runtime mana. Enemy normal attacks add one point; reaching maximum does not auto-cast a skill.")]
        [SerializeField, Min(0)] private int currentMana;

        [Header("Replaceable Prefab Hooks")]
        [Tooltip("Drop the final hero/enemy prefab here. It is instantiated under Actor Root at runtime.")]
        [SerializeField] private GameObject actorPrefab;
        [Tooltip("Spawned when this unit performs a normal attack. A FightAttackEffect component is optional.")]
        [SerializeField] private GameObject normalAttackEffectPrefab;
        [SerializeField] private GameObject skillEffectPrefab;
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
        private Vector3 actorBaseScale = Vector3.one;
        private FightCharacterDefinition characterDefinition;
        private FightCharacterCombatAnimator combatAnimator;
        private bool hasCharacter = true;
        private int currentHp;
        private float pulse;
        private float pulseScaleBonus = 0.14f;
        private int normalAttackPlayCount;
        private int lightAttackPlayCount;
        private int heavyAttackPlayCount;
        private int skillAttackPlayCount;
        private int guardPlayCount;

        public event Action<FightUnitSlot, int, int> HealthChanged;

        public string SlotId => slotId;
        public string DisplayName => displayName;
        public UnitTeam Team => team;
        public UnitRole Role => role;
        public int SlotIndex => slotIndex;
        public int MaxHp => maxHp;
        public int CurrentHp => currentHp;
        public int AttackPower => attackPower;
        public int SkillDamage => skillDamage;
        public int AttackIntervalBeats => attackIntervalBeats;
        public int MaxMana => maxMana;
        public int CurrentMana => currentMana;
        public bool ManaFull => currentMana >= maxMana;
        public bool HasCharacter => hasCharacter;
        public FightCharacterDefinition CharacterDefinition => characterDefinition;
        public FightCharacterCombatAnimator CombatAnimator => combatAnimator;
        public GameObject ActorPrefab => actorPrefab;
        public GameObject ActorInstance => actorInstance;
        public GameObject NormalAttackEffectPrefab => normalAttackEffectPrefab;
        public GameObject SkillEffectPrefab => skillEffectPrefab;
        public Transform ActorRoot => actorRoot;
        public Transform NormalAttackEffectSpawnPoint => normalAttackEffectSpawnPoint;
        public int NormalAttackPlayCount => normalAttackPlayCount;
        public int LightAttackPlayCount => lightAttackPlayCount;
        public int HeavyAttackPlayCount => heavyAttackPlayCount;
        public int SkillAttackPlayCount => skillAttackPlayCount;
        public int GuardPlayCount => guardPlayCount;

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
            skillDamage = Mathf.Max(0, power * 3);
            attackIntervalBeats = 4;
            maxMana = 4;
            accentColor = color;
            actorRoot = prefabRoot;
            normalAttackEffectSpawnPoint = effectSpawnPoint;
            placeholderVisual = placeholder;
            fallbackEffectSprite = fallbackSprite;
            hpFill = healthFill;
            hpLabel = healthLabel;
            currentHp = maxHp;
            currentMana = 0;
            hasCharacter = true;
            RefreshHealthVisuals();
        }

        public void SpawnCharacter(FightCharacterDefinition prefab, FightCombatController beatSource)
        {
            if (prefab == null)
            {
                ClearCharacter();
                return;
            }

            RemoveSpawnedActor();
            ApplyDefinition(prefab);
            hasCharacter = true;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            if (actorRoot != null)
                actorRoot.gameObject.SetActive(true);

            DisableSceneIdleVisual();
            SpawnActorPrefab();
            characterDefinition = actorInstance != null
                ? actorInstance.GetComponent<FightCharacterDefinition>()
                : prefab;
            combatAnimator = actorInstance != null
                ? actorInstance.GetComponent<FightCharacterCombatAnimator>()
                : null;

            BeatSyncedIdleAnimator animator = actorInstance != null
                ? actorInstance.GetComponentInChildren<BeatSyncedIdleAnimator>(true)
                : null;
            FightCharacterDefinition runtimeDefinition = actorInstance != null
                ? actorInstance.GetComponent<FightCharacterDefinition>()
                : null;
            if (animator != null && runtimeDefinition != null)
            {
                animator.Configure(
                    beatSource,
                    runtimeDefinition.CharacterRenderer,
                    runtimeDefinition.IdleFrames,
                    runtimeDefinition.IdleCyclesPerBeat,
                    runtimeDefinition.IdlePingPong);
            }

            currentHp = maxHp;
            currentMana = 0;
            RefreshHealthVisuals();
        }

        public void ClearCharacter()
        {
            RemoveSpawnedActor();
            characterDefinition = null;
            combatAnimator = null;
            actorPrefab = null;
            hasCharacter = false;
            currentMana = 0;
            gameObject.SetActive(false);
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
            Transform visualRoot = actorInstance != null
                ? actorInstance.transform
                : actorRoot != null
                    ? actorRoot
                    : placeholderVisual != null
                        ? placeholderVisual.transform
                        : null;
            if (visualRoot != null)
            {
                Vector3 baseScale = actorInstance != null ? actorBaseScale : Vector3.one;
                visualRoot.localScale = baseScale * Mathf.Lerp(1f, 1f + pulseScaleBonus, pulse);
            }
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
            Pulse(0.14f);
        }

        public void Pulse(float scaleBonus)
        {
            pulse = 1f;
            pulseScaleBonus = Mathf.Max(0.02f, scaleBonus);
        }

        public void GainMana(int amount = 1)
        {
            currentMana = Mathf.Clamp(currentMana + Mathf.Max(0, amount), 0, maxMana);
        }

        public bool PlayCombatAnimation(
            FightCharacterCombatAnimator.CombatAnimation animation,
            Action onWarning,
            Action onAttackEffect,
            Action onDamage)
        {
            return combatAnimator != null && combatAnimator.Play(
                animation,
                onWarning,
                onAttackEffect,
                onDamage);
        }

        public void PlayAttackFrameWarning(bool enemy)
        {
            Color warningColor = enemy
                ? new Color(1f, 0.18f, 0.05f, 0.72f)
                : new Color(0.15f, 0.9f, 1f, 0.65f);
            Pulse(enemy ? 0.22f : 0.16f);
            SpawnAttackChargeLayer(warningColor, false, enemy, 0f);
        }

        public void PlayScheduledAttackCountdown(int beatsUntilAttack, int intervalBeats, bool enemy)
        {
            int interval = Mathf.Max(1, intervalBeats);
            int remaining = Mathf.Clamp(beatsUntilAttack, 0, interval - 1);
            bool attackBeat = remaining == 0;
            float readiness = attackBeat ? 1f : 1f - remaining / (float)interval;
            Pulse(attackBeat ? 0.34f : Mathf.Lerp(0.06f, 0.18f, readiness));

            Color chargeColor = enemy
                ? attackBeat
                    ? new Color(1f, 0.12f, 0.05f, 0.9f)
                    : Color.Lerp(new Color(1f, 0.72f, 0.08f, 0.35f), new Color(1f, 0.25f, 0.04f, 0.68f), readiness)
                : attackBeat
                    ? new Color(0.15f, 0.95f, 1f, 0.9f)
                    : Color.Lerp(new Color(0.18f, 0.55f, 1f, 0.3f), new Color(0.2f, 1f, 0.75f, 0.65f), readiness);
            SpawnAttackChargeLayer(chargeColor, attackBeat, enemy, 0f);
            if (attackBeat)
            {
                Color accent = enemy
                    ? new Color(1f, 0.78f, 0.12f, 0.72f)
                    : new Color(0.72f, 1f, 1f, 0.72f);
                SpawnAttackChargeLayer(accent, true, enemy, 35f);
            }
        }

        public void PlayNormalAttack()
        {
            normalAttackPlayCount++;
            Pulse(0.14f);
            SpawnAttackEffect(FightAttackEffect.VisualStyle.Normal, accentColor, 0f);
        }

        public void PlayImmediateNormalAttack()
        {
            normalAttackPlayCount++;
            Pulse(0.28f);
            SpawnAttackEffect(
                FightAttackEffect.VisualStyle.Normal,
                accentColor,
                0f,
                null,
                Mathf.Min(attackEffectLifetime, 0.16f));
        }

        public void PlayLightAttack()
        {
            normalAttackPlayCount++;
            lightAttackPlayCount++;
            Pulse(0.16f);
            Color lightColor = Color.Lerp(accentColor, new Color(0.2f, 0.95f, 1f, 1f), 0.72f);
            SpawnAttackEffect(FightAttackEffect.VisualStyle.Light, lightColor, 0f);
        }

        public void PlayHeavyAttack()
        {
            normalAttackPlayCount++;
            heavyAttackPlayCount++;
            Pulse(0.34f);
            Color heavyColor = new(1f, 0.3f, 0.06f, 1f);
            SpawnAttackEffect(FightAttackEffect.VisualStyle.Heavy, heavyColor, -0.24f);
            SpawnAttackEffect(FightAttackEffect.VisualStyle.Heavy, new Color(1f, 0.68f, 0.08f, 0.9f), 0f);
            SpawnAttackEffect(FightAttackEffect.VisualStyle.Heavy, heavyColor, 0.24f);
        }

        public void PlaySkillAttack()
        {
            normalAttackPlayCount++;
            skillAttackPlayCount++;
            Pulse(0.42f);
            SpawnAttackEffect(FightAttackEffect.VisualStyle.Skill, new Color(0.86f, 0.38f, 1f, 1f), -0.22f, skillEffectPrefab);
            SpawnAttackEffect(FightAttackEffect.VisualStyle.Skill, new Color(1f, 0.82f, 0.2f, 1f), 0.22f, skillEffectPrefab);
        }

        public void PlayGuard()
        {
            guardPlayCount++;
            Pulse(0.22f);
            SpawnGuardLayer(new Color(0.2f, 1f, 0.58f, 0.72f), 0.75f, 0.7f, 1.8f, 220f, 0f);
            SpawnGuardLayer(new Color(0.15f, 0.85f, 1f, 0.58f), 0.95f, 0.95f, 2.25f, -150f, 45f);
        }

        public void SetHealthDisplayVisible(bool visible)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer.name == "HealthBackground" || renderer.name == "HealthFill")
                    renderer.gameObject.SetActive(visible);
            }

            if (hpLabel != null)
                hpLabel.gameObject.SetActive(visible);
        }

        private void SpawnAttackEffect(
            FightAttackEffect.VisualStyle style,
            Color color,
            float verticalOffset,
            GameObject overridePrefab = null,
            float lifetimeOverride = -1f)
        {
            Transform spawn = normalAttackEffectSpawnPoint != null ? normalAttackEffectSpawnPoint : transform;
            Vector3 position = spawn.position + attackEffectLocalOffset + Vector3.up * verticalOffset;
            GameObject effect;

            GameObject effectPrefab = overridePrefab != null ? overridePrefab : normalAttackEffectPrefab;
            if (effectPrefab != null)
            {
                effect = Instantiate(effectPrefab, position, spawn.rotation);
            }
            else
            {
                effect = new GameObject($"{displayName}_NormalAttackVFX");
                effect.transform.position = position;
                SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
                renderer.sprite = fallbackEffectSprite;
                renderer.color = color;
                renderer.sortingOrder = 30;
            }

            effect.SetActive(true);
            Vector3 direction = team == UnitTeam.Hero ? Vector3.left : Vector3.right;
            FightAttackEffect attackEffect = effect.GetComponent<FightAttackEffect>();
            if (attackEffect == null)
                attackEffect = effect.AddComponent<FightAttackEffect>();
            float lifetime = lifetimeOverride > 0f ? lifetimeOverride : attackEffectLifetime;
            attackEffect.Play(direction, lifetime, style, color);
        }

        private void SpawnGuardLayer(
            Color color,
            float duration,
            float fromScale,
            float toScale,
            float rotationSpeed,
            float startingRotation)
        {
            Transform root = actorRoot != null ? actorRoot : transform;
            GameObject effect = new($"{displayName}_GuardVFX", typeof(SpriteRenderer), typeof(FightGuardEffect));
            effect.transform.SetParent(root, false);
            effect.transform.localPosition = new Vector3(0f, 0f, -0.45f);
            effect.transform.localRotation = Quaternion.Euler(0f, 0f, startingRotation);
            SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
            renderer.sprite = fallbackEffectSprite;
            renderer.color = color;
            renderer.sortingOrder = 31;
            effect.GetComponent<FightGuardEffect>().Play(color, duration, fromScale, toScale, rotationSpeed);
        }

        private void SpawnAttackChargeLayer(Color color, bool attackBeat, bool enemy, float startingRotation)
        {
            Transform root = actorRoot != null ? actorRoot : transform;
            GameObject effect = new($"{displayName}_{(enemy ? "Enemy" : "Hero")}BeatVFX", typeof(SpriteRenderer), typeof(FightGuardEffect));
            effect.transform.SetParent(root, false);
            effect.transform.localPosition = new Vector3(0f, 0f, 0.35f);
            effect.transform.localRotation = Quaternion.Euler(0f, 0f, startingRotation);
            SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
            renderer.sprite = fallbackEffectSprite;
            renderer.color = color;
            renderer.sortingOrder = attackBeat ? 29 : 9;
            effect.GetComponent<FightGuardEffect>().Play(
                color,
                attackBeat ? 0.5f : 0.28f,
                attackBeat ? 0.72f : 0.42f,
                attackBeat ? 2.2f : 1.15f,
                attackBeat ? 260f : 90f);
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
            actorBaseScale = actorInstance.transform.localScale;

            if (placeholderVisual != null)
                placeholderVisual.SetActive(false);
        }

        private void ApplyDefinition(FightCharacterDefinition definition)
        {
            characterDefinition = definition;
            actorPrefab = definition.gameObject;
            slotId = definition.CharacterId;
            displayName = definition.DisplayName;
            team = definition.Team;
            role = definition.Role;
            maxHp = definition.MaxHp;
            attackPower = definition.AttackPower;
            skillDamage = definition.SkillDamage;
            attackIntervalBeats = definition.AttackIntervalBeats;
            maxMana = definition.MaxMana;
            currentMana = Mathf.Clamp(currentMana, 0, maxMana);
            skillEffectPrefab = definition.SkillEffectPrefab;
            accentColor = definition.AccentColor;
        }

        private void DisableSceneIdleVisual()
        {
            if (actorRoot == null)
                return;

            for (int i = 0; i < actorRoot.childCount; i++)
            {
                Transform child = actorRoot.GetChild(i);
                if (child.name == "BeatSyncedIdle")
                    child.gameObject.SetActive(false);
            }
        }

        private void RemoveSpawnedActor()
        {
            if (actorInstance == null)
                return;

            if (Application.isPlaying)
                Destroy(actorInstance);
            else
                DestroyImmediate(actorInstance);
            actorInstance = null;
            combatAnimator = null;
            actorBaseScale = Vector3.one;
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
                hpLabel.text = $"HP {currentHp}/{maxHp}  ATK {attackPower}  SKILL {skillDamage}";
        }
    }
}
