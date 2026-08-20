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
        [SerializeField] private string sourceScene = "Assets/PirateOceanPrototype/Scenes/PirateFightScene.unity";
        [SerializeField] private bool includesOceanWaves;
        [SerializeField] private bool includesCinematicCamera = true;
        [SerializeField] private int layoutRevision = 4;

        public string SourceScene => sourceScene;
        public bool IncludesOceanWaves => includesOceanWaves;
        public bool IncludesCinematicCamera => includesCinematicCamera;
        public int LayoutRevision => layoutRevision;

        public void Configure(string source, bool hasOceanWaves, bool hasCinematicCamera, int revision)
        {
            sourceScene = source;
            includesOceanWaves = hasOceanWaves;
            includesCinematicCamera = hasCinematicCamera;
            layoutRevision = revision;
        }
    }
}
