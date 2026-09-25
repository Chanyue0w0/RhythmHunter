using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
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
        static double heldBeatCount;
        static float heldDistance;
        static int pausedTimeline;
        static long pausedBeat;

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
                bool integrated = File.ReadAllText(Request).Trim() == "FightScene3";
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(integrated
                    ? "Assets/FightDemo/Scenes/FightScene3.unity" : "Assets/FightDemo/Scenes/FightScene.unity");
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
                    if (clock.gameObject.scene.name == "FightScene3") ValidateIntegratedBattle(clock);
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
                    var pause = UnityEngine.Object.FindFirstObjectByType<FightPauseController>();
                    if (pause != null) pause.SetPaused(true);
                    else Require(clock.SetPaused(true), "FMOD pause failed.");
                    Time.timeScale = 0;
                    phase = 11;
                    checkpoint = EditorApplication.timeSinceStartup + .2;
                    return;
                }
                if (phase == 11)
                {
                    if (EditorApplication.timeSinceStartup < checkpoint) return;
                    clock.TryGetTimelinePositionMs(out pausedTimeline); pausedBeat = clock.ReceivedBeatCount;
                    phase = 2; checkpoint = EditorApplication.timeSinceStartup + .8;
                    return;
                }
                if (phase == 2)
                {
                    if (EditorApplication.timeSinceStartup < checkpoint) return;
                    foreach (var entry in pausedPositions)
                        Require((entry.Key.localPosition - entry.Value).sqrMagnitude < .000001f, "Background kept scrolling while paused.");
                    clock.TryGetTimelinePositionMs(out int currentTimeline);
                    Require(Math.Abs(currentTimeline - pausedTimeline) < 2 && clock.ReceivedBeatCount == pausedBeat, "Pause must freeze the music timeline and beats.");
                    var pause = UnityEngine.Object.FindFirstObjectByType<FightPauseController>();
                    if (pause != null)
                    {
                        pause.SetPaused(false);
                        Require(!clock.IsPaused && Time.timeScale > 0, "Second pause toggle must resume music and game time.");
                        // Hide the pause overlay while taking frozen scene screenshots.
                        clock.SetPaused(true); Time.timeScale = 0;
                    }
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
                    Time.timeScale = previousTimeScale;
                    var environment = UnityEngine.Object.FindFirstObjectByType<FightEnvironmentController>();
                    environment.MotionEnabled = false;
                    heldDistance = Distance(layers[0]); heldBeatCount = clock.ReceivedBeatCount;
                    phase = 6; checkpoint = EditorApplication.timeSinceStartup + 1.2;
                    return;
                }
                if (phase == 6)
                {
                    if (EditorApplication.timeSinceStartup < checkpoint) return;
                    Require(Mathf.Abs(Distance(layers[0]) - heldDistance) < .00001f, "Master motion switch must freeze scroll without resetting it.");
                    Require(clock.ReceivedBeatCount > heldBeatCount, "Environment switch must not stop music or gameplay beats.");
                    var environment = UnityEngine.Object.FindFirstObjectByType<FightEnvironmentController>();
                    environment.MotionEnabled = true; environment.ScrollingEnabled = false;
                    phase = 7; checkpoint = EditorApplication.timeSinceStartup + .5;
                    return;
                }
                if (phase == 7)
                {
                    if (EditorApplication.timeSinceStartup < checkpoint) return;
                    Require(Mathf.Abs(Distance(layers[0]) - heldDistance) < .00001f, "Scroll switch must preserve offset while beat animation continues.");
                    var environment = UnityEngine.Object.FindFirstObjectByType<FightEnvironmentController>();
                    environment.ScrollingEnabled = true; environment.Direction = FightEnvironmentController.ScrollDirection.Left;
                    environment.BeatPulseEnabled = false;
                    heldDistance = Distance(layers[0]);
                    phase = 8; checkpoint = EditorApplication.timeSinceStartup + .3;
                    return;
                }
                if (phase == 8)
                {
                    if (EditorApplication.timeSinceStartup < checkpoint) return;
                    float delta = Mathf.Repeat(Distance(layers[0]) - heldDistance + widths[layers[0]] * .5f, widths[layers[0]]) - widths[layers[0]] * .5f;
                    Require(delta < -.001f && delta > -1, "Left direction must move left continuously, without a reset/jump.");
                    var environment = UnityEngine.Object.FindFirstObjectByType<FightEnvironmentController>();
                    var pulses = (FightEnvironmentController.Pulse[])typeof(FightEnvironmentController).GetField("pulses", Private).GetValue(environment);
                    Require(pulses.All(p => p.target == null || (p.target.localScale - p.restScale).sqrMagnitude < .000001f), "Disabling beat pulse must restore base scales.");
                    environment.BeatPulseEnabled = true;
                    environment.Direction = FightEnvironmentController.ScrollDirection.Right;
                    heldDistance = Distance(layers[0]);
                    phase = 9; checkpoint = EditorApplication.timeSinceStartup + .3;
                    return;
                }
                if (phase == 9)
                {
                    if (EditorApplication.timeSinceStartup < checkpoint) return;
                    float delta = Mathf.Repeat(Distance(layers[0]) - heldDistance + widths[layers[0]] * .5f, widths[layers[0]]) - widths[layers[0]] * .5f;
                    Require(delta > .001f && delta < 1, "Right direction must resume continuously.");
                    File.AppendAllText(Result, "PASS: master motion/scroll switches preserve offsets; music beats continue; left/right reverse without jumping.\n");
                    ScreenCapture.CaptureScreenshot("Temp/FightArtIntegration-live.png");
                    phase = 10; checkpoint = EditorApplication.timeSinceStartup + .5;
                    return;
                }
                if (phase == 10 && EditorApplication.timeSinceStartup >= checkpoint) Finish();
            }
            catch (Exception exception)
            {
                File.AppendAllText(Result, "FAIL: " + exception + "\n");
                Finish();
            }
        }

        static string ImagePath(LoopingBackgroundScroller layer, bool before) =>
            $"Temp/ArtWrap-{layer.name}-{(before ? "before" : "after")}.png";

        static float Distance(LoopingBackgroundScroller layer) => (float)typeof(LoopingBackgroundScroller).GetField("travelledDistance", Private).GetValue(layer);

        static void ValidateIntegratedBattle(FmodBeatClock clock)
        {
            Require(UnityEngine.Object.FindObjectsByType<FmodBeatClock>(FindObjectsSortMode.None).Length == 1, "Expected exactly one FMOD clock.");
            Require(clock.MusicEventPath == "event:/Ritual Slam _120_3", "Battle music changed.");
            Require(Math.Abs(clock.LatestBeat.Tempo - 121.15f) < .01f, "FMOD tempo must be 121.15 BPM.");
            var fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
            Require(fight.UsesEqualBeats, "Integrated scene must retain EqualBeat combat.");
            var roster = UnityEngine.Object.FindFirstObjectByType<FightRosterManager>();
            var judge = UnityEngine.Object.FindFirstObjectByType<FmodRhythmJudge>();
            foreach (string profile in new[] { "Keyboard", "Gamepad" })
            {
                judge.SetInputProfile(profile);
                Require(judge.PersonalDelayMs == RhythmCalibrationStore.GetDelay(profile), "Battle must read shared saved calibration.");
            }
            judge.SetInputProfile("Keyboard");
            Require(roster.ActiveHeroes.Count == 3, "Three role-based heroes must spawn.");
            foreach (var slot in roster.ActiveHeroes.Concat(roster.ActiveEnemies))
            {
                var animator = slot.CombatAnimator;
                Require(animator != null && animator.BeatSource == fight, "Character must use the battle's beat source.");
                Require(animator.TargetRenderer.sharedMaterial.shader.name.Contains("Sprite-Lit"), "Character must receive 2D lighting.");
                Require(animator.Frames.All(sprite => AssetDatabase.GetAssetPath(sprite).StartsWith("Assets/FightDemo/Arts/")), "Idle must use official art.");
                Require(animator.Sequences.All(sequence => sequence.Frames.All(sprite => AssetDatabase.GetAssetPath(sprite).StartsWith("Assets/FightDemo/Arts/"))), "Combat must never switch to old art.");
            }
            // Respawning exercises the same roster API used by formation changes.
            var savedHeroes = roster.HeroPrefabs.ToArray(); var savedEnemies = roster.EnemyPrefabs.ToArray();
            var giant = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Giant.prefab").GetComponent<FightCharacterDefinition>();
            var killer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Killer.prefab").GetComponent<FightCharacterDefinition>();
            roster.SetRoster(savedHeroes, new[] { giant, killer, null });
            Require(roster.ActiveEnemies.Count == 2 && roster.ActiveEnemies.All(slot => slot.CombatAnimator.FrameCount == 3), "Both official goblin prefabs must spawn through the existing roster.");
            roster.SetRoster(savedHeroes, savedEnemies);
            Require(roster.ActiveHeroes.All(slot => slot.CombatAnimator.TargetRenderer.sharedMaterial.shader.name.Contains("Sprite-Lit")), "Respawn must preserve lighting.");
            File.AppendAllText(Result, "PASS: official-art prefab roster, lit respawns, EqualBeat combat, single FMOD event Ritual Slam _120_3, tempo 121.15.\n");
        }

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
