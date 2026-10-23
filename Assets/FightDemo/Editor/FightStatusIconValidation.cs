using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightStatusIconValidation
    {
        static FightStatusIconValidation() => EditorApplication.update += RunRequestedValidation;
        private static void RunRequestedValidation()
        {
            const string request = "Temp/FightStatusIconValidation.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            File.Delete(request);
            try { Run(); File.WriteAllText("Temp/FightStatusIconValidation.result", "PASS"); }
            catch (Exception ex) { File.WriteAllText("Temp/FightStatusIconValidation.result", "FAIL: " + ex); Debug.LogException(ex); }
        }
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
        static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

        [MenuItem("Rhythm Hunter/Validate Status Icon Pool")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Main_FightScene.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                Set(fight.BeatClock, "hasAnchor", true);
                Set(fight.BeatClock, "latestBeat", new FmodBeatClock.BeatSnapshot(0, 1, 1, 0, 120, 4, 4));
                Set(fight.RosterManager, "requireBattlePreparation", false);
                Set(fight, "showEnemyDamageNumbers", false);
                fight.RosterManager.SpawnConfiguredRoster();
                Call(fight, "RebuildRosterAndResetCombat");
                var enemy = fight.ActiveEnemySlot;
                Check(enemy != null, "Missing enemy fixture.");
                Set(enemy, "maxHp", 100f); enemy.RestoreFullHealth();
                enemy.ApplyFragile(); enemy.ApplyFragile();
                Check(fight.ActiveStatusIconCount == 1, "Repeated Fragile must reuse one icon.");
                enemy.ApplyBleeding(.5f, 4);
                Check(fight.ActiveStatusIconCount == 2, "Fragile and bleeding have separate icons.");
                enemy.TakeDamage(1);
                Check(!enemy.IsFragile && fight.ActiveStatusIconCount == 2, "Fragile is consumed immediately while its visual pulse finishes.");
                Call(fight, "AdvanceStatusIconVisuals", .3f);
                Check(fight.ActiveStatusIconCount == 1, "Consumed Fragile icon returns after its pulse.");
                for (int i = 1; i <= 4; i++)
                {
                    enemy.AdvanceTimedStatuses(i);
                    var live = (IList)fight.GetType().GetField("activeStatusIcons", Flags).GetValue(fight);
                    Check(live.Count == 1, "Each bleeding damage tick retains a pulse to show the hit.");
                    var bleed = (FightStatusIcon)live[0];
                    float before = bleed.transform.localScale.x;
                    Call(fight, "AdvanceStatusIconVisuals", .04f);
                    Check(bleed.transform.localScale.x > before, "Each bleeding tick visibly expands.");
                    Call(fight, "AdvanceStatusIconVisuals", .3f);
                    Check(fight.ActiveStatusIconCount == (i == 4 ? 0 : 1), "Bleeding returns to idle display until the final pulse.");
                }
                var active = (IList)fight.GetType().GetField("activeStatusIcons", Flags).GetValue(fight);
                Call(fight, "ShowBlockedEffectIcon");
                var popup = (FightStatusIcon)active[0];
                Vector3 start = popup.transform.position;
                Call(fight, "AdvanceStatusIconVisuals", .25f);
                Check(popup.transform.position.x > start.x && popup.transform.position.y > start.y,
                    "Blocked popup jumps toward the right.");
                var sprite = popup.GetComponentInChildren<SpriteRenderer>();
                Check(sprite.color.a > 0 && sprite.color.a < 1, "Blocked popup fades during its lifetime.");
                Call(fight, "AdvanceStatusIconVisuals", .25f);
                Check(fight.ActiveStatusIconCount == 0 && !popup.gameObject.activeSelf, "Blocked popup returns to pool after 0.5 seconds.");
                int created = fight.CreatedStatusIconCount;
                for (int i = 0; i < 1000; i++)
                {
                    Call(fight, "ShowBlockedEffectIcon");
                    Call(fight, "AdvanceStatusIconVisuals", .5f);
                }
                Check(fight.CreatedStatusIconCount == created, "1000 warmed-up block popups must not create more objects.");
                Set(fight, "maxBlockedEffectIcons", 2);
                Call(fight, "ShowBlockedEffectIcon"); Call(fight, "ShowBlockedEffectIcon"); Call(fight, "ShowBlockedEffectIcon");
                Check(fight.ActiveStatusIconCount == 2, "Block burst respects pool capacity.");
                Call(fight, "ClearStatusIcons");
                enemy.ApplyFragile(); enemy.ApplyBleeding(.5f, 4); enemy.TakeDamage(200);
                Check(fight.ActiveStatusIconCount == 0, "Death removes all persistent icons.");
                Call(fight, "ShowBlockedEffectIcon"); Call(fight, "OnDisable");
                Check(fight.ActiveStatusIconCount == 0, "Battle teardown recycles block popups.");
                UnityEngine.Object.DestroyImmediate(fight);
                // Reproduce the recorded Console error: the slot outlives its controller.
                enemy.ClearTimedStatuses(); enemy.ClearFragile();
                Call(enemy, "OnDisable");
                Debug.Log("STATUS_ICON_POOL_VALIDATION_PASS: status lifecycle, jump/fade, 1000 reuse cycles, capacity and teardown.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
