using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    [CreateAssetMenu(menuName = "Rhythm Hunter/Levels/Tutorial Lesson")]
    public sealed class FightTutorialLesson : ScriptableObject
    {
        public enum Goal { Dialogue, OnBeatCalls, NormalHits, ChargedHits, FreePractice }
        [Serializable]
        public sealed class Step
        {
            public string title;
            [TextArea(3, 8)] public string text;
            public Goal goal;
            [Min(1)] public int required = 1;
            public bool allowSwordCharge;
        }
        public List<Step> steps = new();
    }
}
