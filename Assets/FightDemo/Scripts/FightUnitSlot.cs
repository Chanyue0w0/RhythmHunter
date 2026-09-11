using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

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
        [SerializeField, Min(0.5f)] private float maxHp = 4f;
        [SerializeField] private FightCharacterDefinition.AbilityBehavior normalAbilityBehavior;
        [SerializeField, Min(0)] private float normalAbilityPower = 1f;
        [SerializeField, Min(0.5f)] private float attackPower = 1f;
        [FormerlySerializedAs("skillDamage")]
        [SerializeField, Min(0)] private float skillPower = 1f;
        [SerializeField] private FightCharacterDefinition.AbilityBehavior skillBehavior;
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
        [SerializeField] private FightUnitEffects effectPlayer;
        [SerializeField] private SpriteRenderer hpBackground;
        [SerializeField] private SpriteRenderer hpFill;
        [SerializeField] private TextMesh hpLabel;
        [SerializeField] private TextMesh roleLabel;

        [Header("Input Feedback")]
        [SerializeField, Min(0f)] private float onBeatJumpHeight = 0.22f;
        [SerializeField, Min(0.01f)] private float onBeatJumpDuration = 0.18f;
        [SerializeField, Min(0f)] private float missShakeStrength = 0.12f;
        [SerializeField, Min(0.01f)] private float missShakeDuration = 0.16f;
        [SerializeField, Min(1f)] private float missShakeFrequency = 60f;

        private GameObject actorInstance;
        private FightCharacterDefinition characterDefinition;
        private FightCharacterCombatAnimator combatAnimator;
        private bool hasCharacter = true;
        private float currentHp;
        private int normalAttackPlayCount;
        private int lightAttackPlayCount;
        private int heavyAttackPlayCount;
        private int skillAttackPlayCount;
        private int guardPlayCount;
        private int onBeatFeedbackPlayCount;
        private int missFeedbackPlayCount;
        private Coroutine inputFeedbackRoutine;
        private Transform inputFeedbackTarget;
        private Vector3 inputFeedbackOrigin;
        private Transform runtimeCastEffectAnchor;
        private Transform runtimeImpactEffectAnchor;

        public event Action<FightUnitSlot, float, float> HealthChanged;

        public string SlotId => slotId;
        public string DisplayName => displayName;
        public UnitTeam Team => team;
        public UnitRole Role => role;
        public int SlotIndex => slotIndex;
        public float MaxHp => maxHp;
        public float CurrentHp => currentHp;
        public FightCharacterDefinition.AbilityBehavior NormalAbilityBehavior => normalAbilityBehavior;
        public float NormalAbilityPower => normalAbilityPower;
        public float AttackPower => attackPower;
        public float SkillPower => skillPower;
        public FightCharacterDefinition.AbilityBehavior SkillBehavior => skillBehavior;
        public int AttackIntervalBeats => attackIntervalBeats;
        public int MaxMana => maxMana;
        public int CurrentMana => currentMana;
        public bool ManaFull => currentMana >= maxMana;
        public bool HasCharacter => hasCharacter;
        public FightCharacterDefinition CharacterDefinition => characterDefinition;
        public FightCharacterCombatAnimator CombatAnimator => combatAnimator;
        public GameObject ActorPrefab => actorPrefab;
        public GameObject ActorInstance => actorInstance;
        public GameObject NormalAttackEffectPrefab => characterDefinition != null && characterDefinition.NormalAbilityEffectPrefab != null
            ? characterDefinition.NormalAbilityEffectPrefab
            : normalAttackEffectPrefab;
        public GameObject SkillEffectPrefab => characterDefinition != null && characterDefinition.SkillEffectPrefab != null
            ? characterDefinition.SkillEffectPrefab
            : skillEffectPrefab;
        public Transform ActorRoot => actorRoot;
        public Transform NormalAttackEffectSpawnPoint => normalAttackEffectSpawnPoint;
        public Transform CastEffectAnchor => runtimeCastEffectAnchor != null
            ? runtimeCastEffectAnchor
            : (characterDefinition != null ? characterDefinition.CastEffectAnchor : actorRoot);
        public Transform ImpactEffectAnchor => runtimeImpactEffectAnchor != null
            ? runtimeImpactEffectAnchor
            : (characterDefinition != null ? characterDefinition.ImpactEffectAnchor : actorRoot);
        public int NormalAttackPlayCount => normalAttackPlayCount;
        public int LightAttackPlayCount => lightAttackPlayCount;
        public int HeavyAttackPlayCount => heavyAttackPlayCount;
        public int SkillAttackPlayCount => skillAttackPlayCount;
        public int GuardPlayCount => guardPlayCount;
        public int OnBeatFeedbackPlayCount => onBeatFeedbackPlayCount;
        public int MissFeedbackPlayCount => missFeedbackPlayCount;

        private FightUnitEffects EffectPlayer
        {
            get
            {
                if (effectPlayer == null)
                    effectPlayer = GetComponent<FightUnitEffects>();
                if (effectPlayer == null)
                    effectPlayer = gameObject.AddComponent<FightUnitEffects>();
                return effectPlayer;
            }
        }

        public void Configure(
            string id,
            string unitName,
            UnitTeam unitTeam,
            UnitRole unitRole,
            int index,
            float hp,
            float power,
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
            maxHp = QuantizePositive(hp);
            attackPower = QuantizePositive(power);
            normalAbilityPower = attackPower;
            skillPower = QuantizePositive(power * 3f);
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

        public void ConfigurePresentation(TextMesh role, SpriteRenderer healthBackground)
        {
            roleLabel = role;
            hpBackground = healthBackground;
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
            CacheOrCreateRuntimeEffectAnchors();
            combatAnimator?.BindBeatSource(beatSource);

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
            CacheLegacyPresentationReferences();
            currentHp = maxHp;
            SpawnActorPrefab();
            RefreshHealthVisuals();
        }

        private void OnDisable()
        {
            ResetInputFeedback();
        }

        public void RestoreFullHealth()
        {
            currentHp = maxHp;
            RefreshHealthVisuals();
            HealthChanged?.Invoke(this, currentHp, maxHp);
        }

        public float TakeDamage(float amount)
        {
            float applied = Mathf.Clamp(QuantizeNonNegative(amount), 0f, currentHp);
            currentHp -= applied;
            RefreshHealthVisuals();
            HealthChanged?.Invoke(this, currentHp, maxHp);
            return applied;
        }

        public float Heal(float amount)
        {
            float applied = Mathf.Clamp(QuantizeNonNegative(amount), 0f, maxHp - currentHp);
            currentHp += applied;
            RefreshHealthVisuals();
            HealthChanged?.Invoke(this, currentHp, maxHp);
            return applied;
        }

        public void GainMana(int amount = 1)
        {
            currentMana = Mathf.Clamp(currentMana + Mathf.Max(0, amount), 0, maxMana);
        }

        public void ResetMana(int amount = 0)
        {
            currentMana = Mathf.Clamp(amount, 0, maxMana);
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
            SpawnAttackChargeLayer(warningColor, false, enemy, 0f);
        }

        public void PlayScheduledAttackCountdown(int beatsUntilAttack, int intervalBeats, bool enemy)
        {
            int interval = Mathf.Max(1, intervalBeats);
            int remaining = Mathf.Clamp(beatsUntilAttack, 0, interval - 1);
            bool attackBeat = remaining == 0;
            float readiness = attackBeat ? 1f : 1f - remaining / (float)interval;

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
            SpawnAttackEffect(FightAttackEffect.VisualStyle.Normal, accentColor, 0f);
        }

        public void PlayImmediateNormalAttack()
        {
            normalAttackPlayCount++;
            SpawnAttackEffect(
                FightAttackEffect.VisualStyle.Normal,
                accentColor,
                0f,
                null,
                Mathf.Min(attackEffectLifetime, 0.16f));
        }

        public void PlayLightAttack()
        {
            PlayLightAttackAt();
        }

        public void PlayLightAttackAt(params Transform[] effectAnchors)
        {
            normalAttackPlayCount++;
            lightAttackPlayCount++;
            Color lightColor = Color.Lerp(accentColor, new Color(0.2f, 0.95f, 1f, 1f), 0.72f);
            if (!SpawnConfiguredAbilityEffects(false, FightAttackEffect.VisualStyle.Light, lightColor, effectAnchors))
                SpawnAttackEffect(FightAttackEffect.VisualStyle.Light, lightColor, 0f);
        }

        public void PlayHeavyAttack()
        {
            normalAttackPlayCount++;
            heavyAttackPlayCount++;
            Color heavyColor = new(1f, 0.3f, 0.06f, 1f);
            SpawnAttackEffect(FightAttackEffect.VisualStyle.Heavy, heavyColor, -0.24f);
            SpawnAttackEffect(FightAttackEffect.VisualStyle.Heavy, new Color(1f, 0.68f, 0.08f, 0.9f), 0f);
            SpawnAttackEffect(FightAttackEffect.VisualStyle.Heavy, heavyColor, 0.24f);
        }

        public void PlaySkillAttack()
        {
            PlaySkillAttackAt();
        }

        public void PlaySkillAttackAt(params Transform[] effectAnchors)
        {
            normalAttackPlayCount++;
            skillAttackPlayCount++;
            Color skillColor = Color.Lerp(accentColor, new Color(1f, 0.82f, 0.2f, 1f), 0.42f);
            if (!SpawnConfiguredAbilityEffects(true, FightAttackEffect.VisualStyle.Skill, skillColor, effectAnchors))
            {
                SpawnAttackEffect(FightAttackEffect.VisualStyle.Skill, new Color(0.86f, 0.38f, 1f, 1f), -0.22f, skillEffectPrefab);
                SpawnAttackEffect(FightAttackEffect.VisualStyle.Skill, new Color(1f, 0.82f, 0.2f, 1f), 0.22f, skillEffectPrefab);
            }
        }

        public void PlayGuard()
        {
            PlayGuardAt(false);
        }

        public void PlayGuardAt(bool skill)
        {
            guardPlayCount++;
            Color guardColor = new(0.2f, 1f, 0.58f, 0.72f);
            if (SpawnConfiguredAbilityEffects(
                    skill,
                    skill ? FightAttackEffect.VisualStyle.Skill : FightAttackEffect.VisualStyle.Light,
                    guardColor,
                    new[] { CastEffectAnchor }))
            {
                return;
            }

            SpawnGuardLayer(guardColor, 0.75f, 0.7f, 1.8f, 220f, 0f);
            SpawnGuardLayer(new Color(0.15f, 0.85f, 1f, 0.58f), 0.95f, 0.95f, 2.25f, -150f, 45f);
        }

        public void PlayInputFeedback(bool onBeat)
        {
            if (onBeat)
                onBeatFeedbackPlayCount++;
            else
                missFeedbackPlayCount++;
            ResetInputFeedback();
            inputFeedbackTarget = actorInstance != null ? actorInstance.transform : actorRoot;
            if (inputFeedbackTarget == null)
                return;

            inputFeedbackOrigin = inputFeedbackTarget.localPosition;
            inputFeedbackRoutine = StartCoroutine(onBeat ? PlayOnBeatJump() : PlayMissShake());
        }

        private IEnumerator PlayOnBeatJump()
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, onBeatJumpDuration);
            while (elapsed < duration && inputFeedbackTarget != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float height = Mathf.Sin(progress * Mathf.PI) * onBeatJumpHeight;
                inputFeedbackTarget.localPosition = inputFeedbackOrigin + Vector3.up * height;
                yield return null;
            }

            CompleteInputFeedback();
        }

        private IEnumerator PlayMissShake()
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, missShakeDuration);
            while (elapsed < duration && inputFeedbackTarget != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float damping = 1f - progress;
                float offset = Mathf.Sin(elapsed * missShakeFrequency) * missShakeStrength * damping;
                inputFeedbackTarget.localPosition = inputFeedbackOrigin + Vector3.right * offset;
                yield return null;
            }

            CompleteInputFeedback();
        }

        private void CompleteInputFeedback()
        {
            if (inputFeedbackTarget != null)
                inputFeedbackTarget.localPosition = inputFeedbackOrigin;
            inputFeedbackRoutine = null;
            inputFeedbackTarget = null;
        }

        private void ResetInputFeedback()
        {
            if (inputFeedbackRoutine != null)
                StopCoroutine(inputFeedbackRoutine);
            if (inputFeedbackTarget != null)
                inputFeedbackTarget.localPosition = inputFeedbackOrigin;
            inputFeedbackRoutine = null;
            inputFeedbackTarget = null;
        }

        public void SetHealthDisplayVisible(bool visible)
        {
            SetHealthDisplayMode(visible, visible);
        }

        public void SetHealthDisplayMode(bool showBar, bool showNumber)
        {
            if (hpBackground != null)
                hpBackground.gameObject.SetActive(showBar);
            if (hpFill != null)
                hpFill.gameObject.SetActive(showBar);
            if (hpLabel != null)
                hpLabel.gameObject.SetActive(showNumber);
        }

        public void SetRoleLabel(string value)
        {
            if (roleLabel != null)
                roleLabel.text = value;
        }

        private void SpawnAttackEffect(
            FightAttackEffect.VisualStyle style,
            Color color,
            float verticalOffset,
            GameObject overridePrefab = null,
            float lifetimeOverride = -1f)
        {
            GameObject effectPrefab = overridePrefab != null ? overridePrefab : NormalAttackEffectPrefab;
            float lifetime = lifetimeOverride > 0f ? lifetimeOverride : attackEffectLifetime;
            EffectPlayer.SpawnAttack(
                displayName,
                team,
                normalAttackEffectSpawnPoint,
                attackEffectLocalOffset,
                fallbackEffectSprite,
                effectPrefab,
                lifetime,
                style,
                color,
                verticalOffset);
        }

        private bool SpawnConfiguredAbilityEffects(
            bool skill,
            FightAttackEffect.VisualStyle style,
            Color color,
            Transform[] anchors)
        {
            GameObject prefab = skill ? SkillEffectPrefab : NormalAttackEffectPrefab;
            if (prefab == null)
                return false;

            float lifetime = characterDefinition != null
                ? characterDefinition.AbilityEffectLifetime
                : attackEffectLifetime;
            if (anchors == null || anchors.Length == 0)
            {
                EffectPlayer.SpawnConfiguredAbility(prefab, CastEffectAnchor, lifetime, style, color);
                return true;
            }

            foreach (Transform anchor in anchors)
                EffectPlayer.SpawnConfiguredAbility(prefab, anchor != null ? anchor : CastEffectAnchor, lifetime, style, color);
            return true;
        }

        private void SpawnGuardLayer(
            Color color,
            float duration,
            float fromScale,
            float toScale,
            float rotationSpeed,
            float startingRotation)
        {
            EffectPlayer.SpawnGuard(
                displayName,
                actorRoot,
                fallbackEffectSprite,
                color,
                duration,
                fromScale,
                toScale,
                rotationSpeed,
                startingRotation);
        }

        private void SpawnAttackChargeLayer(Color color, bool attackBeat, bool enemy, float startingRotation)
        {
            EffectPlayer.SpawnAttackCharge(
                displayName,
                actorRoot,
                fallbackEffectSprite,
                color,
                attackBeat,
                enemy,
                startingRotation);
        }

        private void CacheLegacyPresentationReferences()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in renderers)
            {
                if (hpBackground == null && renderer.name == "HealthBackground")
                    hpBackground = renderer;
            }

            if (roleLabel != null)
                return;
            TextMesh[] labels = GetComponentsInChildren<TextMesh>(true);
            foreach (TextMesh label in labels)
            {
                if (label.name == "RoleAndInput")
                {
                    roleLabel = label;
                    return;
                }
            }
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
            combatAnimator = actorInstance.GetComponentInChildren<FightCharacterCombatAnimator>(true);
            FightCharacterDefinition spawnedDefinition = actorInstance.GetComponent<FightCharacterDefinition>();
            if (spawnedDefinition != null)
                characterDefinition = spawnedDefinition;
            CacheOrCreateRuntimeEffectAnchors();

            if (placeholderVisual != null)
                placeholderVisual.SetActive(false);
        }

        private void CacheOrCreateRuntimeEffectAnchors()
        {
            if (actorInstance == null)
                return;

            runtimeCastEffectAnchor = characterDefinition != null
                ? characterDefinition.AuthoredCastEffectAnchor
                : null;
            runtimeImpactEffectAnchor = characterDefinition != null
                ? characterDefinition.AuthoredImpactEffectAnchor
                : null;
            if (runtimeCastEffectAnchor == null)
                runtimeCastEffectAnchor = FindOrCreateRuntimeAnchor("VFX_CastAnchor", new Vector3(0f, 0.35f, -0.2f));
            if (runtimeImpactEffectAnchor == null)
                runtimeImpactEffectAnchor = FindOrCreateRuntimeAnchor("VFX_ImpactAnchor", new Vector3(0f, 1.1f, -0.25f));
        }

        private Transform FindOrCreateRuntimeAnchor(string anchorName, Vector3 localPosition)
        {
            Transform existing = actorInstance.transform.Find(anchorName);
            if (existing != null)
                return existing;

            GameObject anchor = new(anchorName);
            anchor.transform.SetParent(actorInstance.transform, false);
            anchor.transform.localPosition = localPosition;
            return anchor.transform;
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
            normalAbilityBehavior = definition.NormalAbilityType;
            normalAbilityPower = definition.NormalAbilityPower;
            attackPower = definition.AttackPower;
            skillPower = definition.SkillPower;
            skillBehavior = definition.SkillType;
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
            ResetInputFeedback();
            if (actorInstance == null)
                return;

            if (Application.isPlaying)
                Destroy(actorInstance);
            else
                DestroyImmediate(actorInstance);
            actorInstance = null;
            combatAnimator = null;
            runtimeCastEffectAnchor = null;
            runtimeImpactEffectAnchor = null;
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
            {
                if (team == UnitTeam.Enemy)
                {
                    hpLabel.text = $"{currentHp:0.#} / {maxHp:0.#}";
                    return;
                }

                string skillSummary = skillBehavior switch
                {
                    FightCharacterDefinition.AbilityBehavior.Guard => "GUARD",
                    FightCharacterDefinition.AbilityBehavior.HealParty => $"HEAL {skillPower:0.#}",
                    FightCharacterDefinition.AbilityBehavior.DamageAll => $"ALL {skillPower:0.#}",
                    FightCharacterDefinition.AbilityBehavior.GuardAndDamageFront => $"GUARD+DMG {skillPower:0.#}",
                    _ => $"DMG {skillPower:0.#}"
                };
                hpLabel.text = $"HP {currentHp:0.#}/{maxHp:0.#}  SKILL {skillSummary}";
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheLegacyPresentationReferences();
            onBeatJumpHeight = Mathf.Max(0f, onBeatJumpHeight);
            onBeatJumpDuration = Mathf.Max(0.01f, onBeatJumpDuration);
            missShakeStrength = Mathf.Max(0f, missShakeStrength);
            missShakeDuration = Mathf.Max(0.01f, missShakeDuration);
            missShakeFrequency = Mathf.Max(1f, missShakeFrequency);
            maxHp = QuantizePositive(maxHp);
            normalAbilityPower = normalAbilityBehavior == FightCharacterDefinition.AbilityBehavior.Guard
                ? 0f
                : QuantizePositive(normalAbilityPower);
            attackPower = QuantizePositive(attackPower);
            skillPower = skillBehavior == FightCharacterDefinition.AbilityBehavior.Guard
                ? 0f
                : QuantizePositive(skillPower);
        }
#endif

        private static float QuantizePositive(float value)
        {
            return Mathf.Max(0.5f, Mathf.Round(value * 2f) * 0.5f);
        }

        private static float QuantizeNonNegative(float value)
        {
            return value <= 0f ? 0f : QuantizePositive(value);
        }
    }
}
