using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using FMOD;
using FMOD.Studio;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightMusicSectionValidation
    {
        static FMOD.Studio.System system;
        static FmodBeatClock clock;
        static GameObject root;
        static double started, pauseAt, pauseRequestedAt;
        static int phase, pausedPosition, lastPosition, beats, markers;
        static long lastBeat;
        static EVENT_CALLBACK callback;
        const string resultFile = "Temp/MusicSectionValidation.result";
        static FightMusicSectionValidation()
        {
            EditorApplication.update += Poll;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingEditMode) Cleanup(); };
        }
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Ok(RESULT result) { Check(result == RESULT.OK, "FMOD: " + result); }
        static void Set(string field, object value) => typeof(FmodBeatClock).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(clock, value);
        static object Call(string method) => typeof(FmodBeatClock).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(clock, null);
        [MenuItem("Rhythm Hunter/Diagnostics/Validate Music Sections")]
        public static void Request() => File.WriteAllText("Temp/MusicSectionValidation.request", "validate");
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                if (root == null)
                {
                    if (!File.Exists("Temp/MusicSectionValidation.request")) return;
                    try { File.Delete("Temp/MusicSectionValidation.request"); }
                    catch (IOException) { return; } // Request writer may still hold the file for this editor tick.
                    Begin();
                    return;
                }
                Check(EditorApplication.timeSinceStartup - started < 40, "Timed out waiting for native FMOD loops");
                Ok(system.update());
                Ok(system.flushCommands());
                Call("Update");
                if (phase == 2 && !clock.IsPlaying)
                {
                    File.WriteAllText(resultFile, $"PASS: native FMOD loop twice, {beats} unique beats, {markers} markers, continuous timeline, pause/resume, fade stop; marker metadata and parameter verified.");
                    Cleanup(); return;
                }
                Check(clock.TryGetTimelinePositionMs(out int now), "Clock missing position");
                Check(now >= lastPosition - 2, "Gameplay timeline moved backward: " + lastPosition + " -> " + now);
                lastPosition = now;
                if (phase == 0 && clock.CompletedMusicLoops >= 1)
                {
                    Check(clock.SetPaused(true), "Pause failed");
                    system.update();
                    system.flushCommands();
                    clock.TryGetRawTimelinePositionMs(out pausedPosition);
                    pauseAt = EditorApplication.timeSinceStartup;
                    pauseRequestedAt = pauseAt;
                    phase = 4;
                }
                else if (phase == 4)
                {
                    Check(EditorApplication.timeSinceStartup - pauseRequestedAt < 2, "FMOD never settled after pause");
                    clock.TryGetRawTimelinePositionMs(out int position);
                    if (position != pausedPosition) { pausedPosition = position; pauseAt = EditorApplication.timeSinceStartup; }
                    if (EditorApplication.timeSinceStartup - pauseAt >= .25)
                    { pauseAt = EditorApplication.timeSinceStartup; phase = 1; }
                }
                else if (phase == 1 && EditorApplication.timeSinceStartup - pauseAt >= .25)
                {
                    clock.TryGetRawTimelinePositionMs(out int position);
                    Check(Math.Abs(position - pausedPosition) < 3, $"Paused music advanced: {pausedPosition} -> {position}");
                    clock.SetPaused(false); phase = 3;
                }
                else if (phase == 3 && clock.CompletedMusicLoops >= 2)
                {
                    Check(beats >= 32 && markers > 0, "Missing FMOD beat/marker callbacks");
                    clock.FadeOutAndStop(); phase = 2;
                }
            }
            catch (Exception e) { File.WriteAllText(resultFile, "FAIL: " + e); Cleanup(); UnityEngine.Debug.LogException(e); }
        }
        static void Begin()
        {
            started = EditorApplication.timeSinceStartup; phase = 0; lastPosition = 0; lastBeat = -1; beats = markers = 0;
            Ok(FMOD.Studio.System.create(out system));
            Ok(system.getCoreSystem(out var core));
            Ok(core.setOutput(OUTPUTTYPE.NOSOUND));
            Ok(system.initialize(128, FMOD.Studio.INITFLAGS.NORMAL, FMOD.INITFLAGS.NORMAL, IntPtr.Zero));
            string bankRoot = Path.GetFullPath("ProjectFMOD/FightSceneTest/build/Desktop");
            Ok(system.loadBankFile(Path.Combine(bankRoot, "Master.strings.bank"), LOAD_BANK_FLAGS.NORMAL, out _));
            Ok(system.loadBankFile(Path.Combine(bankRoot, "Master.bank"), LOAD_BANK_FLAGS.NORMAL, out _));
            const string path = "event:/Ritual Slam _120_3";
            Ok(system.getEvent(path, out var description));
            Ok(description.createInstance(out var instance));
            root = new GameObject("MusicSectionValidation") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            clock = root.AddComponent<FmodBeatClock>();
            clock.Configure(path, 0, false);
            clock.ConfigureSongMarkers(FightOpeningTempoImporter.ReadMarkers(path));
            Check(clock.HasSectionMarkers, "Missing section metadata");
            Set("musicInstance", instance); Set("initialized", true);
            var handle = GCHandle.Alloc(clock);
            Set("selfHandle", handle); Set("selfHandleAllocated", true);
            callback = (EVENT_CALLBACK)Delegate.CreateDelegate(typeof(EVENT_CALLBACK), typeof(FmodBeatClock).GetMethod("TimelineBeatCallback", BindingFlags.Static | BindingFlags.NonPublic));
            Set("timelineBeatCallback", callback);
            Ok(instance.setUserData(GCHandle.ToIntPtr(handle)));
            Ok(instance.setCallback(callback, EVENT_CALLBACK_TYPE.TIMELINE_BEAT | EVENT_CALLBACK_TYPE.TIMELINE_MARKER));
            clock.Beat += beat => { Check(beat.GlobalBeat == lastBeat + 1, "Duplicate/missing global beat"); lastBeat = beat.GlobalBeat; beats++; };
            clock.MarkerReached += _ => markers++;
            clock.TryGetMarkerPosition("Overtime_End", out int end);
            Ok(instance.setTimelinePosition(end - 1000));
            clock.SetPlaybackPitch(4);
            Check(clock.EnableOvertimeMusic(), "Cannot activate Overtime parameter");
            clock.StartMusic();
        }
        static void Cleanup()
        {
            if (clock != null) clock.StopMusic();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            root = null; clock = null; callback = null;
            if (system.isValid()) { system.release(); system.clearHandle(); }
        }
    }
}

