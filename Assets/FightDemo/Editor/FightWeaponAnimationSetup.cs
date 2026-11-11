using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightWeaponAnimationSetup
    {
        const string Folder = "Assets/FightDemo/Prefabs/ArtBattle/WeaponPrototype/";
        const string Path = Folder + "Weapon_ResonanceSword.prefab";
        const string Art = "Assets/FightDemo/Arts/角色/1.hero/spined-animate/";
        static FightWeaponAnimationSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            if (!File.Exists("Temp/WeaponAnimationSetup.request") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete("Temp/WeaponAnimationSetup.request");
            try { Run(); } catch (Exception e) { File.WriteAllText("Temp/WeaponAnimationValidation.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Weapon/Configure Resonance Sword Animation")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            const string scenePath = "Assets/FightDemo/Scenes/Art2_FightScene.unity";
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var source = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<FightWeaponVisual>(true))
                .FirstOrDefault(w => w.name == "Weapon_ResonanceSword");
            bool temporary = source == null;
            if (temporary)
            {
                var original = AssetDatabase.LoadAssetAtPath<GameObject>(Path) ?? AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Weapon_GripPlaceholder.prefab");
                source = UnityEngine.Object.Instantiate(original).GetComponent<FightWeaponVisual>();
                SceneManager.MoveGameObjectToScene(source.gameObject, scene);
                source.name = "Weapon_ResonanceSword";
            }
            bool active = source.gameObject.activeSelf;
            try
            {
                var root = source.gameObject;
                var player = root.GetComponent<FightCharacterCombatAnimator>() ?? root.AddComponent<FightCharacterCombatAnimator>();
                player.ConfigureIdle(null, source.Renderer, new[] { source.Renderer.sprite });
                var gearAnchor = root.transform.Find("GearAnchor");
                if (gearAnchor == null)
                {
                    gearAnchor = new GameObject("GearAnchor").transform; gearAnchor.SetParent(root.transform, false);
                    gearAnchor.localPosition = new Vector3(source.Renderer.sprite.bounds.center.x, source.Renderer.sprite.bounds.min.y + .04f, -.01f);
                    var gearPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "gear.prefab");
                    var gearSource = scene.GetRootGameObjects().FirstOrDefault(go => go.name.Equals("gear", StringComparison.OrdinalIgnoreCase));
                    GameObject gear;
                    if (gearPrefab != null) gear = (GameObject)PrefabUtility.InstantiatePrefab(gearPrefab, gearAnchor);
                    else if (gearSource != null) gear = UnityEngine.Object.Instantiate(gearSource, gearAnchor, false);
                    else
                    {
                        gear = new GameObject("GearVisual", typeof(SpriteRenderer), typeof(Animator)); gear.transform.SetParent(gearAnchor, false);
                        gear.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "gear.png");
                        gear.GetComponent<Animator>().runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/FightDemo/AnimatorAnimation/gear.controller");
                    }
                    gear.transform.localPosition = Vector3.zero; gear.transform.localRotation = Quaternion.identity;
                    var gearRenderer = gear.GetComponentInChildren<SpriteRenderer>();
                    float width = gearRenderer != null && gearRenderer.sprite != null ? gearRenderer.sprite.bounds.size.x : 1;
                    gearAnchor.localScale = Vector3.one * (.16f / Mathf.Max(.01f, width));
                }
                var arcs = root.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(ps => ps.name.Replace(" ", "") == "ElectricArcs");
                var so = new SerializedObject(source);
                so.FindProperty("gearAnchor").objectReferenceValue = gearAnchor;
                var states = so.FindProperty("animationStates");
                if (states.arraySize == 0)
                {
                    states.arraySize = 3;
                    Fill(0, "Normal", new[] { "sword_1.png" });
                    Fill(1, "Charge_1", new[] { "sword_2_1.png", "sword_2_2.png", "sword_2_3.png" });
                    Fill(2, "Charge_Full", new[] { "sword_3_1.png", "sword_3_2.png", "sword_3_3.png" });
                    void Fill(int index, string id, string[] filenames)
                    {
                        var state = states.GetArrayElementAtIndex(index); state.FindPropertyRelative("stateId").stringValue = id;
                        state.FindPropertyRelative("loop").boolValue = true;
                        var frames = state.FindPropertyRelative("frames"); frames.arraySize = filenames.Length;
                        for (int i = 0; i < filenames.Length; i++)
                        {
                            var frame = frames.GetArrayElementAtIndex(i);
                            frame.FindPropertyRelative("sprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Art + filenames[i]) ?? source.Renderer.sprite;
                            frame.FindPropertyRelative("beats").floatValue = filenames.Length == 1 ? 1 : i == filenames.Length - 1 ? .34f : .33f;
                        }
                        var visible = state.FindPropertyRelative("visibleObjects"); visible.arraySize = index == 2 ? arcs != null ? 2 : 1 : 0;
                        if (index == 2)
                        {
                            visible.GetArrayElementAtIndex(0).objectReferenceValue = gearAnchor.gameObject;
                            if (arcs != null) visible.GetArrayElementAtIndex(1).objectReferenceValue = arcs.gameObject;
                        }
                    }
                }
                if (states.arraySize >= 3 && states.GetArrayElementAtIndex(2).FindPropertyRelative("stateId").stringValue == "Charge_Full")
                    states.GetArrayElementAtIndex(2).FindPropertyRelative("animatorStateOnEnter").stringValue = "Base Layer.GearSpin";
                so.ApplyModifiedPropertiesWithoutUndo();
                gearAnchor.gameObject.SetActive(false); if (arcs != null) arcs.gameObject.SetActive(false);
                root.SetActive(true);
                if (temporary) PrefabUtility.SaveAsPrefabAsset(root, Path);
                else PrefabUtility.SaveAsPrefabAssetAndConnect(root, Path, InteractionMode.AutomatedAction);
                root.SetActive(active);
                if (!temporary) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }
            finally { if (temporary) UnityEngine.Object.DestroyImmediate(source.gameObject); }
            var hero = PrefabUtility.LoadPrefabContents("Assets/FightDemo/Prefabs/ArtBattle/LongSwordWarrior_Weapon.prefab");
            try
            {
                var attachment = hero.GetComponentInChildren<FightWeaponAttachment>(true);
                if (attachment == null) throw new Exception("Warrior weapon attachment missing.");
                var so = new SerializedObject(attachment);
                so.FindProperty("weaponPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Path).GetComponent<FightWeaponVisual>();
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(hero, "Assets/FightDemo/Prefabs/ArtBattle/LongSwordWarrior_Weapon.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(hero); }
            Validate();
        }

        [MenuItem("Rhythm Hunter/Weapon/Validate Weapon State Animation")]
        public static void Validate()
        {
            var lines = new List<string>();
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
            try
            {
                var weapon = root.GetComponent<FightWeaponVisual>();
                weapon.BindAnimationSource(null);
                Check(weapon.SetWeaponAnimationState("Normal"), "Normal state accepted");
                Check(!weapon.GearAnchor.gameObject.activeSelf, "Normal hides gear");
                Check(weapon.SetWeaponAnimationState("Charge_1"), "Half charge accepted");
                var player = weapon.VisualPlayer;
                Step(.17f); Check(player.ActiveFrame == 1, "Fractional beats use existing animation stepping");
                Check(weapon.SetWeaponAnimationState("Charge_1") && player.ActiveFrame == 1, "Repeated state does not restart");
                Step(.38f); Check(player.ActiveFrame == 0, "Loop retains overshoot across cycle boundary");
                player.SetPlaybackPaused(true); Step(1); Check(player.ActiveFrame == 0, "Paused weapon holds frame");
                player.SetPlaybackPaused(false); Step(.12f); Check(player.ActiveFrame == 1, "Resume continues progress");
                Check(!weapon.SetWeaponAnimationState("Unknown") && weapon.CurrentStateId == "Charge_1", "Unknown state leaves current animation intact");
                weapon.SetWeaponChargeStage(FightWeaponVisual.ChargeStage.FullCharged);
                var arcs = root.GetComponentsInChildren<ParticleSystem>(true).First(p => p.name.Replace(" ", "") == "ElectricArcs");
                Check(weapon.GearAnchor.gameObject.activeSelf && arcs.gameObject.activeSelf, "Full charge shows gear and Electric Arcs");
                var gearAnimator = weapon.GearAnchor.GetComponentInChildren<Animator>();
                gearAnimator.Rebind(); gearAnimator.Play("Base Layer.GearSpin", 0, 0); gearAnimator.Update(0);
                Quaternion startRotation = gearAnimator.transform.localRotation;
                gearAnimator.Update(.15f);
                Check(Quaternion.Angle(startRotation, gearAnimator.transform.localRotation) > .1f, "Authored GearSpin actually rotates the gear");
                weapon.SetWeaponChargeStage(FightWeaponVisual.ChargeStage.Uncharged);
                Check(!weapon.GearAnchor.gameObject.activeSelf && !arcs.gameObject.activeSelf && arcs.particleCount == 0, "Leaving full charge hides extras and clears particles");
                var state = weapon.AnimationStates[1]; state.loop = false;
                weapon.SetWeaponAnimationState("Charge_1"); Step(2); Check(player.ActiveFrame == 2, "Nonloop state holds final frame");
                state.loop = true;
                weapon.SetWeaponAnimationState("Normal");
                typeof(FightCharacterCombatAnimator).GetField("fallbackBpm", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 60f);
                player.BindBeatSource(null);
                weapon.SetWeaponAnimationState("Charge_1"); Step(.34f);
                Check(player.ActiveFrame == 1 && weapon.Renderer.sprite == state.frames[1].sprite, "BPM changes preserve beat-based frame durations and sprite output");
                typeof(FightCharacterCombatAnimator).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
                var properties = new MaterialPropertyBlock(); weapon.Renderer.GetPropertyBlock(properties);
                Check(properties.GetTexture("_MainTex") == weapon.Renderer.sprite.texture, "Animated sprite texture binding follows each frame");
                var owner = new GameObject("WeaponAnimationTestOwner").AddComponent<FightCharacterCombatAnimator>();
                try
                {
                    weapon.BindAnimationSource(owner); owner.SetPlaybackPaused(true);
                    typeof(FightWeaponVisual).GetMethod("SyncPause", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(weapon, null);
                    Check(player.PlaybackPaused, "Weapon follows owner's pause state");
                    owner.SetPlaybackPaused(false);
                    typeof(FightWeaponVisual).GetMethod("SyncPause", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(weapon, null);
                    Check(!player.PlaybackPaused && player.ActiveFrame == 1, "Owner resume does not restart weapon frame");
                }
                finally { UnityEngine.Object.DestroyImmediate(owner.gameObject); }
                var custom = new FightWeaponVisual.AnimationState { stateId = "Extra_Custom", loop = true };
                custom.frames.Add(new FightWeaponVisual.AnimationFrame { sprite = weapon.Renderer.sprite, beats = .5f });
                ((List<FightWeaponVisual.AnimationState>)typeof(FightWeaponVisual).GetField("animationStates", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(weapon)).Add(custom);
                Check(weapon.SetWeaponAnimationState("Extra_Custom"), "Additional arbitrary state IDs are supported");
                var controller = weapon.GearAnchor.GetComponentInChildren<Animator>(true)?.runtimeAnimatorController;
                Check(controller != null, "Authored gear Animator preserved");
                bool curves = controller.animationClips.Any(c => AnimationUtility.GetCurveBindings(c).Length > 0);
                lines.Add(curves ? "Gear controller has animated curves." : "NOTE: gear controller currently has no animation curves; author rotation in its clip.");
                void Step(float seconds) => typeof(FightCharacterCombatAnimator).GetMethod("AdvancePlayback", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, new object[] { seconds });
                void Check(bool ok, string label) { if (!ok) throw new Exception(label); lines.Add("PASS: " + label); }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            File.WriteAllLines("Temp/WeaponAnimationValidation.result", lines);
        }
    }
}
