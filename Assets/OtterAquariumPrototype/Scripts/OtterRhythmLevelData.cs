using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    [CreateAssetMenu(
        fileName = "OtterShellBeatLevel",
        menuName = "Rhythm Hunter/Otter Aquarium/Shell Beat Level")]
    public sealed class OtterRhythmLevelData : ScriptableObject
    {
        public const int DefaultPpq = 480;

        [Serializable]
        public sealed class Pattern
        {
            [SerializeField] private string id = "pattern";
            [SerializeField] private int[] hitTicks = { 0, DefaultPpq, DefaultPpq * 2 };

            public string Id => id;
            public IReadOnlyList<int> HitTicks => hitTicks;

            public Pattern(string patternId, params int[] ticks)
            {
                id = string.IsNullOrWhiteSpace(patternId) ? "custom" : patternId.Trim();
                hitTicks = SanitizeTicks(ticks);
            }

            public Pattern Clone()
            {
                int[] ticks = new int[hitTicks != null ? hitTicks.Length : 0];
                if (hitTicks != null)
                    Array.Copy(hitTicks, ticks, hitTicks.Length);
                return new Pattern(id, ticks);
            }

            private static int[] SanitizeTicks(int[] ticks)
            {
                if (ticks == null || ticks.Length == 0)
                    return Array.Empty<int>();

                List<int> sanitized = new(ticks.Length);
                foreach (int tick in ticks)
                {
                    int safeTick = Mathf.Max(0, tick);
                    if (!sanitized.Contains(safeTick))
                        sanitized.Add(safeTick);
                }
                sanitized.Sort();
                return sanitized.ToArray();
            }
        }

        [Serializable]
        public sealed class Phrase
        {
            [SerializeField, Min(1)] private int startBar = 3;
            [SerializeField] private string label = "LISTEN, THEN REPEAT";
            [SerializeField] private bool adaptive;
            [SerializeField] private Pattern assistPattern;
            [SerializeField] private Pattern standardPattern;
            [SerializeField] private Pattern challengePattern;

            public int StartBar => startBar;
            public string Label => label;
            public bool Adaptive => adaptive;
            public Pattern AssistPattern => assistPattern ?? standardPattern;
            public Pattern StandardPattern => standardPattern;
            public Pattern ChallengePattern => challengePattern ?? standardPattern;

            public Phrase(
                int configuredStartBar,
                string configuredLabel,
                bool isAdaptive,
                Pattern assist,
                Pattern standard,
                Pattern challenge)
            {
                startBar = configuredStartBar;
                label = configuredLabel;
                adaptive = isAdaptive;
                assistPattern = assist;
                standardPattern = standard;
                challengePattern = challenge;
            }

            public Phrase Clone()
            {
                return new Phrase(
                    startBar,
                    label,
                    adaptive,
                    assistPattern?.Clone(),
                    standardPattern?.Clone(),
                    challengePattern?.Clone());
            }
        }

        [SerializeField, HideInInspector] private int dataVersion = 2;

        [Header("Authoring")]
        [SerializeField] private string levelId = "otter-shell-beat-01";
        [SerializeField] private string displayName = "海獺敲貝實驗室";
        [SerializeField, TextArea(2, 5)] private string authoringNotes = "螃蟹示範一小節，海獺在下一小節重複。";

        [Header("Music")]
        [SerializeField] private string musicEventPath = "event:/Combat soundtracks/Combat 01";
        [SerializeField, Min(0f)] private float musicStartDelaySeconds = 1f;
        [SerializeField] private float chartOffsetMs;
        [SerializeField, Min(1f)] private float authoredBpm = 100f;
        [SerializeField, Min(1)] private int beatsPerBar = 4;
        [SerializeField, Min(4)] private int totalBars = 24;
        [SerializeField, Min(24)] private int ppq = DefaultPpq;

        [Header("Judgement")]
        [SerializeField, Min(1f)] private float perfectWindowMs = 70f;
        [SerializeField, Min(1f)] private float goodWindowMs = 140f;
        [SerializeField] private float judgementOffsetMs = 30f;

        [Header("Optional FMOD SFX Slots")]
        [SerializeField] private string cueSoundEventPath = string.Empty;
        [SerializeField] private string hitSoundEventPath = string.Empty;
        [SerializeField] private string missSoundEventPath = string.Empty;
        [SerializeField] private string successSoundEventPath = string.Empty;

        [Header("Two-bar Call / Response Phrases")]
        [SerializeField] private List<Phrase> phrases = new();

        public int DataVersion => dataVersion;
        public string LevelId => levelId;
        public string DisplayName => displayName;
        public string AuthoringNotes => authoringNotes;
        public string MusicEventPath => musicEventPath;
        public float MusicStartDelaySeconds => musicStartDelaySeconds;
        public float ChartOffsetMs => chartOffsetMs;
        public float AuthoredBpm => authoredBpm;
        public int BeatsPerBar => beatsPerBar;
        public int TotalBars => totalBars;
        public int Ppq => ppq;
        public int TicksPerBar => ppq * beatsPerBar;
        public float PerfectWindowMs => perfectWindowMs;
        public float GoodWindowMs => Mathf.Max(perfectWindowMs, goodWindowMs);
        public float JudgementOffsetMs => judgementOffsetMs;
        public string CueSoundEventPath => cueSoundEventPath;
        public string HitSoundEventPath => hitSoundEventPath;
        public string MissSoundEventPath => missSoundEventPath;
        public string SuccessSoundEventPath => successSoundEventPath;
        public IReadOnlyList<Phrase> Phrases => phrases;

        public void ConfigurePrototypeDefaults()
        {
            dataVersion = 2;
            levelId = "otter-shell-beat-01";
            displayName = "海獺敲貝實驗室";
            authoringNotes = "螃蟹示範一小節，海獺在下一小節重複。";
            musicEventPath = "event:/Combat soundtracks/Combat 01";
            musicStartDelaySeconds = 1f;
            chartOffsetMs = 0f;
            authoredBpm = 100f;
            beatsPerBar = 4;
            totalBars = 24;
            ppq = DefaultPpq;
            perfectWindowMs = 70f;
            goodWindowMs = 140f;
            judgementOffsetMs = 30f;
            cueSoundEventPath = string.Empty;
            hitSoundEventPath = string.Empty;
            missSoundEventPath = string.Empty;
            successSoundEventPath = string.Empty;

            Pattern threeStraight = new("three-straight", 0, ppq, ppq * 2);
            Pattern twoWide = new("two-wide", 0, ppq * 2);
            Pattern restThird = new("rest-third", 0, ppq, ppq * 3);
            Pattern offbeat = new("offbeat", 0, ppq + ppq / 2, ppq * 3);
            Pattern syncopated = new("syncopated", 0, ppq + ppq / 2, ppq * 2 + ppq / 2);
            Pattern shortAssist = new("assist-two", 0, ppq * 2);

            phrases = new List<Phrase>
            {
                Fixed(3, "LISTEN  •  THEN REPEAT", threeStraight),
                Fixed(5, "KEEP THE WATER PULSE", threeStraight),
                Fixed(7, "ONE MORE CLEAN SHELL", twoWide),
                Fixed(9, "COPY THE REST", restThird),
                Fixed(11, "WIDE KNOCKS", twoWide),
                Fixed(13, "THE LAST KNOCK MOVES", offbeat),
                Fixed(15, "DON'T FILL THE SILENCE", restThird),
                Fixed(17, "FOLLOW THE HALF BEAT", offbeat),
                Fixed(19, "SYNCOPATED SHELL", syncopated),
                Adaptive(21, "ADAPTIVE FINALE I", shortAssist, restThird, syncopated),
                Adaptive(23, "ADAPTIVE FINALE II", twoWide, offbeat, syncopated)
            };
        }

        public void ConfigureAuthoring(string id, string levelName, string notes)
        {
            levelId = string.IsNullOrWhiteSpace(id) ? name : id.Trim();
            displayName = string.IsNullOrWhiteSpace(levelName) ? levelId : levelName.Trim();
            authoringNotes = notes ?? string.Empty;
            dataVersion = 2;
        }

        public void ConfigureMusic(
            string eventPath,
            float startDelaySeconds,
            float offsetMs,
            float bpm,
            int configuredBeatsPerBar,
            int configuredTotalBars,
            int configuredPpq = DefaultPpq)
        {
            musicEventPath = eventPath?.Trim() ?? string.Empty;
            musicStartDelaySeconds = Mathf.Max(0f, startDelaySeconds);
            chartOffsetMs = offsetMs;
            authoredBpm = Mathf.Max(1f, bpm);
            beatsPerBar = Mathf.Max(1, configuredBeatsPerBar);
            totalBars = Mathf.Max(4, configuredTotalBars);
            ppq = Mathf.Max(24, configuredPpq);
        }

        public void ConfigureJudgement(float perfectMs, float goodMs, float offsetMs)
        {
            perfectWindowMs = Mathf.Max(1f, perfectMs);
            goodWindowMs = Mathf.Max(perfectWindowMs, goodMs);
            judgementOffsetMs = offsetMs;
        }

        public void ConfigureOptionalSfx(string cue, string hit, string miss, string success)
        {
            cueSoundEventPath = cue?.Trim() ?? string.Empty;
            hitSoundEventPath = hit?.Trim() ?? string.Empty;
            missSoundEventPath = miss?.Trim() ?? string.Empty;
            successSoundEventPath = success?.Trim() ?? string.Empty;
        }

        public void ReplacePhrases(IEnumerable<Phrase> replacement)
        {
            phrases = new List<Phrase>();
            if (replacement != null)
            {
                foreach (Phrase phrase in replacement)
                {
                    if (phrase != null)
                        phrases.Add(phrase.Clone());
                }
            }
            SortPhrases();
            FitTotalBarsToPhrases();
        }

        public void AddPhrase(Phrase phrase)
        {
            if (phrase == null)
                return;
            phrases.Add(phrase.Clone());
            SortPhrases();
            FitTotalBarsToPhrases();
        }

        public void ReplacePhrase(int index, Phrase phrase)
        {
            if (phrase == null || index < 0 || index >= phrases.Count)
                return;
            phrases[index] = phrase.Clone();
            SortPhrases();
            FitTotalBarsToPhrases();
        }

        public void RemovePhraseAt(int index)
        {
            if (index < 0 || index >= phrases.Count)
                return;
            phrases.RemoveAt(index);
            FitTotalBarsToPhrases();
        }

        public void MovePhrase(int index, int direction)
        {
            int target = index + direction;
            if (index < 0 || index >= phrases.Count || target < 0 || target >= phrases.Count)
                return;
            (phrases[index], phrases[target]) = (phrases[target], phrases[index]);
        }

        public bool EnsureAuthoringDefaults()
        {
            bool changed = false;
            if (dataVersion < 2)
            {
                dataVersion = 2;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(levelId))
            {
                levelId = string.IsNullOrWhiteSpace(name) ? "otter-rhythm-level" : name;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = levelId;
                changed = true;
            }
            if (musicStartDelaySeconds < 0f)
            {
                musicStartDelaySeconds = 0f;
                changed = true;
            }
            return changed;
        }

        private void SortPhrases()
        {
            phrases.Sort((left, right) => left.StartBar.CompareTo(right.StartBar));
        }

        private void FitTotalBarsToPhrases()
        {
            int requiredBars = 4;
            foreach (Phrase phrase in phrases)
                requiredBars = Mathf.Max(requiredBars, phrase.StartBar + 1);
            totalBars = Mathf.Max(totalBars, requiredBars);
        }

        private static Phrase Fixed(int bar, string label, Pattern pattern)
        {
            return new Phrase(bar, label, false, pattern, pattern, pattern);
        }

        private static Phrase Adaptive(
            int bar,
            string label,
            Pattern assist,
            Pattern standard,
            Pattern challenge)
        {
            return new Phrase(bar, label, true, assist, standard, challenge);
        }

        private void OnValidate()
        {
            EnsureAuthoringDefaults();
            authoredBpm = Mathf.Max(1f, authoredBpm);
            beatsPerBar = Mathf.Max(1, beatsPerBar);
            totalBars = Mathf.Max(4, totalBars);
            ppq = Mathf.Max(24, ppq);
            perfectWindowMs = Mathf.Max(1f, perfectWindowMs);
            goodWindowMs = Mathf.Max(perfectWindowMs, goodWindowMs);
            SortPhrases();
            FitTotalBarsToPhrases();
        }
    }
}
