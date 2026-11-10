using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightPressureAttackValidation
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Get(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
        static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
        static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static FightPressureAttackValidation() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/PressureAttackValidation.request";
            if (!File.Exists(request) || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try { File.Delete(request); } catch (IOException) { return; }
            try { Run(); }
            catch (Exception e) { File.WriteAllText("Temp/PressureAttackValidation.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Levels/Validate Pressure Attacks")]
        public static void Run()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play first.");
            var report = new List<string>();
            foreach (int rest in new[] { 1, 2 }) Validate(true, rest, report);
            Validate(false, 2, report);
            File.WriteAllText("Temp/PressureAttackValidation.result", "PASS\n" + string.Join("\n", report));
            Debug.Log("Pressure attack validation PASS\n" + string.Join("\n", report));
        }
        static void Validate(bool enabled, int recovery, List<string> report)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            FightStageDefinition stage = null;
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                var roster = fight.RosterManager;
                // Preview scenes have no FMOD runtime. Preserve VFX/frame events, mute only this test copy.
                Set(fight.GetComponent<FightAudioPlayer>(), "soundEnabled", false);
                stage = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<FightStageDefinition>("Assets/FightDemo/Levels/Stage01.asset"));
                var waves = (List<FightStageDefinition.Wave>)Get(stage, "waves");
                waves[0].front = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Normal.prefab");
                waves[0].middle = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Killer.prefab");
                waves[0].back = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Giant.prefab");
                waves[0].pressure = new FightEnemyPressureSettings { enablePressurePhase = enabled, recoveryBeats = recovery };
                Check(roster.TryConfigureStage(stage, out var error), error);
                roster.SpawnConfiguredRoster(); Call(fight, "RebuildRosterAndResetCombat");
                Set(fight, "beatClock", null); Set(fight, "waveEntrance", null); Set(fight, "endBattleOnZeroHp", false);
                Check(fight.TryBeginBattle(), "Cannot start battle");
                var enemies = roster.ActiveEnemies.ToArray();
                var starts = enemies.ToDictionary(e => e, _ => new List<long>());
                var seenHits = new HashSet<object>();
                long global = -1;
                void Tick()
                {
                    global++;
                    foreach (var enemy in enemies) Call(enemy.CombatAnimator, "AdvancePlayback", .5f);
                    Set(fight, "latestCombatBeat", global);
                    var beat = new FmodBeatClock.BeatSnapshot(global, (int)global / 4 + 1, (int)global % 4 + 1, (int)global * 500, 120, 4, 4);
                    Call(fight, "AdvanceNormalBattleBeat", beat);
                    Call(fight, "StartAuthoredEnemyTurns", global);
                    var newSources = new HashSet<FightUnitSlot>();
                    foreach (var hit in (IEnumerable)Get(fight, "enemyHits"))
                        if (seenHits.Add(hit)) newSources.Add((FightUnitSlot)hit.GetType().GetField("source").GetValue(hit));
                    foreach (var enemy in newSources) starts[enemy].Add(fight.WaveCombatBeatCount);
                    int count = fight.ScheduledEnemyHitCount;
                    Call(fight, "StartAuthoredEnemyTurns", global);
                    Check(count == fight.ScheduledEnemyHitCount, "Duplicate callback spawned another attack");
                    Call(fight, "ResolveEnemyCombatWork", (int)global * 500 + 250);
                }
                while (fight.WaveCombatBeatCount < 20) Tick();
                if (enabled)
                {
                    int hits = fight.ScheduledEnemyHitCount;
                    var frames = enemies.Select(e => e.CombatAnimator.ActiveFrame).ToArray();
                    Call(fight, "BeginEnemyPause");
                    for (int i = 0; i < 5; i++) Tick();
                    Check(fight.WaveCombatBeatCount == 20 && fight.ScheduledEnemyHitCount == hits &&
                        enemies.Select(e => e.CombatAnimator.ActiveFrame).SequenceEqual(frames), "Pause consumed cadence/animation");
                    Call(fight, "EndEnemyPause", true);
                }
                while (fight.WaveCombatBeatCount < 70) Tick();
                foreach (var enemy in enemies)
                {
                    var pressureStarts = starts[enemy].Where(b => b >= 20 && b < 40).ToArray();
                    if (enabled)
                    {
                        Check(pressureStarts.Length >= 2, enemy.DisplayName + " did not repeat during pressure");
                        var sequence = enemy.CombatAnimator.FindSequence(FightCharacterCombatAnimator.CombatAnimation.NormalAttack);
                        double duration = Enumerable.Range(0, sequence.Frames.Count).Sum(i => (double)sequence.GetFrameBeats(i));
                        for (int i = 1; i < pressureStarts.Length; i++)
                        {
                            long interval = pressureStarts[i] - pressureStarts[i - 1];
                            Check(interval >= Math.Ceiling(duration + recovery - .00001) && interval <= Math.Ceiling(duration + recovery - .00001) + 1,
                                enemy.DisplayName + " recovery did not use full animation + rest: " + interval);
                        }
                        Check(!starts[enemy].Any(b => b >= 40 && b < 56), "Exit caught up an old normal attack");
                        Check(starts[enemy].Any(b => b >= 60 && b < 70), "Repeat pressure did not resume");
                    }
                    else Check(starts[enemy].SequenceEqual(new long[] { 16, 32, 48, 64 }), "Disabled pressure changed ordinary intervals");
                    report.Add($"enabled={enabled} rest={recovery} {enemy.DisplayName}: starts {string.Join(",", starts[enemy])}");
                }
                if (enabled)
                {
                    // Cancel the middle enemy independently, preserving the other active attacks.
                    Call(fight, "ClearEnemyScheduledActions");
                    foreach (var enemy in enemies) enemy.CombatAnimator.InterruptPlayback();
                    Call(fight, "TryStartAuthoredEnemyAttack", enemies[1]);
                    Call(fight, "StartIndependentEnemyAttack", enemies[0]);
                    int before = fight.ScheduledEnemyHitCount;
                    Check(fight.TryInterruptEnemyAttack(enemies[1]) && fight.ScheduledEnemyHitCount == before - 2 && enemies[0].CombatAnimator.IsPlaying,
                        "Interruption failed or canceled another enemy");
                    enemies[1].ApplyStun(4);
                    int middleStarts = starts[enemies[1]].Count;
                    Tick(); Tick();
                    Check(starts[enemies[1]].Count == middleStarts, "Stunned enemy restarted");
                    Call(fight, "ClearEnemyScheduledActions");
                    Check(((IDictionary)Get(fight, "enemyCadences")).Count == 0, "Wave/teardown retained cooldowns");
                }
            }
            finally { if (stage != null) UnityEngine.Object.DestroyImmediate(stage); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
