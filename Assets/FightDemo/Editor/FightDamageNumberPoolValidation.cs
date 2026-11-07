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
                var cameraObject = new GameObject("DamageNumberTestCamera");
                cameraObject.transform.SetParent(fight.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                try
                {
                    Set(fight, "damageNumberCamera", camera);
                    foreach (bool orthographic in new[] { false, true })
                    {
                        camera.orthographic = orthographic;
                        camera.transform.rotation = Quaternion.Euler(45, 15, 0);
                        Check(target.HeadAnchor != null, "3D character must have an authored head anchor.");
                        Show(12.5f);
                        var popup = (FightDamageNumber)active[0];
                        Vector3 offset = (Vector3)fight.GetType().GetField("damageNumberOffset", Flags).GetValue(fight);
                        Vector3 expected = target.HeadAnchor.position + camera.transform.TransformDirection(offset);
                        Check(Vector3.Distance(popup.transform.position, expected) < .0001f, "Damage popup must use the head anchor and camera-relative offset.");
                        Check(Vector3.Dot(popup.transform.forward, camera.transform.forward) > .9999f, "Damage digits must face the view.");
                        Call(popup, "Advance", .1f);
                        Vector3 movement = popup.transform.position - expected;
                        Check(Vector3.Dot(movement.normalized, camera.transform.up) > .9999f, "Damage number must rise along screen up.");
                        Expire();
                    }
                    Set(fight, "damageNumberCamera", null);
                    Show(1);
                    Check(Quaternion.Angle(((FightDamageNumber)active[0]).transform.rotation, Quaternion.identity) < .001f,
                        "Pool reuse must restore 2D orientation.");
                    Expire();
                }
                finally { UnityEngine.Object.DestroyImmediate(cameraObject); }
                Debug.Log("DAMAGE_NUMBER_POOL_VALIDATION_PASS: 1000 reuse cycles, decimal digits, capacity, cleanup and disable.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
