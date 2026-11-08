using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class Art2FocusWallValidation
    {
        static Art2FocusWallValidation() => EditorApplication.update += Requested;
        private static void Requested()
        {
            if (!File.Exists("Temp/FocusWallValidation.request") || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            bool tune;
            try { tune = File.ReadAllText("Temp/FocusWallValidation.request").Trim() == "enhance-details"; File.Delete("Temp/FocusWallValidation.request"); } catch (IOException) { return; }
            try { if (tune) ConfigureDetailBlur(); Run(); }
            catch (Exception ex) { File.WriteAllText("Temp/FocusWallValidation.result", "FAIL: " + ex); Debug.LogException(ex); }
        }
        private static void ConfigureDetailBlur()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode before saving detail blur.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.path.EndsWith("Art2_FightScene.unity")) throw new Exception("Open Art2 scene.");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var wall = all.Single(t => t.name == "DistantFocusWall_MoveMe").GetComponent<RhythmHunter.FightDemo.Art2.Art2FocusWall>();
            var names = new[] { "OverheadMoss_GroundAnchor", "WallTorch_Left_Outer", "WallTorch_Left_Inner", "WallTorch_Right_Inner", "WallTorch_Right_Outer" };
            Undo.RecordObject(wall, "Enhance crowd and torch blur");
            var data = new SerializedObject(wall);
            var roots = data.FindProperty("emphasizedRoots"); roots.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++) roots.GetArrayElementAtIndex(i).objectReferenceValue = all.Single(t => t.name == names[i]);
            data.FindProperty("detailBlurMultiplier").floatValue = 1.5f;
            data.FindProperty("detailBlurTransitionScale").floatValue = .2f;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(wall);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene)) throw new Exception("Cannot save scene.");
        }
        [MenuItem("Rhythm Hunter/Art2/Validate Focus Wall")]
        public static void Run()
        {
            const string path = "Assets/FightDemo/Art2Prototype/Art2FocusWall.cs";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/FightDemo/Prefabs/ArtBattle/CaveEnvironment_2_5D.prefab", ImportAssetOptions.ForceUpdate);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (!script || script.GetClass() == null) throw new Exception("Art2FocusWall MonoScript cannot resolve its class.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/ArtBattle/CaveEnvironment_2_5D.prefab");
            var wall = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "DistantFocusWall_MoveMe");
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(wall.gameObject) != 0)
                throw new Exception("Focus wall contains a missing script reference.");
            var component = wall.GetComponent(script.GetClass());
            if (!component) throw new Exception("Focus wall component absent.");
            var targets = new SerializedObject(component).FindProperty("targets");
            for (int i = 0; i < targets.arraySize; i++)
                if (!targets.GetArrayElementAtIndex(i).objectReferenceValue) throw new Exception("Missing renderer " + i);
            int sceneWalls = 0;
            var coverage = new System.Text.StringBuilder();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var item in root.GetComponentsInChildren<Transform>(true))
                        if (item.name == "DistantFocusWall_MoveMe")
                        {
                            if (!item.GetComponent(script.GetClass()) || GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) != 0)
                                throw new Exception("Loaded scene wall still has a missing component: " + scene.path);
                            sceneWalls++;
                            var liveWall = item.GetComponent(script.GetClass());
                            script.GetClass().GetMethod("Apply", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(liveWall, null);
                            var renderers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true));
                            int checkedCount = 0;
                            foreach (var renderer in renderers)
                            {
                                bool scenery = renderer.name.StartsWith("WallTorch_") || renderer.name.StartsWith("CaveCrowd_");
                                bool fire = renderer is ParticleSystemRenderer && renderer.sharedMaterial && renderer.sharedMaterial.shader.name == "RhythmHunter/Art2/TorchParticleFocus";
                                if (!scenery && !fire) continue;
                                var block = new MaterialPropertyBlock();
                                renderer.GetPropertyBlock(block);
                                var settings = block.GetVector("_FocusWallSettings");
                                var plane = block.GetVector("_FocusWallPlane");
                                var center = renderer.bounds.center;
                                float distance = Vector3.Dot(new Vector3(plane.x, plane.y, plane.z), center) + plane.w;
                                float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(distance / Mathf.Max(.01f, settings.y)));
                                if (settings.x != 1) throw new Exception("Focus parameters missing: " + renderer.name);
                                var profile = block.GetVector("_FocusWallBlurProfile");
                                float blurWeight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(distance / Mathf.Max(.01f, settings.y * profile.y)));
                                coverage.AppendLine(renderer.name + ": effective blur=" + (settings.z * profile.x * blurWeight).ToString("F2") + ", fog=" + settings.w + ", fog weight=" + weight.ToString("F3") + ", enabled=" + renderer.enabled);
                                checkedCount++;
                            }
                            if (scene.path.EndsWith("Art2_FightScene.unity") && checkedCount != 10) throw new Exception("Expected 4 torch bodies, 2 crowds, 4 fire renderers; got " + checkedCount);
                        }
            }
            foreach (var shaderPath in new[] { "SpriteCardLit.shader", "CaveFloorUnlit.shader", "TorchParticleFocus.shader" })
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/FightDemo/Art2Prototype/" + shaderPath);
                if (!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("Shader import error: " + shaderPath);
            }
            string result = "PASS: " + script.GetClass().FullName + "; prefab component resolved; " + targets.arraySize + " valid targets; loaded scene walls: " + sceneWalls + "; no shader import errors.";
            File.WriteAllText("Temp/FocusWallValidation.result", result + "\n" + coverage);
            Debug.Log(result);
        }
    }
}
