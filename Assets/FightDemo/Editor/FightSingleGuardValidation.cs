using System;
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
    public static class FightSingleGuardValidation
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static FightSingleGuardValidation() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/SingleGuardValidation.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool sync = File.ReadAllText(request).Trim() == "sync";
            File.Delete(request);
            try
            {
                if (sync)
                    foreach (var fight in UnityEngine.Object.FindObjectsByType<FightCombatController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (fight.gameObject.scene.path.EndsWith("Art2_FightScene.unity"))
                        {
                            var data = new SerializedObject(fight);
                            data.FindProperty("pressureDamagePerPair").floatValue = 10;
                            data.FindProperty("pressureReductionSegments").intValue = 40;
                            data.FindProperty("pressureBlocksPerPair").intValue = 1;
                            data.FindProperty("pressureGuardChipDamage").floatValue = 0;
                            if (data.ApplyModifiedProperties()) EditorSceneManager.MarkSceneDirty(fight.gameObject.scene);
                        }
                Run();
                File.WriteAllText("Temp/SingleGuardValidation.result", "PASS: one-hit guard; strongest simultaneous hit in both insertion orders; adjacent guards; longer windows consume once; expired guard; pressure 10 damage / 1 block, 1/40 duration per reduction; consumed guard stays consumed through pause.");
            }
            catch (Exception e) { File.WriteAllText("Temp/SingleGuardValidation.result", "FAIL: " + e); }
        }
        [MenuItem("Rhythm Hunter/Validate Single Hit Guard and Pressure")]
        public static void Run()
        {
            using var f = new Fixture();
            var fight = f.Fight;
            void Reset()
            {
                f.Reset();
                Set(fight, "latestCombatBeat", 0L);
                foreach (var enemy in f.Enemies) Set(enemy, "attackIntervalBeats", 10000);
                Set(fight, "partyArmor", 20f); Set(fight, "partyHp", 20f);
            }
            void Resolve(int beat) => Call(fight, "ResolveEnemyCombatWork", 1000 + beat * 500 + 140);
            foreach (bool reverse in new[] { false, true })
            {
                Reset();
                Check(fight.ScheduleEnemyHits(f.Enemies[0], new[] { 1 }, reverse ? 4 : 1), "Schedule first hit");
                Check(fight.ScheduleEnemyHits(f.Enemies[1], new[] { 1 }, reverse ? 1 : 4), "Schedule second hit");
                f.Beat(1); Call(fight, "ArmGuard", 1L, 1); Resolve(1);
                Check(fight.BlockedAttackCount == 1 && fight.PartyArmor == 19, "Strongest simultaneous hit must be blocked regardless of insertion order");
                Resolve(1); Check(fight.BlockedAttackCount == 1 && fight.PartyArmor == 19, "No duplicate resolution");
            }
            Reset();
            fight.ScheduleEnemyHits(f.Enemies[0], new[] { 1, 2, 2 }, 1);
            Call(fight, "ArmGuard", 1L, 1); Call(fight, "ArmGuard", 2L, 1);
            f.Beat(1); Resolve(1); f.Beat(2); Resolve(2);
            Check(fight.BlockedAttackCount == 2 && fight.PartyArmor == 19, "Adjacent casts have independent single charges");
            foreach (bool pendingIsLarger in new[] { false, true })
            {
                Reset();
                fight.ScheduleEnemyHits(f.Enemies[1], new[] { 1 }, pendingIsLarger ? 1 : 4);
                f.Beat(1); f.Queue(1);
                Set(fight, "pendingEnemyWindup", false);
                Set(fight, "pendingEnemyAnimationDriven", !pendingIsLarger);
                Set(fight, "pendingAttackGlobalBeat", 1L); Set(fight, "pendingAttackTimelineMs", 1500);
                Set(fight, "pendingBossDamage", pendingIsLarger ? 4f : 1f);
                Call(fight, "ArmGuard", 1L, 1);
                f.Resolve();
                if (!pendingIsLarger)
                    Check(!(bool)typeof(FightCombatController).GetField("pendingEnemyAnimationDriven", Flags).GetValue(fight),
                        "Deferred animation callback must remain eligible for normal resolution");
                Resolve(1); f.Resolve();
                Check(fight.BlockedAttackCount == 1 && fight.PartyArmor == 19,
                    "Pending and scheduled attacks share one shield and prioritize strongest simultaneous damage");
            }
            Reset();
            fight.ScheduleEnemyHits(f.Enemies[0], new[] { 1, 2 }, 1);
            Call(fight, "ArmGuard", 1L, 2);
            f.Beat(1); Resolve(1); f.Beat(2); Resolve(2);
            Check(fight.BlockedAttackCount == 1 && fight.PartyArmor == 19, "Longer window must not restore consumed charge");
            Reset();
            fight.ScheduleEnemyHits(f.Enemies[0], new[] { 2 }, 1);
            Call(fight, "ArmGuard", 1L, 1); f.Beat(1); f.Beat(2); Resolve(2);
            Check(fight.BlockedAttackCount == 0 && fight.PartyArmor == 19, "Expired guard cannot block next beat");
            Reset();
            Set(fight, "wavePressure", new FightEnemyPressureSettings { enablePressurePhase = true, durationBeats = 40 });
            Set(fight, "pressureRemaining", 40d); Set(fight, "pressureDamagePerPair", 10f);
            Set(fight, "pressureBlocksPerPair", 1); Set(fight, "pressureReductionSegments", 40);
            for (int i = 0; i < 9; i++) Call(fight, "RecordPressureDamage", f.Enemies[0], 1f);
            Check(fight.PressureRemainingExact == 40, "Nine damage does not remove a segment");
            Call(fight, "RecordPressureDamage", f.Enemies[0], 1f);
            Check(fight.PressureRemainingExact == 39, "Ten damage removes one segment");
            Call(fight, "RecordPressureBlock"); Check(fight.PressureRemainingExact == 38, "First block removes one segment");
            Call(fight, "RecordPressureBlock"); Check(fight.PressureRemainingExact == 37, "Every subsequent block removes one segment");
            fight.ScheduleEnemyHits(f.Enemies[0], new[] { 1, 2 }, 1);
            Call(fight, "ArmGuard", 1L, 1); Call(fight, "ArmGuard", 2L, 1);
            f.Beat(1); Resolve(1); Check(fight.PressureRemainingExact == 36, "Resolved guard reduces pressure immediately");
            f.Beat(2); Resolve(2); Check(fight.PressureRemainingExact == 35, "Next resolved guard also reduces pressure");
            Check(fight.PartyArmor == 20 && fight.PartyHp == 20, "Pressure guard has no chip damage when disabled");
            Reset(); Call(fight, "ArmGuard", 1L, 2);
            Call(fight, "BeginEnemyPause"); Call(fight, "AdvanceEnemyPause", 2L);
            var shifted = (HashSet<long>)typeof(FightCombatController).GetField("guardedBeats", Flags).GetValue(fight);
            Check(shifted.Contains(3) && shifted.Contains(4), "Unspent shield window shifts during pause");
            Call(fight, "ConsumeGuard", 3L); Check(shifted.Count == 0, "Shifted shield still has one total charge");
            Reset(); Call(fight, "ArmGuard", 1L, 2); Call(fight, "ConsumeGuard", 1L);
            Call(fight, "BeginEnemyPause"); Call(fight, "AdvanceEnemyPause", 2L);
            var guards = (HashSet<long>)typeof(FightCombatController).GetField("guardedBeats", Flags).GetValue(fight);
            Check(guards.Count == 0, "Pause must not resurrect consumed shield");
        }

        // Use the current Art2 scene, not the retired FightScene3 checkpoint scene.
        sealed class Fixture : IDisposable
        {
            readonly UnityEngine.SceneManagement.Scene scene;
            readonly FightStageDefinition stage;
            public FightCombatController Fight;
            public FightUnitSlot[] Enemies;
            static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
            public Fixture()
            {
                scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                Fight = all.OfType<FightCombatController>().Single();
                foreach (var sound in all.OfType<FightAudioPlayer>()) Set(sound, "soundEnabled", false);
                stage = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<FightStageDefinition>("Assets/FightDemo/Levels/Chapter02_Stage01.asset"));
                var enemy = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Normal.prefab");
                Set(stage, "waves", new List<FightStageDefinition.Wave> { new() { front = enemy, middle = enemy, back = enemy } });
                Check(Fight.RosterManager.TryConfigureStage(stage, out string error), error);
                Fight.RosterManager.SpawnConfiguredRoster();
                Set(Fight, "beatClock", null); Set(Fight, "waveEntrance", null);
                var judge = (FmodRhythmJudge)Get(Fight, "rhythmJudge");
                Set(judge, "personalDelayMs", 0f);
                Call(Fight, "RebuildRosterAndResetCombat");
                Check(Fight.TryBeginBattle(), "Begin test battle");
                Enemies = Fight.RosterManager.ActiveEnemies.ToArray();
            }
            public void Reset() => Call(Fight, "RebuildRosterAndResetCombat");
            public void Beat(int beat)
            {
                Set(Fight, "<NormalBattleBeatCount>k__BackingField", (long)beat);
                Set(Fight, "latestCombatBeat", (long)beat);
                Call(Fight, "AdvanceEnemyCombatWork", Snapshot(beat));
            }
            static FmodBeatClock.BeatSnapshot Snapshot(int beat) => new(beat, beat / 4 + 1, beat % 4 + 1, 1000 + beat * 500, 120, 4, 4);
            public void Queue(int beat) => Call(Fight, "QueueEnemyAttack", Snapshot(beat));
            public void Resolve() => Call(Fight, "ResolvePendingEnemyAttack", Get(Fight, "pendingAttackRosterVersion"),
                Get(Fight, "pendingEnemyActionId"), Get(Fight, "pendingEnemyAttacker"));
            public void Dispose()
            {
                EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }
    }
}
