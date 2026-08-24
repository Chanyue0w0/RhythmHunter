using RhythmHunter.OtterAquariumPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    [InitializeOnLoad]
    public static class OtterShellBeatLabPlayModeValidation
    {
        private const double TimeoutSeconds = 22.0;
        private const string ActiveKey = "OtterShellBeatLab.Validation.Active";
        private const string StartedAtKey = "OtterShellBeatLab.Validation.StartedAt";
        private const string PassedKey = "OtterShellBeatLab.Validation.Passed";
        private const string FailureKey = "OtterShellBeatLab.Validation.Failure";

        static OtterShellBeatLabPlayModeValidation()
        {
            if (SessionState.GetBool(ActiveKey, false))
                RegisterCallbacks();
        }

        [MenuItem("Rhythm Hunter/Otter Aquarium/Run Shell Beat Lab Play Test")]
        public static void Run()
        {
            if (!OtterShellBeatLabValidation.ValidateScene(false))
                OtterShellBeatLabSceneBuilder.BuildScene();

            EditorSceneManager.OpenScene(OtterShellBeatLabSceneBuilder.ScenePath);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetFloat(StartedAtKey, 0f);
            SessionState.SetBool(PassedKey, false);
            SessionState.SetString(FailureKey, "Timed out before the first authored response target.");
            RegisterCallbacks();
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

            CleanupCallbacks();
            bool passed = SessionState.GetBool(PassedKey, false);
            string failure = SessionState.GetString(FailureKey, "Unknown validation failure.");
            ClearState();

            if (passed)
            {
                Debug.Log("OTTER_SHELL_BEAT_PLAY_TEST_PASS");
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"OTTER_SHELL_BEAT_PLAY_TEST_FAIL: {failure}");
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            double startedAt = SessionState.GetFloat(StartedAtKey, 0f);
            double elapsed = EditorApplication.timeSinceStartup - startedAt;
            OtterRhythmLevelRunner runner = Object.FindFirstObjectByType<OtterRhythmLevelRunner>();

            if (runner != null && runner.IsRunning && runner.TryGetNextTargetTimelineMs(out double targetTimelineMs)
                && runner.BeatClock.TryGetTimelinePositionMs(out int timelineMs))
            {
                double evaluatedMs = timelineMs + runner.LevelData.JudgementOffsetMs;
                if (Mathf.Abs((float)(evaluatedMs - targetTimelineMs)) <= 24f)
                {
                    OtterRhythmLevelRunner.JudgementResult result = runner.SubmitInput();
                    bool passed = result.Judgement == OtterRhythmLevelRunner.Grade.Perfect
                        && !result.ExtraInput
                        && runner.BeatClock.IsPlaying
                        && string.IsNullOrEmpty(runner.BeatClock.LastError);
                    SessionState.SetBool(PassedKey, passed);
                    SessionState.SetString(
                        FailureKey,
                        passed
                            ? string.Empty
                            : $"Authored input was {result.Judgement} at {result.DeltaMs:0.0} ms; FMOD={runner.BeatClock.LastError}");
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

            if (elapsed >= TimeoutSeconds)
                EditorApplication.ExitPlaymode();
        }

        private static void RegisterCallbacks()
        {
            CleanupCallbacks();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void CleanupCallbacks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
        }

        private static void ClearState()
        {
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(StartedAtKey);
            SessionState.EraseBool(PassedKey);
            SessionState.EraseString(FailureKey);
        }
    }
}
