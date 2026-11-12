using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightSlimeShieldSetup
    {
        const string PrefabPath = "Assets/FightDemo/Prefabs/VFX/SmallSlimeShield.prefab";
        static FightSlimeShieldSetup() => EditorApplication.update += Poll;
        private static void Poll()
        {
            const string request = "Temp/SlimeShield.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Configure(); }
            catch (Exception e) { File.WriteAllText("Temp/SlimeShield.result", "FAIL " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Effects/Configure Slime Shield Projectile")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            if (!scene.isLoaded) throw new Exception("Open Art2_FightScene first.");
            var objects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                var source = objects.FirstOrDefault(t => t.name == "SmallSlimeShield" || t.name == "SmallSlimeSheild");
                if (source == null) throw new Exception("SmallSlimeShield scene source missing");
                var copy = UnityEngine.Object.Instantiate(source.gameObject);
                try
                {
                    copy.name = "SmallSlimeShield"; copy.transform.position = Vector3.zero;
                    copy.SetActive(true);
                    prefab = PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(copy); }
            }
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var facing = contents.GetComponent<FightCameraFacingEffect>() ?? contents.AddComponent<FightCameraFacingEffect>();
                facing.faceCamera = true; facing.sortingOrder = 31;
                foreach (var sprite in contents.GetComponentsInChildren<SpriteRenderer>(true))
                { sprite.sortingLayerID = 0; sprite.sortingOrder = 31; }
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var mage = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Mage.prefab");
            if (mage == null) throw new Exception("Mage prefab missing");
            var data = new SerializedObject(mage);
            data.FindProperty("guardProjectilePrefab").objectReferenceValue = prefab;
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(mage); AssetDatabase.SaveAssets();
            var fight = objects.Select(t => t.GetComponent<FightCombatController>()).First(c => c != null);
            Validate(prefab, fight);
            Selection.activeObject = prefab;
            File.WriteAllText("Temp/SlimeShield.result", "PASS: actual warrior FootAnchor resolution, destination offset during flight/fade, camera facing, sorting 31, flight 0.1 beat, shield callback only 0.1 beat after impact, fade 0.4 beat and cleanup.");
        }
        private static void Validate(GameObject prefab, FightCombatController fight)
        {
            GameObject visual = null;
            var target = new GameObject("Shield validation target"); target.transform.position = new Vector3(10, 0, 0);
            try
            {
                var warrior = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/ArtBattle/LongSwordWarrior_Weapon.prefab");
                var expectedFoot = warrior.GetComponentsInChildren<Transform>(true).Single(t => t.name == "FootAnchor");
                var resolver = typeof(FightCombatController).GetMethod("ResolveGuardProjectileAnchor", BindingFlags.Static | BindingFlags.NonPublic);
                var resolvedFoot = resolver.Invoke(null, new object[] { warrior.transform, "FootAnchor" });
                if (!ReferenceEquals(resolvedFoot, expectedFoot)) throw new Exception("Warrior foot was not resolved");
                visual = UnityEngine.Object.Instantiate(prefab); visual.transform.position = Vector3.zero;
                var camera = target.AddComponent<Camera>(); camera.enabled = false;
                camera.transform.rotation = Quaternion.Euler(60, 15, 0);
                visual.GetComponent<FightCameraFacingEffect>().Bind(camera);
                if (Quaternion.Angle(visual.transform.rotation, camera.transform.rotation) > .01f || visual.GetComponent<SpriteRenderer>().sortingOrder != 31)
                    throw new Exception("Camera facing / sorting failed");
                var projectile = visual.AddComponent<FightBeatProjectile>();
                int hits = 0;
                var targetOffset = new Vector3(0, -.25f, .1f);
                projectile.Initialize(fight, target.transform, .1f, () => true, () => hits++, .4f, Vector3.one * 1.5f, targetOffset, .1f);
                var advance = typeof(FightBeatProjectile).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic);
                advance.Invoke(projectile, new object[] { .05d });
                if (hits != 0 || Mathf.Abs(visual.transform.position.x - 5) > .01f) throw new Exception("Half flight failed");
                advance.Invoke(projectile, new object[] { .05001d });
                if (hits != 0 || Vector3.Distance(visual.transform.position, target.transform.position + targetOffset) > .001f) throw new Exception("Impact timing/offset failed");
                advance.Invoke(projectile, new object[] { .09d });
                if (hits != 0) throw new Exception("Shield appeared before the post-impact delay");
                advance.Invoke(projectile, new object[] { .01d });
                if (hits != 1) throw new Exception("Shield did not appear 0.1 beat after impact");
                target.transform.position += Vector3.right;
                advance.Invoke(projectile, new object[] { .299d });
                if (Vector3.Distance(visual.transform.position, target.transform.position + targetOffset) > .001f) throw new Exception("Fade lost target offset");
                if (hits != 1 || Vector3.Distance(visual.transform.localScale, Vector3.one * 1.5f) > .002f || visual.GetComponent<SpriteRenderer>().color.a > .002f)
                    throw new Exception("Expansion/fade failed");
                advance.Invoke(projectile, new object[] { .002d });
                if (visual != null) throw new Exception("Projectile not cleaned up");
            }
            finally { if (visual != null) UnityEngine.Object.DestroyImmediate(visual); UnityEngine.Object.DestroyImmediate(target); }
        }
    }
}
