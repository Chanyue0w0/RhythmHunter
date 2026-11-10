using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class ElectricChargeGlowDiagnostic
    {
        const string PrefabPath = "Assets/FightDemo/Prefabs/Skills/Mage/ElectricChargeGlow.prefab";
        static ElectricChargeGlowDiagnostic() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/ElectricChargeGlowDiagnostic.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(request);
            try { Run(); } catch (Exception e) { File.WriteAllText("Temp/ElectricChargeGlowDiagnostic.result", "FAIL: " + e); }
        }
        [MenuItem("Rhythm Hunter/VFX/Diagnose Electric Charge Glow")]
        static void Run()
        {
            var report = new StringBuilder();
            report.AppendLine("Pipeline=" + GraphicsSettings.currentRenderPipeline);
            CaptureActualScene(report);
            foreach (var particle in Resources.FindObjectsOfTypeAll<ParticleSystem>())
                if (!EditorUtility.IsPersistent(particle) && particle.gameObject.scene.IsValid() && particle.name.Contains("ElectricCharge"))
                {
                    var r = particle.GetComponent<ParticleSystemRenderer>();
                    report.AppendLine($"Scene={particle.gameObject.scene.path} particle={particle.name} active={particle.gameObject.activeInHierarchy} position={particle.transform.position} scale={particle.transform.lossyScale} count={particle.particleCount} playing={particle.isPlaying} material={r.sharedMaterial} shader={r.sharedMaterial?.shader} layer={particle.gameObject.layer}");
                }
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject effect = null;
            try
            {
                effect = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                SceneManager.MoveGameObjectToScene(effect, scene);
                var ps = effect.GetComponentInChildren<ParticleSystem>(); var renderer = ps.GetComponent<ParticleSystemRenderer>();
                var main = ps.main; main.stopAction = ParticleSystemStopAction.None;
                ps.useAutoRandomSeed = false; ps.randomSeed = 1;
                ps.Simulate(.075f, true, true, false);
                var material = renderer.sharedMaterial;
                report.AppendLine($"Prefab particle count={ps.particleCount} mode={renderer.renderMode} alignment={renderer.alignment} bounds={renderer.bounds} lifetime={main.startLifetime.constant} missingScripts={GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(effect)}");
                report.AppendLine($"Material={AssetDatabase.GetAssetPath(material)} Shader={material.shader.name} path={AssetDatabase.GetAssetPath(material.shader)} supported={material.shader.isSupported} errors={ShaderUtil.ShaderHasError(material.shader)} keywords={string.Join(",", material.shaderKeywords)}");
                var cameraObject = new GameObject("Art2_SurfaceDiagnosticCamera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.cameraType = CameraType.Preview;
                camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = 3; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.transform.position = renderer.bounds.center + new Vector3(0, 0, -10);
                camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.enabled = false;
                var target = new RenderTexture(256, 256, 24); camera.targetTexture = target;
                try
                {
                    Capture("Original");
                    var copy = new Material(material); renderer.sharedMaterial = copy;
                    try { copy.DisableKeyword("_FADING_ON"); copy.SetFloat("_UseSP", 0); Capture("NoSoftParticles"); }
                    finally { renderer.sharedMaterial = material; UnityEngine.Object.DestroyImmediate(copy); }
                    void Capture(string label)
                    {
                        camera.Render(); var old = RenderTexture.active; RenderTexture.active = target;
                        var image = new Texture2D(256, 256, TextureFormat.RGB24, false);
                        try
                        {
                            image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply();
                            int lit = image.GetPixels32().Count(c => c.r > 8 || c.g > 8 || c.b > 8);
                            report.AppendLine(label + " visiblePixels=" + lit);
                            File.WriteAllBytes("Temp/ElectricChargeGlow_" + label + ".png", image.EncodeToPNG());
                        }
                        finally { RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image); }
                    }
                }
                finally { camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(target); }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            File.WriteAllText("Temp/ElectricChargeGlowDiagnostic.result", report.ToString());
        }
        static void CaptureActualScene(StringBuilder report)
        {
            var source = Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(p =>
                !EditorUtility.IsPersistent(p) && p.gameObject.scene == SceneManager.GetActiveScene() && p.name == "ElectricChargeGlowVFX" && p.gameObject.hideFlags == HideFlags.None);
            if (source == null) { report.AppendLine("No live scene instance found"); return; }
            var liveCamera = Resources.FindObjectsOfTypeAll<Camera>().FirstOrDefault(c => c.gameObject.scene == source.gameObject.scene && c.name == "Art2_TestCamera");
            if (liveCamera == null) { report.AppendLine("No Art2_TestCamera found"); return; }
            var originalRenderer = source.GetComponent<ParticleSystemRenderer>();
            report.AppendLine($"LIVE renderer enabled={originalRenderer.enabled} forceOff={originalRenderer.forceRenderingOff} sceneHidden={SceneVisibilityManager.instance.IsHidden(source.gameObject)} view={liveCamera.WorldToViewportPoint(source.transform.position)} camera={liveCamera.transform.position}/{liveCamera.transform.eulerAngles} mask={liveCamera.cullingMask}");
            var obj = UnityEngine.Object.Instantiate(source.gameObject); obj.hideFlags = HideFlags.HideAndDontSave;
            var ps = obj.GetComponent<ParticleSystem>(); var renderer = obj.GetComponent<ParticleSystemRenderer>();
            obj.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation); obj.transform.localScale = source.transform.lossyScale;
            var main = ps.main; main.stopAction = ParticleSystemStopAction.None;
            var camObj = new GameObject("ElectricGlow_LiveDiagnostic"); camObj.hideFlags = HideFlags.HideAndDontSave;
            var cam = camObj.AddComponent<Camera>(); cam.CopyFrom(liveCamera); cam.enabled = false;
            cam.transform.SetPositionAndRotation(liveCamera.transform.position, liveCamera.transform.rotation);
            var extra = camObj.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (liveCamera.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var liveExtra)) EditorUtility.CopySerialized(liveExtra, extra);
            extra.renderPostProcessing = false;
            var rt = new RenderTexture(640, 360, 24); cam.targetTexture = rt;
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/cfxr proc glow soft add.mat"));
            try
            {
                obj.SetActive(false); var baseline = Capture("SceneBaseline");
                obj.SetActive(true); ps.Simulate(.075f, true, true, false); ps.Pause();
                var normal = Capture("SceneOriginal");
                renderer.sharedMaterial = material;
                extra.requiresDepthTexture = false;
                var depthOff = Capture("SceneDepthOff");
                extra.requiresDepthTexture = true;
                var depthOn = Capture("SceneDepthOn");
                renderer.sharedMaterial = material; material.DisableKeyword("_FADING_ON"); material.SetFloat("_UseSP", 0);
                var noSoft = Capture("SceneNoSoft");
                obj.transform.position = cam.transform.position + cam.transform.forward * 5;
                ps.Simulate(.075f, true, true, false); ps.Pause();
                var centered = Capture("SceneCentered");
                int Difference(Color32[] a) { int count = 0; for (int i = 0; i < a.Length; i++) if (Math.Abs(a[i].r-baseline[i].r)+Math.Abs(a[i].g-baseline[i].g)+Math.Abs(a[i].b-baseline[i].b)>15) count++; return count; }
                report.AppendLine($"LIVE particleCount={ps.particleCount} diffOriginal={Difference(normal)} diffNoSoft={Difference(noSoft)} diffCentered={Difference(centered)}");
                report.AppendLine($"Original vendor material: depthOff={Difference(depthOff)} depthOn={Difference(depthOn)}");
                Color32[] Capture(string name)
                {
                    cam.Render(); var old = RenderTexture.active; RenderTexture.active = rt;
                    var image = new Texture2D(640, 360, TextureFormat.RGB24, false);
                    try { image.ReadPixels(new Rect(0,0,640,360),0,0); image.Apply(); File.WriteAllBytes("Temp/ElectricChargeGlow_"+name+".png",image.EncodeToPNG()); return image.GetPixels32(); }
                    finally { RenderTexture.active=old; UnityEngine.Object.DestroyImmediate(image); }
                }
            }
            finally { cam.targetTexture=null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(obj); UnityEngine.Object.DestroyImmediate(camObj); }
        }
    }
}
