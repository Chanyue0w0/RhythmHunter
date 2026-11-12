using System;
using System.IO;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.FightDemo.Art2;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class Art2CharacterFillValidation
    {
        static Art2CharacterFillValidation() => EditorApplication.update += Poll;
        private static void Poll()
        {
            const string request = "Temp/Art2CharacterFill.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Validate(); }
            catch (Exception e) { File.WriteAllText("Temp/Art2CharacterFill.result", "FAIL " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Art2/Validate Character Fill Lighting")]
        public static void Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var root = new GameObject("Character fill validation") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(64, 64, 24);
            var readback = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            var texture = new Texture2D(2, 2);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 1);
            var material = new Material(Shader.Find("RhythmHunter/Art2/SpriteCardLit"));
            var previous = RenderTexture.active;
            try
            {
                material.SetFloat("_AmbientStrength", 0); material.SetFloat("_LightInfluence", 0);
                material.SetFloat("_EmissionStrength", 0);
                var camera = new GameObject("Test camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform); camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = 1.2f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << 31; camera.targetTexture = rt;
                var pipeline = (UniversalRenderPipelineAsset)(QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline);
                int index = -1;
                for (int i = 0; i < pipeline.rendererDataList.Length; i++)
                    if (pipeline.rendererDataList[i] != null && pipeline.rendererDataList[i].name == "Art2ForwardPlusRenderer") index = i;
                if (index < 0) throw new Exception("Art2 renderer missing");
                camera.GetUniversalAdditionalCameraData().SetRenderer(index);
                var renderer = new GameObject("Test sprite", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                renderer.transform.SetParent(root.transform); renderer.gameObject.layer = 31;
                renderer.sprite = sprite; renderer.sharedMaterial = material;
                var fill = root.AddComponent<Art2CharacterFillLighting>();
                fill.enableRim = false;
                fill.heroes.color = Color.red; fill.heroes.intensity = .5f;
                fill.enemies.color = Color.blue; fill.enemies.intensity = .5f;
                fill.heroes.softness = fill.enemies.softness = 1;
                Color baseline = Render();
                fill.Register(renderer, FightUnitSlot.UnitTeam.Hero); Tick();
                Color hero = Render();
                if (hero.r < baseline.r + .2f || hero.b > .05f) throw new Exception("Hero fill failed: " + hero);
                fill.Register(renderer, FightUnitSlot.UnitTeam.Enemy); Tick();
                Color enemy = Render();
                if (enemy.b < baseline.b + .2f || enemy.r > .05f) throw new Exception("Enemy fill failed: " + enemy);
                fill.enableFill = false; Tick();
                if (Render().maxColorComponent > baseline.maxColorComponent + .02f) throw new Exception("Toggle restore failed");
                fill.enableFill = true; Tick(); fill.enabled = false;
                // This fixture runs outside Play; invoke the runtime lifecycle explicitly.
                typeof(Art2CharacterFillLighting).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fill, null);
                if (Render().maxColorComponent > baseline.maxColorComponent + .02f) throw new Exception("Disable restore failed");
                // Shared material remains unmodified: a second renderer is not opted into fill.
                renderer.SetPropertyBlock(null);
                if (Render().maxColorComponent > baseline.maxColorComponent + .02f) throw new Exception("Shared material leaked fill");
                // Transparent border fixture verifies a one-sided edge, not an all-around outline.
                UnityEngine.Object.DestroyImmediate(sprite);
                texture.Reinitialize(16, 16); texture.filterMode = FilterMode.Point;
                var pixels = new Color[256];
                for (int y = 4; y < 12; y++) for (int x = 4; x < 12; x++) pixels[y * 16 + x] = Color.white;
                texture.SetPixels(pixels); texture.Apply();
                sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * .5f, 8);
                renderer.sprite = sprite;
                var edgeBlock = new MaterialPropertyBlock();
                edgeBlock.SetColor("_CharacterRimColor", new Color(1, 0, 0, .5f));
                edgeBlock.SetVector("_CharacterRimParams", new Vector4(1, 0, 1, 0));
                renderer.SetPropertyBlock(edgeBlock); Render();
                float left = 0, right = 0;
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                    if (x < 32) left += readback.GetPixel(x, y).r; else right += readback.GetPixel(x, y).r;
                if (right < 1 || left > right * .1f) throw new Exception($"One-sided rim failed: left={left}, right={right}");
                edgeBlock.SetVector("_CharacterRimParams", new Vector4(-1, 0, 1, 0));
                renderer.SetPropertyBlock(edgeBlock); Render();
                left = right = 0;
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                    if (x < 32) left += readback.GetPixel(x, y).r; else right += readback.GetPixel(x, y).r;
                if (left < 1 || right > left * .1f) throw new Exception("Rim direction did not reverse");
                File.WriteAllText("Temp/Art2CharacterFill.result", $"PASS GPU: hero={hero}, enemy={enemy}; toggle, disable restoration, shared material isolation and one-sided rim direction reversal passed.");
                void Tick() => typeof(Art2CharacterFillLighting).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fill, null);
                Color Render()
                {
                    camera.Render(); RenderTexture.active = rt;
                    readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); readback.Apply();
                    return readback.GetPixel(32, 32);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(readback);
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
        }
    }
}
