using System;
using System.Collections;
using System.Collections.Generic;
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
    internal static class FightBlockFeedbackValidation
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Set(object o, string field, object value) => o.GetType().GetField(field, Flags).SetValue(o, value);
        static object Get(object o, string field) => o.GetType().GetField(field, Flags).GetValue(o);
        static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Flags).Invoke(o, args);
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static FightBlockFeedbackValidation() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/BlockFeedbackValidation.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(request);
            try { Run(); }
            catch (Exception e) { File.WriteAllText("Temp/BlockFeedbackValidation.result", "FAIL: " + e); }
        }
        [MenuItem("Rhythm Hunter/Validation/Validate Block Feedback Timing")]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                fight.RosterManager.SpawnConfiguredRoster();
                Call(fight, "RebuildRosterAndResetCombat");
                Set(fight.AudioPlayer, "soundEnabled", false); // No FMOD runtime in a preview scene.
                Set(fight, "beatClock", null);
                Set(fight, "waveEntrance", null);
                Set(fight, "endBattleOnZeroHp", false);
                Check(fight.TryBeginBattle(), "Cannot start preview battle");
                var guards = (HashSet<long>)Get(fight, "guardedBeats");
                var hits = (IList)Get(fight, "enemyHits");
                var hitType = typeof(FightCombatController).GetNestedType("ScheduledEnemyHit", BindingFlags.NonPublic);
                void Queue()
                {
                    var hit = Activator.CreateInstance(hitType);
                    void Field(string name, object value) => hitType.GetField(name).SetValue(hit, value);
                    Field("hitBeat", 1L); Field("timeline", 1000); Field("version", Get(fight, "rosterVersion"));
                    Field("damage", 1f); Field("launched", true);
                    hits.Add(hit);
                }
                void Resolve(int ms) => Call(fight, "ResolveEnemyCombatWork", ms);
                int resolved = 0;
                fight.EnemyAttackResolved += _ => resolved++;
                guards.Add(1); Queue();
                Resolve(999); Check(resolved == 0, "Early input resolved before impact");
                Resolve(1000); Check(resolved == 1 && fight.BlockedAttackCount == 1, "Guard did not resolve at impact");
                Resolve(2000); Check(resolved == 1, "Guard resolved twice");
                guards.Clear(); Queue(); Resolve(1000);
                Check(resolved == 1, "Unguarded hit ignored late-input window");
                guards.Add(1); Resolve(1001);
                Check(resolved == 2 && fight.BlockedAttackCount == 2, "Late valid guard did not resolve immediately");
                Queue(); Queue(); Resolve(1000);
                Check(resolved == 4 && fight.BlockedAttackCount == 4, "Simultaneous attacks lost block feedback");
                guards.Clear(); Queue(); Resolve(1000); Resolve(10000);
                Check(resolved == 5 && fight.BlockedAttackCount == 4 && hits.Count == 0, "Unguarded hit failed to resolve");
                File.WriteAllText("Temp/BlockFeedbackValidation.result", "PASS: no pre-impact feedback; immediate guarded impact; late valid guard; no duplicate resolution; simultaneous blocks; unguarded late window preserved. Audio muted in preview; live listening required.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
