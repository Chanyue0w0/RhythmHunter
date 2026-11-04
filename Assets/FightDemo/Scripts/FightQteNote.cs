using System;

namespace RhythmHunter.FightDemo
{
    // Pure timing rule: a prompt can resolve exactly once. Targets remain owned
    // by the skill, so misses never compact later shots into earlier enemy slots.
    public sealed class FightQteNote
    {
        public enum Result { Pending, Hit, Miss }
        public readonly double DueBeat, ShowBeat, WindowBeats;
        public Result Outcome { get; private set; }
        public FightQteNote(double dueBeat, double leadBeats, double windowBeats)
        { DueBeat = dueBeat; ShowBeat = dueBeat - leadBeats; WindowBeats = windowBeats; }
        public bool Press(double evaluatedBeat, double visualBeat)
        {
            if (Outcome != Result.Pending || visualBeat < ShowBeat) return false;
            Outcome = Math.Abs(evaluatedBeat - DueBeat) <= WindowBeats ? Result.Hit : Result.Miss;
            return true;
        }
        public bool Expire(double evaluatedBeat)
        {
            if (Outcome != Result.Pending || evaluatedBeat <= DueBeat + WindowBeats) return false;
            Outcome = Result.Miss;
            return true;
        }
    }
}
