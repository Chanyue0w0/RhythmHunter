using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    [CreateAssetMenu(menuName = "Rhythm Hunter/Levels/Chapter")]
    public sealed class FightChapterDefinition : ScriptableObject
    {
        [SerializeField] private string chapterId = "Chapter01";
        [SerializeField] private string displayName = "Chapter 1";
        [Tooltip("Used by Stages whose Environment is unassigned. A Stage override takes priority.")]
        [SerializeField] private FightEnvironmentDefinition defaultEnvironment;
        [SerializeField] private List<FightStageDefinition> stages = new();
        public string ChapterId => chapterId;
        public string DisplayName => displayName;
        public FightEnvironmentDefinition DefaultEnvironment => defaultEnvironment;
        public IReadOnlyList<FightStageDefinition> Stages => stages;
    }
}
