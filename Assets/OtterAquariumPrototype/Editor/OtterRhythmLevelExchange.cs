using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RhythmHunter.OtterAquariumPrototype;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    internal static class OtterRhythmLevelExchange
    {
        [Serializable]
        internal sealed class LevelDocument
        {
            public int schemaVersion = 1;
            public string levelId;
            public string displayName;
            public string authoringNotes;
            public string musicEventPath;
            public float musicStartDelaySeconds;
            public float chartOffsetMs;
            public float bpm;
            public int beatsPerBar;
            public int totalBars;
            public int ppq;
            public float perfectWindowMs;
            public float goodWindowMs;
            public float judgementOffsetMs;
            public string cueSoundEventPath;
            public string hitSoundEventPath;
            public string missSoundEventPath;
            public string successSoundEventPath;
            public List<PhraseDocument> phrases = new();
        }

        [Serializable]
        internal sealed class PhraseDocument
        {
            public int startBar;
            public string label;
            public bool adaptive;
            public PatternDocument assist;
            public PatternDocument standard;
            public PatternDocument challenge;
        }

        [Serializable]
        internal sealed class PatternDocument
        {
            public string id;
            public int[] ticks;
        }

        public static void ExportJson(OtterRhythmLevelData level, string path)
        {
            LevelDocument document = FromLevel(level);
            File.WriteAllText(path, JsonUtility.ToJson(document, true), new UTF8Encoding(false));
        }

        public static bool TryImportJson(string path, OtterRhythmLevelData destination, out string error)
        {
            error = string.Empty;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                LevelDocument document = JsonUtility.FromJson<LevelDocument>(json);
                if (document == null || document.schemaVersion != 1)
                {
                    error = "不支援的 JSON schemaVersion。";
                    return false;
                }

                Undo.RecordObject(destination, "Import Rhythm Level JSON");
                ApplyDocument(document, destination);
                EditorUtility.SetDirty(destination);
                AssetDatabase.SaveAssets();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static void ExportProducerCsv(OtterRhythmLevelData level, string path)
        {
            StringBuilder csv = new();
            csv.AppendLine("level_id,phrase_index,phrase_start_bar,event_bar,variant,event_role,pattern_id,tick_in_bar,beat_in_bar,absolute_tick,absolute_beat,timeline_seconds");
            for (int phraseIndex = 0; phraseIndex < level.Phrases.Count; phraseIndex++)
            {
                OtterRhythmLevelData.Phrase phrase = level.Phrases[phraseIndex];
                WriteVariant(csv, level, phraseIndex, phrase, "standard", phrase.StandardPattern);
                if (phrase.Adaptive)
                {
                    WriteVariant(csv, level, phraseIndex, phrase, "assist", phrase.AssistPattern);
                    WriteVariant(csv, level, phraseIndex, phrase, "challenge", phrase.ChallengePattern);
                }
            }
            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
        }

        public static void Validate(
            OtterRhythmLevelData level,
            List<string> errors,
            List<string> warnings)
        {
            errors.Clear();
            warnings.Clear();
            if (level == null)
            {
                errors.Add("尚未選擇關卡資產。 ");
                return;
            }
            if (string.IsNullOrWhiteSpace(level.LevelId))
                errors.Add("Level ID 不可留白。 ");
            if (string.IsNullOrWhiteSpace(level.MusicEventPath))
                errors.Add("FMOD Music Event 不可留白。 ");
            if (level.AuthoredBpm <= 0f)
                errors.Add("BPM 必須大於 0。 ");
            if (level.Phrases.Count == 0)
                errors.Add("至少需要一組節奏 Phrase。 ");

            HashSet<int> usedBars = new();
            for (int i = 0; i < level.Phrases.Count; i++)
            {
                OtterRhythmLevelData.Phrase phrase = level.Phrases[i];
                string prefix = $"Phrase {i + 1}（第 {phrase.StartBar} 小節）";
                if (phrase.StartBar < 1)
                    errors.Add($"{prefix}：起始小節必須大於 0。 ");
                if (!usedBars.Add(phrase.StartBar))
                    errors.Add($"{prefix}：與另一組 Phrase 使用相同起始小節。 ");
                if (phrase.StartBar + 1 > level.TotalBars)
                    errors.Add($"{prefix}：回應小節超出關卡總長。 ");
                ValidatePattern(level, phrase.StandardPattern, $"{prefix} Standard", errors, warnings);
                if (phrase.Adaptive)
                {
                    ValidatePattern(level, phrase.AssistPattern, $"{prefix} Assist", errors, warnings);
                    ValidatePattern(level, phrase.ChallengePattern, $"{prefix} Challenge", errors, warnings);
                }
            }

            if (level.ChartOffsetMs != 0f)
                warnings.Add($"目前套用 Chart Offset {level.ChartOffsetMs:+0.##;-0.##} ms，請與音樂製作確認第一拍對位。 ");
            if (string.IsNullOrWhiteSpace(level.CueSoundEventPath))
                warnings.Add("Cue SFX 尚未指定；遊戲仍可用視覺提示測試。 ");
        }

        private static LevelDocument FromLevel(OtterRhythmLevelData level)
        {
            LevelDocument document = new()
            {
                levelId = level.LevelId,
                displayName = level.DisplayName,
                authoringNotes = level.AuthoringNotes,
                musicEventPath = level.MusicEventPath,
                musicStartDelaySeconds = level.MusicStartDelaySeconds,
                chartOffsetMs = level.ChartOffsetMs,
                bpm = level.AuthoredBpm,
                beatsPerBar = level.BeatsPerBar,
                totalBars = level.TotalBars,
                ppq = level.Ppq,
                perfectWindowMs = level.PerfectWindowMs,
                goodWindowMs = level.GoodWindowMs,
                judgementOffsetMs = level.JudgementOffsetMs,
                cueSoundEventPath = level.CueSoundEventPath,
                hitSoundEventPath = level.HitSoundEventPath,
                missSoundEventPath = level.MissSoundEventPath,
                successSoundEventPath = level.SuccessSoundEventPath
            };
            foreach (OtterRhythmLevelData.Phrase phrase in level.Phrases)
            {
                document.phrases.Add(new PhraseDocument
                {
                    startBar = phrase.StartBar,
                    label = phrase.Label,
                    adaptive = phrase.Adaptive,
                    assist = FromPattern(phrase.AssistPattern),
                    standard = FromPattern(phrase.StandardPattern),
                    challenge = FromPattern(phrase.ChallengePattern)
                });
            }
            return document;
        }

        private static PatternDocument FromPattern(OtterRhythmLevelData.Pattern pattern)
        {
            if (pattern == null)
                return new PatternDocument { id = "empty", ticks = Array.Empty<int>() };
            int[] ticks = new int[pattern.HitTicks.Count];
            for (int i = 0; i < ticks.Length; i++)
                ticks[i] = pattern.HitTicks[i];
            return new PatternDocument { id = pattern.Id, ticks = ticks };
        }

        private static void ApplyDocument(LevelDocument document, OtterRhythmLevelData destination)
        {
            destination.ConfigureAuthoring(document.levelId, document.displayName, document.authoringNotes);
            destination.ConfigureMusic(
                document.musicEventPath,
                document.musicStartDelaySeconds,
                document.chartOffsetMs,
                document.bpm,
                document.beatsPerBar,
                document.totalBars,
                document.ppq);
            destination.ConfigureJudgement(document.perfectWindowMs, document.goodWindowMs, document.judgementOffsetMs);
            destination.ConfigureOptionalSfx(
                document.cueSoundEventPath,
                document.hitSoundEventPath,
                document.missSoundEventPath,
                document.successSoundEventPath);

            List<OtterRhythmLevelData.Phrase> phrases = new();
            if (document.phrases != null)
            {
                foreach (PhraseDocument phrase in document.phrases)
                {
                    phrases.Add(new OtterRhythmLevelData.Phrase(
                        phrase.startBar,
                        phrase.label,
                        phrase.adaptive,
                        ToPattern(phrase.assist),
                        ToPattern(phrase.standard),
                        ToPattern(phrase.challenge)));
                }
            }
            destination.ReplacePhrases(phrases);
        }

        private static OtterRhythmLevelData.Pattern ToPattern(PatternDocument document)
        {
            return document == null
                ? new OtterRhythmLevelData.Pattern("empty")
                : new OtterRhythmLevelData.Pattern(document.id, document.ticks ?? Array.Empty<int>());
        }

        private static void WriteVariant(
            StringBuilder csv,
            OtterRhythmLevelData level,
            int phraseIndex,
            OtterRhythmLevelData.Phrase phrase,
            string variant,
            OtterRhythmLevelData.Pattern pattern)
        {
            if (pattern == null)
                return;
            foreach (int tick in pattern.HitTicks)
            {
                WriteEvent(csv, level, phraseIndex, phrase.StartBar, variant, "cue", pattern.Id, tick, 0);
                WriteEvent(csv, level, phraseIndex, phrase.StartBar, variant, "response", pattern.Id, tick, level.TicksPerBar);
            }
        }

        private static void WriteEvent(
            StringBuilder csv,
            OtterRhythmLevelData level,
            int phraseIndex,
            int startBar,
            string variant,
            string role,
            string patternId,
            int tickInBar,
            int roleTickOffset)
        {
            long phraseStartTick = (long)(startBar - 1) * level.TicksPerBar;
            long absoluteTick = phraseStartTick + roleTickOffset + tickInBar;
            double beatInBar = tickInBar / (double)level.Ppq + 1.0;
            double absoluteBeat = absoluteTick / (double)level.Ppq;
            double seconds = level.ChartOffsetMs / 1000.0 + absoluteBeat * 60.0 / level.AuthoredBpm;
            int eventBar = startBar + (roleTickOffset / level.TicksPerBar);
            csv.Append(Escape(level.LevelId)).Append(',')
                .Append(phraseIndex + 1).Append(',')
                .Append(startBar).Append(',')
                .Append(eventBar).Append(',')
                .Append(variant).Append(',')
                .Append(role).Append(',')
                .Append(Escape(patternId)).Append(',')
                .Append(tickInBar).Append(',')
                .Append(beatInBar.ToString("0.####", CultureInfo.InvariantCulture)).Append(',')
                .Append(absoluteTick).Append(',')
                .Append(absoluteBeat.ToString("0.####", CultureInfo.InvariantCulture)).Append(',')
                .Append(seconds.ToString("0.000000", CultureInfo.InvariantCulture))
                .AppendLine();
        }

        private static void ValidatePattern(
            OtterRhythmLevelData level,
            OtterRhythmLevelData.Pattern pattern,
            string label,
            List<string> errors,
            List<string> warnings)
        {
            if (pattern == null || pattern.HitTicks.Count == 0)
            {
                errors.Add($"{label}：節奏不可為空。 ");
                return;
            }
            foreach (int tick in pattern.HitTicks)
            {
                if (tick < 0 || tick >= level.TicksPerBar)
                    errors.Add($"{label}：tick {tick} 超出單一小節範圍 0–{level.TicksPerBar - 1}。 ");
                if (tick % Mathf.Max(1, level.Ppq / 4) != 0)
                    warnings.Add($"{label}：tick {tick} 不在十六分音符格線上，請確認是刻意的微調。 ");
            }
        }

        private static string Escape(string value)
        {
            value ??= string.Empty;
            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
        }
    }
}
