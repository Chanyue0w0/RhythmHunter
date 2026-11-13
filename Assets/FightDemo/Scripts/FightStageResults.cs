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

        private void OnEnable()
        {
            if (fight == null) return;
            fight.BattleVictory += OnVictory;
            fight.BattleLost += OnDefeat;
            fight.WaveCleared += OnWaveCleared;
        }
        private void OnDisable()
        {
            if (fight == null) return;
            fight.BattleVictory -= OnVictory;
            fight.BattleLost -= OnDefeat;
            fight.WaveCleared -= OnWaveCleared;
        }
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
