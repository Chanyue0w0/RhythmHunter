using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>Follows position without inheriting sprite-card tilt, mirroring or nonuniform scale.</summary>
    [DefaultExecutionOrder(220)]
    public sealed class FightEffectPlacement : MonoBehaviour
    {
        private Transform anchor;
        private Vector3 worldOffset;
        public static Quaternion Rotation(FightEffectSettings settings, Transform prefab, Transform anchor)
        {
            if (settings == null || settings.spawnOrientation == FightEffectSettings.SpawnOrientation.Anchor) return anchor.rotation;
            if (settings.spawnOrientation == FightEffectSettings.SpawnOrientation.Prefab) return prefab.rotation;
            return Quaternion.Euler(0, prefab.eulerAngles.y, 0);
        }
        public void Bind(Transform source, Vector3 offset) { anchor = source; worldOffset = offset; }
        private void LateUpdate() { if (anchor != null) transform.position = anchor.position + worldOffset; }
    }
}
