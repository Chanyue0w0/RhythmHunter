using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightEnemyOnHitValidation
    {
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        static FightEnemyOnHitValidation() => EditorApplication.update += Poll;
        static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
        static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Poll()
        {
            const string path = "Temp/EnemyOnHitValidation.request";
            if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(path); } catch (IOException) { return; }
            try { Run(); ValidateAllGoblinPositions(); File.WriteAllText("Temp/EnemyOnHitValidation.result", "PASS: normal OnHit/guard/interval tests; Normal/Killer/Giant each in every position, mixed and three identical enemies; simultaneous attacks; duplicate beats; per-source interruption."); }
            catch (Exception e) { File.WriteAllText("Temp/EnemyOnHitValidation.result", "FAIL: " + e); Debug.LogException(e); }
        }
        static void ValidateAllGoblinPositions()
        {
            string[][] lineups = {
                new[] { "Normal", "Killer", "Giant" }, new[] { "Giant", "Normal", "Killer" }, new[] { "Killer", "Giant", "Normal" },
                new[] { "Normal", "Normal", "Normal" }, new[] { "Killer", "Killer", "Killer" }, new[] { "Giant", "Giant", "Giant" }
            };
            foreach (var lineup in lineups)
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                try
                {
                    var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                    var roster = fight.RosterManager;
                    Set(roster, "requireBattlePreparation", false);
                    Set(roster, "enemyPrefabs", lineup.Select(k => AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>($"Assets/FightDemo/Prefabs/ArtBattle/Goblin_{k}.prefab")).ToArray());
                    roster.SpawnConfiguredRoster(); Call(fight, "RebuildRosterAndResetCombat");
                    Set(fight, "beatClock", null);
                    for (int n = 0; n <= 15; n++) Call(fight, "OnBeat", new FmodBeatClock.BeatSnapshot(n, n / 4 + 1, n % 4 + 1, n * 500, 120, 4, 4));
                    var enemies = roster.ActiveEnemies.ToArray();
                    int expected = lineup.Sum(k => k == "Killer" ? 2 : 1);
                    Check(enemies.Length == 3 && enemies.All(e => e.CurrentHp > 0 && e.CombatAnimator.IsPlaying) && fight.ScheduledEnemyHitCount == expected,
                        "Not all positions attacked: " + string.Join(",", lineup));
                    Call(fight, "StartAuthoredEnemyTurns", 15L);
                    Check(fight.ScheduledEnemyHitCount == expected, "Duplicate beat repeated attacks");
                    int removed = lineup[1] == "Killer" ? 2 : 1;
                    Check(fight.TryInterruptEnemyAttack(enemies[1]) && fight.ScheduledEnemyHitCount == expected - removed &&
                        enemies[0].CombatAnimator.IsPlaying && enemies[2].CombatAnimator.IsPlaying, "Middle interruption affected other slots");
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }
        static void Run()
        {
            const string path = "Assets/FightDemo/Prefabs/VFX/ClawSlash_Pixel.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var settings = root.GetComponent<FightEffectSettings>();
                Check(settings != null, "ClawSlash lacks effect settings");
                settings.onHitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/VFX/OnHitPixelVFX(Red).prefab");
                Check(settings.onHitPrefab != null, "Missing red OnHit prefab");
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                var roster = fight.RosterManager;
                var normal = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Normal.prefab");
                Set(roster, "requireBattlePreparation", false);
                Set(roster, "enemyPrefabs", new[] { normal, normal, null });
                roster.SpawnConfiguredRoster();
                Call(fight, "RebuildRosterAndResetCombat");
                Set(fight, "endBattleOnZeroHp", false);
                Set(fight, "beatClock", null);
                var enemies = roster.ActiveEnemies.ToArray();
                void Beat(int n) => Call(fight, "OnBeat", new FmodBeatClock.BeatSnapshot(n, n / 4 + 1, n % 4 + 1, n * 500, 120, 4, 4));
                int Impacts() => scene.GetRootGameObjects().Count(g => g.name.StartsWith("OnHitPixelVFX(Red)"));
                for (int n = 0; n <= 15; n++) Beat(n);
                Check(fight.ScheduledEnemyHitCount == 2 && enemies.All(e => e.CombatAnimator.IsPlaying), "Both normals must attack while both alive");
                Beat(15);
                Check(fight.ScheduledEnemyHitCount == 2, "Duplicate beat duplicated attacks");
                float before = fight.PartyHp + fight.PartyArmor;
                Beat(16); Call(fight, "ResolveEnemyCombatWork", 8499);
                Check(fight.PartyHp + fight.PartyArmor == before, "Windup hit too early");
                Beat(17); Call(fight, "ResolveEnemyCombatWork", 8999);
                Check(fight.PartyHp + fight.PartyArmor == before - enemies.Sum(e => e.AttackPower), "Both hits did not resolve");
                Check(Impacts() == 2, "Each unblocked enemy hit must spawn red OnHit");
                foreach (var enemy in enemies) Call(fight, "StartIndependentEnemyAttack", enemy);
                Beat(18); Beat(19);
                var guards = (HashSet<long>)fight.GetType().GetField("guardedBeats", Flags).GetValue(fight);
                guards.Add(19);
                before = fight.PartyHp + fight.PartyArmor;
                Call(fight, "ResolveEnemyCombatWork", 9999);
                Check(fight.PartyHp + fight.PartyArmor == before && Impacts() == 2, "Guard produced damage/OnHit");
                foreach (var enemy in enemies) { enemy.CombatAnimator.InterruptPlayback(); Call(fight, "StartIndependentEnemyAttack", enemy); }
                Check(fight.TryInterruptEnemyAttack(enemies[0]) && fight.ScheduledEnemyHitCount == 1 && enemies[1].CombatAnimator.IsPlaying, "Interrupt leaked between enemies");
                Call(fight, "CancelEnemyCombatWork");
                Set(enemies[0], "attackIntervalBeats", 4); Set(enemies[1], "attackIntervalBeats", 8);
                Call(fight, "StartAuthoredEnemyTurns", 3L);
                Check(fight.ScheduledEnemyHitCount == 1 && enemies[0].CombatAnimator.IsPlaying && !enemies[1].CombatAnimator.IsPlaying, "Distinct attack intervals ignored");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
