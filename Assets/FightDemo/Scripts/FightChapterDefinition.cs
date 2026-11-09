using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    [CreateAssetMenu(menuName = "Rhythm Hunter/Levels/Chapter")]
    public sealed class FightChapterDefinition : ScriptableObject
    {
        [SerializeField] private string chapterId = "Chapter01";
        [SerializeField] private string displayName = "Chapter 1";
        [SerializeField] private List<FightStageDefinition> stages = new();
        public string ChapterId => chapterId;
        public string DisplayName => displayName;
        public IReadOnlyList<FightStageDefinition> Stages => stages;
    }
}
