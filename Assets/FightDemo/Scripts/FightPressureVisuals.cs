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

        private sealed class Entry
        {
            public GameObject actor, effect;
        }
        private readonly Dictionary<FightUnitSlot, Entry> effects = new();
        private readonly List<FightUnitSlot> stale = new();
        private bool active, ownsColor;
        private Color originalColor, fromColor, targetColor;
        private float originalIntensity, fromIntensity;
        private float transitionTime;

        private void LateUpdate()
        {
            bool next = combat != null && combat.isActiveAndEnabled && combat.PressurePhaseActive;
            if (next != active)
            {
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

        private void UpdateLight()
        {
            if (!ownsColor || mainLight == null) return;
            if (combat == null || !combat.IsPaused && !combat.TimingCalibrationActive)
                transitionTime += Time.deltaTime;
            float t = transitionSeconds <= 0 ? 1 : Mathf.Clamp01(transitionTime / transitionSeconds);
            mainLight.color = Color.Lerp(fromColor, targetColor, t);
            float targetIntensity = active ? originalIntensity * pressureIntensityMultiplier : originalIntensity;
            mainLight.intensity = Mathf.Lerp(fromIntensity, targetIntensity, t);
            if (!active && t >= 1) ownsColor = false;
        }
        private static void RemoveEffect(GameObject effect)
        {
            if (effect == null) return;
            effect.SetActive(false);
            Destroy(effect);
        }
        private void ClearEffects()
        {
            foreach (var entry in effects.Values) RemoveEffect(entry.effect);
            effects.Clear();
        }
        private void OnDisable()
        {
            ClearEffects();
            if (ownsColor && mainLight != null)
            {
                mainLight.color = originalColor;
                mainLight.intensity = originalIntensity;
            }
            ownsColor = active = false;
        }
    }
}
