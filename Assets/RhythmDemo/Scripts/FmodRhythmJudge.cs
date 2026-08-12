using System;
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

        public event Action<Result> Judged;

        public float PerfectWindowMs => perfectWindowMs;
        public float JudgementOffsetMs => judgementOffsetMs;
        public FmodBeatClock BeatClock => beatClock;

        public void Configure(FmodBeatClock clock, float windowMs, float offsetMs)
        {
            beatClock = clock;
            perfectWindowMs = Mathf.Max(1f, windowMs);
            judgementOffsetMs = offsetMs;
        }

        public Result JudgeNow()
        {
            if (beatClock == null || !beatClock.HasTimingAnchor ||
                !beatClock.TryGetTimelinePositionMs(out int rawTimelineMs))
            {
                return Publish(new Result(
                    Grade.NotReady,
                    0.0,
                    0,
                    0.0,
                    default,
                    false,
                    "Waiting for the first FMOD beat callback."));
            }

            // Preserve the legacy listener's +30 ms judgement convention.
            double evaluatedTimelineMs = rawTimelineMs + judgementOffsetMs;
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

            bool insidePerfectWindow = Math.Abs(nearestBeat.DeltaMs) <= perfectWindowMs;
            bool duplicateBeat = insidePerfectWindow && nearestBeat.GlobalBeat == lastPerfectGlobalBeat;

            if (insidePerfectWindow && !(duplicatePerfectBecomesMiss && duplicateBeat))
            {
                lastPerfectGlobalBeat = nearestBeat.GlobalBeat;
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
        }

        private Result Publish(Result result)
        {
            Judged?.Invoke(result);
            return result;
        }
    }
}
