using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RhythmHunter.FightDemoEditor
{
    // All objects/materials live in a disposable PreviewScene. No active scene is opened or edited.
    public sealed class Art2SurfaceLightingDiagnostic : EditorWindow
    {
        private static readonly string[] Columns = { "原圖（不受光）", "灰底／平面法線", "灰底／表面法線", "世界法線方向", "原圖＋表面受光" };
        private Rig rig;
        private float azimuth = -65, elevation = 15, strength = 1, ambient = .05f;
        private bool sweep;
        private double lastRefresh;
        private string result = "尚未執行 GPU 明暗交換檢查。";

        [MenuItem("Rhythm Hunter/Art2/Surface Lighting Diagnostic")]
        public static void Open()
        {
            var window = GetWindow<Art2SurfaceLightingDiagnostic>("Art2 受光診斷");
            window.minSize = new Vector2(1000, 560);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            if (File.Exists("Temp/Art2SurfaceDiagnostic/result.txt"))
                result = File.ReadAllText("Temp/Art2SurfaceDiagnostic/result.txt");
        }
        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            rig?.Dispose();
            rig = null;
        }

        private void Tick()
        {
            if (!sweep || EditorApplication.timeSinceStartup - lastRefresh < 1.0 / 20) return;
            lastRefresh = EditorApplication.timeSinceStartup;
            azimuth = Mathf.Sin((float)lastRefresh * .7f) * 70;
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("獨立白光測試｜上排石頭、下排木柵欄｜使用場景同一份 SpriteCardLit Shader", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("場景主光、火把、後製與擺位不參與此預覽；第五欄保留來源材質的補光參數。", EditorStyles.wordWrappedMiniLabel);
            azimuth = EditorGUILayout.Slider("測試光左右角度", azimuth, -80, 80);
            elevation = EditorGUILayout.Slider("測試光高度角度", elevation, -30, 70);
            strength = EditorGUILayout.Slider("表面法線強度", strength, 0, 2);
            ambient = EditorGUILayout.Slider("灰底比較的環境亮度", ambient, 0, .5f);
            using (new EditorGUILayout.HorizontalScope())
            {
                sweep = GUILayout.Toggle(sweep, "自動左右掃光", "Button");
                if (GUILayout.Button("左側光")) { sweep = false; azimuth = -65; }
                if (GUILayout.Button("右側光")) { sweep = false; azimuth = 65; }
                if (GUILayout.Button("GPU 明暗交換檢查"))
                {
                    try { result = RunGpuCheck(); }
                    catch (Exception e) { result = "檢查失敗：" + e.Message; Debug.LogException(e); }
                }
                if (GUILayout.Button("重新載入素材")) { rig?.Dispose(); rig = null; }
            }
            EditorGUILayout.HelpBox(result, MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
                foreach (string title in Columns) GUILayout.Label(title, EditorStyles.centeredGreyMiniLabel, GUILayout.Width((position.width - 20) / 5));
            Rect rect = GUILayoutUtility.GetRect(100, 10000, 240, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type != EventType.Repaint) return;
            try
            {
                rig ??= new Rig();
                Texture texture = rig.Render(rect, azimuth, elevation, strength, ambient);
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            }
            catch (Exception e)
            {
                GUI.Label(rect, "預覽失敗：" + e.Message);
            }
        }

        [MenuItem("Rhythm Hunter/Art2/Verify Surface Light Response (GPU)")]
        public static void VerifyMenu() => Debug.Log(RunGpuCheck());

        public static string RunGpuCheck()
        {
            Directory.CreateDirectory("Temp/Art2SurfaceDiagnostic");
            File.Delete("Temp/Art2SurfaceDiagnostic/error.txt");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool wasDirty = scene.isDirty;
            int roots = scene.rootCount;
            var sun = RenderSettings.sun;
            string originalSun = sun != null ? EditorJsonUtility.ToJson(sun) + EditorJsonUtility.ToJson(sun.transform) : "";
            using var test = new Rig();
            Texture2D left = null, right = null;
            try
            {
                left = Readback(test.Render(new Rect(0, 0, 2000, 800), -65, 15, 1, .05f));
                right = Readback(test.Render(new Rect(0, 0, 2000, 800), 65, 15, 1, .05f));
                WriteCapture("Temp/Art2SurfaceDiagnostic/left.png", left);
                WriteCapture("Temp/Art2SurfaceDiagnostic/right.png", right);
                var report = new System.Text.StringBuilder();
                bool passed = true;
                // Source-pixel points lie inside the authored left/front and right faces.
                var samples = new[] { new[] { new Vector2(90, 125), new Vector2(245, 130) },
                    new[] { new Vector2(54, 38), new Vector2(180, 40) } };
                for (int row = 0; row < 2; row++)
                {
                    float ll = Sample(left, test.PixelPoint(row, 2, samples[row][0]));
                    float lr = Sample(left, test.PixelPoint(row, 2, samples[row][1]));
                    float rl = Sample(right, test.PixelPoint(row, 2, samples[row][0]));
                    float rr = Sample(right, test.PixelPoint(row, 2, samples[row][1]));
                    float flatA = Sample(left, test.PixelPoint(row, 1, samples[row][0]));
                    float flatB = Sample(left, test.PixelPoint(row, 1, samples[row][1]));
                    bool ok = ll - lr > .08f && rr - rl > .08f && Mathf.Abs(flatA - flatB) < .025f;
                    passed &= ok;
                    report.AppendLine($"{(row == 0 ? "石頭" : "柵欄")}: {(ok ? "PASS" : "FAIL")}；左光兩面 {ll:F3}/{lr:F3}；右光兩面 {rl:F3}/{rr:F3}；平面對照差 {Mathf.Abs(flatA-flatB):F3}");
                }
                bool sceneUntouched = scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene() &&
                    scene.isDirty == wasDirty && scene.rootCount == roots && RenderSettings.sun == sun &&
                    (sun == null || originalSun == EditorJsonUtility.ToJson(sun) + EditorJsonUtility.ToJson(sun.transform));
                passed &= sceneUntouched;
                report.AppendLine("目前場景／主光保留檢查：" + (sceneUntouched ? "PASS" : "FAIL"));
                report.AppendLine(passed ? "PASS：兩個物件均產生正確左右明暗交換。" : "FAIL：不可宣稱受光驗證通過，請檢查輸出圖片、法線與 Renderer。");
                report.AppendLine("固定條件：白色方向光 1、方位 ±65°、高度 15°、法線 1、灰底補光 0.05、無後製。第五欄為現有材質。圖片位於 Temp/Art2SurfaceDiagnostic。");
                File.WriteAllText("Temp/Art2SurfaceDiagnostic/result.txt", report.ToString());
                if (!passed) throw new InvalidOperationException(report.ToString());
                return report.ToString();
            }
            finally
            {
                if (left != null) DestroyImmediate(left);
                if (right != null) DestroyImmediate(right);
            }
        }

        private static void WriteCapture(string path, Texture2D linearCapture)
        {
            // Measurements stay linear; PNGs are encoded for ordinary image viewers.
            var copy = new Texture2D(linearCapture.width, linearCapture.height, TextureFormat.RGBA32, false);
            try
            {
                var pixels = linearCapture.GetPixels32();
                if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                {
                    var lut = new byte[256];
                    for (int i = 0; i < 256; i++) lut[i] = (byte)Mathf.RoundToInt(Mathf.LinearToGammaSpace(i / 255f) * 255);
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        var p = pixels[i];
                        pixels[i] = new Color32(lut[p.r], lut[p.g], lut[p.b], p.a);
                    }
                }
                copy.SetPixels32(pixels);
                copy.Apply();
                File.WriteAllBytes(path, copy.EncodeToPNG());
            }
            finally { DestroyImmediate(copy); }
        }

        private static Texture2D Readback(Texture texture)
        {
            var previous = RenderTexture.active;
            var result = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
            try
            {
                RenderTexture.active = (RenderTexture)texture;
                result.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                result.Apply();
                return result;
            }
            catch { DestroyImmediate(result); throw; }
            finally { RenderTexture.active = previous; }
        }

        private static float Sample(Texture2D texture, Vector2 pixel)
        {
            float total = 0;
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
                total += texture.GetPixel(Mathf.Clamp(Mathf.RoundToInt(pixel.x) + x, 0, texture.width - 1),
                    Mathf.Clamp(Mathf.RoundToInt(pixel.y) + y, 0, texture.height - 1)).grayscale;
            return total / 9;
        }

        private sealed class Rig : IDisposable
        {
            private readonly PreviewRenderUtility preview;
            private readonly List<Material> materials = new();
            private readonly SpriteRenderer[,] sprites = new SpriteRenderer[2, 5];
            private readonly Light key, fill;
            private readonly Quaternion tilt = Quaternion.Euler(45, 0, 0);
            private const string Folder = "Assets/FightDemo/Art2Prototype/";

            public Rig()
            {
                preview = new PreviewRenderUtility();
                try
                {
                    var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                    if (pipeline == null) throw new InvalidOperationException("需要目前專案的 URP。");
                    int renderer = -1;
                    for (int i = 0; i < pipeline.rendererDataList.Length; i++)
                        if (pipeline.rendererDataList[i] != null && pipeline.rendererDataList[i].name == "Art2ForwardPlusRenderer")
                        { renderer = i; break; }
                    if (renderer < 0) throw new InvalidOperationException("找不到 Art2ForwardPlusRenderer。");
                    var camera = preview.camera;
                    camera.name = "Art2_SurfaceDiagnosticCamera";
                    camera.overrideSceneCullingMask = UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(camera.scene);
                    camera.gameObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(renderer);
                    var data = camera.GetComponent<UniversalAdditionalCameraData>();
                    data.renderPostProcessing = false;
                    data.renderShadows = false;
                    data.volumeLayerMask = 0;
                    camera.orthographic = true;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(.12f, .12f, .12f, 1);
                    camera.allowHDR = false;
                    camera.allowMSAA = false;
                    camera.nearClipPlane = .1f;
                    camera.farClipPlane = 100;
                    camera.transform.SetPositionAndRotation(tilt * new Vector3(0, 0, -20), tilt);
                    preview.ambientColor = Color.black;
                    var lights = preview.lights;
                    key = lights[0]; fill = lights[1];
                    key.type = LightType.Directional;
                    key.color = Color.white;
                    key.intensity = 1;
                    key.shadows = LightShadows.None;
                    fill.intensity = 0;
                    for (int row = 0; row < 2; row++)
                    {
                        string file = row == 0 ? "cave_01_00" : "cave_02_00";
                        string materialName = row == 0 ? "CaveRockLeftLit" : "CaveWoodLeftLit";
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FightDemo/Arts/場景/Pixel/Cave/" + file + ".png");
                        var source = AssetDatabase.LoadAssetAtPath<Material>(Folder + materialName + ".mat");
                        if (sprite == null || source == null || source.GetTexture("_NormalMap") == null)
                            throw new InvalidOperationException("缺少測試 Sprite／法線材質：" + file);
                        if (ShaderUtil.ShaderHasError(source.shader)) throw new InvalidOperationException("SpriteCardLit 有 Shader 編譯錯誤。");
                        for (int column = 0; column < 5; column++)
                        {
                            var obj = new GameObject(file + "_" + column) { hideFlags = HideFlags.HideAndDontSave };
                            preview.AddSingleGO(obj);
                            obj.transform.SetPositionAndRotation(tilt * new Vector3((column - 2) * 5, row == 0 ? 1.4f : -1.4f, 0), tilt);
                            obj.transform.localScale = Vector3.one * (1.7f / sprite.bounds.size.y);
                            var sr = obj.AddComponent<SpriteRenderer>();
                            sr.sprite = sprite;
                            var material = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
                            materials.Add(material);
                            material.SetFloat("_SurfaceDebugMode", column == 0 ? 1 : column == 3 ? 3 : column == 4 ? 0 : 2);
                            material.SetFloat("_UseNormalMap", column == 1 ? 0 : 1);
                            if (column < 4)
                            {
                                material.SetColor("_AmbientTint", Color.white);
                                material.SetFloat("_LightInfluence", 1);
                                material.SetFloat("_DiffuseFloor", 0);
                                material.SetFloat("_EmissionStrength", 0);
                                material.SetFloat("_AtmosphereMix", 0);
                            }
                            sr.sharedMaterial = material;
                            sr.shadowCastingMode = ShadowCastingMode.Off;
                            sprites[row, column] = sr;
                        }
                    }
                }
                catch { Dispose(); throw; }
            }

            public Texture Render(Rect rect, float azimuth, float elevation, float strength, float ambient)
            {
                float a = azimuth * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
                Vector3 direction = tilt * new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), -Mathf.Cos(a) * Mathf.Cos(e));
                key.transform.SetPositionAndRotation(direction * 10, Quaternion.LookRotation(-direction, tilt * Vector3.up));
                foreach (var material in materials)
                {
                    material.SetFloat("_NormalStrength", strength);
                    if (material.GetFloat("_SurfaceDebugMode") == 2) material.SetFloat("_AmbientStrength", ambient);
                }
                preview.BeginPreview(rect, GUIStyle.none);
                try
                {
                    preview.camera.aspect = rect.width / rect.height;
                    preview.camera.orthographicSize = Mathf.Max(3, 12.5f / preview.camera.aspect);
                    key.enabled = true;
                    fill.intensity = 0;
                    preview.Render(true);
                }
                catch { preview.EndPreview(); throw; }
                return preview.EndPreview();
            }

            public Vector2 PixelPoint(int row, int column, Vector2 sourcePixel)
            {
                var renderer = sprites[row, column];
                var sprite = renderer.sprite;
                Vector3 local = new Vector3(sourcePixel.x - sprite.pivot.x,
                    sprite.rect.height - sourcePixel.y - sprite.pivot.y, 0) / sprite.pixelsPerUnit;
                return preview.camera.WorldToScreenPoint(renderer.transform.TransformPoint(local));
            }

            public void Dispose()
            {
                preview?.Cleanup();
                foreach (var material in materials) if (material != null) DestroyImmediate(material);
                materials.Clear();
            }
        }

        // Optional one-shot request made by local tooling; never runs on ordinary project load.
        [InitializeOnLoadMethod]
        private static void PendingLocalCheck()
        {
            const string request = "Temp/Art2SurfaceDiagnostic.request";
            if (!File.Exists(request)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(request);
                try { Debug.Log(RunGpuCheck()); Open(); }
                catch (Exception e)
                {
                    Directory.CreateDirectory("Temp/Art2SurfaceDiagnostic");
                    File.WriteAllText("Temp/Art2SurfaceDiagnostic/error.txt", e.ToString());
                    Debug.LogException(e);
                }
            };
        }
    }
}
