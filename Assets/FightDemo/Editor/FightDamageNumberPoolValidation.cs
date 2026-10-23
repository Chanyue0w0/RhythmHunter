using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    public static class FightDamageNumberPoolValidation
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
        private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

        [MenuItem("Rhythm Hunter/Validate Damage Number Pool")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Main_FightScene.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                Set(fight.RosterManager, "requireBattlePreparation", false);
                fight.RosterManager.SpawnConfiguredRoster();
                Call(fight, "RebuildRosterAndResetCombat");
                var target = fight.ActiveEnemySlot;
                Check(target != null, "Missing enemy target.");
                Set(fight, "damageNumberMaxActive", 2);
                Call(fight, "EnsureDamageNumberPool");
                var active = (IList)fight.GetType().GetField("activeDamageNumbers", Flags).GetValue(fight);
                void Show(float damage) => Call(fight, "ShowEnemyDamageNumber", target, damage);
                void Expire() { while (active.Count > 0) Call(active[0], "Advance", 10f); }
                Show(12.5f);
                var first = (FightDamageNumber)active[0];
                Expire();
                Check(!first.gameObject.activeSelf, "Expired popup was not deactivated.");
                int roots = fight.CreatedDamageNumberCount, glyphs = fight.CreatedDamageGlyphCount;
                for (int i = 0; i < 1000; i++) { Show(12.5f); Expire(); }
                Check(roots == fight.CreatedDamageNumberCount && glyphs == fight.CreatedDamageGlyphCount,
                    "Repeated warmed-up damage grew the pool.");
                Show(0); Show(float.NaN); Show(float.PositiveInfinity);
                Check(fight.ActiveDamageNumberCount == 0, "Invalid damage produced a popup.");
                Show(1); Show(2); Show(3);
                Check(fight.ActiveDamageNumberCount == 2 && fight.CreatedDamageNumberCount == roots,
                    "Capacity did not recycle the oldest popup.");
                Call(fight, "ClearCombatEffectsKeepingDamageNumbers");
                Check(fight.ActiveDamageNumberCount == 2, "Wave effect cleanup prematurely removed damage feedback.");
                Call(fight, "ClearBattleEffects");
                Check(fight.ActiveDamageNumberCount == 0, "Full reset did not return active popups.");
                Show(.5f);
                Call(fight, "OnDisable");
                Check(fight.ActiveDamageNumberCount == 0, "Disabling combat left active numbers.");
                Show(1);
                Expire();
                Check(fight.CreatedDamageNumberCount == roots, "Reset discarded reusable popup instances.");
                Debug.Log("DAMAGE_NUMBER_POOL_VALIDATION_PASS: 1000 reuse cycles, decimal digits, capacity, cleanup and disable.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
