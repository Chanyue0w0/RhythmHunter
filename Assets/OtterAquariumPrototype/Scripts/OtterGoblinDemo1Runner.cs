using System;
using System.Collections.Generic;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
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
            Defeat,
            Error
        }

        public readonly struct JudgementResult
        {
            public JudgementResult(Grade grade, double deltaMs, int health, bool extraInput)
            {
                Judgement = grade;
                DeltaMs = deltaMs;
                Health = health;
                ExtraInput = extraInput;
            }

            public Grade Judgement { get; }
            public double DeltaMs { get; }
            public int Health { get; }
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

        private readonly List<TargetState> activeTargets = new();
        private int phraseIndex;
        private int nextWarningIndex;
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
        private CombatPhase phase = CombatPhase.Intro;

        public event Action<FmodBeatClock.BeatSnapshot> BeatObserved;
        public event Action<CombatPhase> PhaseChanged;
        public event Action<int, OtterGoblinDemo1LevelData.AttackPhrase> PhraseStarted;
        public event Action<int, int, string> WarningCue;
        public event Action<int, int, string> AttackCue;
        public event Action<JudgementResult> Judged;
        public event Action<int, int> HealthChanged;
        public event Action<CombatSummary> BattleWon;
        public event Action BattleLost;
        public event Action<string> BattleError;

        public FmodBeatClock BeatClock => beatClock;
        public OtterGoblinDemo1LevelData LevelData => levelData;
        public CombatPhase Phase => phase;
        public int Health { get; private set; }
        public bool HasEnded => ended;
        public long CurrentSongTick { get; private set; }
        public int CurrentBar => levelData == null ? 1 : Mathf.Max(1, (int)(CurrentSongTick / levelData.TicksPerBar) + 1);
        public int CurrentPhraseNumber => activePhrase ? phraseIndex + 1 : Mathf.Clamp(phraseIndex, 0, levelData != null ? levelData.Phrases.Count : 0);

        public void Configure(FmodBeatClock clock, OtterGoblinDemo1LevelData data)
        {
            beatClock = clock;
            levelData = data;
            ResetState();
        }

        private void Awake()
        {
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

            if (!ended && CurrentSongTick >= (long)levelData.TotalBars * levelData.TicksPerBar)
                WinBattle();
        }

        public JudgementResult SubmitInput()
        {
            if (!running || ended || levelData == null || beatClock == null
                || !beatClock.TryGetTimelinePositionMs(out int timelineMs))
            {
                return Publish(new JudgementResult(Grade.NotReady, 0.0, Health, false));
            }

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
                return Publish(new JudgementResult(grade, nearestDelta, Health, false));
            }

            extraCount++;
            return Publish(new JudgementResult(Grade.Miss, nearestDelta, Health, true));
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

        public string GetCurrentPatternDisplay()
        {
            if (!activePhrase || levelData == null)
                return "— — — —";

            OtterGoblinDemo1LevelData.AttackPhrase phrase = levelData.Phrases[phraseIndex];
            int steps = phrase.WarningLengthBeats * 2;
            char[] display = new char[steps];
            for (int i = 0; i < display.Length; i++)
                display[i] = '·';
            foreach (int tick in phrase.Pattern.HitTicks)
            {
                int step = Mathf.RoundToInt(tick / (levelData.Ppq * 0.5f));
                if (step >= 0 && step < display.Length)
                    display[step] = '●';
            }
            return string.Join(" ", display);
        }

        private void ProcessTimeline(long songTick, double evaluatedTimelineMs)
        {
            if (phraseIndex >= levelData.Phrases.Count)
            {
                ChangePhase(CombatPhase.Rest);
                return;
            }

            OtterGoblinDemo1LevelData.AttackPhrase phrase = levelData.Phrases[phraseIndex];
            long scheduledStart = (long)(phrase.StartBar - 1) * levelData.TicksPerBar;
            if (!activePhrase)
            {
                if (songTick < scheduledStart)
                {
                    ChangePhase(phraseIndex == 0 ? CombatPhase.Intro : CombatPhase.Rest);
                    return;
                }
                StartPhrase(phrase, scheduledStart);
            }

            while (nextWarningIndex < phrase.Pattern.HitTicks.Count)
            {
                long cueTick = activePhraseStartTick + phrase.Pattern.HitTicks[nextWarningIndex];
                if (songTick < cueTick)
                    break;
                WarningCue?.Invoke(nextWarningIndex + 1, phrase.Pattern.HitTicks.Count, phrase.Pattern.Id);
                nextWarningIndex++;
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
                ApplyDamage();
                Publish(new JudgementResult(Grade.Miss, evaluatedTimelineMs - TickToTimelineMs(target.Tick), Health, false));
                if (ended)
                    return;
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
            warningEndTick = startTick + (long)phrase.WarningLengthBeats * levelData.Ppq;
            attackStartTick = warningEndTick + (long)phrase.GapBeats * levelData.Ppq;
            phraseEndTick = attackStartTick + (long)phrase.AttackLengthBeats * levelData.Ppq;
            nextWarningIndex = 0;
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

        private void ApplyDamage()
        {
            int oldHealth = Health;
            Health = Mathf.Max(0, Health - levelData.DamagePerMiss);
            if (Health != oldHealth)
                HealthChanged?.Invoke(Health, levelData.OtterMaxHealth);
            if (Health <= 0)
                LoseBattle();
        }

        private void WinBattle()
        {
            if (ended)
                return;
            ended = true;
            ChangePhase(CombatPhase.Victory);
            BattleWon?.Invoke(GetSummary());
        }

        private void LoseBattle()
        {
            if (ended)
                return;
            ended = true;
            ChangePhase(CombatPhase.Defeat);
            BattleLost?.Invoke();
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
            Health = levelData != null ? levelData.OtterMaxHealth : 3;
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

        private double MillisecondsPerBeat => 60000.0 / Mathf.Max(1f, levelData.AuthoredBpm);

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
