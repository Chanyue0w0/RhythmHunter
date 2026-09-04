using System;
using RhythmHunter.RhythmArena;
using RhythmHunter.TopDownBeatCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.TopDownBeatCombatEditor
{
    [InitializeOnLoad]
    public static class TopDownBeatCombatValidation
    {
        private const string ActiveKey = "TopDownBeatCombat.Validation.Active";
        private const string StartedKey = "TopDownBeatCombat.Validation.Started";
        private const string StateKey = "TopDownBeatCombat.Validation.State";
        private const string YKey = "TopDownBeatCombat.Validation.StartY";
        private const string PassedKey = "TopDownBeatCombat.Validation.Passed";
        private const string FailureKey = "TopDownBeatCombat.Validation.Failure";
        private const double TimeoutSeconds = 16.0;

        static TopDownBeatCombatValidation()
        {
            if (SessionState.GetBool(ActiveKey, false))
                RegisterCallbacks();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TopDownBeatCombatSceneBuilder.ScenePath) == null)
                EditorApplication.delayCall += BuildMissingScene;
            else
                EditorApplication.delayCall += TopDownBeatCombatSceneBuilder.ApplyBuildSettings;
        }

        [MenuItem("Rhythm Hunter/Validate Top Down Beat Combat Prototype")]
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TopDownBeatCombatSceneBuilder.ScenePath) == null)
                TopDownBeatCombatSceneBuilder.BuildScene();

            EditorSceneManager.OpenScene(TopDownBeatCombatSceneBuilder.ScenePath);
            ValidateStructure();
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetFloat(StartedKey, 0f);
            SessionState.SetInt(StateKey, 0);
            SessionState.SetBool(PassedKey, false);
            SessionState.SetString(FailureKey, "Validation timed out.");
            RegisterCallbacks();
            EditorApplication.EnterPlaymode();
        }

        private static void BuildMissingScene()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(TopDownBeatCombatSceneBuilder.ScenePath) != null)
            {
                return;
            }
            TopDownBeatCombatSceneBuilder.BuildScene();
        }

        private static void ValidateStructure()
        {
            if (UnityEngine.Object.FindFirstObjectByType<TopDownBeatPlayer>() == null ||
                UnityEngine.Object.FindFirstObjectByType<TopDownBeatCamera>() == null ||
                UnityEngine.Object.FindFirstObjectByType<BeatTrainingDummy>() == null ||
                UnityEngine.Object.FindFirstObjectByType<SoundfallBeatHud>() == null ||
                UnityEngine.Object.FindFirstObjectByType<RhythmClock>() == null)
            {
                throw new InvalidOperationException("Top Down Beat Combat scene is missing a required component.");
            }

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || !scenes[0].enabled || scenes[0].path != TopDownBeatCombatSceneBuilder.ScenePath)
                throw new InvalidOperationException("Top Down Beat Combat is not the first enabled Build scene.");
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedKey, 0f);
            RhythmClock clock = UnityEngine.Object.FindFirstObjectByType<RhythmClock>();
            TopDownBeatPlayer player = UnityEngine.Object.FindFirstObjectByType<TopDownBeatPlayer>();
            BeatTrainingDummy dummy = UnityEngine.Object.FindFirstObjectByType<BeatTrainingDummy>();
            TopDownBeatCamera camera = UnityEngine.Object.FindFirstObjectByType<TopDownBeatCamera>();

            if (clock == null || player == null || dummy == null || camera == null)
            {
                Fail("Runtime component missing.");
                return;
            }

            if (elapsed > TimeoutSeconds)
            {
                Fail("Runtime validation timed out.");
                return;
            }

            int state = SessionState.GetInt(StateKey, 0);
            if (state == 0)
            {
                if (!clock.IsReady || !clock.IsUsingFmod)
                    return;

                double phase = clock.AbsoluteBeatTime - Math.Floor(clock.AbsoluteBeatTime);
                if (phase > 0.07 && phase < 0.93)
                    return;

                if (!player.TryAttack() || dummy.HitCount != 1 || dummy.LastDamage != 20)
                {
                    Fail("On-beat attack did not hit the dummy for doubled damage.");
                    return;
                }

                player.SetTestMove(Vector2.up);
                SessionState.SetFloat(YKey, player.transform.position.y);
                if (!player.TryDodge())
                {
                    Fail("Quick dodge did not start.");
                    return;
                }
                SessionState.SetInt(StateKey, 1);
                return;
            }

            if (state == 1 && !player.IsDodging)
            {
                player.SetTestMove(Vector2.zero);
                float distance = player.transform.position.y - SessionState.GetFloat(YKey, player.transform.position.y);
                if (distance < 1f || camera.Target != player.transform)
                {
                    Fail("Dodge displacement or camera follow target validation failed.");
                    return;
                }

                SessionState.SetBool(PassedKey, true);
                SessionState.SetString(FailureKey, string.Empty);
                EditorApplication.ExitPlaymode();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetFloat(StartedKey, (float)EditorApplication.timeSinceStartup);
                return;
            }
            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            CleanupCallbacks();
            bool passed = SessionState.GetBool(PassedKey, false);
            string failure = SessionState.GetString(FailureKey, "Unknown failure.");
            ClearState();
            if (passed)
            {
                Debug.Log("TOP_DOWN_BEAT_COMBAT_SMOKE_TEST_PASS");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"TOP_DOWN_BEAT_COMBAT_SMOKE_TEST_FAIL: {failure}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void Fail(string reason)
        {
            SessionState.SetString(FailureKey, reason);
            EditorApplication.ExitPlaymode();
        }

        private static void RegisterCallbacks()
        {
            CleanupCallbacks();
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void CleanupCallbacks()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private static void ClearState()
        {
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(StartedKey);
            SessionState.EraseInt(StateKey);
            SessionState.EraseFloat(YKey);
            SessionState.EraseBool(PassedKey);
            SessionState.EraseString(FailureKey);
        }
    }
}
