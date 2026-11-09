using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmHunter.FightDemo.Art2;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class Art2TorchRigSetup
    {
        static Art2TorchRigSetup() => EditorApplication.update += Requested;
        static void Requested()
        {
            const string request = "Temp/TorchRigSetup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool beatMotion;
            try { beatMotion = File.ReadAllText(request).Trim() == "beat-motion"; File.Delete(request); } catch (IOException) { return; }
            try { if (beatMotion) ConfigureBeatMotion(); else Run(); } catch (Exception e) { File.WriteAllText("Temp/TorchRigSetup.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Art2/Add Gentle Torch Beat Motion")]
        public static void ConfigureBeatMotion()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/FightDemo/Scenes/Art2_FightScene.unity") throw new Exception("Open Art2 outside Play Mode.");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var manager = all.Select(t => t.GetComponent<RhythmHunter.FightDemo.SceneBeatMotionManager>()).Single(c => c);
            Undo.RecordObject(manager, "Add gentle torch beat motion");
            var data = new SerializedObject(manager);
            if (!data.FindProperty("beatClock").objectReferenceValue) throw new Exception("Scene beat clock is missing.");
            var targets = data.FindProperty("targets");
            foreach (var name in new[] { "WallTorch_Left_Outer", "WallTorch_Left_Inner", "WallTorch_Right_Inner", "WallTorch_Right_Outer" })
            {
                var target = all.Single(t => t.name == name);
                if (!target.Find("FlameAnchor")) throw new Exception("Missing linked flame anchor: " + name);
                int index = -1;
                for (int i = 0; i < targets.arraySize; i++)
                    if (targets.GetArrayElementAtIndex(i).FindPropertyRelative("target").objectReferenceValue == target) { index = i; break; }
                if (index < 0) { index = targets.arraySize; targets.arraySize++; }
                var entry = targets.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("enabled").boolValue = true;
                entry.FindPropertyRelative("target").objectReferenceValue = target;
                entry.FindPropertyRelative("directionSpace").enumValueIndex = (int)Space.World;
                entry.FindPropertyRelative("direction").vector3Value = Vector3.up;
                entry.FindPropertyRelative("amplitude").floatValue = .025f;
                entry.FindPropertyRelative("durationInBeats").floatValue = .4f;
                entry.FindPropertyRelative("oscillations").intValue = 1;
            }
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
            File.WriteAllText("Temp/TorchRigSetup.result", "PASS: four linked torch roots added to existing SceneBeatMotionManager; amplitude 0.025, duration 0.4 beat, world up, one oscillation; existing song clock preserved; Art2 saved.");
        }
        [MenuItem("Rhythm Hunter/Art2/Bind Torch Rigs And Focus")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/FightDemo/Scenes/Art2_FightScene.unity") throw new Exception("Open Art2_FightScene outside Play Mode.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode first.");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var wall = all.Select(t => t.GetComponent<Art2FocusWall>()).FirstOrDefault(c => c);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/FightDemo/Art2Prototype/TorchParticleFocus.shader");
            if (!wall || !shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("Focus wall or particle shader invalid.");
            string[] lights = { "Torch_Left_RedFlicker", "Torch_InnerLeft_RedFlicker", "Torch_InnerRight_RedFlicker", "Torch_Right_RedFlicker" };
            string[] bodies = { "WallTorch_Left_Outer", "WallTorch_Left_Inner", "WallTorch_Right_Inner", "WallTorch_Right_Outer" };
            var rigs = lights.Select(n => all.Single(t => t.name == n)).ToArray();
            var sprites = bodies.Select(n => all.Single(t => t.name == n).GetComponent<SpriteRenderer>()).ToArray();
            if (rigs.Any(r => r.GetComponentsInChildren<ParticleSystem>(true).Length == 0)) throw new Exception("Torch flame is missing.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bind Art2 torch flames and lights");
            int particleRenderers = 0;
            for (int i = 0; i < rigs.Length; i++)
            {
                var body = sprites[i];
                var anchor = body.transform.Find("FlameAnchor");
                if (!anchor)
                {
                    var go = new GameObject("FlameAnchor");
                    Undo.RegisterCreatedObjectUndo(go, "Create flame anchor");
                    anchor = go.transform;
                    Undo.SetTransformParent(anchor, body.transform, "Parent flame anchor");
                    anchor.localPosition = VisibleTop(body.sprite);
                    anchor.localRotation = Quaternion.identity;
                    anchor.localScale = Vector3.one;
                }
                var light = rigs[i];
                Undo.SetTransformParent(light, anchor, "Bind light to torch");
                Undo.RecordObject(light, "Align light");
                light.position = anchor.position;
                foreach (Transform child in light)
                    if (child.GetComponent<ParticleSystem>())
                    {
                        Undo.RecordObject(child, "Align flame origin");
                        child.localPosition = Vector3.zero;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                    }
                foreach (var ps in light.GetComponentsInChildren<ParticleSystem>(true))
                {
                    Undo.RecordObject(ps, "Local flame simulation");
                    var main = ps.main;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(ps);
                }
                foreach (var renderer in light.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    var source = renderer.sharedMaterial;
                    if (!source) continue;
                    string asset = "Assets/FightDemo/Art2Prototype/TorchFocus_" + source.name.Replace("TorchFocus_", "") + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(asset);
                    if (!material)
                    {
                        material = new Material(source) { name = Path.GetFileNameWithoutExtension(asset), shader = shader };
                        AssetDatabase.CreateAsset(material, asset);
                    }
                    Undo.RecordObject(renderer, "Use focus-aware fire material");
                    renderer.sharedMaterial = material;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    particleRenderers++;
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(anchor);
            }
            Undo.RecordObject(wall, "Include torch VFX in focus wall");
            var data = new SerializedObject(wall);
            var roots = data.FindProperty("additionalRoots");
            var existing = Enumerable.Range(0, roots.arraySize).Select(i => roots.GetArrayElementAtIndex(i).objectReferenceValue).Where(o => o).ToList();
            foreach (var rig in rigs) if (!existing.Contains(rig)) existing.Add(rig);
            roots.arraySize = existing.Count;
            for (int i = 0; i < existing.Count; i++) roots.GetArrayElementAtIndex(i).objectReferenceValue = existing[i];
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(wall);
            for (int i = 0; i < rigs.Length; i++)
            {
                if (rigs[i].parent.name != "FlameAnchor" || rigs[i].localPosition.sqrMagnitude > .000001f) throw new Exception("Light anchor alignment failed.");
                foreach (Transform child in rigs[i])
                    if (child.GetComponent<ParticleSystem>() && child.localPosition.sqrMagnitude > .000001f) throw new Exception("Flame anchor alignment failed.");
            }
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
            File.WriteAllText("Temp/TorchRigSetup.result", "PASS: 4 torch anchors; flames and lights share origins; local particle simulation; " + particleRenderers + " focus particle renderers; shader import valid; Art2 scene saved.");
        }
        static Vector3 VisibleTop(Sprite sprite)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)))) throw new Exception("Cannot inspect torch image.");
                var pixels = texture.GetPixels32();
                var rect = sprite.rect;
                int left = texture.width, right = 0, top = -1;
                for (int y = (int)rect.y; y < rect.yMax; y++)
                    for (int x = (int)rect.x; x < rect.xMax; x++)
                        if (pixels[y * texture.width + x].a > 32) { left = Math.Min(left,x); right = Math.Max(right,x); top = Math.Max(top,y); }
                if (top < 0) throw new Exception("Torch sprite has no visible pixels.");
                return new Vector3(((left+right+1)*.5f-rect.x-sprite.pivot.x)/sprite.pixelsPerUnit,
                    (top+1-rect.y-sprite.pivot.y)/sprite.pixelsPerUnit, 0);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
