using System;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    // Statistics only: never drives beats, animation, damage or wave progression.
    [DisallowMultipleComponent]
    public sealed class FightStageResults : MonoBehaviour
    {
        [SerializeField] private FightCombatController fight;
        [SerializeField] private FightLevelPlanner planner;
        [SerializeField] private FmodBeatClock beatClock;
        public enum Grade { S, A, B, C, D }
        [Header("Result rating")]
        [SerializeField, Range(0, 1)] private float gradeAHealthRatio = .75f;
        [SerializeField, Range(0, 1)] private float gradeBHealthRatio = .5f;
        [SerializeField] private Grade overtimeGradeCap = Grade.B;
        public int SuccessfulBlocks { get; private set; }
        public int ReceivedHits { get; private set; }
        public float RemainingHp { get; private set; }
        public float MaximumHp { get; private set; }
        public bool TookDamage { get; private set; }
        public bool UsedOvertime { get; private set; }
        public bool IsTutorial { get; private set; }
        public Grade Rating { get; private set; }
        public string RatingText => IsTutorial ? "訓練完成" : Rating.ToString();

        public static Grade Evaluate(bool won, bool tookDamage, bool overtime, float healthRatio,
            float aThreshold = .75f, float bThreshold = .5f, Grade cap = Grade.B)
        {
            if (!won) return Grade.D;
            bThreshold = Mathf.Clamp01(bThreshold);
            aThreshold = Mathf.Clamp(aThreshold, bThreshold, 1);
            Grade grade = !tookDamage ? Grade.S : healthRatio >= aThreshold ? Grade.A :
                healthRatio >= bThreshold ? Grade.B : Grade.C;
            return overtime ? (Grade)Math.Max((int)grade, Mathf.Clamp((int)cap, 0, (int)Grade.C)) : grade;
        }
        public double ElapsedSeconds { get; private set; }
        public double SongProgressSeconds { get; private set; }
        public float SongLengthSeconds { get; private set; }
        public bool Started { get; private set; }
        public bool Completed { get; private set; }
        public bool Won { get; private set; }
        public string StageName { get; private set; }
        public string ChapterName { get; private set; }
        public int ClearedWaves { get; private set; }
        public int TotalWaves { get; private set; }
        public double OvertimeSeconds => SongLengthSeconds > 0 ? Math.Max(0, SongProgressSeconds - SongLengthSeconds) : 0;
        public event Action ResultReady;
        public FmodBeatClock BeatClock => beatClock;
        public bool PresentationPaused => fight != null && fight.IsPaused;
        public bool DefeatSlowMotionActive => fight != null && fight.DefeatSlowMotionActive;
        public float DefeatPresentationProgress => fight != null ? fight.DefeatPresentationProgress : 1;

        private void OnEnable()
        {
            if (fight == null) return;
            fight.BattleVictory += OnVictory;
            fight.BattleLost += OnDefeat;
            fight.WaveCleared += OnWaveCleared;
            fight.PartyDamageTaken += OnDamageTaken;
            fight.OvertimeStarted += OnOvertime;
        }
        private void OnDisable()
        {
            if (fight == null) return;
            fight.BattleVictory -= OnVictory;
            fight.BattleLost -= OnDefeat;
            fight.WaveCleared -= OnWaveCleared;
            fight.PartyDamageTaken -= OnDamageTaken;
            fight.OvertimeStarted -= OnOvertime;
        }
        private void OnDamageTaken(float armor, float hp) { if (!Completed && (armor > 0 || hp > 0)) TookDamage = true; }
        private void OnOvertime() { if (!Completed) UsedOvertime = true; }
        private void Update()
        {
            if (fight == null || beatClock == null || Completed) return;
            if (!Started && beatClock.IsPlaying && (fight.BattleEntranceRunning || !fight.AwaitingBattleStart)) Begin();
            if (Started && SongLengthSeconds <= 0 && beatClock.TryGetSongLengthSeconds(out float length)) SongLengthSeconds = length;
            Advance(Time.unscaledDeltaTime, beatClock.MusicPitch, fight.IsPaused || fight.TimingCalibrationActive);
            if (fight.StageDeadlineEnabled && fight.StageTimeLimitSeconds > 0)
            {
                SongLengthSeconds = fight.StageTimeLimitSeconds;
                SongProgressSeconds = fight.StageMusicSeconds;
            }
        }
        private void Begin()
        {
            if (Started) return;
            Started = true;
            var stage = planner != null ? planner.ActiveStage : null;
            StageName = stage != null ? stage.DisplayName : "Battle";
            ChapterName = planner != null && planner.Chapter != null ? planner.Chapter.DisplayName : "";
            TotalWaves = fight != null ? fight.WaveCount : 0;
            SongLengthSeconds = stage != null ? stage.PlannedSongSeconds : 0;
            if (SongLengthSeconds <= 0 && beatClock != null && beatClock.TryGetSongLengthSeconds(out float length)) SongLengthSeconds = length;
        }
        private void Advance(float seconds, float pitch, bool suspended)
        {
            if (!Started || Completed || suspended || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            ElapsedSeconds += seconds;
            // Track song-equivalent duration across loops, including TeamSkill and wave gaps.
            SongProgressSeconds += seconds * Mathf.Max(0, pitch);
        }
        private void OnWaveCleared(int wave) => ClearedWaves = Math.Max(ClearedWaves, wave);
        private void OnVictory() => Finish(true);
        private void OnDefeat() => Finish(false);
        private void Finish(bool won)
        {
            if (Completed) return;
            if (!Started) Begin();
            Completed = true;
            Won = won;
            var stage = planner != null ? planner.ActiveStage : null;
            IsTutorial = stage != null && stage.Tutorial;
            SuccessfulBlocks = fight.BlockedAttackCount;
            ReceivedHits = fight.ReceivedAttackCount + (fight.BossRhythm != null ? fight.BossRhythm.MissedNotes : 0);
            RemainingHp = fight.PartyHp;
            MaximumHp = fight.MaxPartyHp;
            UsedOvertime |= fight.StageOvertimeActive;
            Rating = Evaluate(won, TookDamage, UsedOvertime, MaximumHp > 0 ? RemainingHp / MaximumHp : 0,
                gradeAHealthRatio, gradeBHealthRatio, overtimeGradeCap);
            if (won && !fight.StageTimeExpired) ClearedWaves = TotalWaves;
            if (fight.StageDeadlineEnabled && fight.StageTimeLimitSeconds > 0)
            {
                SongLengthSeconds = fight.StageTimeLimitSeconds;
                SongProgressSeconds = fight.StageMusicSeconds;
            }
            ResultReady?.Invoke();
        }
    }
}
