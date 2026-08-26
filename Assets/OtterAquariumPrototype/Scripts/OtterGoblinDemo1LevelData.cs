using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    [CreateAssetMenu(
        fileName = "OtterZooGoblinDemo1Level",
        menuName = "Rhythm Hunter/Otter Aquarium/Zoo Goblin Demo 1")]
    public sealed class OtterGoblinDemo1LevelData : ScriptableObject
    {
        public const int DefaultPpq = 480;

        public enum AttackKind
        {
            Single,
            Triple,
            DoubleSingle,
            TripleThenSingle
        }

        [Serializable]
        public sealed class AttackPattern
        {
            [SerializeField] private string id = "two-strong";
            [SerializeField] private int[] hitTicks = { 0, DefaultPpq * 2 };

            public string Id => id;
            public IReadOnlyList<int> HitTicks => hitTicks;

            public AttackPattern(string patternId, params int[] ticks)
            {
                id = string.IsNullOrWhiteSpace(patternId) ? "custom" : patternId.Trim();
                hitTicks = Sanitize(ticks);
            }

            public AttackPattern Clone()
            {
                int[] copy = hitTicks == null ? Array.Empty<int>() : (int[])hitTicks.Clone();
                return new AttackPattern(id, copy);
            }

            private static int[] Sanitize(int[] ticks)
            {
                if (ticks == null || ticks.Length == 0)
                    return Array.Empty<int>();

                List<int> result = new(ticks.Length);
                foreach (int tick in ticks)
                {
                    int safe = Mathf.Max(0, tick);
                    if (!result.Contains(safe))
                        result.Add(safe);
                }
                result.Sort();
                return result.ToArray();
            }
        }

        [Serializable]
        public sealed class AttackPhrase
        {
            [SerializeField, Min(1)] private int startBar = 5;
            [SerializeField] private string label = "SINGLE • X _";
            [SerializeField] private AttackKind kind = AttackKind.Single;
            [SerializeField, Min(1)] private int warningLengthBeats = 1;
            [SerializeField, HideInInspector] private int responseDelayBeats = 1;
            [SerializeField, Min(1)] private int attackLengthBeats = 1;
            [SerializeField] private AttackPattern warningPattern;
            [SerializeField] private AttackPattern pattern;

            public int StartBar => startBar;
            public string Label => label;
            public AttackKind Kind => kind;
            public int WarningLengthBeats => warningLengthBeats;
            public int WaitBeatCount => kind is AttackKind.DoubleSingle or AttackKind.TripleThenSingle ? 2 : 1;
            public int ResponseDelayBeats => 2;
            public int AttackLengthBeats => attackLengthBeats;
            public AttackPattern WarningPattern => warningPattern;
            public AttackPattern Pattern => pattern;
            public int TotalLengthBeats => ResponseDelayBeats + attackLengthBeats;

            public AttackPhrase(
                int configuredStartBar,
                string configuredLabel,
                AttackKind configuredKind,
                int configuredWarningBeats,
                int configuredResponseDelayBeats,
                int configuredAttackBeats,
                AttackPattern configuredWarningPattern,
                AttackPattern configuredAttackPattern)
            {
                startBar = Mathf.Max(1, configuredStartBar);
                label = string.IsNullOrWhiteSpace(configuredLabel) ? "RHYTHM ATTACK" : configuredLabel.Trim();
                kind = configuredKind;
                warningLengthBeats = Mathf.Max(1, configuredWarningBeats);
                responseDelayBeats = 1;
                attackLengthBeats = Mathf.Max(1, configuredAttackBeats);
                warningPattern = configuredWarningPattern?.Clone();
                pattern = configuredAttackPattern?.Clone();
            }

            public AttackPhrase Clone()
            {
                return new AttackPhrase(
                    startBar,
                    label,
                    kind,
                    warningLengthBeats,
                    responseDelayBeats,
                    attackLengthBeats,
                    warningPattern,
                    pattern);
            }
        }

        [Header("Authoring")]
        [SerializeField] private string levelId = "zoo-goblin-demo-1";
        [SerializeField] private string displayName = "Demo1：動物園哥布林節拍戰";
        [SerializeField, TextArea(3, 7)] private string authoringNotes =
            "Demo1 使用 X _ 與 X X X _ 兩種基本語彙，Bar 11 後開始組合成連續攻勢。";

        [Header("Music")]
        [SerializeField] private string musicEventPath = "event:/ZooGoblinFight/BGM/Goblin Patrol";
        [SerializeField, Min(0f)] private float musicStartDelaySeconds = 1f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.55f;
        [SerializeField] private float musicGridOffsetMs;
        [SerializeField, Min(1f)] private float authoredBpm = 120f;
        [SerializeField, Min(1)] private int beatsPerBar = 4;
        [SerializeField, Min(4)] private int totalBars = 33;
        [SerializeField, Min(24)] private int ppq = DefaultPpq;

        [Header("Combat")]
        [SerializeField, Min(1)] private int otterMaxHealth = 3;
        [SerializeField, Min(1)] private int damagePerMiss = 1;
        [SerializeField, Min(1f)] private float perfectWindowMs = 70f;
        [SerializeField, Min(1f)] private float goodWindowMs = 140f;
        [SerializeField] private float judgementOffsetMs;

        [Header("FMOD Events")]
        [SerializeField] private string warningSoundEventPath = "event:/ZooGoblinFight/SoundEffects/Warning";
        [SerializeField] private string attackSoundEventPath = "event:/ZooGoblinFight/SoundEffects/AxeGoblin_NormalAttack";
        [SerializeField] private string blockSoundEventPath = "event:/ZooGoblinFight/SoundEffects/BeatTapping";
        [SerializeField] private string perfectSoundEventPath = string.Empty;
        [SerializeField] private string goodSoundEventPath = string.Empty;
        [SerializeField] private string missSoundEventPath = string.Empty;

        [Header("Single / Triple Cue → Axe Beat → Defend")]
        [SerializeField] private List<AttackPhrase> phrases = new();

        public string LevelId => levelId;
        public string DisplayName => displayName;
        public string AuthoringNotes => authoringNotes;
        public string MusicEventPath => musicEventPath;
        public float MusicStartDelaySeconds => musicStartDelaySeconds;
        public float MusicVolume => musicVolume;
        public float MusicGridOffsetMs => musicGridOffsetMs;
        public float AuthoredBpm => authoredBpm;
        public int BeatsPerBar => beatsPerBar;
        public int TotalBars => totalBars;
        public int Ppq => ppq;
        public int TicksPerBar => ppq * beatsPerBar;
        public int OtterMaxHealth => otterMaxHealth;
        public int DamagePerMiss => damagePerMiss;
        public float PerfectWindowMs => perfectWindowMs;
        public float GoodWindowMs => Mathf.Max(perfectWindowMs, goodWindowMs);
        public float JudgementOffsetMs => judgementOffsetMs;
        public string WarningSoundEventPath => warningSoundEventPath;
        public string AttackSoundEventPath => attackSoundEventPath;
        public string BlockSoundEventPath => blockSoundEventPath;
        public string PerfectSoundEventPath => perfectSoundEventPath;
        public string GoodSoundEventPath => goodSoundEventPath;
        public string MissSoundEventPath => missSoundEventPath;
        public IReadOnlyList<AttackPhrase> Phrases => phrases;

        public void ConfigureDemo1Defaults()
        {
            levelId = "zoo-goblin-demo-1";
            displayName = "Demo1：動物園哥布林節拍戰";
            authoringNotes =
                "Goblin Patrol 由 X _ 與快速三連構成；Bar 11 起加入雙 X _ 與三連接單擊。";
            musicEventPath = "event:/ZooGoblinFight/BGM/Goblin Patrol";
            musicStartDelaySeconds = 1f;
            musicVolume = 0.55f;
            musicGridOffsetMs = 0f;
            authoredBpm = 120f;
            beatsPerBar = 4;
            totalBars = 33;
            ppq = DefaultPpq;
            otterMaxHealth = 3;
            damagePerMiss = 1;
            perfectWindowMs = 85f;
            goodWindowMs = 170f;
            judgementOffsetMs = 0f;
            warningSoundEventPath = "event:/ZooGoblinFight/SoundEffects/Warning";
            attackSoundEventPath = "event:/ZooGoblinFight/SoundEffects/AxeGoblin_NormalAttack";
            blockSoundEventPath = "event:/ZooGoblinFight/SoundEffects/BeatTapping";
            perfectSoundEventPath = string.Empty;
            goodSoundEventPath = string.Empty;
            missSoundEventPath = string.Empty;

            AttackPattern singleCue = Pattern("single-cue", 0f);
            AttackPattern singleResponse = Pattern("single-response", 0f);
            AttackPattern tripleCue = Pattern("triple-cue", 0f, 0.5f, 1f);
            AttackPattern tripleResponse = Pattern("triple-response", 0f, 1f, 2f);
            AttackPattern doubleSingleCue = Pattern("double-single-cue", 0f, 2f);
            AttackPattern doubleSingleResponse = Pattern("double-single-response", 0f, 2f);
            AttackPattern tripleThenSingleCue = Pattern("triple-single-cue", 0f, 0.5f, 1f, 4f);
            AttackPattern tripleThenSingleResponse = Pattern("triple-single-response", 0f, 1f, 2f, 4f);

            phrases = new List<AttackPhrase>
            {
                Single(5, singleCue, singleResponse),
                Single(7, singleCue, singleResponse),
                Single(9, singleCue, singleResponse),
                DoubleSingle(11, doubleSingleCue, doubleSingleResponse),
                Triple(13, tripleCue, tripleResponse),
                DoubleSingle(15, doubleSingleCue, doubleSingleResponse),
                TripleThenSingle(17, tripleThenSingleCue, tripleThenSingleResponse),
                DoubleSingle(19, doubleSingleCue, doubleSingleResponse),
                TripleThenSingle(21, tripleThenSingleCue, tripleThenSingleResponse),
                DoubleSingle(23, doubleSingleCue, doubleSingleResponse),
                TripleThenSingle(25, tripleThenSingleCue, tripleThenSingleResponse),
                DoubleSingle(27, doubleSingleCue, doubleSingleResponse),
                TripleThenSingle(29, tripleThenSingleCue, tripleThenSingleResponse),
                TripleThenSingle(31, tripleThenSingleCue, tripleThenSingleResponse),
                Triple(33, tripleCue, tripleResponse)
            };
        }

        public bool Validate(out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(musicEventPath))
            {
                error = "Music event path is empty.";
                return false;
            }
            if (phrases == null || phrases.Count == 0)
            {
                error = "No attack phrases are configured.";
                return false;
            }

            long previousEndTick = 0;
            for (int i = 0; i < phrases.Count; i++)
            {
                AttackPhrase phrase = phrases[i];
                if (phrase == null || phrase.WarningPattern == null || phrase.WarningPattern.HitTicks.Count == 0
                    || phrase.Pattern == null || phrase.Pattern.HitTicks.Count == 0)
                {
                    error = $"Phrase #{i + 1} has no pattern.";
                    return false;
                }

                long startTick = (long)(phrase.StartBar - 1) * TicksPerBar;
                if (startTick < previousEndTick)
                {
                    error = $"Phrase #{i + 1} overlaps the preceding phrase.";
                    return false;
                }

                int lastWarning = phrase.WarningPattern.HitTicks[phrase.WarningPattern.HitTicks.Count - 1];
                int lastAttack = phrase.Pattern.HitTicks[phrase.Pattern.HitTicks.Count - 1];
                if (lastWarning >= phrase.WarningLengthBeats * Ppq || lastAttack > phrase.AttackLengthBeats * Ppq)
                {
                    error = $"Phrase #{i + 1} has a hit outside its warning/attack length.";
                    return false;
                }

                int expectedCount = phrase.Kind switch
                {
                    AttackKind.Single => 1,
                    AttackKind.Triple => 3,
                    AttackKind.DoubleSingle => 2,
                    AttackKind.TripleThenSingle => 4,
                    _ => 0
                };
                if (phrase.WarningPattern.HitTicks.Count != expectedCount
                    || phrase.Pattern.HitTicks.Count != expectedCount
                    || (phrase.Kind == AttackKind.Single
                        && (phrase.WarningLengthBeats != 1
                            || phrase.WaitBeatCount != 1
                            || phrase.ResponseDelayBeats != 2
                            || phrase.AttackLengthBeats != 1
                            || !Matches(phrase.WarningPattern, 0)
                            || !Matches(phrase.Pattern, 0)))
                    || (phrase.Kind == AttackKind.Triple
                        && (phrase.WarningLengthBeats != 2
                            || phrase.WaitBeatCount != 1
                            || phrase.ResponseDelayBeats != 2
                            || phrase.AttackLengthBeats != 2
                            || !Matches(phrase.WarningPattern, 0, Ppq / 2, Ppq)
                            || !Matches(phrase.Pattern, 0, Ppq, Ppq * 2)))
                    || (phrase.Kind == AttackKind.DoubleSingle
                        && (phrase.WarningLengthBeats != 3
                            || phrase.WaitBeatCount != 2
                            || phrase.ResponseDelayBeats != 2
                            || phrase.AttackLengthBeats != 3
                            || !Matches(phrase.WarningPattern, 0, Ppq * 2)
                            || !Matches(phrase.Pattern, 0, Ppq * 2)))
                    || (phrase.Kind == AttackKind.TripleThenSingle
                        && (phrase.WarningLengthBeats != 5
                            || phrase.WaitBeatCount != 2
                            || phrase.ResponseDelayBeats != 2
                            || phrase.AttackLengthBeats != 5
                            || !Matches(phrase.WarningPattern, 0, Ppq / 2, Ppq, Ppq * 4)
                            || !Matches(phrase.Pattern, 0, Ppq, Ppq * 2, Ppq * 4))))
                {
                    error = $"Phrase #{i + 1} does not follow the supported primitive/combo rules.";
                    return false;
                }

                previousEndTick = startTick + (long)phrase.TotalLengthBeats * Ppq;
                if (previousEndTick > (long)totalBars * TicksPerBar)
                {
                    error = $"Phrase #{i + 1} exceeds the song length.";
                    return false;
                }
            }
            return true;
        }

        private AttackPattern Pattern(string id, params float[] beats)
        {
            int[] ticks = new int[beats.Length];
            for (int i = 0; i < beats.Length; i++)
                ticks[i] = Mathf.RoundToInt(beats[i] * ppq);
            return new AttackPattern(id, ticks);
        }

        private static bool Matches(AttackPattern pattern, params int[] expectedTicks)
        {
            if (pattern == null || pattern.HitTicks.Count != expectedTicks.Length)
                return false;
            for (int i = 0; i < expectedTicks.Length; i++)
            {
                if (pattern.HitTicks[i] != expectedTicks[i])
                    return false;
            }
            return true;
        }

        private static AttackPhrase Single(
            int bar,
            AttackPattern cue,
            AttackPattern response)
        {
            return new AttackPhrase(
                bar,
                "SINGLE • X _",
                AttackKind.Single,
                1,
                1,
                1,
                cue,
                response);
        }

        private static AttackPhrase Triple(int bar, AttackPattern cue, AttackPattern response)
        {
            return new AttackPhrase(
                bar,
                "SPECIAL • X X X _",
                AttackKind.Triple,
                2,
                1,
                2,
                cue,
                response);
        }

        private static AttackPhrase DoubleSingle(int bar, AttackPattern cue, AttackPattern response)
        {
            return new AttackPhrase(
                bar,
                "COMBO • X _ ×2",
                AttackKind.DoubleSingle,
                3,
                1,
                3,
                cue,
                response);
        }

        private static AttackPhrase TripleThenSingle(int bar, AttackPattern cue, AttackPattern response)
        {
            return new AttackPhrase(
                bar,
                "COMBO • TRIPLE → X _",
                AttackKind.TripleThenSingle,
                5,
                1,
                5,
                cue,
                response);
        }

        private void OnValidate()
        {
            authoredBpm = Mathf.Max(1f, authoredBpm);
            musicVolume = Mathf.Clamp01(musicVolume);
            beatsPerBar = Mathf.Max(1, beatsPerBar);
            totalBars = Mathf.Max(4, totalBars);
            ppq = Mathf.Max(24, ppq);
            otterMaxHealth = Mathf.Max(1, otterMaxHealth);
            damagePerMiss = Mathf.Max(1, damagePerMiss);
            perfectWindowMs = Mathf.Max(1f, perfectWindowMs);
            goodWindowMs = Mathf.Max(perfectWindowMs, goodWindowMs);
            phrases?.Sort((left, right) => left.StartBar.CompareTo(right.StartBar));
        }
    }
}
