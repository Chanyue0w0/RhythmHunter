using System;
using System.Collections.Generic;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>Decorative movement sampled from the existing song clock; owns no timer.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    public sealed class SceneBeatMotionManager : MonoBehaviour
    {
        [Serializable]
        public sealed class MotionTarget
        {
            public bool enabled = true;
            public Transform target;
            [Tooltip("World: X左右、Y高度、Z深度。Self: 沿物件本身的軸向。")]
            public Space directionSpace = Space.World;
            [Tooltip("震動方向；只取方向，長度不影響幅度。")]
            public Vector3 direction = Vector3.up;
            [Tooltip("世界座標單位的最大位移。")]
            [Min(0)] public float amplitude = .04f;
            [Tooltip("每拍前多少拍用於震動，其餘時間回到原位。")]
            [Range(.05f, 1)] public float durationInBeats = .5f;
            [Tooltip("單次震動期間往返的次數。")]
            [Range(1, 8)] public int oscillations = 1;
        }

        [Tooltip("指定目前戰鬥使用的總節拍時鐘，不會建立額外 BPM 或計時器。")]
        [SerializeField] private FmodBeatClock beatClock;
        [SerializeField] private bool motionEnabled = true;
        [SerializeField] private List<MotionTarget> targets = new List<MotionTarget>();

        // Only subtract our own offset, preserving the authored pose and avoiding drift.
        // Keeping last frame's targets also restores objects removed from the Inspector list.
        private readonly Dictionary<Transform, Vector3> appliedOffsets = new Dictionary<Transform, Vector3>();

        private void LateUpdate()
        {
            RestoreOffsets();
            if (!motionEnabled || beatClock == null || !beatClock.isActiveAndEnabled ||
                !beatClock.IsPlaying || !beatClock.TryGetBeatPhase(out float phase)) return;

            foreach (var entry in targets)
            {
                if (entry == null || !entry.enabled || entry.target == null ||
                    !entry.target.gameObject.activeInHierarchy || appliedOffsets.ContainsKey(entry.target)) continue;

                float duration = Mathf.Clamp(entry.durationInBeats, .05f, 1f);
                if (phase >= duration) continue;
                float progress = Mathf.Clamp01(phase / duration);
                // Smooth endpoints and decay keep the next beat from snapping the sprite.
                float wave = Mathf.Sin(progress * Mathf.PI * 2f * Mathf.Clamp(entry.oscillations, 1, 8));
                float amount = wave * (1f - progress) * (1f - progress) * Mathf.Max(0, entry.amplitude);
                Vector3 worldDirection = entry.directionSpace == Space.World
                    ? entry.direction.normalized : entry.target.TransformDirection(entry.direction.normalized);
                Vector3 worldOffset = worldDirection * amount;
                Vector3 localOffset = entry.target.parent != null
                    ? entry.target.parent.InverseTransformVector(worldOffset) : worldOffset;
                entry.target.localPosition += localOffset;
                appliedOffsets.Add(entry.target, localOffset);
            }
        }

        private void OnDisable() => RestoreOffsets();

        private void RestoreOffsets()
        {
            foreach (var pair in appliedOffsets)
                if (pair.Key != null) pair.Key.localPosition -= pair.Value;
            appliedOffsets.Clear();
        }
    }
}
