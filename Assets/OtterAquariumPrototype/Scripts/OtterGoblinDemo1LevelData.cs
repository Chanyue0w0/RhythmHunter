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
            [SerializeField, Min(0)] private int startOffsetTicks;
            [SerializeField] private string label = "SINGLE • X _";
            [SerializeField] private AttackKind kind = AttackKind.Single;
            [SerializeField, Min(1)] private int warningLengthBeats = 1;
            [SerializeField, HideInInspector] private int responseDelayBeats = 1;
            [SerializeField, Min(1)] private int attackLengthBeats = 1;
            [SerializeField] private AttackPattern warningPattern;
            [SerializeField] private AttackPattern pattern;
            [SerializeField] private GameObject[] projectilePrefabs = Array.Empty<GameObject>();

            public int StartBar => startBar;
            public int StartOffsetTicks => Mathf.Max(0, startOffsetTicks);
            public string Label => label;
            public AttackKind Kind => kind;
            public int WarningLengthBeats => warningLengthBeats;
            public int WaitBeatCount => kind is AttackKind.DoubleSingle or AttackKind.TripleThenSingle ? 2 : 1;
            public int ResponseDelayBeats => 2;
            public int AttackLengthBeats => attackLengthBeats;
            public AttackPattern WarningPattern => warningPattern;
            public AttackPattern Pattern => pattern;
            public IReadOnlyList<GameObject> ProjectilePrefabs => projectilePrefabs ?? Array.Empty<GameObject>();
            public int TotalLengthBeats => ResponseDelayBeats + attackLengthBeats;

            public GameObject GetProjectilePrefab(int projectileIndex, GameObject fallback)
            {
                if (projectilePrefabs == null || projectileIndex < 0 || projectileIndex >= projectilePrefabs.Length)
                    return fallback;
                return projectilePrefabs[projectileIndex] != null ? projectilePrefabs[projectileIndex] : fallback;
            }

            public AttackPhrase(
                int configuredStartBar,
                string configuredLabel,
                AttackKind configuredKind,
                int configuredWarningBeats,
                int configuredResponseDelayBeats,
                int configuredAttackBeats,
                AttackPattern configuredWarningPattern,
                AttackPattern configuredAttackPattern,
                int configuredStartOffsetTicks = 0)
            {
                startBar = Mathf.Max(1, configuredStartBar);
                startOffsetTicks = Mathf.Max(0, configuredStartOffsetTicks);
                label = string.IsNullOrWhiteSpace(configuredLabel) ? "RHYTHM ATTACK" : configuredLabel.Trim();
                kind = configuredKind;
                warningLengthBeats = Mathf.Max(1, configuredWarningBeats);
                responseDelayBeats = 1;
                attackLengthBeats = Mathf.Max(1, configuredAttackBeats);
                warningPattern = configuredWarningPattern?.Clone();
                pattern = configuredAttackPattern?.Clone();
                projectilePrefabs = new GameObject[pattern?.HitTicks.Count ?? 0];
            }

            public AttackPhrase Clone()
            {
                AttackPhrase clone = new AttackPhrase(
                    startBar,
                    label,
                    kind,
                    warningLengthBeats,
                    responseDelayBeats,
                    attackLengthBeats,
                    warningPattern,
                    pattern,
                    startOffsetTicks);
                clone.projectilePrefabs = projectilePrefabs == null
                    ? Array.Empty<GameObject>()
                    : (GameObject[])projectilePrefabs.Clone();
                return clone;
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
        [SerializeField, Min(1f)] private float perfectWindowMs = 70f;
        [SerializeField, Min(1f)] private float goodWindowMs = 140f;
        [SerializeField] private float judgementOffsetMs;
        [SerializeField, Min(0f)] private float extraInputStunBeats = 0.5f;

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
        public float PerfectWindowMs => perfectWindowMs;
        public float GoodWindowMs => Mathf.Max(perfectWindowMs, goodWindowMs);
        public float JudgementOffsetMs => judgementOffsetMs;
        public float ExtraInputStunBeats => Mathf.Max(0f, extraInputStunBeats);
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
                "Goblin Patrol 只使用固定的 X _ 與 X X X _；難度由兩種基底的排列與連續組合提升。";
            musicEventPath = "event:/ZooGoblinFight/BGM/Goblin Patrol";
            musicStartDelaySeconds = 1f;
            musicVolume = 0.55f;
            musicGridOffsetMs = 0f;
            authoredBpm = 120f;
            beatsPerBar = 4;
            totalBars = 33;
            ppq = DefaultPpq;
            perfectWindowMs = 85f;
            goodWindowMs = 170f;
            judgementOffsetMs = 0f;
            extraInputStunBeats = 0.5f;
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
                Single(3, singleCue, singleResponse),
                DoubleSingle(5, doubleSingleCue, doubleSingleResponse),
                Single(7, singleCue, singleResponse),
                Triple(9, tripleCue, tripleResponse),

                Single(10, singleCue, singleResponse),
                Single(10, singleCue, singleResponse, 3f),
                Triple(11, tripleCue, tripleResponse, 2f),
                DoubleSingle(12, doubleSingleCue, doubleSingleResponse, 2f),
                TripleThenSingle(13, tripleThenSingleCue, tripleThenSingleResponse, 3f),
                Single(15, singleCue, singleResponse, 2f),
                Triple(16, tripleCue, tripleResponse, 1f),
                DoubleSingle(17, doubleSingleCue, doubleSingleResponse, 1f),
                TripleThenSingle(18, tripleThenSingleCue, tripleThenSingleResponse, 2f),
                Single(20, singleCue, singleResponse, 1f),
                DoubleSingle(21, doubleSingleCue, doubleSingleResponse),
                Triple(22, tripleCue, tripleResponse, 1f),
                TripleThenSingle(23, tripleThenSingleCue, tripleThenSingleResponse, 1f),
                DoubleSingle(25, doubleSingleCue, doubleSingleResponse),
                Single(26, singleCue, singleResponse, 1f),

                Triple(29, tripleCue, tripleResponse),
                Single(30, singleCue, singleResponse),
                Triple(30, tripleCue, tripleResponse, 3f),
                DoubleSingle(31, doubleSingleCue, doubleSingleResponse, 3f),
                Triple(33, tripleCue, tripleResponse)
            };
        }

        public void ConfigureOtterVsDefaults()
        {
            levelId = "zoo-goblin-otter-vs";
            displayName = "Demo1：Otter vs";
            authoringNotes =
                "Otter vs 使用與 Goblin Patrol 相同的固定短拍與三連拍語彙。"
                + " Bar 16-17 與 22-24 保留段落呼吸，Bar 25 後進入終段加壓。";
            musicEventPath = "event:/ZooGoblinFight/BGM/Otter vs";
            musicStartDelaySeconds = 1f;
            musicVolume = 0.55f;
            musicGridOffsetMs = -35f;
            authoredBpm = 120f;
            beatsPerBar = 4;
            totalBars = 32;
            ppq = DefaultPpq;
            perfectWindowMs = 85f;
            goodWindowMs = 170f;
            judgementOffsetMs = 0f;
            extraInputStunBeats = 0.5f;
            warningSoundEventPath = "event:/ZooGoblinFight/SoundEffects/Warning";
            attackSoundEventPath = "event:/ZooGoblinFight/SoundEffects/AxeGoblin_NormalAttack";
            blockSoundEventPath = "event:/ZooGoblinFight/SoundEffects/BeatTapping";
            perfectSoundEventPath = string.Empty;
            goodSoundEventPath = string.Empty;
            missSoundEventPath = "event:/SoundEffects/BeatMiss";

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
                // Bars 1-5: intro, one short lesson, one triple lesson, then a breath.
                Single(3, singleCue, singleResponse),
                Triple(4, tripleCue, tripleResponse),

                // Bars 6-15: connected phrases follow the first sustained groove.
                Single(6, singleCue, singleResponse),
                Single(6, singleCue, singleResponse, 3f),
                Triple(7, tripleCue, tripleResponse, 2f),
                Single(8, singleCue, singleResponse, 2f),
                Single(9, singleCue, singleResponse, 1f),
                DoubleSingle(10, doubleSingleCue, doubleSingleResponse),
                Triple(11, tripleCue, tripleResponse, 1f),
                Single(12, singleCue, singleResponse, 1f),
                TripleThenSingle(13, tripleThenSingleCue, tripleThenSingleResponse),
                Single(14, singleCue, singleResponse, 3f),
                Triple(15, tripleCue, tripleResponse, 2f),

                // Bars 16-17 breathe; Bars 18-21 answer with a compact second wave.
                DoubleSingle(18, doubleSingleCue, doubleSingleResponse),
                Triple(19, tripleCue, tripleResponse, 1f),
                Single(20, singleCue, singleResponse, 1f),
                Triple(21, tripleCue, tripleResponse),

                // Bars 22-24 are the main breakdown; Bars 25-32 form the final climb and tail.
                Triple(25, tripleCue, tripleResponse),
                DoubleSingle(26, doubleSingleCue, doubleSingleResponse),
                Single(27, singleCue, singleResponse, 1f),
                Triple(28, tripleCue, tripleResponse),
                TripleThenSingle(29, tripleThenSingleCue, tripleThenSingleResponse),
                Single(30, singleCue, singleResponse, 3f),
                Triple(31, tripleCue, tripleResponse, 2f)
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

                if (phrase.StartOffsetTicks >= TicksPerBar)
                {
                    error = $"Phrase #{i + 1} has a start offset outside its bar.";
                    return false;
                }

                long startTick = (long)(phrase.StartBar - 1) * TicksPerBar + phrase.StartOffsetTicks;
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
                int expectedLength = phrase.Kind switch
                {
                    AttackKind.Single => 1,
                    AttackKind.Triple => 2,
                    AttackKind.DoubleSingle => 3,
                    AttackKind.TripleThenSingle => 5,
                    _ => 0
                };
                if (phrase.WarningPattern.HitTicks.Count != expectedCount
                    || phrase.Pattern.HitTicks.Count != expectedCount
                    || phrase.WarningLengthBeats != expectedLength
                    || phrase.ResponseDelayBeats != 2
                    || phrase.AttackLengthBeats != expectedLength
                    || !HasOrderedUniqueTicks(phrase.WarningPattern)
                    || !HasOrderedUniqueTicks(phrase.Pattern))
                {
                    error = $"Phrase #{i + 1} does not follow the supported count/length rules.";
                    return false;
                }

                if (phrase.ProjectilePrefabs.Count != 0 && phrase.ProjectilePrefabs.Count != expectedCount)
                {
                    error = $"Phrase #{i + 1} has {phrase.ProjectilePrefabs.Count} projectile slots; expected {expectedCount}.";
                    return false;
                }
                for (int projectileIndex = 0; projectileIndex < phrase.ProjectilePrefabs.Count; projectileIndex++)
                {
                    GameObject prefab = phrase.ProjectilePrefabs[projectileIndex];
                    if (prefab != null && prefab.GetComponent<RhythmTimelineProjectile>() == null)
                    {
                        error = $"Phrase #{i + 1} projectile #{projectileIndex + 1} has no RhythmTimelineProjectile component.";
                        return false;
                    }
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

        private static bool HasOrderedUniqueTicks(AttackPattern pattern)
        {
            if (pattern == null || pattern.HitTicks.Count == 0)
                return false;
            int previous = -1;
            for (int i = 0; i < pattern.HitTicks.Count; i++)
            {
                int tick = pattern.HitTicks[i];
                if (tick < 0 || tick <= previous)
                    return false;
                previous = tick;
            }
            return true;
        }

        private static AttackPhrase Single(
            int bar,
            AttackPattern cue,
            AttackPattern response,
            float startOffsetBeats = 0f)
        {
            return new AttackPhrase(
                bar,
                "SINGLE • X _",
                AttackKind.Single,
                1,
                1,
                1,
                cue,
                response,
                StartOffsetTicks(startOffsetBeats));
        }

        private static AttackPhrase Triple(
            int bar,
            AttackPattern cue,
            AttackPattern response,
            float startOffsetBeats = 0f)
        {
            return new AttackPhrase(
                bar,
                "SPECIAL • X X X _",
                AttackKind.Triple,
                2,
                1,
                2,
                cue,
                response,
                StartOffsetTicks(startOffsetBeats));
        }

        private static AttackPhrase DoubleSingle(
            int bar,
            AttackPattern cue,
            AttackPattern response,
            float startOffsetBeats = 0f)
        {
            return new AttackPhrase(
                bar,
                "COMBO • X _ ×2",
                AttackKind.DoubleSingle,
                3,
                1,
                3,
                cue,
                response,
                StartOffsetTicks(startOffsetBeats));
        }

        private static AttackPhrase TripleThenSingle(
            int bar,
            AttackPattern cue,
            AttackPattern response,
            float startOffsetBeats = 0f)
        {
            return new AttackPhrase(
                bar,
                "COMBO • TRIPLE → X _",
                AttackKind.TripleThenSingle,
                5,
                1,
                5,
                cue,
                response,
                StartOffsetTicks(startOffsetBeats));
        }

        private static int StartOffsetTicks(float beats)
        {
            return Mathf.Max(0, Mathf.RoundToInt(beats * DefaultPpq));
        }

        private void OnValidate()
        {
            authoredBpm = Mathf.Max(1f, authoredBpm);
            musicVolume = Mathf.Clamp01(musicVolume);
            beatsPerBar = Mathf.Max(1, beatsPerBar);
            totalBars = Mathf.Max(4, totalBars);
            ppq = Mathf.Max(24, ppq);
            perfectWindowMs = Mathf.Max(1f, perfectWindowMs);
            goodWindowMs = Mathf.Max(perfectWindowMs, goodWindowMs);
            extraInputStunBeats = Mathf.Max(0f, extraInputStunBeats);
            phrases?.Sort((left, right) =>
            {
                int barOrder = left.StartBar.CompareTo(right.StartBar);
                return barOrder != 0 ? barOrder : left.StartOffsetTicks.CompareTo(right.StartOffsetTicks);
            });
        }
    }
}
