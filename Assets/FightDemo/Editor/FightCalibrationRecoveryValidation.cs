using System;
using System.IO;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightCalibrationRecoveryValidation
    {
        const string Request = "Temp/FightCalibrationRecoveryValidation.request";
        const string Result = "Temp/FightCalibrationRecoveryValidation.result";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static int checks;
        static FightCalibrationRecoveryValidation() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            File.Delete(Request); Run();
        }
        static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        static T Field<T>(object source, string name) => (T)source.GetType().GetField(name, Private).GetValue(source);
        static void Refresh(FightTimingCalibration ui) => typeof(FightTimingCalibration).GetMethod("Refresh", Private).Invoke(ui, null);

        [MenuItem("Rhythm Hunter/Validate Calibration Recovery")]
        public static void Run()
        {
            checks = 0;
            GameObject root = null;
            try
            {
                var samples = new RhythmTimingSamples();
                for (int i = 0; i < 36; i++) samples.Add(i, i * 3, 500);
                Check(samples.Complete && !samples.Reliable && samples.SaveBlockedReason.Contains("drift"), "Drifting inputs explain the blocked save.");
                Check(!samples.Add(35, 60, 500), "Duplicates cannot replace a sample.");
                Check(!samples.Add(20, 60, 500), "Old beats cannot replace a sample.");
                for (int i = 36; i < 100 && !samples.Reliable; i++) samples.Add(i, 60 + (i % 3 - 1) * 3, 500);
                Check(samples.Count == 32 && samples.Reliable, "Stable following inputs recover without Retry.");
                double median = samples.MedianMs;
                Check(!samples.Add(101, -180, 500) && samples.MedianMs == median, "Reliable result freezes for Preview/Save.");
                samples.Clear();
                for (int i = 0; i < 70; i++) samples.Add(i, 180, 500);
                Check(samples.Count == 32 && !samples.Reliable && samples.SaveBlockedReason.Contains("offset"), "Stable +180 is not silently clamped into a valid saved offset.");
                samples.Clear();
                Check(samples.Count == 0 && samples.WarmupRemaining == 4 && samples.Add(0, 0, 500), "Replay resets beat tracking and warmup.");
                Check(!samples.Add(1, 0, double.NaN) && !samples.Add(1, 0, double.PositiveInfinity), "Invalid tempo intervals cannot corrupt statistics.");
                samples.Clear();
                for (int i = 0; i < 36; i++) samples.Add(i, i == 14 ? 180 : -55 + (i % 3 - 1) * 3, 500);
                Check(samples.Reliable && Math.Abs(samples.MedianMs + 55) < 4, "Early calibration still tolerates one outlier.");

                root = new GameObject("CalibrationRecoveryValidation");
                root.SetActive(false); // No playback, input subscriptions or saved profile writes.
                var clock = root.AddComponent<FmodBeatClock>();
                var judge = root.AddComponent<FmodRhythmJudge>();
                var canvas = root.AddComponent<Canvas>();
                var ui = root.AddComponent<FightTimingCalibration>();
                ui.Configure(null, clock, judge, null, canvas, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), () => { });
                ui.Begin();
                var collected = Field<RhythmTimingSamples>(ui, "samples");
                for (int i = 0; i < 36; i++) collected.Add(i, i * 3, 500);
                Refresh(ui);
                Check(!Field<Button>(ui, "saveButton").interactable && Field<Text>(ui, "progress").text.Contains("drift"), "Disabled Save explains the failed condition in the UI.");
                for (int i = 36; i < 100 && !collected.Reliable; i++) collected.Add(i, 60, 500);
                Refresh(ui);
                Check(Field<Button>(ui, "saveButton").interactable && Field<Button>(ui, "previewButton").interactable, "Recovered samples enable Preview and Save.");
                ui.Begin(); Refresh(ui);
                Check(!Field<Button>(ui, "saveButton").interactable && collected.Count == 0, "Retry clears a ready result and disables saving.");
                File.WriteAllText(Result, $"PASS: {checks} checks. Rolling recovery, duplicate/old beat rejection, frozen ready results, offset range, reset, invalid tempo, robust early estimate and Save UI. No live audio or song alignment verified.\n");
            }
            catch (Exception exception) { File.WriteAllText(Result, "FAIL: " + exception); Debug.LogException(exception); }
            finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
