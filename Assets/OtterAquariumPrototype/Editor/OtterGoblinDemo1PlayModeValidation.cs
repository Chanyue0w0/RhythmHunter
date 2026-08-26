using RhythmHunter.OtterAquariumPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    [InitializeOnLoad]
    public static class OtterGoblinDemo1PlayModeValidation
    {
        private const double TimeoutSeconds = 15.0;
        private const string ActiveKey = "OtterGoblinDemo1.Validation.Active";
        private const string StartedAtKey = "OtterGoblinDemo1.Validation.StartedAt";
        private const string PassedKey = "OtterGoblinDemo1.Validation.Passed";
        private const string FailureKey = "OtterGoblinDemo1.Validation.Failure";
        private const string PreviousSceneKey = "OtterGoblinDemo1.Validation.PreviousScene";

        static OtterGoblinDemo1PlayModeValidation()
        {
            if (SessionState.GetBool(ActiveKey, false))
                Register();
        }

        [MenuItem("Rhythm Hunter/Otter Aquarium/Run Zoo Goblin Demo 1 Play Test")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!OtterGoblinDemo1Validation.ValidateScene(false))
                OtterGoblinDemo1SceneBuilder.BuildScene();

            UnityEngine.SceneManagement.Scene previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (previous.IsValid() && previous.isDirty)
            {
                Debug.LogWarning("OTTER_GOBLIN_DEMO1_PLAY_TEST_SKIPPED: The active scene has unsaved changes.");
                return;
            }
            SessionState.SetString(PreviousSceneKey, previous.IsValid() ? previous.path : string.Empty);
            EditorSceneManager.OpenScene(OtterGoblinDemo1SceneBuilder.ScenePath);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetFloat(StartedAtKey, 0f);
            SessionState.SetBool(PassedKey, false);
            SessionState.SetString(FailureKey, "Timed out before the first attack target.");
            Register();
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                return;
            }
            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            Unregister();
            bool passed = SessionState.GetBool(PassedKey, false);
            string failure = SessionState.GetString(FailureKey, "Unknown failure.");
            string previousScene = SessionState.GetString(PreviousSceneKey, string.Empty);
            Clear();
            if (!string.IsNullOrEmpty(previousScene) && AssetDatabase.LoadAssetAtPath<SceneAsset>(previousScene) != null)
                EditorSceneManager.OpenScene(previousScene);
            if (passed)
                Debug.Log("OTTER_GOBLIN_DEMO1_PLAY_TEST_PASS");
            else
                Debug.LogError($"OTTER_GOBLIN_DEMO1_PLAY_TEST_FAIL: {failure}");
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            OtterGoblinDemo1Runner runner = Object.FindFirstObjectByType<OtterGoblinDemo1Runner>();
            if (runner != null && runner.TryGetNextTargetTimelineMs(out double targetMs)
                && runner.BeatClock.TryGetTimelinePositionMs(out int timelineMs))
            {
                double evaluatedMs = timelineMs + runner.LevelData.JudgementOffsetMs;
                if (Mathf.Abs((float)(evaluatedMs - targetMs)) <= 22f)
                {
                    OtterGoblinDemo1Runner.JudgementResult result = runner.SubmitInput();
                    bool passed = result.Judgement == OtterGoblinDemo1Runner.Grade.Perfect
                        && !result.ExtraInput
                        && result.Health == 3
                        && Object.FindFirstObjectByType<RhythmTimelineProjectile>() != null
                        && runner.BeatClock.IsPlaying
                        && string.IsNullOrEmpty(runner.BeatClock.LastError);
                    SessionState.SetBool(PassedKey, passed);
                    SessionState.SetString(FailureKey, passed
                        ? string.Empty
                        : $"Input={result.Judgement}, delta={result.DeltaMs:0.0}ms, HP={result.Health}, FMOD={runner.BeatClock.LastError}");
                    EditorApplication.ExitPlaymode();
                    return;
                }
            }

            if (runner != null && !string.IsNullOrEmpty(runner.BeatClock.LastError))
            {
                SessionState.SetString(FailureKey, runner.BeatClock.LastError);
                EditorApplication.ExitPlaymode();
                return;
            }

            double start = SessionState.GetFloat(StartedAtKey, 0f);
            if (EditorApplication.timeSinceStartup - start >= TimeoutSeconds)
                EditorApplication.ExitPlaymode();
        }

        private static void Register()
        {
            Unregister();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void Unregister()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
        }

        private static void Clear()
        {
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(StartedAtKey);
            SessionState.EraseBool(PassedKey);
            SessionState.EraseString(FailureKey);
            SessionState.EraseString(PreviousSceneKey);
        }
    }
}
