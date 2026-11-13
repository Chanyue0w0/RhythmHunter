using System.Collections.Generic;
using UnityEngine;


namespace RhythmHunter.FightDemo
{
    // Presentation only: pressure timing remains owned by the combat controller.
    [DisallowMultipleComponent]
    public sealed class FightPressureVisuals : MonoBehaviour
    {
        [SerializeField] private FightCombatController combat;
        [Header("Pressure Main Light")]
        [SerializeField] private Light mainLight;
        [SerializeField] private Color pressureColor = new Color(1f, .025f, .025f);
        [Tooltip("密集攻擊期間的主光強度倍率。0.6 = 原本強度的 60%；1 = 不降低。可在 Play 時調整。")]
        [SerializeField, Range(0, 1)] private float pressureIntensityMultiplier = .6f;
        [SerializeField, Min(0)] private float transitionSeconds = .15f;
        [Header("Enemy Electricity")]
        [SerializeField] private GameObject enemyEffectPrefab;
        [Tooltip("Offset in the enemy impact anchor's local directions, without inheriting its scale.")]
        [SerializeField] private Vector3 effectOffset;
        [Tooltip("Added to the VFX prefab's authored world rotation; avoids actor-card tilt and scale distortion.")]
        [SerializeField] private Vector3 effectRotationOffset;
        [SerializeField] private Vector3 effectScaleMultiplier = Vector3.one;

        [Header("Pressure Enemy Head Effect")]
        [SerializeField] private bool showAngryEffect = true;
        [SerializeField] private GameObject angryEffectPrefab;
        [Tooltip("頭頂特效的世界座標偏移；不繼承角色傾斜角度與縮放。")]
        [SerializeField] private Vector3 angryEffectOffset;
        [SerializeField] private Vector3 angryEffectRotationOffset;
        [SerializeField] private Vector3 angryEffectScaleMultiplier = Vector3.one;

        private sealed class Entry
        {
            public GameObject actor, effect;
        }
        private readonly Dictionary<FightUnitSlot, Entry> effects = new();
        private readonly Dictionary<FightUnitSlot, Entry> angryEffects = new();
        private readonly List<FightUnitSlot> stale = new();
        public FightCombatController CombatController => combat;
        public float PressureLightBlend { get; private set; }
        private float fromBlend;
        private bool active, ownsColor;
        private Color originalColor, fromColor, targetColor;
        private float originalIntensity, fromIntensity;
        private float transitionTime;

        private void LateUpdate()
        {
            bool next = combat != null && combat.isActiveAndEnabled && combat.PressureVisualsActive;
            if (next != active)
            {
                fromBlend = PressureLightBlend;
                transitionTime = 0;
                active = next;
                if (mainLight != null)
                {
                    if (active && !ownsColor)
                    {
                        originalColor = mainLight.color;
                        originalIntensity = mainLight.intensity;
                        ownsColor = true;
                    }
                    fromColor = mainLight.color;
                    fromIntensity = mainLight.intensity;
                    targetColor = active ? pressureColor : originalColor;
                    transitionTime = 0;
                }
                if (!active) ClearEffects();
            }
            UpdateLight();
            UpdateAngryEffects();
            if (!active) return;

            stale.Clear();
            foreach (var pair in effects)
                if (!Eligible(pair.Key) || pair.Value.actor != pair.Key.ActorInstance || pair.Value.effect == null)
                    stale.Add(pair.Key);
            foreach (var slot in stale)
            {
                RemoveEffect(effects[slot].effect);
                effects.Remove(slot);
            }
            foreach (var slot in combat.PressureEnemySlots)
            {
                if (!Eligible(slot)) continue;
                if (!effects.TryGetValue(slot, out var entry))
                {
                    if (enemyEffectPrefab == null) continue;
                    var effect = Instantiate(enemyEffectPrefab);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effect, gameObject.scene);
                    effect.name = "PressureElectric_" + slot.name;
                    // Reuse battle effect pause/cleanup instead of adding another clock.
                    (effect.GetComponent<FightBattleEffect>() ?? effect.AddComponent<FightBattleEffect>()).Configure(combat, true, 0);
                    entry = new Entry { actor = slot.ActorInstance, effect = effect };
                    effects.Add(slot, entry);
                }
                var anchor = slot.ImpactEffectAnchor != null ? slot.ImpactEffectAnchor : slot.transform;
                entry.effect.transform.SetPositionAndRotation(anchor.position + anchor.rotation * effectOffset,
                    Quaternion.Euler(effectRotationOffset) * enemyEffectPrefab.transform.rotation);
                entry.effect.transform.localScale = Vector3.Scale(enemyEffectPrefab.transform.localScale, effectScaleMultiplier);
            }
        }

        private static bool Eligible(FightUnitSlot slot) => slot != null && slot.isActiveAndEnabled &&
            slot.HasCharacter && slot.CurrentHp > 0 && slot.ActorInstance != null && slot.ActorInstance.activeInHierarchy;

        private void UpdateAngryEffects()
        {
            // Unlike the lighting warning, anger starts only with actual Pressure.
            if (!showAngryEffect || angryEffectPrefab == null || combat == null || !combat.isActiveAndEnabled || !combat.PressurePhaseActive)
            {
                ClearAngryEffects();
                return;
            }
            stale.Clear();
            foreach (var pair in angryEffects)
                if (!Eligible(pair.Key) || pair.Value.actor != pair.Key.ActorInstance || pair.Value.effect == null)
                    stale.Add(pair.Key);
            foreach (var slot in stale)
            {
                RemoveEffect(angryEffects[slot].effect);
                angryEffects.Remove(slot);
            }
            foreach (var slot in combat.PressureEnemySlots)
            {
                if (!Eligible(slot)) continue;
                if (!angryEffects.TryGetValue(slot, out var entry))
                {
                    var effect = Instantiate(angryEffectPrefab);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effect, gameObject.scene);
                    effect.name = "PressureAngry_" + slot.name;
                    entry = new Entry { actor = slot.ActorInstance, effect = effect };
                    angryEffects.Add(slot, entry);
                    (effect.GetComponent<FightBattleEffect>() ?? effect.AddComponent<FightBattleEffect>()).Configure(combat, true, 0);
                }
                var anchor = slot.HeadAnchor != null ? slot.HeadAnchor : slot.ImpactEffectAnchor != null ? slot.ImpactEffectAnchor : slot.transform;
                entry.effect.transform.SetPositionAndRotation(anchor.position + angryEffectOffset,
                    Quaternion.Euler(angryEffectRotationOffset) * angryEffectPrefab.transform.rotation);
                entry.effect.transform.localScale = Vector3.Scale(angryEffectPrefab.transform.localScale, angryEffectScaleMultiplier);
            }
        }

        private void ClearAngryEffects()
        {
            foreach (var entry in angryEffects.Values) RemoveEffect(entry.effect);
            angryEffects.Clear();
        }

        private void UpdateLight()
        {

            if (combat == null || !combat.IsPaused && !combat.TimingCalibrationActive)
                transitionTime += Time.deltaTime;
            float t = transitionSeconds <= 0 ? 1 : Mathf.Clamp01(transitionTime / transitionSeconds);
            PressureLightBlend = Mathf.Lerp(fromBlend, active ? 1 : 0, t);
            if (!ownsColor || mainLight == null) return;
            mainLight.color = Color.Lerp(fromColor, targetColor, t);
            float targetIntensity = active ? originalIntensity * pressureIntensityMultiplier : originalIntensity;
            mainLight.intensity = Mathf.Lerp(fromIntensity, targetIntensity, t);
            if (!active && t >= 1) ownsColor = false;
        }
        private static void RemoveEffect(GameObject effect)
        {
            if (effect == null) return;
            effect.SetActive(false);
            if (Application.isPlaying) Destroy(effect); else DestroyImmediate(effect);
        }
        private void ClearEffects()
        {
            foreach (var entry in effects.Values) RemoveEffect(entry.effect);
            effects.Clear();
        }
        private void OnDisable()
        {
            ClearEffects();
            ClearAngryEffects();
            if (ownsColor && mainLight != null)
            {
                mainLight.color = originalColor;
                mainLight.intensity = originalIntensity;
            }
            PressureLightBlend = 0;
            ownsColor = active = false;
        }
    }
}
