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
        }
        [Tooltip("Arrival beats relative to Normal_End, including the phase transition. One note per whole beat, no holds.")]
        public List<Note> notes = new();
        [Min(.25f)] public float travelBeats = 2;
        [Min(.1f)] public float animationBeats = 1;
        public bool Validate(out string error)
        {
            var occupied = new HashSet<int>();
            foreach (var note in notes)
                if (note.arrivalBeat < 0 || (int)note.lane < 0 || (int)note.lane > 2 || !occupied.Add(note.arrivalBeat))
                { error = "Each arrival beat must be nonnegative and unique across all three lanes."; return false; }
            error = null; return true;
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
