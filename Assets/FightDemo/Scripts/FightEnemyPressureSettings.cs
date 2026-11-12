using System;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>Wave authoring data; evaluated on the existing ordinary combat beat clock.</summary>
    [Serializable]
    public sealed class FightEnemyPressureSettings
    {
        public bool enablePressurePhase;
        [Tooltip("First effective combat beat of this Wave. Opening/entry, Team Skill and pauses do not count.")]
        [Min(1)] public int startBeat = 20;
        [Tooltip("End is exclusive: start 20 + duration 20 exits at beat 40.")]
        [Min(1)] public int durationBeats = 20;
        [Tooltip("Rest after the complete authored attack animation, in ordinary combat beats. The next attack starts on an eligible whole beat. Does not accelerate animation.")]
        [Min(0)] public int recoveryBeats = 2;
        public bool repeat = true;
        [Tooltip("Distance between starts. 40 gives starts at 20, 60, 100. Must be at least Duration Beats.")]
        [Min(1)] public int repeatEveryBeats = 40;

        public FightEnemyPressureSettings Copy() => (FightEnemyPressureSettings)MemberwiseClone();

        public bool Validate(out string error)
        {
            error = null;
            if (!enablePressurePhase) return true;
            if (startBeat < 1 || durationBeats < 1 || recoveryBeats < 0)
                error = "Pressure Start/Duration must be positive and Recovery must not be negative.";
            else if (repeat && repeatEveryBeats < durationBeats)
                error = "Pressure Repeat Every Beats must be at least Duration Beats (no overlapping windows).";
            return error == null;
        }

        public bool IsActiveAt(long waveBeat) => RemainingAt(waveBeat) > 0;

        public bool IsWarningAt(long waveBeat, int leadBeats)
        {
            if (leadBeats <= 0 || IsActiveAt(waveBeat)) return false;
            long next = NextStartAt(waveBeat);
            return next >= 0 && next > waveBeat && next - waveBeat <= leadBeats;
        }

        public long RemainingAt(long waveBeat)
        {
            if (!enablePressurePhase || !Validate(out _) || waveBeat < startBeat) return 0;
            long offset = waveBeat - startBeat;
            if (repeat) offset %= repeatEveryBeats;
            return offset < durationBeats ? durationBeats - offset : 0;
        }

        public long NextStartAt(long waveBeat)
        {
            if (!enablePressurePhase || !Validate(out _)) return -1;
            if (waveBeat < startBeat) return startBeat;
            if (!repeat) return -1;
            long offset = (waveBeat - startBeat) % repeatEveryBeats;
            long distance = repeatEveryBeats - offset;
            return waveBeat <= long.MaxValue - distance ? waveBeat + distance : -1;
        }
    }
}
