using UnityEngine;

namespace RhythmHunter.FightDemo
{
    // Stable identity for Stage selection. Scene bindings belong to the environment
    // presenter, not to shared Stage assets or the combat system.
    [CreateAssetMenu(menuName = "Rhythm Hunter/Levels/Environment")]
    public sealed class FightEnvironmentDefinition : ScriptableObject
    {
        [SerializeField] private string environmentId = "Cave";
        [SerializeField] private string displayName = "Cave";
        public string EnvironmentId => environmentId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? environmentId : displayName;
    }
}
