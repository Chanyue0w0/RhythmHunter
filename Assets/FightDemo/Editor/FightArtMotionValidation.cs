using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    // Opt-in live check; never saves the user's scene or changes build settings.
    [InitializeOnLoad]
    public static class FightArtMotionValidation
    {
        const string Request = "Temp/FightArtMotionValidation.request";
        const string Result = "Temp/FightArtMotionValidation.result";
        const string Running = "FightArtMotionValidation.Running";
        const string Previous = "FightArtMotionValidation.Previous";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly Dictionary<LoopingBackgroundScroller, float> widths = new();
        static readonly Dictionary<Transform, Vector3> pausedPositions = new();
        static double deadline, checkpoint;
        static int phase, samples, visualCopies;
        static bool playingInBackground;
        static float previousTimeScale;
        static int layerIndex;
        static LoopingBackgroundScroller[] layers;

        static FightArtMotionValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Running, false))
                {
                    Restore();
                    if (File.Exists(Request))
                    {
                        File.Delete(Request);
                        File.AppendAllText(Result, "FAIL: test interrupted.\n");
                    }
                }
            };
        }

        [MenuItem("Rhythm Hunter/Validate Art Background Motion")]
        public static void Run() => File.WriteAllText(Request, "run");

        static float Width(LoopingBackgroundScroller scroller) =>
            (float)typeof(LoopingBackgroundScroller).GetMethod("GetLoopWidth", Private).Invoke(scroller, null);

        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling) return;
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetString(Previous, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                SessionState.SetBool(Running, true);
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/FightDemo/Scenes/FightScene.unity");
                File.WriteAllText(Result, "Starting live art background motion check.\n");
                EditorApplication.isPlaying = true;
                return;
            }
            try
            {
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 90;
                Require(EditorApplication.timeSinceStartup < deadline, "Timed out waiting for art scene/music.");
                var clock = UnityEngine.Object.FindFirstObjectByType<FmodBeatClock>();
                if (clock == null) return;
                if (!playingInBackground)
                {
                    Application.runInBackground = true;
                    FMODUnity.RuntimeManager.CoreSystem.mixerResume();
                    playingInBackground = true;
                }
                if (phase == 0)
                {
                    if (clock.ReceivedBeatCount < 3) return;
                    previousTimeScale = Time.timeScale;
                    foreach (var scroller in UnityEngine.Object.FindObjectsByType<LoopingBackgroundScroller>(FindObjectsSortMode.None))
                    {
                        float width = Width(scroller);
                        Require(width > 0, "Scroller must initialize its tile width.");
                        widths.Add(scroller, width);
                        ValidateTextures(scroller);
                        Vector3 scale = scroller.transform.localScale;
                        try
                        {
                            scroller.transform.localScale = scale * 1.015f;
                            Require(Mathf.Abs(Width(scroller) - width) < .00001f,
                                scroller.name + ": animation must not change the scroll period.");
                        }
                        finally { scroller.transform.localScale = scale; }
                    }
                    Require(widths.Count > 0, "No scrolling backgrounds found.");
                    layers = new List<LoopingBackgroundScroller>(widths.Keys).ToArray();
                    Array.Sort(layers, (a, b) => string.CompareOrdinal(a.name, b.name));
                    phase = 1;
                    checkpoint = EditorApplication.timeSinceStartup + 6;
                }
                if (phase == 1)
                {
                    visualCopies = 0;
                    foreach (var entry in widths)
                    {
                        Require(Mathf.Abs(Width(entry.Key) - entry.Value) < .00001f, "Loop period changed during a beat.");

                        var copies = (List<(Transform source, Transform copy)>)typeof(LoopingBackgroundScroller)
                            .GetField("copiedTransforms", Private).GetValue(entry.Key);
                        foreach (var pair in copies)
                        {
                            Require(pair.copy.GetComponent<BeatBounce>() == null, "Loop visual must not run a second animation.");
                            Require((pair.copy.localScale - pair.source.localScale).sqrMagnitude < .000001f,
                                "Loop visual must match the original grass pose.");
                            visualCopies++;
                        }
                    }
                    Require(visualCopies >= 14, "Expected both copies of all seven grass slices.");
                    samples++;
                    if (EditorApplication.timeSinceStartup < checkpoint) return;
                    foreach (var entry in widths) pausedPositions.Add(entry.Key.transform, entry.Key.transform.localPosition);
                    Require(clock.SetPaused(true), "FMOD pause failed.");
                    Time.timeScale = 0;
                    phase = 2;
                    checkpoint = EditorApplication.timeSinceStartup + 1;
                    return;
                }
                if (phase == 2)
                {
                    if (EditorApplication.timeSinceStartup < checkpoint) return;
                    foreach (var entry in pausedPositions)
                        Require((entry.Key.localPosition - entry.Value).sqrMagnitude < .000001f, "Background kept scrolling while paused.");
                    File.AppendAllText(Result, $"PASS: {widths.Count} stable loop periods, {visualCopies} synchronized grass copies, {samples} live samples, background pause.\n");
                    phase = 3;
                    checkpoint = EditorApplication.timeSinceStartup + .2;
                    return;
                }
                if (phase == 3 || phase == 4)
                {
                    if (EditorApplication.timeSinceStartup < checkpoint) return;
                    var layer = layers[layerIndex];
                    typeof(LoopingBackgroundScroller).GetField("travelledDistance", Private).SetValue(layer, widths[layer] * .5f + (phase == 3 ? -.0001f : .0001f));
                    typeof(LoopingBackgroundScroller).GetMethod("LateUpdate", Private).Invoke(layer, null);
                    ScreenCapture.CaptureScreenshot(ImagePath(layer, phase == 3));
                    phase++;
                    checkpoint = EditorApplication.timeSinceStartup + .5;
                    return;
                }
                if (phase == 5)
                {
                    if (EditorApplication.timeSinceStartup < checkpoint) return;
                    var layer = layers[layerIndex];
                    double changed = ChangedPixelFraction(ImagePath(layer, true), ImagePath(layer, false));
                    File.AppendAllText(Result, $"WRAP {layer.name}: changed pixels={changed:P4}\n");
                    Require(changed < .002, "Visible pop at wrap boundary: " + layer.name);
                    if (++layerIndex < layers.Length) { phase = 3; return; }
                    File.AppendAllText(Result, "PASS: all 11 rendered wrap boundaries and explicit sprite texture bindings.\n");
                    clock.SetPaused(false);
                    Finish();
                }
            }
            catch (Exception exception)
            {
                File.AppendAllText(Result, "FAIL: " + exception + "\n");
                Finish();
            }
        }

        static string ImagePath(LoopingBackgroundScroller layer, bool before) =>
            $"Temp/ArtWrap-{layer.name}-{(before ? "before" : "after")}.png";

        static void ValidateTextures(LoopingBackgroundScroller layer)
        {
            foreach (string field in new[] { "leftLoopCopy", "rightLoopCopy" })
            {
                var copy = (Transform)typeof(LoopingBackgroundScroller).GetField(field, Private).GetValue(layer);
                Require(copy != null, "Missing copy: " + layer.name);
                foreach (var renderer in copy.GetComponentsInChildren<SpriteRenderer>())
                {
                    var properties = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(properties);
                    Require(renderer.sprite != null && properties.GetTexture("_MainTex") == renderer.sprite.texture,
                        "Copy is missing its texture binding: " + renderer.name);
                }
            }
        }

        static double ChangedPixelFraction(string first, string second)
        {
            var a = new Texture2D(2, 2);
            var b = new Texture2D(2, 2);
            try
            {
                a.LoadImage(File.ReadAllBytes(first)); b.LoadImage(File.ReadAllBytes(second));
                Require(a.width == b.width && a.height == b.height, "Game view size changed during validation.");
                var pa = a.GetPixels32(); var pb = b.GetPixels32();
                int changed = 0, sampled = 0;
                // Exclude the top FMOD debug text, but include the actual scene.
                for (int y = 0; y < a.height * .85f; y += 2)
                    for (int x = 0; x < a.width; x += 2)
                    {
                        int i = y * a.width + x;
                        int delta = Math.Abs(pa[i].r - pb[i].r) + Math.Abs(pa[i].g - pb[i].g) + Math.Abs(pa[i].b - pb[i].b);
                        if (delta > 30) changed++;
                        sampled++;
                    }
                return (double)changed / Math.Max(1, sampled);
            }
            finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void Finish()
        {
            Time.timeScale = previousTimeScale > 0 ? previousTimeScale : 1;
            File.Delete(Request);
            Restore();
            EditorApplication.isPlaying = false;
            widths.Clear(); pausedPositions.Clear();
            phase = samples = layerIndex = 0; deadline = 0; playingInBackground = false;
        }

        static void Restore()
        {
            string path = SessionState.GetString(Previous, "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            SessionState.SetBool(Running, false);
        }
    }
}
