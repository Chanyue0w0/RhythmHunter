using System;
using System.Collections.Generic;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    public sealed class OtterRhythmLevelRunner : MonoBehaviour
    {
        public enum Grade
        {
            NotReady,
            Perfect,
            Good,
            Miss
        }

        public enum AdaptiveTier
        {
            Assist,
            Standard,
            Challenge
        }

        public readonly struct JudgementResult
        {
            public JudgementResult(Grade grade, double deltaMs, long targetTick, bool extraInput)
            {
                Judgement = grade;
                DeltaMs = deltaMs;
                TargetTick = targetTick;
                ExtraInput = extraInput;
            }

            public Grade Judgement { get; }
            public double DeltaMs { get; }
            public long TargetTick { get; }
            public bool ExtraInput { get; }
        }

        public readonly struct LevelSummary
        {
            public LevelSummary(int perfect, int good, int miss, int extra, double meanAbsoluteDeltaMs)
            {
                Perfect = perfect;
                Good = good;
                Miss = miss;
                Extra = extra;
                MeanAbsoluteDeltaMs = meanAbsoluteDeltaMs;
            }

            public int Perfect { get; }
            public int Good { get; }
            public int Miss { get; }
            public int Extra { get; }
            public double MeanAbsoluteDeltaMs { get; }
            public int TotalTargets => Perfect + Good + Miss;
            public float Accuracy => TotalTargets == 0
                ? 0f
                : (Perfect + Good * 0.6f) / TotalTargets;
        }

        private sealed class TargetState
        {
            public long Tick;
            public bool Judged;
        }

        [Header("Dependencies")]
        [SerializeField] private FmodBeatClock beatClock;
        [SerializeField] private OtterRhythmLevelData levelData;

        private readonly List<TargetState> activeTargets = new();
        private bool running;
        private bool completed;
        private int anchorTimelineMs;
        private int phraseIndex;
        private int nextCueIndex;
        private long activePhraseStartTick;
        private long activePhraseEndTick;
        private OtterRhythmLevelData.Pattern activePattern;
        private int phraseTargetCount;
        private int phraseHitCount;
        private int phraseMissCount;
        private int recentTargetCount;
        private int recentHitCount;
        private int recentMissCount;
        private int perfectCount;
        private int goodCount;
        private int missCount;
        private int extraCount;
        private double absoluteDeltaTotal;
        private int timedHitCount;

        public event Action<FmodBeatClock.BeatSnapshot> BeatObserved;
        public event Action<int, int> CountInChanged;
        public event Action<int, string, AdaptiveTier> PhraseStarted;
        public event Action<int, int, string> CueTriggered;
        public event Action<JudgementResult> Judged;
        public event Action<int, int> PhraseCompleted;
        public event Action<LevelSummary> LevelCompleted;
        public event Action<string> LevelError;

        public FmodBeatClock BeatClock => beatClock;
        public OtterRhythmLevelData LevelData => levelData;
        public bool IsRunning => running;
        public bool IsCompleted => completed;
        public int CurrentPhraseNumber => Mathf.Min(phraseIndex + 1, levelData != null ? levelData.Phrases.Count : 0);
        public AdaptiveTier CurrentTier { get; private set; } = AdaptiveTier.Standard;
        public string CurrentPatternId => activePattern != null ? activePattern.Id : string.Empty;
        public long CurrentSongTick { get; private set; }

        public void Configure(FmodBeatClock clock, OtterRhythmLevelData data)
        {
            beatClock = clock;
            levelData = data;
        }

        private void OnEnable()
        {
            if (beatClock != null)
            {
                beatClock.Beat += OnBeat;
                beatClock.PlaybackError += OnPlaybackError;
            }
        }

        private void OnDisable()
        {
            if (beatClock != null)
            {
                beatClock.Beat -= OnBeat;
                beatClock.PlaybackError -= OnPlaybackError;
            }
        }

        private void Update()
        {
            if (!running || completed || levelData == null || beatClock == null)
                return;

            if (!beatClock.TryGetTimelinePositionMs(out int timelineMs))
                return;

            CurrentSongTick = TimelineMsToTick(timelineMs);
            ProcessPhrase(CurrentSongTick, timelineMs + levelData.JudgementOffsetMs);

            long levelEndTick = (long)levelData.TotalBars * levelData.TicksPerBar;
            if (CurrentSongTick >= levelEndTick)
                CompleteLevel();
        }

        public JudgementResult SubmitInput()
        {
            if (!running || completed || levelData == null || beatClock == null
                || !beatClock.TryGetTimelinePositionMs(out int rawTimelineMs))
            {
                return Publish(new JudgementResult(Grade.NotReady, 0.0, -1, false));
            }

            double evaluatedTimelineMs = rawTimelineMs + levelData.JudgementOffsetMs;
            TargetState nearest = null;
            double nearestDeltaMs = double.MaxValue;
            foreach (TargetState target in activeTargets)
            {
                if (target.Judged)
                    continue;

                double deltaMs = evaluatedTimelineMs - TickToTimelineMs(target.Tick);
                if (Math.Abs(deltaMs) < Math.Abs(nearestDeltaMs))
                {
                    nearest = target;
                    nearestDeltaMs = deltaMs;
                }
            }

            if (nearest != null && Math.Abs(nearestDeltaMs) <= levelData.GoodWindowMs)
            {
                nearest.Judged = true;
                phraseHitCount++;
                timedHitCount++;
                absoluteDeltaTotal += Math.Abs(nearestDeltaMs);
                Grade grade = Math.Abs(nearestDeltaMs) <= levelData.PerfectWindowMs
                    ? Grade.Perfect
                    : Grade.Good;
                if (grade == Grade.Perfect)
                    perfectCount++;
                else
                    goodCount++;
                return Publish(new JudgementResult(grade, nearestDeltaMs, nearest.Tick, false));
            }

            extraCount++;
            phraseMissCount++;
            double reportedDelta = nearest != null ? nearestDeltaMs : 0.0;
            return Publish(new JudgementResult(Grade.Miss, reportedDelta, -1, true));
        }

        public bool TryGetNextTargetTimelineMs(out double timelineMs)
        {
            timelineMs = 0.0;
            foreach (TargetState target in activeTargets)
            {
                if (target.Judged)
                    continue;
                timelineMs = TickToTimelineMs(target.Tick);
                return true;
            }
            return false;
        }

        public LevelSummary GetSummary()
        {
            double mean = timedHitCount > 0 ? absoluteDeltaTotal / timedHitCount : 0.0;
            return new LevelSummary(perfectCount, goodCount, missCount, extraCount, mean);
        }

        private void OnBeat(FmodBeatClock.BeatSnapshot beat)
        {
            if (!running)
            {
                anchorTimelineMs = beat.TimelinePositionMs;
                running = true;
                CurrentSongTick = 0;
            }

            BeatObserved?.Invoke(beat);
            if (phraseIndex == 0 && CurrentSongTick < levelData.TicksPerBar * 2L)
            {
                int count = (int)(CurrentSongTick / levelData.Ppq) % levelData.BeatsPerBar + 1;
                CountInChanged?.Invoke(count, levelData.BeatsPerBar);
            }
        }

        private void ProcessPhrase(long songTick, double evaluatedTimelineMs)
        {
            if (phraseIndex >= levelData.Phrases.Count)
                return;

            OtterRhythmLevelData.Phrase phrase = levelData.Phrases[phraseIndex];
            long phraseStartTick = (long)(phrase.StartBar - 1) * levelData.TicksPerBar;
            if (activePattern == null)
            {
                if (songTick < phraseStartTick)
                    return;
                StartPhrase(phrase, phraseStartTick);
            }

            while (nextCueIndex < activePattern.HitTicks.Count)
            {
                long cueTick = activePhraseStartTick + activePattern.HitTicks[nextCueIndex];
                if (songTick < cueTick)
                    break;
                CueTriggered?.Invoke(nextCueIndex + 1, activePattern.HitTicks.Count, activePattern.Id);
                nextCueIndex++;
            }

            foreach (TargetState target in activeTargets)
            {
                if (target.Judged)
                    continue;
                if (evaluatedTimelineMs <= TickToTimelineMs(target.Tick) + levelData.GoodWindowMs)
                    continue;

                target.Judged = true;
                missCount++;
                phraseMissCount++;
                Publish(new JudgementResult(Grade.Miss, levelData.GoodWindowMs, target.Tick, false));
            }

            if (songTick < activePhraseEndTick || !AllTargetsJudged())
                return;

            CompletePhrase();
            phraseIndex++;
            activePattern = null;
            activeTargets.Clear();

            if (phraseIndex < levelData.Phrases.Count)
                ProcessPhrase(songTick, evaluatedTimelineMs);
        }

        private void StartPhrase(OtterRhythmLevelData.Phrase phrase, long phraseStartTick)
        {
            CurrentTier = phrase.Adaptive ? ChooseAdaptiveTier() : AdaptiveTier.Standard;
            activePattern = CurrentTier switch
            {
                AdaptiveTier.Assist => phrase.AssistPattern,
                AdaptiveTier.Challenge => phrase.ChallengePattern,
                _ => phrase.StandardPattern
            };

            activePhraseStartTick = phraseStartTick;
            activePhraseEndTick = phraseStartTick + levelData.TicksPerBar * 2L;
            nextCueIndex = 0;
            phraseHitCount = 0;
            phraseMissCount = 0;
            activeTargets.Clear();

            foreach (int relativeTick in activePattern.HitTicks)
            {
                activeTargets.Add(new TargetState
                {
                    Tick = phraseStartTick + levelData.TicksPerBar + relativeTick
                });
            }
            phraseTargetCount = activeTargets.Count;
            PhraseStarted?.Invoke(phraseIndex + 1, phrase.Label, CurrentTier);
        }

        private void CompletePhrase()
        {
            recentTargetCount += phraseTargetCount;
            recentHitCount += phraseHitCount;
            recentMissCount += phraseMissCount;
            if (recentTargetCount > 8)
            {
                recentTargetCount = phraseTargetCount;
                recentHitCount = phraseHitCount;
                recentMissCount = phraseMissCount;
            }
            PhraseCompleted?.Invoke(phraseHitCount, phraseTargetCount);
        }

        private AdaptiveTier ChooseAdaptiveTier()
        {
            if (recentTargetCount <= 0)
                return AdaptiveTier.Standard;

            float accuracy = (float)recentHitCount / recentTargetCount;
            if (accuracy >= 0.85f && recentMissCount == 0)
                return AdaptiveTier.Challenge;
            if (accuracy <= 0.55f || recentMissCount >= 3)
                return AdaptiveTier.Assist;
            return AdaptiveTier.Standard;
        }

        private bool AllTargetsJudged()
        {
            foreach (TargetState target in activeTargets)
            {
                if (!target.Judged)
                    return false;
            }
            return true;
        }

        private long TimelineMsToTick(double timelineMs)
        {
            double elapsedMs = timelineMs - anchorTimelineMs - levelData.ChartOffsetMs;
            return (long)Math.Round(elapsedMs / MillisecondsPerBeat * levelData.Ppq);
        }

        private double TickToTimelineMs(long tick)
        {
            return anchorTimelineMs + levelData.ChartOffsetMs + tick / (double)levelData.Ppq * MillisecondsPerBeat;
        }

        private double MillisecondsPerBeat => 60000.0 / Mathf.Max(1f, levelData.AuthoredBpm);

        private JudgementResult Publish(JudgementResult result)
        {
            Judged?.Invoke(result);
            return result;
        }

        private void CompleteLevel()
        {
            if (completed)
                return;
            completed = true;
            LevelCompleted?.Invoke(GetSummary());
        }

        private void OnPlaybackError(string message)
        {
            LevelError?.Invoke(message);
        }
    }
}
