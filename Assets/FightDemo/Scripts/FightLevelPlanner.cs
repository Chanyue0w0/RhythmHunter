using UnityEngine;
using RhythmHunter.RhythmDemo;

namespace RhythmHunter.FightDemo
{
    // Configure data before roster (-500) spawns and before FMOD initializes.
    [DefaultExecutionOrder(-1000), DisallowMultipleComponent]
    public sealed class FightLevelPlanner : MonoBehaviour
    {
        [SerializeField] private FightChapterDefinition chapter;
        [Tooltip("Zero-based index in Chapter.Stages. Selected before entering Play Mode.")]
        [SerializeField, Min(0)] private int stageIndex;
        [SerializeField] private FightRosterManager roster;
        [SerializeField] private FmodBeatClock beatClock;
        public FightChapterDefinition Chapter => chapter;
        public int StageIndex => stageIndex;
        public FightStageDefinition SelectedStage => chapter != null && stageIndex >= 0 && stageIndex < chapter.Stages.Count ? chapter.Stages[stageIndex] : null;
        public FightStageDefinition ActiveStage { get; private set; }
        public string ConfigurationError { get; private set; }

        private void Awake()
        {
            if (chapter == null) return; // Legacy scenes retain their own roster and song.
            var stage = SelectedStage;
            if (stage == null) { Fail("Selected Chapter/Stage is missing."); return; }
            if (roster == null || beatClock == null) { Fail("Roster or song clock reference is missing."); return; }
            if (beatClock.IsReady) { Fail("Stage must be selected before the song clock initializes."); return; }
            if (!roster.TryConfigureStage(stage, out string error)) { Fail(error); return; }
            beatClock.Configure(stage.MusicEventPath, beatClock.MusicStartDelaySeconds, false, beatClock.MusicVolume);
            beatClock.ConfigureStageSections(stage.UseNormalEndDeadline);
            ActiveStage = stage;
        }
        private void Fail(string message)
        {
            ConfigurationError = message;
            Debug.LogError("[FightLevelPlanner] " + message + " Existing scene configuration retained.", this);
        }
    }
}
