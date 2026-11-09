using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using RhythmHunter.FightDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightUiParticleValidation
    {
        static FightUiParticleValidation() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/UiParticleValidation.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(request); } catch (IOException) { return; }
            var go = new GameObject("UI Particle Validation", typeof(RectTransform));
            var previous = RenderTexture.active;
            Texture2D readback = null;
            Camera camera = null;
            try
            {
                var burst = go.AddComponent<FightUiParticleBurst>();
                burst.Initialize(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/VFX/UI/BlueExplosion_UI.prefab"), new Vector2(240, 240), 1.4f, 1.2f);
                burst.Play();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                camera = (Camera)typeof(FightUiParticleBurst).GetField("view", flags).GetValue(burst);
                foreach (var ps in camera.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(.1f, false, true, true);
                camera.Render();
                RenderTexture.active = camera.targetTexture;
                readback = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); readback.Apply();
                int visible = 0;
                foreach (var pixel in readback.GetPixels32()) if (pixel.a > 5 && (pixel.r > 5 || pixel.g > 5 || pixel.b > 5)) visible++;
                if (visible < 10) throw new Exception("UI particle render is empty: " + visible);
                File.WriteAllBytes("Temp/StoneSuccessEffect.png", readback.EncodeToPNG());
                File.WriteAllText("Temp/UiParticleValidation.result", "PASS: rendered BlueExplosion into transparent UI texture; visible pixels=" + visible);
            }
            catch (Exception e) { File.WriteAllText("Temp/UiParticleValidation.result", "FAIL: " + e); }
            finally
            {
                RenderTexture.active = previous;
                if (readback) UnityEngine.Object.DestroyImmediate(readback);
                var target = camera ? camera.targetTexture : null;
                if (camera) { camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(camera.gameObject); }
                if (target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
