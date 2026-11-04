using System;

namespace RhythmHunter.FightDemo
{
    // Uses calibrated musical beats supplied by the existing FMOD clock/judge.
    public sealed class FightTeamSkillBeatWindow
    {
        public double Start { get; private set; }
        public double End { get; private set; }
        private long lastAccepted = -1;
        public void Begin(double start, int duration)
        { Start = start; End = start + Math.Max(1, duration); lastAccepted = -1; }
        public bool IsOpen(double now) => now >= Start && now < End;
        public bool TryAccept(double evaluatedBeat, double actualBeat, double toleranceBeats)
        {
            if (!IsOpen(actualBeat) || double.IsNaN(evaluatedBeat) || double.IsInfinity(evaluatedBeat)) return false;
            long slot = (long)Math.Round((evaluatedBeat - Start) * 2, MidpointRounding.AwayFromZero);
            double due = Start + slot * .5;
            if (slot < 0 || due >= End || slot <= lastAccepted ||
                Math.Abs(evaluatedBeat - due) > Math.Min(.24, Math.Max(0, toleranceBeats))) return false;
            lastAccepted = slot;
            return true;
        }
    }
}
