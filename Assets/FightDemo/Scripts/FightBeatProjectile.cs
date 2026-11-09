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
        private double progress;
        private float duration;
        private int lastMs;
        private Func<bool> valid;
        private Action impact;

        public void Initialize(FightCombatController controller, Transform destination, float beats,
            Func<bool> isValid, Action onImpact)
        {
            fight = controller; target = destination; duration = Mathf.Max(.01f, beats);
            valid = isValid; impact = onImpact; start = transform.position;
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
            progress += delta / Math.Max(1.0, fight.BeatClock.MillisecondsPerBeat);
            float t = Mathf.Clamp01((float)(progress / duration));
            transform.position = Vector3.Lerp(start, target.position, t);
            if (t < 1) return;
            var callback = impact; impact = null;
            enabled = false;
            callback?.Invoke();
            if (owner) owner.Cancel();
        }
    }
}
