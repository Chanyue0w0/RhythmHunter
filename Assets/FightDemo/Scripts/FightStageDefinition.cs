using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    [CreateAssetMenu(menuName = "Rhythm Hunter/Levels/Stage")]
    public sealed class FightStageDefinition : ScriptableObject
    {
        public enum WaveKind { Combat, Treasure }
        [Serializable]
        public sealed class Wave
        {
            public string name = "Wave";
            public WaveKind kind;
            public FightCharacterDefinition front;
            public FightCharacterDefinition middle;
            public FightCharacterDefinition back;
            [Tooltip("Shared pressure window for this Wave. Disabled preserves the existing attack interval.")]
            public FightEnemyPressureSettings pressure = new();
            public FightCharacterDefinition At(int position) => position == 0 ? front : position == 1 ? middle : back;
        }
        [SerializeField] private string stageId = "Stage01";
        [SerializeField] private string displayName = "Stage 1";
        [Tooltip("Existing FMOD song event. A Stage plays one song across all waves.")]
        [SerializeField] private string musicEventPath;
        [Tooltip("Non-Boss stages only. Normal_End ends regular time; overtime clears only the current combat Wave.")]
        [SerializeField] private bool useNormalEndDeadline;
        public bool UseNormalEndDeadline => useNormalEndDeadline;
        [Tooltip("Only the last Combat Wave permits overtime. Earlier Combat Waves (including their transition gap) expire as defeat. Treasure Waves still settle immediately.")]
        [SerializeField] private bool overtimeOnlyOnFinalCombatWave;
        public bool OvertimeOnlyOnFinalCombatWave => overtimeOnlyOnFinalCombatWave;
        [Tooltip("Song duration for results/overtime statistics. 0 = read FMOD event length. Set explicitly for looping/adaptive events. No overtime penalty.")]
        [SerializeField, Min(0)] private float plannedSongSeconds;
        [SerializeField] private List<Wave> waves = new();
        public string StageId => stageId;
        public string DisplayName => displayName;
        public string MusicEventPath => musicEventPath;
        public float PlannedSongSeconds => plannedSongSeconds;
        public IReadOnlyList<Wave> Waves => waves;

        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(stageId)) { error = "Stage ID is empty."; return false; }
            if (string.IsNullOrWhiteSpace(musicEventPath) || !musicEventPath.StartsWith("event:/", StringComparison.Ordinal))
            { error = "Stage requires an FMOD event:/ song path."; return false; }
            if (waves == null || waves.Count == 0) { error = "Stage requires at least one Wave."; return false; }
            for (int i = 0; i < waves.Count; i++)
            {
                var wave = waves[i];
                if (wave == null || (!wave.front && !wave.middle && !wave.back))
                { error = $"Wave {i + 1} has no enemies."; return false; }
                if (wave.pressure != null && !wave.pressure.Validate(out error))
                { error = $"Wave {i + 1}: {error}"; return false; }
                for (int p = 0; p < 3; p++)
                    if (wave.At(p) != null && wave.At(p).Team != FightUnitSlot.UnitTeam.Enemy)
                    { error = $"Wave {i + 1}, position {p + 1} must reference an enemy prefab."; return false; }
            }
            error = null; return true;
        }
    }
}
