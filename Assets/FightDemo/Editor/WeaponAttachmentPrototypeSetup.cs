using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class WeaponAttachmentPrototypeSetup
    {
        const string Folder = "Assets/FightDemo/Prefabs/ArtBattle/WeaponPrototype";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static WeaponAttachmentPrototypeSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/WeaponAttachmentSetup.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(request);
            try { Run(); File.WriteAllText("Temp/WeaponAttachmentSetup.result", "PASS: sample variant and weapon created; Idle/Attack frame identity, actual-frame attachment, repeated sprite identity, interruption, reflection, grip alignment, scale preservation, hidden/missing poses and weapon replacement validated. No production prefab or scene modified."); }
            catch (Exception e) { File.WriteAllText("Temp/WeaponAttachmentSetup.result", "FAIL: " + e); }
        }
        [MenuItem("Rhythm Hunter/Weapons/Create and Validate Phase 1 Prototype")]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/FightDemo/Prefabs/ArtBattle", "WeaponPrototype");
            string weaponPath = Folder + "/Weapon_GripPlaceholder.prefab", actorPath = Folder + "/Warrior_WeaponPrototype.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<FightWeaponVisual>(weaponPath);
            if (prefab == null)
            {
                var go = new GameObject("Weapon_GripPlaceholder");
                try
                {
                    var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/FightDemo/Arts/Pixel Crawler - Free Pack/Weapons/Wood/Wood.png").OfType<Sprite>().First();
                    var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                    var visual = go.AddComponent<FightWeaponVisual>(); visual.Configure(renderer, Vector2.zero, 90);
                    PrefabUtility.SaveAsPrefabAsset(go, weaponPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
                prefab = AssetDatabase.LoadAssetAtPath<FightWeaponVisual>(weaponPath);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(actorPath) == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/ArtBattle/LongswordWarrior.prefab");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
                try
                {
                    root.name = "Warrior_WeaponPrototype";
                    var animator = root.GetComponentInChildren<FightCharacterCombatAnimator>(true);
                    animator.EnsureAttachmentFrameIds();
                    var data = ScriptableObject.CreateInstance<WeaponAttachmentData>();
                    for (int i = 0; i < animator.FrameCount; i++) data.Set(new WeaponAttachmentData.Pose { frameId = animator.AttachmentFrameId(-1, i), position = new Vector2(.35f, .25f), rotation = 90, visible = true });
                    foreach (var sequence in animator.Sequences)
                        for (int i = 0; i < sequence.Frames.Count; i++) data.Set(new WeaponAttachmentData.Pose { frameId = sequence.AttachmentFrameId(i), position = new Vector2(.35f, .25f), rotation = 90, visible = true });
                    AssetDatabase.CreateAsset(data, Folder + "/Warrior_Attachment.asset");
                    root.AddComponent<FightWeaponAttachment>().Configure(animator, data, prefab);
                    PrefabUtility.SaveAsPrefabAsset(root, actorPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            Validate(prefab);
            var regressionScene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                typeof(FightAnimationValidation).GetMethod("ValidatePlayback", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { regressionScene });
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(regressionScene); }
            AssetDatabase.SaveAssets();
        }
        static void Validate(FightWeaponVisual prefab)
        {
            void Check(bool value, string message) { if (!value) throw new Exception(message); }
            var root = new GameObject("Attachment validation");
            var data = ScriptableObject.CreateInstance<WeaponAttachmentData>();
            var testWeapon = UnityEngine.Object.Instantiate(prefab);
            testWeapon.Configure(testWeapon.Renderer, new Vector2(.13f, .2f), 90);
            testWeapon.transform.localScale = new Vector3(1.25f, .8f, 1);
            root.transform.rotation = Quaternion.Euler(45, 0, 0);
            root.transform.localScale = new Vector3(-1.7f, .8f, 1);
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>();
                var animator = root.AddComponent<FightCharacterCombatAnimator>();
                var attack = new FightCharacterCombatAnimator.Sequence();
                var sprite = prefab.Renderer.sprite;
                attack.Configure(FightCharacterCombatAnimator.CombatAnimation.NormalAttack, new[] { sprite, sprite }, 2, -1, -1, -1);
                attack.ConfigureFrameBeats(new[] { .5f, .5f });
                animator.Configure(renderer, new[] { attack }); animator.ConfigureIdle(null, renderer, new[] { sprite });
                animator.EnsureAttachmentFrameIds();
                string first = attack.AttachmentFrameId(0), second = attack.AttachmentFrameId(1);
                Check(first != second, "Repeated sprite frames share identity");
                data.Set(new WeaponAttachmentData.Pose { frameId = first, visible = true, position = new Vector2(.3f, .4f), rotation = 30 });
                data.Set(new WeaponAttachmentData.Pose { frameId = second, visible = false, position = new Vector2(.5f, .2f) });
                var binding = root.AddComponent<FightWeaponAttachment>(); binding.Configure(animator, data, testWeapon);
                typeof(FightWeaponAttachment).GetMethod("Subscribe", Flags).Invoke(binding, null);
                binding.ReplaceWeapon(testWeapon);
                Check(!binding.Weapon.gameObject.activeSelf, "Missing pose must hide weapon");
                int notices = 0; animator.DisplayedFrameChanged += _ => notices++;
                animator.Play(FightCharacterCombatAnimator.CombatAnimation.NormalAttack);
                Check(animator.DisplayedFrame.Id == first && binding.Weapon.gameObject.activeSelf, "First attack frame did not attach");
                var gripWorld = binding.Weapon.transform.TransformPoint(binding.Weapon.GripPoint);
                var localGrip = renderer.transform.InverseTransformPoint(gripWorld);
                Check(Vector2.Distance(localGrip, new Vector2(.3f, .4f)) < .0001f, "Grip alignment failed");
                Check(binding.Weapon.transform.localScale == testWeapon.transform.localScale, "Weapon scale changed");
                renderer.flipX = true;
                typeof(FightWeaponAttachment).GetMethod("LateUpdate", Flags).Invoke(binding, null);
                localGrip = renderer.transform.InverseTransformPoint(binding.Weapon.transform.TransformPoint(binding.Weapon.GripPoint));
                Check(Mathf.Abs(localGrip.x + .3f) < .0001f, "Flip did not reflect grip");
                typeof(FightCharacterCombatAnimator).GetMethod("AdvancePlayback", Flags).Invoke(animator, new object[] { .26f });
                Check(animator.DisplayedFrame.Id == second && !binding.Weapon.gameObject.activeSelf && notices == 2, "Same-sprite second frame did not notify/hide");
                animator.InterruptPlayback();
                Check(animator.DisplayedFrame.Source == -1, "Interrupt did not display Idle identity");
                var opening = new FightCharacterCombatAnimator.Sequence();
                opening.Configure(FightCharacterCombatAnimator.CombatAnimation.OpeningIdle, new[] { sprite }, 2, -1, -1, -1);
                animator.Configure(renderer, new[] { attack, opening }); animator.EnsureAttachmentFrameIds();
                animator.BeginOpeningIdle();
                Check(animator.DisplayedFrame.Source == 7 && animator.DisplayedFrame.Id == opening.AttachmentFrameId(0), "Opening Idle identity missing");
                binding.ReplaceWeapon(prefab);
                Check(root.GetComponentsInChildren<FightWeaponVisual>(true).Length == 1, "Replacement retained previous weapon");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(testWeapon.gameObject); UnityEngine.Object.DestroyImmediate(data); }
        }
    }
}
