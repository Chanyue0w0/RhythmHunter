using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.RhythmDemo
{
    public sealed class FmodRhythmJudge : MonoBehaviour
    {
        public enum Grade
        {
            NotReady,
            Perfect,
            Miss
        }

        public readonly struct Result
        {
            public Result(
                Grade grade,
                double deltaMs,
                int rawTimelinePositionMs,
                double evaluatedTimelinePositionMs,
                FmodBeatClock.NearestBeat nearestBeat,
                bool duplicateBeat,
                string message)
            {
                Judgement = grade;
                DeltaMs = deltaMs;
                RawTimelinePositionMs = rawTimelinePositionMs;
                EvaluatedTimelinePositionMs = evaluatedTimelinePositionMs;
                NearestBeat = nearestBeat;
                DuplicateBeat = duplicateBeat;
                Message = message;
            }

            public Grade Judgement { get; }
            public double DeltaMs { get; }
            public int RawTimelinePositionMs { get; }
            public double EvaluatedTimelinePositionMs { get; }
            public FmodBeatClock.NearestBeat NearestBeat { get; }
            public bool DuplicateBeat { get; }
            public string Message { get; }
        }

        [Header("Dependencies")]
        [SerializeField] private FmodBeatClock beatClock;

        [Header("Legacy-compatible Judgement")]
        [SerializeField, Min(1f)] private float perfectWindowMs = 120f;
        [SerializeField] private float judgementOffsetMs = 30f;
        [SerializeField] private bool duplicatePerfectBecomesMiss = true;

        private long lastPerfectGlobalBeat = long.MinValue;
        private readonly HashSet<long> successfulBeats = new();
        private bool personalCalibrationEnabled;
        private bool profileLoaded;
        private float personalDelayMs;
        private string inputProfile = "Keyboard";
        public const float MaxPersonalDelayMs = 150f;

        public event Action<Result> Judged;

        public float PerfectWindowMs => perfectWindowMs;
        // Positive personal delay means the player taps late. Subtract it from input time.
        public float JudgementOffsetMs => judgementOffsetMs - personalDelayMs;
        public float VisualOffsetMs => judgementOffsetMs;
        public float PersonalDelayMs => personalDelayMs;
        public bool PersonalCalibrationEnabled => personalCalibrationEnabled;
        public string InputProfile => inputProfile;
        public FmodBeatClock BeatClock => beatClock;

        public void EnablePersonalCalibration()
        {
            personalCalibrationEnabled = true;
            SetInputProfile(inputProfile);
        }

        public void SetInputProfile(string profile)
        {
            profile = profile == "Gamepad" ? "Gamepad" : "Keyboard";
            if (personalCalibrationEnabled && profileLoaded && inputProfile == profile) return;
            inputProfile = profile;
            personalDelayMs = personalCalibrationEnabled
                ? Mathf.Clamp(PlayerPrefs.GetFloat("FightTiming.v1." + inputProfile, 0f), -MaxPersonalDelayMs, MaxPersonalDelayMs) : 0f;
            profileLoaded = personalCalibrationEnabled;
        }

        public void SetPersonalDelay(float delayMs, bool save)
        {
            if (!personalCalibrationEnabled || float.IsNaN(delayMs) || float.IsInfinity(delayMs)) return;
            personalDelayMs = Mathf.Clamp(delayMs, -MaxPersonalDelayMs, MaxPersonalDelayMs);
            if (save)
            {
                PlayerPrefs.SetFloat("FightTiming.v1." + inputProfile, personalDelayMs);
                PlayerPrefs.Save();
            }
        }

        public bool TryMeasureInput(double inputAgeMs, out double deltaMs, out long globalBeat)
        {
            deltaMs = 0; globalBeat = -1;
            if (!TryGetInputTimeline(inputAgeMs, out int rawMs) ||
                !beatClock.TryGetNearestBeat(rawMs + VisualOffsetMs, out var nearest) || nearest.GlobalBeat < 0) return false;
            deltaMs = nearest.DeltaMs;
            globalBeat = nearest.GlobalBeat;
            return true;
        }

        private bool TryGetInputTimeline(double inputAgeMs, out int rawMs)
        {
            rawMs = 0;
            if (double.IsNaN(inputAgeMs) || double.IsInfinity(inputAgeMs) || inputAgeMs > 250 ||
                beatClock == null || !beatClock.HasTimingAnchor || !beatClock.TryGetTimelinePositionMs(out rawMs)) return false;
            rawMs -= (int)Math.Round(Math.Max(0, inputAgeMs) * beatClock.MusicPitch);
            return true;
        }

        public void Configure(FmodBeatClock clock, float windowMs, float offsetMs)
        {
            beatClock = clock;
            perfectWindowMs = Mathf.Max(1f, windowMs);
            judgementOffsetMs = offsetMs;
        }

        public Result JudgeNow() => JudgeInput(0);

        public Result JudgeInput(double inputAgeMs)
        {
            if (!TryGetInputTimeline(inputAgeMs, out int rawTimelineMs))
            {
                return Publish(new Result(
                    Grade.NotReady,
                    0.0,
                    0,
                    0.0,
                    default,
                    false,
                    "Waiting for timing, or input arrived too late."));
            }

            return JudgeTimelinePosition(rawTimelineMs);
        }

        // Separate timeline evaluation from FMOD polling so timing boundaries can be
        // validated deterministically against the exact same production judge.
        private Result JudgeTimelinePosition(int rawTimelineMs)
        {
            double evaluatedTimelineMs = rawTimelineMs + JudgementOffsetMs;
            if (!beatClock.TryGetNearestBeat(evaluatedTimelineMs, out FmodBeatClock.NearestBeat nearestBeat))
            {
                return Publish(new Result(
                    Grade.NotReady,
                    0.0,
                    rawTimelineMs,
                    evaluatedTimelineMs,
                    default,
                    false,
                    "FMOD timing data is not ready."));
            }

            bool insidePerfectWindow = nearestBeat.GlobalBeat >= 0 && Math.Abs(nearestBeat.DeltaMs) <= perfectWindowMs;
            bool duplicateBeat = insidePerfectWindow && (successfulBeats.Contains(nearestBeat.GlobalBeat) ||
                (lastPerfectGlobalBeat != long.MinValue && nearestBeat.GlobalBeat < lastPerfectGlobalBeat - 8));

            if (insidePerfectWindow && !(duplicatePerfectBecomesMiss && duplicateBeat))
            {
                lastPerfectGlobalBeat = Math.Max(lastPerfectGlobalBeat, nearestBeat.GlobalBeat);
                successfulBeats.Add(nearestBeat.GlobalBeat);
                successfulBeats.RemoveWhere(beat => beat < lastPerfectGlobalBeat - 8);
                return Publish(new Result(
                    Grade.Perfect,
                    nearestBeat.DeltaMs,
                    rawTimelineMs,
                    evaluatedTimelineMs,
                    nearestBeat,
                    false,
                    "Perfect"));
            }

            string missMessage = duplicateBeat
                ? "Miss - this beat was already hit."
                : "Miss - outside the Perfect window.";

            return Publish(new Result(
                Grade.Miss,
                nearestBeat.DeltaMs,
                rawTimelineMs,
                evaluatedTimelineMs,
                nearestBeat,
                duplicateBeat,
                missMessage));
        }

        public void ResetDuplicateTracking()
        {
            lastPerfectGlobalBeat = long.MinValue;
            successfulBeats.Clear();
        }

        private Result Publish(Result result)
        {
            Judged?.Invoke(result);
            return result;
        }
    }
}
