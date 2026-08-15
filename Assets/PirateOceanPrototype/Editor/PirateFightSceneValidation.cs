using System.Collections.Generic;
using System.Linq;
using RhythmHunter.FightDemo;
using RhythmHunter.PirateOceanPrototype;
using RhythmHunter.RhythmDemo;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RhythmHunter.PirateOceanPrototypeEditor
{
    /// <summary>
    /// Validates the combined pirate environment and rhythm-fight scene, with
    /// an optional Play Mode smoke test that waits for live FMOD beat callbacks.
    /// </summary>
    [InitializeOnLoad]
    public static class PirateFightSceneValidation
    {
        private const string ActiveKey = "PirateFight.Validation.Active";
        private const string PassedKey = "PirateFight.Validation.Passed";
        private const string FailureKey = "PirateFight.Validation.Failure";
        private const string StartedAtKey = "PirateFight.Validation.StartedAt";
        private const string NormalAttemptedKey = "PirateFight.Validation.NormalAttempted";
        private const string GuardAttemptedKey = "PirateFight.Validation.GuardAttempted";
        private const string EnvironmentAttemptedKey = "PirateFight.Validation.EnvironmentAttempted";
        private const double TimeoutSeconds = 25.0;

        private static readonly Dictionary<string, Vector3> ExpectedSlotPositions = new()
        {
            { "EnemySlot_1", new Vector3(-5.8f, 0.15f, 0f) },
            { "EnemySlot_2", new Vector3(-4.05f, 0.15f, 0f) },
            { "EnemySlot_3", new Vector3(-2.3f, 0.15f, 0f) },
            { "HeroSlot_Tank", new Vector3(2.3f, 0.15f, 0f) },
            { "HeroSlot_Support", new Vector3(4.05f, 0.15f, 0f) },
            { "HeroSlot_Damage", new Vector3(5.8f, 0.15f, 0f) }
        };

        static PirateFightSceneValidation()
        {
            if (SessionState.GetBool(ActiveKey, false))
                RegisterCallbacks();
            else
                EditorApplication.delayCall += ValidateAfterScriptReload;
        }

        private static void ValidateAfterScriptReload()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PirateFightSceneBuilder.ScenePath) != null)
                ValidateScene(true);
        }

        [MenuItem("Rhythm Hunter/Validate Pirate Fight Scene Setup")]
        public static void ValidateSceneFromMenu()
        {
            ValidateScene(true);
        }

        [MenuItem("Rhythm Hunter/Run Pirate Fight Play Mode Smoke Test")]
        public static void RunPlayModeSmokeTest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("PIRATE_FIGHT_SMOKE_TEST: Exit Play Mode before starting validation.");
                return;
            }

            if (!ValidateScene(true))
                return;

            EditorSceneManager.OpenScene(PirateFightSceneBuilder.ScenePath);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(PassedKey, false);
            SessionState.SetString(FailureKey, "Pirate fight validation timed out.");
            SessionState.SetFloat(StartedAtKey, 0f);
            SessionState.SetBool(NormalAttemptedKey, false);
            SessionState.SetBool(GuardAttemptedKey, false);
            SessionState.SetBool(EnvironmentAttemptedKey, false);
            RegisterCallbacks();
            EditorApplication.EnterPlaymode();
        }

        public static bool ValidateScene(bool logResult)
        {
            Scene scene = SceneManager.GetSceneByPath(PirateFightSceneBuilder.ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(PirateFightSceneBuilder.ScenePath, OpenSceneMode.Additive);

            List<string> failures = ValidateLoadedScene(scene);
            if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);

            if (failures.Count == 0)
            {
                if (logResult)
                    Debug.Log("PIRATE_FIGHT_SCENE_VALIDATION_PASS");
                return true;
            }

            if (logResult)
                Debug.LogError("PIRATE_FIGHT_SCENE_VALIDATION_FAIL:\n- " + string.Join("\n- ", failures));
            return false;
        }

        private static List<string> ValidateLoadedScene(Scene scene)
        {
            List<string> failures = new();
            PirateOceanWaveController[] waves = FindComponents<PirateOceanWaveController>(scene);
            PirateOceanSurface[] surfaces = FindComponents<PirateOceanSurface>(scene);
            PirateShipMotionController[] ships = FindComponents<PirateShipMotionController>(scene);
            PirateBossCameraController[] cameras = FindComponents<PirateBossCameraController>(scene);
            PirateOceanRuntimePanel[] panels = FindComponents<PirateOceanRuntimePanel>(scene);
            FightUnitSlot[] slots = FindComponents<FightUnitSlot>(scene);
            FmodBeatClock[] clocks = FindComponents<FmodBeatClock>(scene);
            FmodRhythmJudge[] judges = FindComponents<FmodRhythmJudge>(scene);
            FightInputRouter[] inputs = FindComponents<FightInputRouter>(scene);
            FightCombatController[] fights = FindComponents<FightCombatController>(scene);
            FightScenePresenter[] huds = FindComponents<FightScenePresenter>(scene);
            FightBattlefieldPresenter[] battlefields = FindComponents<FightBattlefieldPresenter>(scene);

            RequireCount(waves, 1, "PirateOceanWaveController", failures);
            RequireCount(surfaces, 1, "PirateOceanSurface", failures);
            RequireCount(ships, 1, "PirateShipMotionController", failures);
            RequireCount(cameras, 1, "PirateBossCameraController", failures);
            RequireCount(panels, 1, "PirateOceanRuntimePanel", failures);
            RequireCount(slots, 6, "FightUnitSlot", failures);
            RequireCount(clocks, 1, "FmodBeatClock", failures);
            RequireCount(judges, 1, "FmodRhythmJudge", failures);
            RequireCount(inputs, 1, "FightInputRouter", failures);
            RequireCount(fights, 1, "FightCombatController", failures);
            RequireCount(huds, 1, "FightScenePresenter", failures);
            RequireCount(battlefields, 1, "FightBattlefieldPresenter", failures);
            RequireCount(FindComponents<EventSystem>(scene), 1, "EventSystem", failures);
            RequireCount(FindComponents<CinemachineBrain>(scene), 1, "CinemachineBrain", failures);
            RequireCount(FindComponents<FMODUnity.StudioListener>(scene), 1, "FMOD StudioListener", failures);

            ValidateSlots(slots, ships.Length == 1 ? ships[0] : null, failures);

            FightCombatController fight = fights.Length == 1 ? fights[0] : null;
            if (fight != null
                && (fight.TankSlot == null
                    || fight.TankSlot.Role != FightUnitSlot.UnitRole.Tank
                    || fight.ActiveEnemySlot == null
                    || fight.ActiveEnemySlot.Team != FightUnitSlot.UnitTeam.Enemy
                    || fight.ActiveEnemySlot.SlotIndex != 1))
            {
                failures.Add("Fight controller is not bound to the pirate Tank and middle enemy slots.");
            }

            if (battlefields.Length == 1
                && (battlefields[0].HeroSlots == null
                    || battlefields[0].HeroSlots.Length != 3
                    || battlefields[0].EnemySlots == null
                    || battlefields[0].EnemySlots.Length != 3))
            {
                failures.Add("Battlefield presenter does not contain three heroes and three enemies.");
            }

            if (inputs.Length == 1)
            {
                InputActionAsset controls = inputs[0].FightControls;
                if (controls == null
                    || controls.FindAction("Abilities/Character1Attack", false) == null
                    || controls.FindAction("Abilities/Character2Attack", false) == null
                    || controls.FindAction("Abilities/Character3Attack", false) == null
                    || controls.FindAction("Abilities/FeverUlt", false) == null)
                {
                    failures.Add("Fight input asset or required actions are missing.");
                }
            }
            if (clocks.Length == 1 && clocks[0].MusicEventPath != "event:/Combat soundtracks/Combat 01")
                failures.Add("FMOD combat music event path does not match FightScene.");

            if (!scene.GetRootGameObjects().Any(root => root.name == "PirateFightHudCanvas"))
                failures.Add("Pirate fight HUD canvas is missing.");
            if (!EditorBuildSettings.scenes.Any(item => item.enabled && item.path == PirateFightSceneBuilder.ScenePath))
                failures.Add("PirateFightScene is not enabled in Build Settings.");

            return failures;
        }

        private static void ValidateSlots(
            FightUnitSlot[] slots,
            PirateShipMotionController ship,
            List<string> failures)
        {
            int heroCount = slots.Count(slot => slot.Team == FightUnitSlot.UnitTeam.Hero);
            int enemyCount = slots.Count(slot => slot.Team == FightUnitSlot.UnitTeam.Enemy);
            if (heroCount != 3 || enemyCount != 3)
                failures.Add($"Expected three heroes and three enemies; found {heroCount}/{enemyCount}.");

            foreach (FightUnitSlot slot in slots)
            {
                if (!ExpectedSlotPositions.TryGetValue(slot.name, out Vector3 expected)
                    || Vector3.Distance(slot.transform.localPosition, expected) > 0.001f)
                {
                    failures.Add($"Stable slot coordinate is invalid: {slot.name}.");
                }

                if (slot.ActorRoot == null || slot.NormalAttackEffectSpawnPoint == null)
                    failures.Add($"{slot.name} visual references are incomplete.");

                if (ship != null && ship.MotionVisualRoot != null)
                {
                    if (slot.transform.IsChildOf(ship.MotionVisualRoot))
                        failures.Add($"{slot.name} logic transform is under the moving root.");
                    if (slot.ActorRoot != null && !slot.ActorRoot.IsChildOf(ship.MotionVisualRoot))
                        failures.Add($"{slot.name} actor visual is outside the moving root.");
                }
            }
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
            string failure = SessionState.GetString(FailureKey, "Unknown PirateFightScene validation failure.");
            ClearSessionState();
            if (passed)
                Debug.Log("PIRATE_FIGHT_PLAY_MODE_SMOKE_TEST_PASS");
            else
                Debug.LogError($"PIRATE_FIGHT_PLAY_MODE_SMOKE_TEST_FAIL: {failure}");
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedAtKey, 0f);
            FmodBeatClock clock = Object.FindFirstObjectByType<FmodBeatClock>();
            FightInputRouter input = Object.FindFirstObjectByType<FightInputRouter>();
            FightCombatController fight = Object.FindFirstObjectByType<FightCombatController>();
            PirateOceanWaveController waves = Object.FindFirstObjectByType<PirateOceanWaveController>();
            PirateOceanSurface surface = Object.FindFirstObjectByType<PirateOceanSurface>();
            PirateShipMotionController ship = Object.FindFirstObjectByType<PirateShipMotionController>();
            PirateBossCameraController camera = Object.FindFirstObjectByType<PirateBossCameraController>();
            PirateOceanRuntimePanel panel = Object.FindFirstObjectByType<PirateOceanRuntimePanel>();
            FightUnitSlot[] slots = Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None);

            if (clock == null || input == null || fight == null || waves == null || surface == null
                || ship == null || camera == null || panel == null || slots.Length != 6)
            {
                FailAndExit("One or more integrated runtime systems are missing.");
                return;
            }

            if (!string.IsNullOrEmpty(clock.LastError))
            {
                FailAndExit(clock.LastError);
                return;
            }

            if (!input.IsConfigured)
            {
                FailAndExit("FightInputRouter did not resolve FightControl actions.");
                return;
            }

            if (elapsed >= 0.4 && !SessionState.GetBool(EnvironmentAttemptedKey, false))
            {
                panel.ApplyStormPreset();
                camera.ShowBossWideView();
                SessionState.SetBool(EnvironmentAttemptedKey, true);
            }

            if (!RuntimeEnvironmentIsValid(surface, ship, slots, out string environmentFailure))
            {
                if (elapsed >= 0.75)
                {
                    FailAndExit(environmentFailure);
                    return;
                }
            }

            FightUnitSlot tank = fight.TankSlot;
            if (clock.ReceivedBeatCount >= 1
                && clock.LatestBeat.Beat != 4
                && !SessionState.GetBool(NormalAttemptedKey, false)
                && clock.TryGetBeatPhase(out float normalPhase)
                && normalPhase < 0.08f)
            {
                SessionState.SetBool(NormalAttemptedKey, true);
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Tank);
            }

            if (clock.ReceivedBeatCount >= 4
                && clock.LatestBeat.Beat == 4
                && !SessionState.GetBool(GuardAttemptedKey, false)
                && clock.TryGetBeatPhase(out float guardPhase)
                && guardPhase < 0.08f)
            {
                SessionState.SetBool(GuardAttemptedKey, true);
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Tank);
            }

            if (SessionState.GetBool(GuardAttemptedKey, false) && fight.BlockedAttackCount >= 1)
            {
                bool passed = fight.PartyHp == fight.MaxPartyHp
                    && fight.ReceivedAttackCount == 0
                    && tank != null
                    && tank.NormalAttackPlayCount >= 1
                    && clock.IsPlaying
                    && camera.BossViewActive
                    && Mathf.Approximately(waves.Intensity, 1.5f)
                    && Mathf.Approximately(ship.MotionIntensity, 1.45f);

                panel.ApplyCombatPreset();
                camera.ShowShipCombatView();
                if (!passed)
                {
                    FailAndExit($"Integrated fight result invalid. HP={fight.PartyHp}/{fight.MaxPartyHp}, Blocks={fight.BlockedAttackCount}, NormalVFX={tank?.NormalAttackPlayCount ?? 0}.");
                    return;
                }

                SessionState.SetBool(PassedKey, true);
                SessionState.SetString(FailureKey, string.Empty);
                EditorApplication.ExitPlaymode();
                return;
            }

            if (elapsed >= TimeoutSeconds)
                FailAndExit("Timed out waiting for FMOD beats and the fourth-beat guard result.");
        }

        private static bool RuntimeEnvironmentIsValid(
            PirateOceanSurface surface,
            PirateShipMotionController ship,
            FightUnitSlot[] slots,
            out string failure)
        {
            failure = string.Empty;
            foreach (FightUnitSlot slot in slots)
            {
                if (!ExpectedSlotPositions.TryGetValue(slot.name, out Vector3 expected)
                    || Vector3.Distance(slot.transform.localPosition, expected) > 0.001f)
                {
                    failure = $"Stable slot drifted while the ship was moving: {slot.name}.";
                    return false;
                }
            }

            if (ship.MotionVisualRoot == null
                || (ship.MotionVisualRoot.localPosition.sqrMagnitude < 0.000001f
                    && Quaternion.Angle(ship.MotionVisualRoot.localRotation, Quaternion.identity) < 0.01f))
            {
                failure = "Ship visual root did not animate.";
                return false;
            }

            MeshFilter filter = surface.GetComponent<MeshFilter>();
            Vector3[] vertices = filter != null && filter.sharedMesh != null ? filter.sharedMesh.vertices : null;
            if (vertices == null || vertices.Length == 0)
            {
                failure = "Continuous ocean mesh was not generated.";
                return false;
            }

            bool varied = false;
            for (int i = 0; i < vertices.Length; i += 2)
                varied |= Mathf.Abs(vertices[i].y - surface.SurfaceY) > 0.001f;

            if (!varied)
            {
                failure = "Continuous ocean mesh did not animate.";
                return false;
            }

            return true;
        }

        private static T[] FindComponents<T>(Scene scene) where T : Component
        {
            List<T> results = new();
            if (!scene.IsValid() || !scene.isLoaded)
                return results.ToArray();
            foreach (GameObject root in scene.GetRootGameObjects())
                results.AddRange(root.GetComponentsInChildren<T>(true));
            return results.ToArray();
        }

        private static void RequireCount<T>(T[] values, int expected, string label, List<string> failures)
        {
            if (values.Length != expected)
                failures.Add($"Expected {expected} {label} component(s), found {values.Length}.");
        }

        private static void FailAndExit(string failure)
        {
            SessionState.SetString(FailureKey, failure);
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
            SessionState.EraseBool(PassedKey);
            SessionState.EraseString(FailureKey);
            SessionState.EraseFloat(StartedAtKey);
            SessionState.EraseBool(NormalAttemptedKey);
            SessionState.EraseBool(GuardAttemptedKey);
            SessionState.EraseBool(EnvironmentAttemptedKey);
        }
    }
}
