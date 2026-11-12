using System;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>Flight uses the existing song timeline; collision never decides gameplay hits.</summary>
    public sealed class FightBeatProjectile : MonoBehaviour
    {
        private FightCombatController fight;
        private FightBattleEffect owner;
        private Transform target;
        private Vector3 start;
        private Vector3 targetOffset;
        private Vector3 TargetPosition => target.position + targetOffset;
        private double progress;
        private float duration;
        private int lastMs;
        private Func<bool> valid;
        private Action impact;
        private float fadeDuration;
        private float impactDelay;
        private bool arrived;
        private Vector3 startScale, endScale;
        private SpriteRenderer[] sprites;
        private Color[] colors;

        public void Initialize(FightCombatController controller, Transform destination, float beats,
            Func<bool> isValid, Action onImpact, float impactFadeBeats = 0, Vector3? impactScale = null,
            Vector3 destinationOffset = default, float impactCallbackDelayBeats = 0)
        {
            fight = controller; target = destination; duration = Mathf.Max(.01f, beats);
            valid = isValid; impact = onImpact; start = transform.position;
            targetOffset = destinationOffset;
            progress = 0; arrived = false;
            fadeDuration = Mathf.Max(0, impactFadeBeats);
            impactDelay = Mathf.Max(0, impactCallbackDelayBeats);
            startScale = transform.localScale; endScale = impactScale ?? startScale;
            if (fadeDuration > 0)
            {
                sprites = GetComponentsInChildren<SpriteRenderer>(true);
                colors = new Color[sprites.Length];
                for (int i = 0; i < sprites.Length; i++) colors[i] = sprites[i].color;
            }
            owner = GetComponent<FightBattleEffect>() ?? gameObject.AddComponent<FightBattleEffect>();
            owner.Configure(fight, false, 0);
            fight.BeatClock.TryGetTimelinePositionMs(out lastMs);
        }

        private void Update()
        {
            if (fight == null || target == null || valid == null || !valid()) { if (owner) owner.Cancel(); return; }
            if (!fight.BeatClock.TryGetTimelinePositionMs(out int now)) return;
            int delta = Math.Max(0, now - lastMs); lastMs = now;
            if (owner.Frozen) return;
            Advance(delta / Math.Max(1.0, fight.BeatClock.MillisecondsPerBeat));
        }

        private void Advance(double beats)
        {
            progress += beats;
            if (arrived) { UpdateImpactFade(); return; }
            float t = Mathf.Clamp01((float)(progress / duration));
            transform.position = Vector3.Lerp(start, TargetPosition, t);
            if (t < 1) return;
            arrived = true;
            progress = Math.Max(0, progress - duration);
            UpdateImpactFade();
        }

        private void UpdateImpactFade()
        {
            if (target != null) transform.position = TargetPosition;
            if (progress >= impactDelay && impact != null)
            {
                var callback = impact; impact = null;
                callback.Invoke();
                if (this == null || !gameObject.activeInHierarchy) return;
            }
            float t = fadeDuration > 0 ? Mathf.Clamp01((float)(progress / fadeDuration)) : 1;
            float eased = Mathf.SmoothStep(0, 1, t);
            if (fadeDuration > 0) transform.localScale = Vector3.Lerp(startScale, endScale, eased);
            for (int i = 0; fadeDuration > 0 && i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                var tint = colors[i]; tint.a *= 1 - eased; sprites[i].color = tint;
            }
            if (progress >= Math.Max(fadeDuration, impactDelay)) { enabled = false; if (owner) owner.Cancel(); }
        }
    }
}
