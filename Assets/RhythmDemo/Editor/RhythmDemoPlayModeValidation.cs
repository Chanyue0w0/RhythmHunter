using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.RhythmDemoEditor
{
    [InitializeOnLoad]
    public static class RhythmDemoPlayModeValidation
    {
        private const double TimeoutSeconds = 15.0;
        private const string ActiveKey = "RhythmDemo.Validation.Active";
        private const string StartedAtKey = "RhythmDemo.Validation.StartedAt";
        private const string AttemptedKey = "RhythmDemo.Validation.Attempted";
        private const string PassedKey = "RhythmDemo.Validation.Passed";
        private const string FailureKey = "RhythmDemo.Validation.Failure";

        static RhythmDemoPlayModeValidation()
        {
            if (SessionState.GetBool(ActiveKey, false))
                RegisterCallbacks();
        }

        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RhythmDemoSceneBuilder.ScenePath) == null)
                RhythmDemoSceneBuilder.BuildScene();

            EditorSceneManager.OpenScene(RhythmDemoSceneBuilder.ScenePath);

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetFloat(StartedAtKey, 0f);
            SessionState.SetBool(AttemptedKey, false);
            SessionState.SetBool(PassedKey, false);
            SessionState.SetString(
                FailureKey,
                "Validation timed out before FMOD delivered stable beat callbacks.");

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
            bool validationPassed = SessionState.GetBool(PassedKey, false);
            string failureReason = SessionState.GetString(FailureKey, "Unknown validation failure.");
            ClearSessionState();

            if (validationPassed)
            {
                Debug.Log("RHYTHM_DEMO_SMOKE_TEST_PASS");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"RHYTHM_DEMO_SMOKE_TEST_FAIL: {failureReason}");
                EditorApplication.Exit(1);
            }
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            double startedAt = SessionState.GetFloat(StartedAtKey, 0f);
            double elapsed = EditorApplication.timeSinceStartup - startedAt;
            FmodBeatClock clock = Object.FindFirstObjectByType<FmodBeatClock>();
            FmodRhythmJudge judge = Object.FindFirstObjectByType<FmodRhythmJudge>();

            if (clock != null && judge != null && clock.ReceivedBeatCount >= 3 &&
                !SessionState.GetBool(AttemptedKey, false) &&
                clock.TryGetBeatPhase(out float phase) && phase < 0.08f)
            {
                SessionState.SetBool(AttemptedKey, true);
                FmodRhythmJudge.Result result = judge.JudgeNow();

                if (result.Judgement == FmodRhythmJudge.Grade.Perfect)
                {
                    bool passed = clock.IsPlaying && string.IsNullOrEmpty(clock.LastError);
                    SessionState.SetBool(PassedKey, passed);
                    SessionState.SetString(FailureKey, passed
                        ? string.Empty
                        : $"Clock state was invalid. Playing={clock.IsPlaying}, Error={clock.LastError}");
                }
                else
                {
                    SessionState.SetString(
                        FailureKey,
                        $"Automated near-beat input was {result.Judgement} at {result.DeltaMs:0.0} ms.");
                }

                EditorApplication.ExitPlaymode();
                return;
            }

            if (clock != null && !string.IsNullOrEmpty(clock.LastError))
            {
                SessionState.SetString(FailureKey, clock.LastError);
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

        private static void ClearSessionState()
        {
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(StartedAtKey);
            SessionState.EraseBool(AttemptedKey);
            SessionState.EraseBool(PassedKey);
            SessionState.EraseString(FailureKey);
        }
    }
}
