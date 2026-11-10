using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    [CreateAssetMenu(menuName = "Rhythm Hunter/Weapons/Attachment Data")]
    public sealed class WeaponAttachmentData : ScriptableObject
    {
        [Serializable]
        public struct Pose
        {
            public string frameId;
            public Vector2 position;
            public float rotation;
            public bool visible;
        }
        [SerializeField] private List<Pose> poses = new();
        private readonly Dictionary<string, Pose> lookup = new();
        private bool indexed;
        public bool TryGet(string id, out Pose pose)
        {
            if (!indexed)
            {
                lookup.Clear();
                foreach (var entry in poses) if (!string.IsNullOrEmpty(entry.frameId)) lookup[entry.frameId] = entry;
                indexed = true;
            }
            pose = default;
            return !string.IsNullOrEmpty(id) && lookup.TryGetValue(id, out pose);
        }
        public void Set(Pose pose)
        {
            if (string.IsNullOrEmpty(pose.frameId)) return;
            int index = poses.FindIndex(p => p.frameId == pose.frameId);
            if (index >= 0) poses[index] = pose; else poses.Add(pose);
            indexed = false;
        }
        private void OnValidate() => indexed = false;
        private void OnEnable() => indexed = false;
    }
}
