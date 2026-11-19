using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    [CreateAssetMenu(menuName = "Rhythm Hunter/Levels/Tutorial Lesson")]
    public sealed class FightTutorialLesson : ScriptableObject
    {
        public enum Goal { Dialogue, OnBeatCalls, NormalHits, ChargedHits, FreePractice, SuccessfulBlocks, SlimeHits }
        [Serializable]
        public sealed class Step
        {
            public string title;
            [TextArea(3, 8)] public string text;
            public Goal goal;
            [Min(1)] public int required = 1;
            public bool allowSwordCharge;
            public bool allowEnemyAttacks;
            public bool suppressMageRetaliation;
        }
        public bool controlEnemyAttacks;
        public bool preventPlayerDeath;
        [Min(1)] public int enemyAttackIntervalBeats = 4;
        public List<Step> steps = new();
    }
}
