using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    [CreateAssetMenu(menuName = "Rhythm Hunter/Boss Rhythm Chart")]
    public sealed class FightBossRhythmChart : ScriptableObject
    {
        public enum Lane { Left, Middle, Right }
        [Serializable] public struct Note
        {
            [Min(0)] public int arrivalBeat;
            public Lane lane;
            [Tooltip("Positive = later, negative = earlier. Song milliseconds, separate from input calibration.")]
            public float offsetMs;
            public bool useFreeTime;
            [Min(0), Tooltip("Seconds after Normal_End, including the transition.")]
            public double timeSeconds;
            public double EffectiveBeat(double msPerBeat, double currentBeat, double sectionTimeMs) =>
                useFreeTime ? currentBeat + (timeSeconds * 1000 + offsetMs - sectionTimeMs) / msPerBeat
                    : arrivalBeat + offsetMs / msPerBeat;
        }
        [Tooltip("Arrivals relative to Normal_End, including the transition. Overlapping windows are allowed; one press selects the nearest unjudged note in its lane.")]
        public List<Note> notes = new();
        [Min(.25f)] public float travelBeats = 2;
        [Min(.1f)] public float animationBeats = 1;
        public bool Validate(out string error)
        {
            var occupied = new HashSet<string>();
            foreach (var note in notes)
                if (note.arrivalBeat < 0 || (int)note.lane < 0 || (int)note.lane > 2 ||
                    float.IsNaN(note.offsetMs) || float.IsInfinity(note.offsetMs) ||
                    double.IsNaN(note.timeSeconds) || double.IsInfinity(note.timeSeconds) || note.timeSeconds < 0 ||
                    !occupied.Add(note.useFreeTime ? $"time:{note.timeSeconds:R}:{note.offsetMs:R}" : $"beat:{note.arrivalBeat}:{note.offsetMs:R}"))
                { error = "Invalid or duplicate arrival. Times must be finite and nonnegative; each arrival has only one lane."; return false; }
            if (travelBeats <= 0 || animationBeats <= 0) { error = "Travel/animation beats must be positive."; return false; }
            error = null; return true;
        }
        public bool ValidateTiming(double msPerBeat, double currentBeat, double sectionTimeMs, double windowMs, out string error)
        {
            if (!Validate(out error)) return false;
            var times = new List<double>();
            foreach (var note in notes) times.Add(note.EffectiveBeat(msPerBeat, currentBeat, sectionTimeMs));
            times.Sort();
            for (int i = 0; i < times.Count; i++)
            {
                if (times[i] < 0 || (i > 0 && Math.Abs(times[i] - times[i - 1]) * msPerBeat < .001))
                { error = "Arrival precedes Normal_End or exactly coincides with another note. Simultaneous keys are not supported."; return false; }
            }
            return true;
        }
        public void GeneratePrototype()
        {
            notes.Clear(); int count = 0;
            // Deliberate phrases: spacious introduction, alternating lanes, short rests, dense finale.
            int[] pattern = { 0, 2, 1, 0, 1, 2, 0, 2, 1, 2, 0, 1 };
            for (int beat = 16; beat <= 110;)
            {
                if ((beat >= 64 && beat < 68) || (beat >= 96 && beat < 100)) { beat++; continue; }
                notes.Add(new Note { arrivalBeat = beat, lane = (Lane)pattern[count++ % pattern.Length] });
                beat += beat < 32 ? 4 : beat < 64 ? 2 : 1;
            }
        }
    }
}
