using System;
using System.Collections.Generic;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    [DefaultExecutionOrder(-100)]
    public sealed class OtterGoblinDemo1Runner : MonoBehaviour
    {
        public enum Grade
        {
            NotReady,
            Perfect,
            Good,
            Miss
        }

        public enum CombatPhase
        {
            Intro,
            Warning,
            Gap,
            Defend,
            Rest,
            Victory,
            Error
        }

        public readonly struct JudgementResult
        {
            public JudgementResult(Grade grade, double deltaMs, int failureCount, bool extraInput)
            {
                Judgement = grade;
                DeltaMs = deltaMs;
                FailureCount = failureCount;
                ExtraInput = extraInput;
            }

            public Grade Judgement { get; }
            public double DeltaMs { get; }
            public int FailureCount { get; }
            public bool ExtraInput { get; }
        }

        public readonly struct CombatSummary
        {
            public CombatSummary(int perfect, int good, int miss, int extra)
            {
                Perfect = perfect;
                Good = good;
                Miss = miss;
                Extra = extra;
            }

            public int Perfect { get; }
            public int Good { get; }
            public int Miss { get; }
            public int Extra { get; }
            public int TotalTargets => Perfect + Good + Miss;
            public float Accuracy => TotalTargets == 0
                ? 0f
                : (Perfect + Good * 0.65f) / TotalTargets;
        }

        private sealed class TargetState
        {
            public long Tick;
            public bool Judged;
        }

        [SerializeField] private FmodBeatClock beatClock;
        [SerializeField] private OtterGoblinDemo1LevelData levelData;

        private bool playbackGated;

        private readonly List<TargetState> activeTargets = new();
        private int phraseIndex;
        private int nextWarningIndex;
        private int nextWaitIndex;
        private int nextAttackIndex;
        private long activePhraseStartTick;
        private long warningEndTick;
        private long attackStartTick;
        private long phraseEndTick;
        private bool activePhrase;
        private bool running;
        private bool ended;
        private int perfectCount;
        private int goodCount;
        private int missCount;
        private int extraCount;
        private double inputLockedUntilTimelineMs = double.NegativeInfinity;
        private CombatPhase phase = CombatPhase.Intro;

        public event Action<FmodBeatClock.BeatSnapshot> BeatObserved;
        public event Action<CombatPhase> PhaseChanged;
        public event Action<int, OtterGoblinDemo1LevelData.AttackPhrase> PhraseStarted;
        public event Action<int, int, string> WarningCue;
        public event Action<int, int, int> WaitCue;
        public event Action<int, int, string> AttackCue;
        public event Action<JudgementResult> Judged;
        public event Action<int> FailureCountChanged;
        public event Action<CombatSummary> BattleWon;
        public event Action<string> BattleError;

        public FmodBeatClock BeatClock => beatClock;
        public OtterGoblinDemo1LevelData LevelData => levelData;
        public CombatPhase Phase => phase;
        public int FailureCount => missCount;
        public bool HasEnded => ended;
        public long CurrentSongTick { get; private set; }
        public int CurrentBar => levelData == null ? 1 : Mathf.Max(1, (int)(CurrentSongTick / levelData.TicksPerBar) + 1);
        public int CurrentPhraseNumber => activePhrase ? phraseIndex + 1 : Mathf.Clamp(phraseIndex, 0, levelData != null ? levelData.Phrases.Count : 0);
        public double CurrentMillisecondsPerBeat => MillisecondsPerBeat;

        public void Configure(FmodBeatClock clock, OtterGoblinDemo1LevelData data)
        {
            beatClock = clock;
            levelData = data;
            SyncBeatClockConfiguration();
            ResetState();
        }

        public void SetLevelData(OtterGoblinDemo1LevelData data)
        {
            levelData = data;
            SyncBeatClockConfiguration();
            ResetState();
        }

        public void SetPlaybackGated(bool gated)
        {
            playbackGated = gated;
            SyncBeatClockConfiguration();
        }

        public void SyncBeatClockConfiguration()
        {
            if (beatClock == null || levelData == null)
                return;

            beatClock.Configure(
                levelData.MusicEventPath,
                levelData.MusicStartDelaySeconds,
                !playbackGated,
                levelData.MusicVolume);
        }

        private void Awake()
        {
            SyncBeatClockConfiguration();
            ResetState();
        }

        private void OnEnable()
        {
            if (beatClock == null)
                return;
            beatClock.Beat += OnBeat;
            beatClock.PlaybackError += OnPlaybackError;
        }

        private void OnDisable()
        {
            if (beatClock == null)
                return;
            beatClock.Beat -= OnBeat;
            beatClock.PlaybackError -= OnPlaybackError;
        }

        private void Update()
        {
            if (ended || levelData == null || beatClock == null || !beatClock.HasTimingAnchor)
                return;
            if (!beatClock.TryGetTimelinePositionMs(out int timelineMs))
                return;

            running = true;
            CurrentSongTick = TimelineMsToTick(timelineMs);
            ProcessTimeline(CurrentSongTick, timelineMs + levelData.JudgementOffsetMs);

            if (!ended
                && !activePhrase
                && phraseIndex >= levelData.Phrases.Count
                && CurrentSongTick >= (long)levelData.TotalBars * levelData.TicksPerBar)
                WinBattle();
        }

        public JudgementResult SubmitInput()
        {
            if (!running || ended || levelData == null || beatClock == null
                || !beatClock.TryGetTimelinePositionMs(out int timelineMs))
            {
                return Publish(new JudgementResult(Grade.NotReady, 0.0, FailureCount, false));
            }

            if (timelineMs < inputLockedUntilTimelineMs)
                return Publish(new JudgementResult(Grade.NotReady, 0.0, FailureCount, false));

            double evaluatedMs = timelineMs + levelData.JudgementOffsetMs;
            TargetState nearest = null;
            double nearestDelta = double.MaxValue;
            foreach (TargetState target in activeTargets)
            {
                if (target.Judged)
                    continue;
                double delta = evaluatedMs - TickToTimelineMs(target.Tick);
                if (Math.Abs(delta) < Math.Abs(nearestDelta))
                {
                    nearest = target;
                    nearestDelta = delta;
                }
            }

            if (nearest != null && Math.Abs(nearestDelta) <= levelData.GoodWindowMs)
            {
                nearest.Judged = true;
                Grade grade = Math.Abs(nearestDelta) <= levelData.PerfectWindowMs
                    ? Grade.Perfect
                    : Grade.Good;
                if (grade == Grade.Perfect)
                    perfectCount++;
                else
                    goodCount++;
                return Publish(new JudgementResult(grade, nearestDelta, FailureCount, false));
            }

            extraCount++;
            inputLockedUntilTimelineMs = timelineMs
                + levelData.ExtraInputStunBeats * MillisecondsPerBeat;
            return Publish(new JudgementResult(Grade.Miss, nearestDelta, FailureCount, true));
        }

        public CombatSummary GetSummary()
        {
            return new CombatSummary(perfectCount, goodCount, missCount, extraCount);
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

        public void CopyPendingTargetTimelineMs(List<double> results)
        {
            if (results == null)
                return;
            results.Clear();
            foreach (TargetState target in activeTargets)
            {
                if (!target.Judged)
                    results.Add(TickToTimelineMs(target.Tick));
            }
        }

        public string GetCurrentPatternDisplay()
        {
            if (!activePhrase || levelData == null)
                return "— — — —";

            OtterGoblinDemo1LevelData.AttackPhrase phrase = levelData.Phrases[phraseIndex];
            return phrase.Kind switch
            {
                OtterGoblinDemo1LevelData.AttackKind.Single => "CUE  X _   •   CATCH  X'",
                OtterGoblinDemo1LevelData.AttackKind.Triple => "CUE  X X X _   •   CATCH  X' _ X' _ X'",
                OtterGoblinDemo1LevelData.AttackKind.DoubleSingle => "COMBO  X _ X _   •   CATCH  X' / X'",
                _ => "COMBO  X X X _  →  X _   •   CATCH  X' _ X' _ X'  →  X'"
            };
        }

        private void ProcessTimeline(long songTick, double evaluatedTimelineMs)
        {
            if (phraseIndex >= levelData.Phrases.Count)
            {
                ChangePhase(CombatPhase.Rest);
                return;
            }

            OtterGoblinDemo1LevelData.AttackPhrase phrase = levelData.Phrases[phraseIndex];
            long scheduledStart = (long)(phrase.StartBar - 1) * levelData.TicksPerBar
                + phrase.StartOffsetTicks;
            if (!activePhrase)
            {
                if (songTick < scheduledStart)
                {
                    ChangePhase(phraseIndex == 0 ? CombatPhase.Intro : CombatPhase.Rest);
                    return;
                }
                StartPhrase(phrase, scheduledStart);
            }

            while (nextWarningIndex < phrase.WarningPattern.HitTicks.Count)
            {
                long cueTick = activePhraseStartTick + phrase.WarningPattern.HitTicks[nextWarningIndex];
                if (songTick < cueTick)
                    break;
                WarningCue?.Invoke(
                    nextWarningIndex + 1,
                    phrase.WarningPattern.HitTicks.Count,
                    phrase.WarningPattern.Id);
                nextWarningIndex++;
            }

            while (nextWaitIndex < phrase.WaitBeatCount)
            {
                long waitTick = GetWaitCueTick(phrase, nextWaitIndex);
                if (songTick < waitTick)
                    break;
                WaitCue?.Invoke(
                    nextWaitIndex + 1,
                    phrase.WaitBeatCount,
                    GetWaitProjectileCount(phrase, nextWaitIndex));
                nextWaitIndex++;
            }

            if (songTick >= attackStartTick)
                ChangePhase(CombatPhase.Defend);
            else if (songTick >= warningEndTick)
                ChangePhase(CombatPhase.Gap);
            else
                ChangePhase(CombatPhase.Warning);

            while (nextAttackIndex < phrase.Pattern.HitTicks.Count)
            {
                long cueTick = attackStartTick + phrase.Pattern.HitTicks[nextAttackIndex];
                if (songTick < cueTick)
                    break;
                AttackCue?.Invoke(nextAttackIndex + 1, phrase.Pattern.HitTicks.Count, phrase.Pattern.Id);
                nextAttackIndex++;
            }

            foreach (TargetState target in activeTargets)
            {
                if (target.Judged)
                    continue;
                if (evaluatedTimelineMs <= TickToTimelineMs(target.Tick) + levelData.GoodWindowMs)
                    continue;

                target.Judged = true;
                missCount++;
                FailureCountChanged?.Invoke(FailureCount);
                Publish(new JudgementResult(
                    Grade.Miss,
                    evaluatedTimelineMs - TickToTimelineMs(target.Tick),
                    FailureCount,
                    false));
            }

            if (songTick < phraseEndTick || !AllTargetsJudged())
                return;

            activePhrase = false;
            activeTargets.Clear();
            phraseIndex++;
            ChangePhase(CombatPhase.Rest);
            ProcessTimeline(songTick, evaluatedTimelineMs);
        }

        private void StartPhrase(OtterGoblinDemo1LevelData.AttackPhrase phrase, long startTick)
        {
            activePhrase = true;
            activePhraseStartTick = startTick;
            warningEndTick = phrase.Kind is OtterGoblinDemo1LevelData.AttackKind.Triple
                or OtterGoblinDemo1LevelData.AttackKind.TripleThenSingle
                ? startTick + (long)levelData.Ppq * 3 / 2
                : startTick + levelData.Ppq;
            attackStartTick = startTick + (long)phrase.ResponseDelayBeats * levelData.Ppq;
            phraseEndTick = attackStartTick + (long)phrase.AttackLengthBeats * levelData.Ppq;
            nextWarningIndex = 0;
            nextWaitIndex = 0;
            nextAttackIndex = 0;
            activeTargets.Clear();
            foreach (int relativeTick in phrase.Pattern.HitTicks)
            {
                activeTargets.Add(new TargetState
                {
                    Tick = attackStartTick + relativeTick
                });
            }
            ChangePhase(CombatPhase.Warning);
            PhraseStarted?.Invoke(phraseIndex + 1, phrase);
        }

        private long GetWaitCueTick(OtterGoblinDemo1LevelData.AttackPhrase phrase, int waitIndex)
        {
            long relativeTick = phrase.Kind switch
            {
                OtterGoblinDemo1LevelData.AttackKind.Single => levelData.Ppq,
                OtterGoblinDemo1LevelData.AttackKind.Triple => (long)levelData.Ppq * 3 / 2,
                OtterGoblinDemo1LevelData.AttackKind.DoubleSingle => (long)(waitIndex * 2 + 1) * levelData.Ppq,
                OtterGoblinDemo1LevelData.AttackKind.TripleThenSingle when waitIndex == 0 => (long)levelData.Ppq * 3 / 2,
                _ => (long)levelData.Ppq * 5
            };
            return activePhraseStartTick + relativeTick;
        }

        private static int GetWaitProjectileCount(
            OtterGoblinDemo1LevelData.AttackPhrase phrase,
            int waitIndex)
        {
            return phrase.Kind switch
            {
                OtterGoblinDemo1LevelData.AttackKind.Triple => 3,
                OtterGoblinDemo1LevelData.AttackKind.TripleThenSingle when waitIndex == 0 => 3,
                _ => 1
            };
        }

        private void WinBattle()
        {
            if (ended)
                return;
            ended = true;
            ChangePhase(CombatPhase.Victory);
            BattleWon?.Invoke(GetSummary());
        }

        private void ResetState()
        {
            phraseIndex = 0;
            activePhrase = false;
            running = false;
            ended = false;
            phase = CombatPhase.Intro;
            CurrentSongTick = 0;
            activeTargets.Clear();
            perfectCount = 0;
            goodCount = 0;
            missCount = 0;
            extraCount = 0;
            inputLockedUntilTimelineMs = double.NegativeInfinity;
        }

        private void OnBeat(FmodBeatClock.BeatSnapshot beat)
        {
            BeatObserved?.Invoke(beat);
        }

        private void OnPlaybackError(string message)
        {
            ended = true;
            ChangePhase(CombatPhase.Error);
            BattleError?.Invoke(message);
        }

        private long TimelineMsToTick(double timelineMs)
        {
            double musicMs = timelineMs - levelData.MusicGridOffsetMs;
            return (long)Math.Floor(musicMs / MillisecondsPerBeat * levelData.Ppq);
        }

        private double TickToTimelineMs(long tick)
        {
            return levelData.MusicGridOffsetMs + tick / (double)levelData.Ppq * MillisecondsPerBeat;
        }

        private double MillisecondsPerBeat
        {
            get
            {
                float liveTempo = beatClock != null && beatClock.HasTimingAnchor
                    ? beatClock.LatestBeat.Tempo
                    : 0f;
                return 60000.0 / Mathf.Max(1f, liveTempo > 0f ? liveTempo : levelData.AuthoredBpm);
            }
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

        private JudgementResult Publish(JudgementResult result)
        {
            Judged?.Invoke(result);
            return result;
        }

        private void ChangePhase(CombatPhase next)
        {
            if (phase == next)
                return;
            phase = next;
            PhaseChanged?.Invoke(phase);
        }
    }
}
