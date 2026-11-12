using System;
using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    [DisallowMultipleComponent]
    public sealed class Art2SkillFlashLighting : MonoBehaviour
    {
        [Serializable] public sealed class Rule
        {
            public bool enabled = true;
            [Tooltip("Exact spawned prefab name, without (Clone).")]
            public string effectName;
            public Color color = new Color(1, .65f, .2f);
            [Min(0)] public float intensity = 1.5f;
            [Min(.1f)] public float range = 3;
            [Range(.05f, 1)] public float durationBeats = .3f;
            public Vector3 worldOffset = new Vector3(0, .7f, -.2f);
        }
        public bool enableSkillFlashes = true;
        public FightCombatController combat;
        [Range(1, 4)] public int maxConcurrentLights = 2;
        public Rule[] rules = Array.Empty<Rule>();
        private sealed class Flash { public Light light; public float remaining, duration, intensity; }
        private readonly Flash[] pool = new Flash[4];
        private FightCombatController subscribed;
        private double previousBeat;
        private bool hasBeat;

        private void OnEnable()
        {
            subscribed = combat;
            if (subscribed != null) subscribed.BattleEffectSpawned += OnEffect;
        }
        private void OnEffect(FightBattleEffect effect)
        {
            if (!enableSkillFlashes || combat == null || !combat.isActiveAndEnabled || combat.IsPaused || combat.TimingCalibrationActive || combat.BattleEnded || effect == null) return;
            string effectName = effect.name.Replace("(Clone)", "").Trim();
            foreach (var rule in rules)
            {
                if (!rule.enabled || string.IsNullOrEmpty(rule.effectName) || effectName != rule.effectName) continue;
                Flash flash = null;
                int count = Mathf.Clamp(maxConcurrentLights, 1, pool.Length);
                for (int i = 0; i < count; i++)
                {
                    if (pool[i] == null)
                    {
                        var go = new GameObject("SkillFlash_" + i); go.transform.SetParent(transform, false);
                        var light = go.AddComponent<Light>(); light.type = LightType.Point;
                        light.shadows = LightShadows.None; light.enabled = false;
                        pool[i] = new Flash { light = light };
                    }
                    if (flash == null || pool[i].remaining < flash.remaining) flash = pool[i];
                }
                flash.duration = flash.remaining = Mathf.Max(.05f, rule.durationBeats);
                flash.intensity = rule.intensity;
                flash.light.transform.position = effect.transform.position + rule.worldOffset;
                flash.light.color = rule.color; flash.light.range = rule.range;
                flash.light.intensity = rule.intensity; flash.light.enabled = true;
                return;
            }
        }
        private void LateUpdate()
        {
            if (!enableSkillFlashes || combat == null || !combat.isActiveAndEnabled || combat.BattleEnded || combat.WaveTransitionActive)
            { Clear(); return; }
            var clock = combat.BeatClock;
            if (clock == null || !clock.HasTimingAnchor || !clock.TryGetTimelinePositionMs(out int ms)) { Clear(); return; }
            double beat = clock.LatestBeat.GlobalBeat + (ms - clock.LatestBeat.TimelinePositionMs) / Math.Max(1d, clock.MillisecondsPerBeat);
            float delta = hasBeat ? (float)Math.Max(0, beat - previousBeat) : 0;
            previousBeat = beat; hasBeat = true;
            if (combat.IsPaused || combat.TimingCalibrationActive) return;
            for (int i = 0; i < pool.Length; i++)
            {
                var flash = pool[i]; if (flash == null) continue;
                flash.remaining = i >= maxConcurrentLights ? 0 : Mathf.Max(0, flash.remaining - delta);
                flash.light.enabled = flash.remaining > 0;
                flash.light.intensity = flash.intensity * Mathf.SmoothStep(0, 1, flash.remaining / Mathf.Max(.05f, flash.duration));
            }
        }
        private void Clear()
        {
            hasBeat = false;
            foreach (var flash in pool) if (flash != null) { flash.remaining = 0; flash.light.enabled = false; }
        }
        private void OnDisable()
        {
            if (subscribed != null) subscribed.BattleEffectSpawned -= OnEffect;
            subscribed = null; Clear();
        }
        private void OnDestroy()
        {
            foreach (var flash in pool)
                if (flash != null && flash.light != null)
                {
                    if (Application.isPlaying) Destroy(flash.light.gameObject);
                    else DestroyImmediate(flash.light.gameObject);
                }
        }
    }
}
