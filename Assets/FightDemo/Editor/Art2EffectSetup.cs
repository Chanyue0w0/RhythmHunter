using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmHunter.FightDemo.Art2;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class Art2EffectSetup
    {
        static Art2EffectSetup() => EditorApplication.update += Requested;
        static void Requested()
        {
            const string request = "Temp/Art2EffectSetup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(request); } catch (IOException) { return; }
            try { Run(); } catch (Exception e) { File.WriteAllText("Temp/Art2EffectSetup.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Art2/Configure Effect Camera Alignment")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run outside Play Mode.");
            var report = new System.Text.StringBuilder();
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/FightDemo/Prefabs/VFX" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach (var path in paths)
            {
                if (path.Contains("/DamageNumbers/")) { report.AppendLine(path + ": existing pooled camera-facing digits, unchanged"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var adapter = root.GetComponent<Art2EffectPresentation>();
                    if (!adapter) adapter = root.AddComponent<Art2EffectPresentation>();
                    var data = new SerializedObject(adapter);
                    if (path.EndsWith("/ShieldVFXPixel.prefab") || path.EndsWith("/SwordSlash_Pixel.prefab"))
                        data.FindProperty("keepAboveGround").boolValue = true;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    var particleRenderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
                    report.AppendLine(Path.GetFileName(path) + ": sprites=" + root.GetComponentsInChildren<SpriteRenderer>(true).Length + ", particles=" + string.Join(",", particleRenderers.Select(p => p.renderMode + "/" + p.alignment + (p.mesh ? " mesh=" + p.mesh.name + " size=" + p.mesh.bounds.size : ""))));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var path in paths.Where(p => !p.Contains("/DamageNumbers/"))) ValidateFacing(path, report);
            ValidateShield(report);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/Art2EffectSetup.result", "PASS: effect adapters authored; shield/camera validation passed.\n" + report);
        }
        static void ValidateFacing(string path, System.Text.StringBuilder report)
        {
            var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                SceneManager.MoveGameObjectToScene(instance, SceneManager.GetActiveScene());
                var adapter = instance.GetComponent<Art2EffectPresentation>();
                typeof(Art2EffectPresentation).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(adapter, null);
                foreach (var renderer in instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    bool planar = renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh && renderer.mesh.bounds.size.z <= Mathf.Max(renderer.mesh.bounds.size.x, renderer.mesh.bounds.size.y) * .05f;
                    bool billboard = renderer.renderMode == ParticleSystemRenderMode.Billboard || renderer.renderMode == ParticleSystemRenderMode.Stretch;
                    if ((planar || billboard) && renderer.alignment != ParticleSystemRenderSpace.View)
                        throw new Exception("Effect not view-aligned after initialization: " + path);
                }
                report.AppendLine(Path.GetFileName(path) + ": runtime view alignment PASS");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        static void ValidateShield(System.Text.StringBuilder report)
        {
            var scene = SceneManager.GetActiveScene();
            var view = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Art2CameraController>(true)).Select(c=>c.GetComponent<Camera>()).FirstOrDefault();
            if (!view) throw new Exception("Art2 camera required for validation.");
            var effect = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/VFX/ShieldVFXPixel.prefab"));
            var rotation=view.transform.rotation;
            try
            {
                SceneManager.MoveGameObjectToScene(effect,scene);
                effect.transform.SetPositionAndRotation(new Vector3(0,.4f,0),Quaternion.Euler(45,0,0));
                var adapter=effect.GetComponent<Art2EffectPresentation>();
                var update=typeof(Art2EffectPresentation).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
                update.Invoke(adapter,null);
                foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>(true))ps.Simulate(.4f,false,true,true);
                foreach(float pitch in new[]{30f,45f,60f})
                {
                    view.transform.rotation=Quaternion.Euler(pitch,15,0);
                    update.Invoke(adapter,null);
                    // Renderer bounds refresh after the root correction.
                    foreach(var pr in effect.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    {
                        if(pr.renderMode==ParticleSystemRenderMode.None)continue;
                        if(pr.alignment!=ParticleSystemRenderSpace.View)throw new Exception("Shield not camera aligned.");
                        report.AppendLine("Shield pitch="+pitch+", bounds min Y="+pr.bounds.min.y.ToString("F3")+", root Y="+effect.transform.position.y.ToString("F3"));
                        if(pr.GetComponent<ParticleSystem>().particleCount>0 && pr.bounds.min.y < -.01f)throw new Exception("Shield remains below ground.");
                    }
                }
            }
            finally { view.transform.rotation=rotation; UnityEngine.Object.DestroyImmediate(effect); }
        }
    }
}
