using UnityEngine;

namespace RhythmHunter.FightDemo
{
    // Logical progress is separate from the elastic presentation; visual overshoot cannot win.
    [System.Serializable]
    public sealed class FightFeverCharge
    {
        [Min(.001f)] public float initialGain = .05f;
        [Min(.001f)] public float finalGain = .016f;
        [Min(0)] public float decayPerBeat = .024f;
        [Min(1)] public float limitBeats = 16;
        public float Progress { get; private set; }
        public double Elapsed { get; private set; }
        public bool Won { get; private set; }
        public bool Expired => !Won && Elapsed >= limitBeats;
        public void Reset() { Progress = 0; Elapsed = 0; Won = false; }
        public void Advance(double beats)
        {
            if (Won || Expired || beats <= 0) return;
            Elapsed += beats;
            Progress = Mathf.Max(0, Progress - (float)beats * decayPerBeat);
        }
        public bool Press()
        {
            if (Won || Expired) return false;
            Progress = Mathf.Min(1, Progress + Mathf.Lerp(initialGain, finalGain, Progress * Progress));
            Won = Progress >= 1;
            return true;
        }
    }
}
