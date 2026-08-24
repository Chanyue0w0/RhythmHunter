using System;
using System.Collections.Generic;
using RhythmHunter.OtterAquariumPrototype;
using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    internal static class OtterRhythmPresetLibrary
    {
        internal enum ProgressionTemplate
        {
            Beginner,
            PopGroove,
            Syncopation
        }

        internal sealed class Preset
        {
            public Preset(string id, string displayName, string theoryHint, params int[] sixteenthSteps)
            {
                Id = id;
                DisplayName = displayName;
                TheoryHint = theoryHint;
                SixteenthSteps = sixteenthSteps;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string TheoryHint { get; }
            public int[] SixteenthSteps { get; }

            public OtterRhythmLevelData.Pattern CreatePattern(int ppq, int beatsPerBar)
            {
                int maxSteps = Mathf.Max(1, beatsPerBar) * 4;
                List<int> ticks = new();
                foreach (int step in SixteenthSteps)
                {
                    if (step >= 0 && step < maxSteps)
                        ticks.Add(Mathf.RoundToInt(step * ppq / 4f));
                }
                return new OtterRhythmLevelData.Pattern(Id, ticks.ToArray());
            }
        }

        private static readonly Preset[] Presets =
        {
            new("single-one", "只打第 1 拍", "最簡單的定位練習：每小節開頭按一次。", 0),
            new("single-four", "只打第 4 拍", "等待前三拍，在小節最後一拍回應。", 12),
            new("every-beat", "四拍都打", "穩定四分音符：1、2、3、4。", 0, 4, 8, 12),
            new("strong-beats", "強拍 1、3", "常見的穩定骨架，適合入門。", 0, 8),
            new("backbeat", "反拍 2、4", "流行與搖滾常見的鼓組反拍。", 4, 12),
            new("three-straight", "前三拍", "1、2、3，第四拍留白。", 0, 4, 8),
            new("rest-third", "跳過第 3 拍", "1、2、休、4，練習保留空間。", 0, 4, 12),
            new("wide-two", "寬間隔 1、3", "兩個等距敲擊，容易辨識。", 0, 8),
            new("offbeat-finish", "半拍結尾", "1、2-and、4；最後前加入半拍位移。", 0, 6, 12),
            new("tresillo", "3-3-2 律動", "八分音符分成 3+3+2，是常見跨文化節奏型。", 0, 6, 12),
            new("syncopated", "切分 1、2-and、3-and", "重音落在拍與拍之間。", 0, 6, 10),
            new("all-upbeats", "全部後半拍", "每一拍的 and 位置按下：&、&、&、&。", 2, 6, 10, 14),
            new("eighth-run", "連續八分音符", "每半拍一次，共八下。", 0, 2, 4, 6, 8, 10, 12, 14),
            new("sixteenth-pickup", "十六分音符起拍", "在第 4 拍尾端加入快速預備拍。", 0, 4, 8, 12, 14, 15)
        };

        public static IReadOnlyList<Preset> All => Presets;

        public static string[] DisplayNames
        {
            get
            {
                string[] names = new string[Presets.Length];
                for (int i = 0; i < Presets.Length; i++)
                    names[i] = Presets[i].DisplayName;
                return names;
            }
        }

        public static Preset Get(int index)
        {
            return Presets[Mathf.Clamp(index, 0, Presets.Length - 1)];
        }

        public static int FindIndex(string id)
        {
            for (int i = 0; i < Presets.Length; i++)
            {
                if (string.Equals(Presets[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return 0;
        }

        public static List<OtterRhythmLevelData.Phrase> CreateProgression(
            ProgressionTemplate template,
            int phraseCount,
            int ppq,
            int beatsPerBar)
        {
            phraseCount = Mathf.Clamp(phraseCount, 2, 32);
            int[] sequence = template switch
            {
                ProgressionTemplate.PopGroove => new[] { 2, 4, 3, 4, 6, 9, 10, 11 },
                ProgressionTemplate.Syncopation => new[] { 7, 5, 6, 8, 9, 10, 11, 13 },
                _ => new[] { 0, 7, 5, 2, 3, 6, 8, 9 }
            };

            List<OtterRhythmLevelData.Phrase> phrases = new();
            for (int i = 0; i < phraseCount; i++)
            {
                int presetIndex = sequence[Mathf.Min(i, sequence.Length - 1)];
                Preset standardPreset = Get(presetIndex);
                bool adaptive = i >= phraseCount - 2;
                OtterRhythmLevelData.Pattern standard = standardPreset.CreatePattern(ppq, beatsPerBar);
                OtterRhythmLevelData.Pattern assist = Get(Mathf.Max(0, presetIndex - 2)).CreatePattern(ppq, beatsPerBar);
                OtterRhythmLevelData.Pattern challenge = Get(Mathf.Min(Presets.Length - 1, presetIndex + 2)).CreatePattern(ppq, beatsPerBar);
                string label = $"第 {i + 1:00} 組｜{standardPreset.DisplayName}";
                phrases.Add(new OtterRhythmLevelData.Phrase(
                    3 + i * 2,
                    label,
                    adaptive,
                    assist,
                    standard,
                    challenge));
            }
            return phrases;
        }

        public static bool[] PatternToSteps(
            OtterRhythmLevelData.Pattern pattern,
            int ppq,
            int beatsPerBar)
        {
            bool[] steps = new bool[Mathf.Max(1, beatsPerBar) * 4];
            if (pattern == null)
                return steps;
            foreach (int tick in pattern.HitTicks)
            {
                int step = Mathf.RoundToInt(tick / (float)ppq * 4f);
                if (step >= 0 && step < steps.Length)
                    steps[step] = true;
            }
            return steps;
        }

        public static OtterRhythmLevelData.Pattern StepsToPattern(
            string id,
            bool[] steps,
            int ppq)
        {
            List<int> ticks = new();
            if (steps != null)
            {
                for (int i = 0; i < steps.Length; i++)
                {
                    if (steps[i])
                        ticks.Add(Mathf.RoundToInt(i * ppq / 4f));
                }
            }
            return new OtterRhythmLevelData.Pattern(id, ticks.ToArray());
        }

        public static string StepLabel(int step)
        {
            int beat = step / 4 + 1;
            return (step % 4) switch
            {
                0 => beat.ToString(),
                1 => "e",
                2 => "&",
                _ => "a"
            };
        }
    }
}
