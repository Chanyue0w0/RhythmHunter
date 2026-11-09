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
            string mode = File.ReadAllText(request).Trim();
            File.Delete(request);
            try { if (mode == "setup-death") ConfigureDeathIcon(); Run(); File.WriteAllText("Temp/FightStatusIconValidation.result", "PASS"); }
            catch (Exception ex) { File.WriteAllText("Temp/FightStatusIconValidation.result", "FAIL: " + ex); Debug.LogException(ex); }
        }
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
        static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

        private static void ConfigureDeathIcon()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.path.EndsWith("Art2_FightScene.unity")) throw new Exception("Open Art2 outside Play Mode.");
            const string path = "Assets/FightDemo/Prefabs/StatusIcons/DeathEffect.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var icon = root.GetComponent<FightStatusIcon>() ?? root.AddComponent<FightStatusIcon>();
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                var data = new SerializedObject(icon);
                data.FindProperty("popupRightDistance").floatValue = -.6f;
                data.FindProperty("popupLifetime").floatValue = .5f;
                data.FindProperty("popupJumpHeight").floatValue = .25f;
                data.ApplyModifiedPropertiesWithoutUndo();
                foreach (var sprite in root.GetComponentsInChildren<SpriteRenderer>(true)) sprite.sortingOrder = 120;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
            var serialized = new SerializedObject(fight);
            serialized.FindProperty("deathEffectIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FightStatusIcon>(path);
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Rhythm Hunter/Validate Status Icon Pool")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Main_FightScene.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                Set(fight, "deathEffectIcon", AssetDatabase.LoadAssetAtPath<FightStatusIcon>("Assets/FightDemo/Prefabs/StatusIcons/DeathEffect.prefab"));
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
                ValidateCameraSpaceIcons(fight, active);
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
                Check(fight.ActiveStatusIconCount == 1, "Death replaces persistent icons with one popup.");
                var death = (FightStatusIcon)active[0];
                start = death.transform.position;
                Call(enemy, "RemoveDefeatedEnemyPresentation");
                enemy.TakeDamage(1);
                Call(fight, "ClearCombatEffectsKeepingDamageNumbers");
                Check(fight.ActiveStatusIconCount == 1, "Wave clear must retain death popup; overkill must not duplicate it.");
                Call(fight, "AdvanceStatusIconVisuals", .25f);
                Check(death.transform.position.x < start.x && death.transform.position.y > start.y, "Death popup must jump left/up after actor removal.");
                Check(death.GetComponentInChildren<SpriteRenderer>().color.a < 1, "Death popup must fade.");
                Call(fight, "AdvanceStatusIconVisuals", .25f);
                Check(fight.ActiveStatusIconCount == 0, "Death popup must return to pool after .5 seconds.");
                created = fight.CreatedStatusIconCount;
                for (int i = 0; i < 100; i++)
                {
                    Call(fight, "ShowDeathEffectIcon", enemy);
                    Call(fight, "AdvanceStatusIconVisuals", .5f);
                }
                Check(created == fight.CreatedStatusIconCount, "Death popups must reuse pooled objects.");
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

        private static void ValidateCameraSpaceIcons(FightCombatController fight, IList active)
        {
            var cameraObject = new GameObject("StatusIconTestCamera");
            cameraObject.transform.SetParent(fight.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.transform.rotation = Quaternion.Euler(45, 12, 0);
            SpriteRenderer actorSprite = null;
            Quaternion originalRotation = Quaternion.identity;
            try
            {
                Set(fight, "statusIconCamera", camera);
                foreach (bool orthographic in new[] { false, true })
                {
                    camera.orthographic = orthographic;
                    Call(fight, "ShowBlockedEffectIcon");
                    var icon = (FightStatusIcon)active[0];
                    actorSprite = (SpriteRenderer)typeof(FightStatusIcon).GetField("targetSprite", Flags).GetValue(icon);
                    Check(actorSprite != null && actorSprite.sprite != null, "Missing actor sprite for 3D test.");
                    originalRotation = actorSprite.transform.rotation;
                    actorSprite.transform.rotation = Quaternion.Euler(45, 0, 0);
                    Bounds bounds = actorSprite.sprite.bounds;
                    Vector3 top = bounds.center + Vector3.up * bounds.extents.y;
                    if (actorSprite.flipX) top.x = -top.x;
                    if (actorSprite.flipY) top.y = -top.y;
                    Vector3 expectedHead = actorSprite.transform.TransformPoint(top);
                    var slot = (FightUnitSlot)typeof(FightStatusIcon).GetProperty("Target", Flags).GetValue(icon);
                    if (slot.HeadAnchor != null) expectedHead = slot.HeadAnchor.position;
                    Check(Vector3.Distance((Vector3)Call(icon, "HeadPosition"), expectedHead) < .0001f,
                        "3D head anchor must use transformed sprite top, not its world AABB.");
                    // Rent again after rotating the actor so the popup captures the new head.
                    Call(fight, "ClearStatusIcons");
                    Call(fight, "ShowBlockedEffectIcon");
                    icon = (FightStatusIcon)active[0];
                    Vector3 offset = (Vector3)typeof(FightStatusIcon).GetField("headOffset", Flags).GetValue(icon);
                    Check(Vector3.Distance(icon.transform.position, expectedHead + camera.transform.TransformDirection(offset)) < .0001f,
                        "Camera-relative offset must not drift away from the head.");
                    Check(Vector3.Dot(icon.transform.forward, camera.transform.forward) > .9999f,
                        "Icon must face the tilted camera.");
                    Vector3 start = icon.transform.position;
                    Call(fight, "AdvanceStatusIconVisuals", .25f);
                    Vector3 motion = icon.transform.position - start;
                    Check(Vector3.Dot(motion, camera.transform.right) > 0 && Vector3.Dot(motion, camera.transform.up) > 0,
                        "3D block popup must jump toward screen right/up.");
                    Call(fight, "ClearStatusIcons");
                    actorSprite.transform.rotation = originalRotation;
                    actorSprite = null;
                }
            }
            finally
            {
                Call(fight, "ClearStatusIcons");
                if (actorSprite != null) actorSprite.transform.rotation = originalRotation;
                Set(fight, "statusIconCamera", null);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
