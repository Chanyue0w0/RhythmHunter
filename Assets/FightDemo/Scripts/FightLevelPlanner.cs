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
        [SerializeField] private FightEnvironmentSelector environmentSelector;
        [Tooltip("Chapters available in the pause-menu test selector. Explicit references also include their stages in builds.")]
        [SerializeField] private FightChapterDefinition[] selectableChapters = System.Array.Empty<FightChapterDefinition>();
        public System.Collections.Generic.IReadOnlyList<FightChapterDefinition> SelectableChapters => selectableChapters;
        static string selectedScene;
        static FightChapterDefinition runtimeChapter;
        static int runtimeStage;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSessionSelection(){selectedScene=null;runtimeChapter=null;runtimeStage=0;}
        public bool TrySelectForReload(FightChapterDefinition target, int index, out string error)
        {
            if(target==null || (target!=chapter && System.Array.IndexOf(selectableChapters,target)<0))
            {error="Chapter is not available in this scene.";return false;}
            if(index<0||index>=target.Stages.Count||target.Stages[index]==null)
            {error="Stage is not configured.";return false;}
            var stage=target.Stages[index];
            if(!stage.Validate(out error))return false;
            var environment=stage.Environment?stage.Environment:target.DefaultEnvironment;
            if(environmentSelector&&!environmentSelector.CanApply(environment,out error))return false;
            selectedScene=gameObject.scene.path;runtimeChapter=target;runtimeStage=index;
            error=null;return true;
        }
        public FightChapterDefinition Chapter => chapter;
        public int StageIndex => stageIndex;
        public FightStageDefinition SelectedStage => chapter != null && stageIndex >= 0 && stageIndex < chapter.Stages.Count ? chapter.Stages[stageIndex] : null;
        public FightStageDefinition ActiveStage { get; private set; }
        public FightEnvironmentDefinition SelectedEnvironment => SelectedStage != null && SelectedStage.Environment != null
            ? SelectedStage.Environment : chapter != null ? chapter.DefaultEnvironment : null;
        public string ConfigurationError { get; private set; }

        private void Awake()
        {
            if(runtimeChapter&&selectedScene==gameObject.scene.path){chapter=runtimeChapter;stageIndex=runtimeStage;}
            if (chapter == null) return; // Legacy scenes retain their own roster and song.
            var stage = SelectedStage;
            if (stage == null) { Fail("Selected Chapter/Stage is missing."); return; }
            if (roster == null || beatClock == null) { Fail("Roster or song clock reference is missing."); return; }
            if (beatClock.IsReady) { Fail("Stage must be selected before the song clock initializes."); return; }
            if (environmentSelector != null && !environmentSelector.CanApply(SelectedEnvironment, out string environmentError))
            { Fail(environmentError); return; }
            if (!roster.TryConfigureStage(stage, out string error)) { Fail(error); return; }
            if (environmentSelector != null) environmentSelector.TryApply(SelectedEnvironment, out _);
            beatClock.Configure(stage.MusicEventPath, beatClock.MusicStartDelaySeconds, false, beatClock.MusicVolume);
            // Guided practice may continue indefinitely. Reuse the song's native loop,
            // without enabling a stage deadline or overtime Pressure.
            beatClock.ConfigureStageSections(stage.UseNormalEndDeadline || stage.BossStage || stage.TutorialLesson != null);
            ActiveStage = stage;
        }
        private void Fail(string message)
        {
            ConfigurationError = message;
            Debug.LogError("[FightLevelPlanner] " + message + " Existing scene configuration retained.", this);
        }
    }
}
