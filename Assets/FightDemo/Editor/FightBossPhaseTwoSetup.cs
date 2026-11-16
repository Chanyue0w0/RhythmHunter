using System;
using System.IO;
using System.Linq;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightBossPhaseTwoSetup
    {
        static FightBossPhaseTwoSetup() => EditorApplication.update += Poll;
        static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        static void Poll()
        {
            const string request = "Temp/BossPhaseTwoSetup.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            bool fit = File.ReadAllText(request).Trim() == "fit";
            File.Delete(request);
            try
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                if (!scene.isLoaded) throw new Exception("Open Art2_FightScene first.");
                var all = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
                var fight = all.Select(x => x.GetComponent<FightCombatController>()).First(x => x);
                var intro = fight.GetComponent<FightBossEntrance>();
                var introData = new SerializedObject(intro);
                var door = (Transform)introData.FindProperty("doorSpawnAnchor").objectReferenceValue;
                var camera = (Camera)introData.FindProperty("view").objectReferenceValue;
                if (!door || !camera) throw new Exception("Entrance door/camera missing.");
                var root = all.FirstOrDefault(x => x.name == "BossPhaseTwoPositions");
                if (!root) { root = new GameObject("BossPhaseTwoPositions").transform; UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root.gameObject, scene); }
                var boss = Anchor(root, "Boss_Door", door.position);
                var heroes = new Transform[3];
                string[] names = { "Hero_Left_Front", "Hero_Middle", "Hero_Right_Rear" };
                for (int i = 0; i < 3; i++)
                {
                    var ray = camera.ViewportPointToRay(new Vector3(.35f + i * .15f, .25f, 0));
                    var plane = new Plane(Vector3.up, Vector3.zero);
                    if (!plane.Raycast(ray, out float distance)) throw new Exception("Camera cannot project phase-two ground anchors.");
                    heroes[i] = Anchor(root, names[i], ray.GetPoint(distance));
                }
                AddIdle("LongSwordWarrior_Weapon", "Idle_back", "Assets/FightDemo/Arts/角色/1.hero/idle_back");
                AddIdle("Mage", "Idle_back", "Assets/FightDemo/Arts/角色/2.mage/idle_back");
                AddIdle("Skeleton", "Idle_back", "Assets/FightDemo/Arts/角色/5.skelenton/idle_back");
                AddIdle("Goblin_King", "IdlePh2", null);
                var transition = fight.GetComponent<FightBossPhaseTwoTransition>() ?? fight.gameObject.AddComponent<FightBossPhaseTwoTransition>();
                Set(transition, "fight", fight); Set(transition, "bossDestination", boss);
                var settings = new SerializedObject(transition); var array = settings.FindProperty("heroDestinations"); array.arraySize = 3;
                if (fit) settings.FindProperty("bossDestinationScale").floatValue = .6f;
                for (int i = 0; i < 3; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = heroes[i];
                settings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(transition);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
                if (!File.ReadAllText(scene.path).Contains("heroIdleAnimation: Idle_back")) throw new Exception("Transition component did not persist to scene.");
                File.WriteAllText("Temp/BossPhaseTwoSetup.result", "PASS: scene transition, four editable foot anchors, three Idle_back animations (0.33/0.33/0.34 beat), IdlePh2 placeholder from existing Idle. Existing sequences/timings retained.");
            }
            catch (Exception e) { File.WriteAllText("Temp/BossPhaseTwoSetup.result", "FAIL: " + e); }
        }
        static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            var existing = parent.Find(name); if (existing) return existing;
            var result = new GameObject(name).transform; result.SetParent(parent, false); result.position = position; return result;
        }
        static void AddIdle(string prefab, string name, string folder)
        {
            string path = "Assets/FightDemo/Prefabs/ArtBattle/" + prefab + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var animator = root.GetComponentInChildren<FightCharacterCombatAnimator>(true);
                if (!animator) throw new Exception("Animator missing: " + path);
                if (animator.FindSequence(name) != null) return;
                Sprite[] sprites;
                if (folder != null)
                    sprites = Directory.GetFiles(folder, "*.png").OrderBy(x => x, StringComparer.Ordinal)
                        .SelectMany(x => AssetDatabase.LoadAllAssetsAtPath(x.Replace('\\', '/')).OfType<Sprite>()).ToArray();
                else
                {
                    var frames = new SerializedObject(animator).FindProperty("frames");
                    sprites = Enumerable.Range(0, frames.arraySize).Select(i => (Sprite)frames.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
                }
                if (sprites.Length == 0) throw new Exception("No sprites: " + path);
                var sequence = animator.AddCustomSequence(name);
                sequence.Configure(FightCharacterCombatAnimator.CombatAnimation.NormalAttack, sprites, 16, -1, -1, -1);
                var durations = Enumerable.Repeat((float)Math.Floor(100d / sprites.Length) / 100, sprites.Length).ToArray();
                durations[durations.Length - 1] = 1 - durations.Take(durations.Length - 1).Sum();
                sequence.ConfigureFrameBeats(durations); sequence.EnsureAttachmentFrameIds();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
