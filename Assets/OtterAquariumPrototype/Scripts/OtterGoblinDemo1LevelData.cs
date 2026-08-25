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
            [SerializeField] private string label = "TWO STRONG";
            [SerializeField, Min(1)] private int warningLengthBeats = 4;
            [SerializeField, Min(0)] private int gapBeats;
            [SerializeField, Min(1)] private int attackLengthBeats = 4;
            [SerializeField] private AttackPattern pattern;

            public int StartBar => startBar;
            public string Label => label;
            public int WarningLengthBeats => warningLengthBeats;
            public int GapBeats => gapBeats;
            public int AttackLengthBeats => attackLengthBeats;
            public AttackPattern Pattern => pattern;
            public int TotalLengthBeats => warningLengthBeats + gapBeats + attackLengthBeats;

            public AttackPhrase(
                int configuredStartBar,
                string configuredLabel,
                int configuredWarningBeats,
                int configuredGapBeats,
                int configuredAttackBeats,
                AttackPattern configuredPattern)
            {
                startBar = Mathf.Max(1, configuredStartBar);
                label = string.IsNullOrWhiteSpace(configuredLabel) ? "RHYTHM ATTACK" : configuredLabel.Trim();
                warningLengthBeats = Mathf.Max(1, configuredWarningBeats);
                gapBeats = Mathf.Max(0, configuredGapBeats);
                attackLengthBeats = Mathf.Max(1, configuredAttackBeats);
                pattern = configuredPattern?.Clone();
            }

            public AttackPhrase Clone()
            {
                return new AttackPhrase(
                    startBar,
                    label,
                    warningLengthBeats,
                    gapBeats,
                    attackLengthBeats,
                    pattern);
            }
        }

        [Header("Authoring")]
        [SerializeField] private string levelId = "zoo-goblin-demo-1";
        [SerializeField] private string displayName = "Demo1：動物園哥布林節拍戰";
        [SerializeField, TextArea(3, 7)] private string authoringNotes =
            "153.1 BPM / 4-4。警告節奏後可插入空拍，再由攻擊重複同一節奏。音樂網格相對 FMOD timeline 約 +49 ms。";

        [Header("Music")]
        [SerializeField] private string musicEventPath = "event:/ZooGoblinFight/BGM/Otter's Revenge";
        [SerializeField, Min(0f)] private float musicStartDelaySeconds = 1f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.55f;
        [SerializeField] private float musicGridOffsetMs = 49f;
        [SerializeField, Min(1f)] private float authoredBpm = 153.1f;
        [SerializeField, Min(1)] private int beatsPerBar = 4;
        [SerializeField, Min(4)] private int totalBars = 108;
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
        [SerializeField] private string perfectSoundEventPath = string.Empty;
        [SerializeField] private string goodSoundEventPath = string.Empty;
        [SerializeField] private string missSoundEventPath = string.Empty;

        [Header("Warning → Gap → Attack Phrases")]
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
        public string PerfectSoundEventPath => perfectSoundEventPath;
        public string GoodSoundEventPath => goodSoundEventPath;
        public string MissSoundEventPath => missSoundEventPath;
        public IReadOnlyList<AttackPhrase> Phrases => phrases;

        public void ConfigureDemo1Defaults()
        {
            levelId = "zoo-goblin-demo-1";
            displayName = "Demo1：動物園哥布林節拍戰";
            authoringNotes =
                "依 Otter's Revenge 段落編排。前 4 小節進場；警告節奏後插入 0～2 拍空拍，再由斧擊重複節奏。音效事件已接入，Perfect/Good/Miss 額外回饋音仍保留空位。";
            musicEventPath = "event:/ZooGoblinFight/BGM/Otter's Revenge";
            musicStartDelaySeconds = 1f;
            musicVolume = 0.55f;
            musicGridOffsetMs = 49f;
            authoredBpm = 153.1f;
            beatsPerBar = 4;
            totalBars = 108;
            ppq = DefaultPpq;
            otterMaxHealth = 3;
            damagePerMiss = 1;
            perfectWindowMs = 70f;
            goodWindowMs = 140f;
            judgementOffsetMs = 0f;
            warningSoundEventPath = "event:/ZooGoblinFight/SoundEffects/Warning";
            attackSoundEventPath = "event:/ZooGoblinFight/SoundEffects/AxeGoblin_NormalAttack";
            perfectSoundEventPath = string.Empty;
            goodSoundEventPath = string.Empty;
            missSoundEventPath = string.Empty;

            AttackPattern twoStrong = Pattern("two-strong", 0f, 2f);
            AttackPattern backPair = Pattern("back-pair", 2f, 3f);
            AttackPattern skipThird = Pattern("skip-third", 0f, 1f, 3f);
            AttackPattern lateThree = Pattern("late-three", 1f, 2f, 3f);
            AttackPattern allFour = Pattern("all-four", 0f, 1f, 2f, 3f);
            AttackPattern offbeatPair = Pattern("offbeat-pair", 0.5f, 2.5f);
            AttackPattern syncopated = Pattern("syncopated", 0f, 1.5f, 3f);
            AttackPattern pickup = Pattern("pickup", 0f, 2f, 3.5f);
            AttackPattern longWave = Pattern("long-wave", 0f, 2f, 4f, 5.5f, 7f);
            AttackPattern longBreak = Pattern("long-break", 0f, 3f, 4.5f, 7f);

            phrases = new List<AttackPhrase>
            {
                Phrase(5, "TWO STRONG", 4, 0, 4, twoStrong),
                Phrase(9, "BACK PAIR", 4, 0, 4, backPair),
                Phrase(13, "SKIP THE THIRD", 4, 0, 4, skipThird),
                Phrase(17, "WAIT TWO • SYNCOPATE", 4, 2, 4, syncopated),
                Phrase(21, "OFFBEAT ECHO", 4, 0, 4, offbeatPair),
                Phrase(25, "LATE THREE", 4, 0, 4, lateThree),
                Phrase(29, "LONG WAVE", 8, 0, 8, longWave),

                Phrase(33, "PICKUP AFTER TWO", 4, 2, 4, pickup),
                Phrase(37, "RETURN TO TWO", 4, 0, 4, twoStrong),

                Phrase(41, "SKIP PRESSURE", 4, 0, 4, skipThird),
                Phrase(43, "LATE PRESSURE", 4, 0, 4, lateThree),
                Phrase(45, "OFFBEAT PRESSURE", 4, 0, 4, offbeatPair),
                Phrase(49, "FOUR AXES", 4, 0, 4, allFour),
                Phrase(53, "PRESSURE WAVE", 8, 0, 8, longWave),

                Phrase(57, "BREAKDOWN • HOLD", 8, 0, 8, longBreak),
                Phrase(63, "WAKE UP", 4, 0, 4, twoStrong),

                Phrase(65, "SYNCOPATED RETURN", 4, 0, 4, syncopated),
                Phrase(69, "SKIP RETURN", 4, 0, 4, skipThird),
                Phrase(73, "PICKUP AFTER TWO", 4, 2, 4, pickup),
                Phrase(77, "LATE RETURN", 4, 0, 4, lateThree),
                Phrase(81, "OFFBEAT RETURN", 4, 0, 4, offbeatPair),
                Phrase(85, "REBUILD WAVE", 8, 0, 8, longWave),

                Phrase(89, "FINALE FOUR", 4, 0, 4, allFour),
                Phrase(91, "FINALE SYNC", 4, 0, 4, syncopated),
                Phrase(93, "FINALE SKIP", 4, 0, 4, skipThird),
                Phrase(97, "FINAL WAVE", 8, 0, 8, longWave),
                Phrase(101, "LAST FOUR", 4, 0, 4, allFour),
                Phrase(103, "LAST ECHO", 4, 0, 4, pickup)
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
                if (phrase == null || phrase.Pattern == null || phrase.Pattern.HitTicks.Count == 0)
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

                int lastHit = phrase.Pattern.HitTicks[phrase.Pattern.HitTicks.Count - 1];
                if (lastHit >= phrase.WarningLengthBeats * Ppq || lastHit >= phrase.AttackLengthBeats * Ppq)
                {
                    error = $"Phrase #{i + 1} has a hit outside its warning/attack length.";
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

        private static AttackPhrase Phrase(
            int bar,
            string label,
            int warningBeats,
            int gapBeats,
            int attackBeats,
            AttackPattern pattern)
        {
            return new AttackPhrase(bar, label, warningBeats, gapBeats, attackBeats, pattern);
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
