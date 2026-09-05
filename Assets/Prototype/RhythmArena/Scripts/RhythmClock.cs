using System;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.RhythmArena
{
    [DefaultExecutionOrder(-100)]
    public sealed class RhythmClock : MonoBehaviour
    {
        public enum TimingGrade
        {
            Perfect,
            Good,
            Offbeat
        }

        [Header("FMOD Beat Source")]
        [SerializeField] private FmodBeatClock fmodBeatClock;
        [SerializeField] private bool allowRealtimeFallback = true;
        [SerializeField, Min(0.1f)] private float fallbackDelaySeconds = 3f;

        [Header("Arena Rhythm")]
        [SerializeField, Min(1f)] private float bpm = 100f;
        [SerializeField, Min(1f)] private float fmodAuthoredBpm = 100f;
        [SerializeField, Min(1)] private int beatsPerLoop = 4;
        [Tooltip("Manual attack timing tolerance on either side of a beat, measured in beats.")]
        [SerializeField, Range(0.01f, 0.49f)] private float perfectWindowBeats = 0.10f;
        [Tooltip("Manual attack timing tolerance on either side of a beat. Must be at least the Perfect window.")]
        [SerializeField, Range(0.01f, 0.49f)] private float goodWindowBeats = 0.25f;

        private double sourceOriginBeat;
        private double absoluteBeatTime;
        private float enabledAtRealtime;
        private bool running;
        private bool usingFmod;
        private float appliedFmodBpm = -1f;

        public float Bpm => bpm;
        public int BeatsPerLoop => beatsPerLoop;
        public float PerfectWindowBeats => perfectWindowBeats;
        public float GoodWindowBeats => goodWindowBeats;
        public float BeatDurationSeconds => 60f / Mathf.Max(1f, bpm);
        public double AbsoluteBeatTime => absoluteBeatTime;
        public float LoopPhase => Mathf.Repeat((float)absoluteBeatTime, beatsPerLoop);
        public float LoopNormalized => LoopPhase / beatsPerLoop;
        public bool IsReady => running;
        public bool IsUsingFmod => usingFmod;

        private void OnEnable()
        {
            enabledAtRealtime = Time.realtimeSinceStartup;
            ApplyFmodTempo();
        }

        private void OnValidate()
        {
            bpm = Mathf.Max(1f, bpm);
            fmodAuthoredBpm = Mathf.Max(1f, fmodAuthoredBpm);
            beatsPerLoop = Mathf.Max(1, beatsPerLoop);
            perfectWindowBeats = Mathf.Clamp(perfectWindowBeats, 0.01f, 0.49f);
            goodWindowBeats = Mathf.Clamp(Mathf.Max(perfectWindowBeats, goodWindowBeats), 0.01f, 0.49f);
        }

        private void Update()
        {
            ApplyFmodTempo();

            if (TryReadFmodSourceBeat(out double fmodSourceBeat))
            {
                if (!running || !usingFmod)
                {
                    sourceOriginBeat = fmodSourceBeat;
                    absoluteBeatTime = 0.0;
                    running = true;
                    usingFmod = true;
                }
                else
                {
                    absoluteBeatTime = Math.Max(0.0, fmodSourceBeat - sourceOriginBeat);
                }

                return;
            }

            if (!allowRealtimeFallback || Time.realtimeSinceStartup - enabledAtRealtime < fallbackDelaySeconds)
                return;

            double realtimeBeat = Time.realtimeSinceStartupAsDouble * bpm / 60.0;
            if (!running)
            {
                sourceOriginBeat = realtimeBeat;
                running = true;
                usingFmod = false;
            }

            absoluteBeatTime = Math.Max(0.0, realtimeBeat - sourceOriginBeat);
        }

        public void Configure(
            FmodBeatClock source,
            float configuredBpm,
            int configuredBeatsPerLoop,
            float perfectWindow,
            float goodWindow)
        {
            fmodBeatClock = source;
            bpm = Mathf.Max(1f, configuredBpm);
            beatsPerLoop = Mathf.Max(1, configuredBeatsPerLoop);
            perfectWindowBeats = Mathf.Clamp(perfectWindow, 0.01f, 0.49f);
            goodWindowBeats = Mathf.Clamp(Mathf.Max(perfectWindowBeats, goodWindow), 0.01f, 0.49f);
        }

        public TimingGrade JudgeNow()
        {
            double nearestBeat = Math.Round(absoluteBeatTime, MidpointRounding.AwayFromZero);
            double distance = Math.Abs(absoluteBeatTime - nearestBeat);

            if (distance <= perfectWindowBeats)
                return TimingGrade.Perfect;
            if (distance <= goodWindowBeats)
                return TimingGrade.Good;
            return TimingGrade.Offbeat;
        }

        public void ResetCombatTimeline()
        {
            if (TryReadFmodSourceBeat(out double fmodSourceBeat))
            {
                sourceOriginBeat = fmodSourceBeat;
                usingFmod = true;
            }
            else
            {
                sourceOriginBeat = Time.realtimeSinceStartupAsDouble * bpm / 60.0;
                usingFmod = false;
            }

            absoluteBeatTime = 0.0;
            running = true;
        }

        private bool TryReadFmodSourceBeat(out double sourceBeat)
        {
            sourceBeat = 0.0;
            if (fmodBeatClock == null || !fmodBeatClock.HasTimingAnchor ||
                !fmodBeatClock.TryGetTimelinePositionMs(out int timelinePositionMs))
            {
                return false;
            }

            sourceBeat = timelinePositionMs * fmodAuthoredBpm / 60000.0;
            return true;
        }

        private void ApplyFmodTempo()
        {
            if (fmodBeatClock == null || Mathf.Approximately(appliedFmodBpm, bpm))
                return;

            fmodBeatClock.SetPlaybackPitch(bpm / Mathf.Max(1f, fmodAuthoredBpm));
            appliedFmodBpm = bpm;
        }
    }
}
