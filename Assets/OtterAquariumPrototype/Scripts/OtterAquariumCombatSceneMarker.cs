using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    /// <summary>
    /// Marks the aquarium-owned copy of the original rhythm combat scene.
    /// Future combat scene work should be made in this copy rather than the
    /// legacy FightDemo scene.
    /// </summary>
    public sealed class OtterAquariumCombatSceneMarker : MonoBehaviour
    {
        [SerializeField] private string sourceScene = "Assets/FightDemo/Scenes/FightScene.unity";
        [SerializeField] private bool includesOceanWaves;

        public string SourceScene => sourceScene;
        public bool IncludesOceanWaves => includesOceanWaves;

        public void Configure(string source, bool hasOceanWaves)
        {
            sourceScene = source;
            includesOceanWaves = hasOceanWaves;
        }
    }
}
