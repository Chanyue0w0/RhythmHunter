using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    /// <summary>Explicit, camera-scoped setup. Never rewrites imported VFX or global URP assets.</summary>
    [InitializeOnLoad]
    public static class FightVfxCameraSetup
    {
        const string ScenePath = "Assets/FightDemo/Scenes/Art2_FightScene.unity";
        static FightVfxCameraSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/FightVfxCameraSetup.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(request);
            try { Configure(); }
            catch (Exception e) { File.WriteAllText("Temp/FightVfxCameraSetup.result", "FAIL: " + e); }
        }

        [MenuItem("Rhythm Hunter/VFX/Configure Art2 Camera Compatibility")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true))
                    .First(c => c.name == "Art2_TestCamera");
                var data = camera.GetUniversalAdditionalCameraData();
                Undo.RecordObject(data, "Enable Art2 VFX depth and opaque textures");
                data.requiresDepthTexture = true;
                data.requiresColorTexture = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(data);
                EditorUtility.SetDirty(data);
                EditorSceneManager.MarkSceneDirty(scene);

                // Retire the earlier per-effect soft-particle workaround now that the
                // actual missing camera input is fixed. Retain the existing material reference.
                var glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/FightDemo/Materials/ElectricChargeGlow_Art2.mat");
                if (glow != null)
                {
                    Undo.RecordObject(glow, "Restore glow soft particles");
                    glow.EnableKeyword("_FADING_ON"); glow.SetFloat("_UseSP", 1);
                    EditorUtility.SetDirty(glow); AssetDatabase.SaveAssetIfDirty(glow);
                }
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Art2 scene.");
                File.WriteAllText("Temp/FightVfxCameraSetup.result",
                    $"PASS: {camera.name}: depth={data.requiresDepthTexture}, opaque={data.requiresColorTexture}. Scene saved. Global pipeline and purchased materials unchanged.");
                SceneView.RepaintAll();
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
