using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightVfxCompatibilityValidation
    {
        static FightVfxCompatibilityValidation() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/VfxCompatibilityValidation.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(request);
            try { Run(); } catch (Exception e) { File.WriteAllText("Temp/VfxCompatibilityValidation.result", "FAIL: " + e); }
        }
        [MenuItem("Rhythm Hunter/VFX/Validate Skills and Legacy VFX")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var report = new StringBuilder();
            ValidatePreviewRestoration(report);
            string[] folders = { "Assets/FightDemo/Prefabs/Skills", "Assets/FightDemo/Prefabs/VFX_old" };
            var paths = AssetDatabase.FindAssets("t:Prefab", folders).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
            int missingTotal = 0, badMaterials = 0;
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                int missing = prefab.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                var materials = prefab.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled &&
                    (!(r is SpriteRenderer sprite) || sprite.sprite != null) &&
                    (!(r is ParticleSystemRenderer ps) || ps.renderMode != ParticleSystemRenderMode.None))
                    .SelectMany(r => r is ParticleSystemRenderer ps && !ps.GetComponent<ParticleSystem>().trails.enabled ? r.sharedMaterials.Take(1) : r.sharedMaterials)
                    .Distinct().ToArray();
                var invalid = materials.Where(m => m == null || m.shader == null || !m.shader.isSupported || ShaderUtil.ShaderHasError(m.shader)).ToArray();
                missingTotal += missing; badMaterials += invalid.Length;
                report.AppendLine($"AUDIT {path}: renderers={prefab.GetComponentsInChildren<Renderer>(true).Length} missingScripts={missing} invalidMaterials={invalid.Length} softMaterials={materials.Count(m => m != null && m.IsKeywordEnabled("_FADING_ON"))}");
                foreach (var m in invalid) report.AppendLine("  INVALID: " + (m == null ? "Missing material" : AssetDatabase.GetAssetPath(m) + " shader=" + m.shader));
            }
            report.AppendLine($"AUDIT TOTAL prefabs={paths.Length} missingScripts={missingTotal} invalidMaterialReferences={badMaterials}");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            try
            {
                var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First(c => c.name == "Art2_TestCamera");
                camera.scene = scene; camera.enabled = false;
                var extra = camera.GetComponent<UniversalAdditionalCameraData>(); extra.renderPostProcessing = false;
                report.AppendLine($"SAVED CAMERA: depth={extra.requiresDepthTexture} opaque={extra.requiresColorTexture}");
                extra.requiresColorTexture = true;
                var rt = new RenderTexture(480, 270, 24); camera.targetTexture = rt;
                try
                {
                    foreach (string path in new[] {
                        "Assets/FightDemo/Prefabs/Skills/Mage/UltLighteningStrike.prefab",
                        "Assets/FightDemo/Prefabs/Skills/Mage/ElectricChargeGlow.prefab",
                        "Assets/FightDemo/Prefabs/VFX_old/FireExplosion.prefab",
                        "Assets/FightDemo/Prefabs/VFX_old/ChargeEffect.prefab" })
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab == null) continue;
                        var instance = UnityEngine.Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(instance, scene);
                        instance.transform.position = new Vector3(0, .15f, 0);
                        try
                        {
                            foreach (var t in instance.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                            var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                            var renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
                            if (path.EndsWith("ElectricChargeGlow.prefab"))
                                foreach (var r in renderers) r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/cfxr proc glow soft add.mat");
                            foreach (var p in particles) { var main = p.main; main.stopAction = ParticleSystemStopAction.None; p.useAutoRandomSeed = false; p.randomSeed = 1; }
                            foreach (var r in renderers) r.enabled = false;
                            extra.requiresDepthTexture = false; var baselineOff = Capture(null);
                            extra.requiresDepthTexture = true; var baselineOn = Capture(null);
                            foreach (var r in renderers)
                            {
                                int off = 0, on = 0, count = 0;
                                r.enabled = true;
                                foreach (float time in new[] { .075f, .15f, .3f, .6f })
                                {
                                    foreach (var p in particles) { p.Simulate(time, false, true, false); p.Pause(false); }
                                    count = Math.Max(count, r.GetComponent<ParticleSystem>().particleCount);
                                    extra.requiresDepthTexture = false; off = Math.Max(off, Difference(Capture(null), baselineOff));
                                    extra.requiresDepthTexture = true; on = Math.Max(on, Difference(Capture(null), baselineOn));
                                }
                                report.AppendLine($"RENDER {prefab.name}/{r.name}: particles={count} depthOffPixels={off} depthOnPixels={on} soft={r.sharedMaterial.IsKeywordEnabled("_FADING_ON")}");
                                r.enabled = false;
                            }
                            foreach (var r in renderers) r.enabled = true;
                            foreach (var p in particles) { p.Simulate(.15f, false, true, false); p.Pause(false); }
                            extra.requiresDepthTexture = false; Capture(prefab.name + "_DepthOff");
                            extra.requiresDepthTexture = true; Capture(prefab.name + "_DepthOn");
                        }
                        finally { UnityEngine.Object.DestroyImmediate(instance); }
                    }
                    Color32[] Capture(string label)
                    {
                        // A disabled depth camera can inherit a previous camera's global
                        // texture. Reset it to model a genuinely missing input deterministically.
                        if (!extra.requiresDepthTexture) Shader.SetGlobalTexture("_CameraDepthTexture", Texture2D.blackTexture);
                        camera.Render(); var old = RenderTexture.active; RenderTexture.active = rt;
                        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                        try
                        {
                            texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); texture.Apply();
                            if (label != null) File.WriteAllBytes("Temp/Vfx_" + label + ".png", texture.EncodeToPNG());
                            return texture.GetPixels32();
                        }
                        finally { RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(texture); }
                    }
                }
                finally { camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt); }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            File.WriteAllText("Temp/VfxCompatibilityValidation.result", report.ToString());
        }
        static void ValidatePreviewRestoration(StringBuilder report)
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            var rendererField = typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            bool depth = asset.supportsCameraDepthTexture, opaque = asset.supportsCameraOpaqueTexture;
            int renderer = (int)rendererField.GetValue(asset);
            var go = new GameObject("Art2_SurfaceDiagnosticCamera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.cameraType = CameraType.Preview;
                typeof(FightVfxScenePreview).GetMethod("BeginCamera", flags).Invoke(null, new object[] { default(ScriptableRenderContext), camera });
                bool applied = asset.supportsCameraDepthTexture && asset.supportsCameraOpaqueTexture && (int)rendererField.GetValue(asset) != renderer;
                typeof(FightVfxScenePreview).GetMethod("Restore", flags).Invoke(null, null);
                bool restored = asset.supportsCameraDepthTexture == depth && asset.supportsCameraOpaqueTexture == opaque && (int)rendererField.GetValue(asset) == renderer;
                report.AppendLine($"PREVIEW scope: applied={applied}, restored={restored}");
                if (!applied || !restored) throw new InvalidOperationException("Scoped preview render settings failed validation.");
            }
            finally
            {
                typeof(FightVfxScenePreview).GetMethod("Restore", flags).Invoke(null, null);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
        static int Difference(Color32[] image, Color32[] baseline)
        {
            int pixels = 0;
            for (int i=0;i<image.Length;i++) if (Math.Abs(image[i].r-baseline[i].r)+Math.Abs(image[i].g-baseline[i].g)+Math.Abs(image[i].b-baseline[i].b)>15) pixels++;
            return pixels;
        }
    }
}
