using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightStageDeadlineValidation
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static FightStageDeadlineValidation() => EditorApplication.update += Poll;
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Poll()
        {
            const string request = "Temp/StageDeadlineValidation.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(request); } catch (IOException) { return; }
            try { Validate(); File.WriteAllText("Temp/StageDeadlineValidation.result", "PASS: deadline boundary, current-Wave-only overtime, death presentation wait, no next Wave, treasure/gap expiry, Fever deferral, defeat priority, reset and partial result statistics; final-combat-only policy rejects early combat/gaps/Fever and ignores trailing treasure."); }
            catch (Exception e) { File.WriteAllText("Temp/StageDeadlineValidation.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Diagnostics/Validate Stage Deadline")]
        public static void Validate()
        {
            foreach (string scenario in new[] { "combat", "gap", "treasure", "fever", "defeat", "early", "restricted_early", "restricted_gap", "restricted_last" })
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                FightStageDefinition stage = null;
                try
                {
                    var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                    var fight = all.OfType<FightCombatController>().Single();
                    var results = all.OfType<FightStageResults>().Single();
                    foreach (var sound in all.OfType<FightAudioPlayer>()) Set(sound, "soundEnabled", false);
                    stage = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<FightStageDefinition>("Assets/FightDemo/Levels/Stage01.asset"));
                    Set(stage, "useNormalEndDeadline", true);
                    Set(stage, "overtimeOnlyOnFinalCombatWave", scenario.StartsWith("restricted_"));
                    if (scenario == "restricted_last")
                    {
                        var waves = (System.Collections.Generic.List<FightStageDefinition.Wave>)typeof(FightStageDefinition).GetField("waves", Flags).GetValue(stage);
                        waves.Add(new FightStageDefinition.Wave { kind = FightStageDefinition.WaveKind.Treasure, front = stage.Waves[0].front });
                    }
                    if (scenario == "treasure") stage.Waves[0].kind = FightStageDefinition.WaveKind.Treasure;
                    Check(fight.RosterManager.TryConfigureStage(stage, out string error), error);
                    fight.RosterManager.SpawnConfiguredRoster();
                    Call(fight, "RebuildRosterAndResetCombat");
                    Set(fight, "beatClock", null); Set(fight, "waveEntrance", null);
                    Check(fight.TryBeginBattle(), "Battle did not start");
                    Set(fight, "stageDeadlineMs", 63471);
                    int clears = 0; fight.WaveCleared += _ => clears++;
                    Call(fight, "EvaluateStageDeadline", 63470);
                    Check(!fight.StageTimeExpired && !fight.BattleEnded, scenario + " expired early");
                    if (scenario == "early")
                    {
                        Call(fight, "FinishBattle", true);
                        Check(fight.BattleWon && !fight.StageTimeExpired, "Early victory failed");
                        continue;
                    }
                    if (scenario == "gap") Set(fight, "<AwaitingNextWave>k__BackingField", true);
                    if (scenario == "restricted_gap") Set(fight, "<AwaitingNextWave>k__BackingField", true);
                    if (scenario == "restricted_early") Set(fight, "nextTeamSkillBeat", 999L);
                    if (scenario == "restricted_last") Set(fight, "<CurrentWave>k__BackingField", 2);
                    if (scenario == "fever") Set(fight, "nextTeamSkillBeat", 999L);
                    if (scenario == "defeat") { Set(fight, "endBattleOnZeroHp", true); Set(fight, "partyHp", 0f); }
                    Call(fight, "EvaluateStageDeadline", 63471);
                    Check(fight.StageTimeExpired, "Did not latch deadline");
                    if (scenario.StartsWith("restricted_"))
                    {
                        if (scenario == "restricted_last")
                            Check(!fight.BattleEnded && fight.StageOvertimeActive && fight.RosterManager.LastCombatWaveIndex == 1, "Trailing treasure incorrectly prevented last-combat overtime");
                        else
                            Check(fight.BattleEnded && !fight.BattleWon && !fight.StageOvertimeActive, "Earlier combat/gap must immediately defeat, including active Fever");
                        continue;
                    }
                    if (scenario == "gap" || scenario == "treasure")
                        Check(fight.BattleEnded && fight.BattleWon && fight.CurrentWave == 1, scenario + " did not settle immediately");
                    else if (scenario == "defeat") Check(fight.BattleEnded && !fight.BattleWon, "Zero HP must defeat");
                    else
                    {
                        if (scenario == "fever")
                        {
                            Check(!fight.StageOvertimeActive && !fight.BattleEnded, "Interrupted running Fever");
                            Set(fight, "nextTeamSkillBeat", long.MaxValue);
                            Call(fight, "EvaluateStageDeadline", 64000);
                        }
                        Check(fight.StageOvertimeActive && !fight.BattleEnded && fight.TeamSkillActivationBlocked, "Combat overtime not active/locked");
                        foreach (var enemy in fight.RosterManager.ActiveEnemies) enemy.TakeDamage(10000);
                        Call(fight, "EvaluateStageDeadline", 65000);
                        Check(!fight.BattleEnded, "Skipped death presentation");
                        foreach (var enemy in fight.RosterManager.ActiveEnemies)
                        {
                            if (enemy.CombatAnimator != null) Call(enemy.CombatAnimator, "AdvancePlayback", 2f);
                            Call(enemy, "LateUpdate");
                        }
                        Call(fight, "EvaluateStageDeadline", 66000);
                        Check(fight.BattleEnded && fight.BattleWon && fight.CurrentWave == 1 && clears == 1, "Overtime spawned next Wave or missed clear");
                        Call(results, "Finish", true);
                        Check(results.ClearedWaves < results.TotalWaves, "Partial victory reported all Waves cleared");
                    }
                    Call(fight, "ResetStageDeadline");
                    Check(!fight.StageTimeExpired && !fight.StageOvertimeActive && fight.StageMusicSeconds == 0, "Timer reset failed");
                }
                finally { if (stage != null) UnityEngine.Object.DestroyImmediate(stage); EditorSceneManager.ClosePreviewScene(scene); }
            }
        }
    }
}
