using System;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    [CreateAssetMenu(menuName = "Rhythm Hunter/Hero Guide")]
    public sealed class FightHeroGuide : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public Sprite portrait;
            public string characterName, profession, role;
            [TextArea] public string abilities;
        }
        public Entry[] heroes = Array.Empty<Entry>();
    }
}
