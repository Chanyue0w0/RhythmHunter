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
        [Header("Stage Classification / Environment")]
        [Tooltip("Marks this as a tutorial Stage. Does not automatically change damage, music, Pressure or victory rules.")]
        [SerializeField] private bool tutorial;
        [Tooltip("Tutorial only: this hero occupies Front; Middle and Back stay empty. Normal stages retain the scene's default party.")]
        [SerializeField] private FightCharacterDefinition tutorialHero;
        public FightCharacterDefinition TutorialHero => tutorialHero;
        [Tooltip("Overrides Chapter.DefaultEnvironment. Unassigned inherits the chapter, then falls back to Cave.")]
        [SerializeField] private FightEnvironmentDefinition environment;
        public bool Tutorial => tutorial;
        public FightEnvironmentDefinition Environment => environment;
        public string EnvironmentId => environment != null ? environment.EnvironmentId : "Cave";
        [Header("Boss Entrance")]
        [SerializeField] private bool bossStage;
        public bool BossStage => bossStage;
        [Serializable]
        public sealed class BossEntranceSettings
        {
            public bool enabled = true;
            [Min(0)] public float waitBeforeDoorBeats = 6;
            [Min(.01f)] public float doorOpenBeats = 1;
            public string openingAnimationName = "OpenIdle";
            [Min(0)] public float openingIdleBeats = 2;
            [Tooltip("Size multiplier at the distant door. Restores the original battle scale above the landing point while offscreen.")]
            [Range(.05f, 1f)] public float doorBossScale = .6f;
            [Min(.05f)] public float jumpBeats = 4;
            public string jumpAnimationName = "Jump";
            public string flyingAnimationName = "Flying";
            public string landingAnimationName = "Landing";
            [Min(0)] public float flyingHoldBeats = .5f;
            [Min(.01f)] public float doorCloseBeats = 1;
            [Range(.05f,.95f)] public float ascentFraction = .4f;
            [Min(0)] public float extraJumpHeight = 2;
            [Min(0)] public float landingWaitBeats = 4;
            public GameObject landingEffect;
            public Vector3 landingEffectOffset;
            [Header("Door reveal and camera")]
            public GameObject doorExplosion;
            public bool doorExplosionEnabled;
            [Min(.1f)] public float doorExplosionBeats = 2;
            [Tooltip("Fade from transparent to fully visible immediately after spawning, independently of smoke duration.")]
            [Min(.01f)] public float bossRevealBeats = 1;
            public Vector3 doorExplosionOffset;
            [Min(.01f)] public float doorExplosionScale = 1;
            public bool landingExplosionEnabled = true;
            public Vector3 landingExplosionOffset;
            [Min(.01f)] public float landingExplosionScale = 1;
            public bool doorFocusEnabled = true;
            [Min(.01f)] public float doorFocusLeadBeats = 2;
            [Range(0,1)] public float doorFocusWeight = .25f;
            [Range(0,.5f)] public float doorFocusZoom = .1f;
            public Vector3 doorFocusOffset = new Vector3(0,1,0);
            [Min(.01f)] public float doorFocusReturnBeats = 1;
            public bool doorShakeEnabled = true;
            [Min(.01f)] public float doorShakeBeats = 2;
            public Vector2 doorShakeAmplitude = new Vector2(.08f,.06f);
            [Min(.1f)] public float doorShakeCyclesPerBeat = 5;
            [Min(.1f)] public float doorShakeDecay = 2;
            [Header("Entrance presentation")]
            public bool blackSilhouette = true;
            public bool doorLightEnabled = true;
            [ColorUsage(false, true)] public Color doorLightColor = new Color(1f, .78f, .42f, 1f);
            [Min(0)] public float doorLightIntensity = 12;
            [Min(1)] public float doorLightRange = 25;
            [Range(1, 170)] public float doorLightAngle = 65;
            public Vector3 doorLightOffset = new Vector3(0, 2, 3);
            public Vector3 doorLightEuler = new Vector3(25, 180, 0);
            [Min(.01f)] public float doorLightFadeOutBeats = 1;
            public bool doorLightCastShadows = true;
            [Tooltip("Exclude player heroes from both door lighting and door-light shadows. Other lights remain unchanged.")]
            public bool doorLightExcludeHeroes = true;
            [Range(0, 1)] public float doorLightShadowStrength = 1;
            [Range(0, 2)] public float doorLightShadowBias = .05f;
            [Range(0, 2)] public float doorLightShadowNormalBias = .05f;
            [Tooltip("Range, Angle, Offset and Euler control both the spotlight and its visible ground footprint. Disable to use manual Fan dimensions.")]
            public bool fanFollowsDoorLight = true;
            [Min(0)] public float fanBrightness = 1.5f;
            [Min(.1f)] public float fanLength = 12;
            [Min(.1f)] public float fanEndWidth = 10;
            [Min(0)] public float fanStartWidth = 1;
            public float fanYaw = 180;
            public float fanGroundY = .04f;
            public bool landingShakeEnabled = true;
            [Min(0)] public float landingShakeBeats = 6;
            public Vector2 landingShakeAmplitude = new Vector2(.16f, .12f);
            [Min(.1f)] public float landingShakeCyclesPerBeat = 5;
            [Min(.1f)] public float landingShakeDecay = 2;
        }
        [SerializeField] private BossEntranceSettings bossEntrance = new();
        public BossEntranceSettings BossEntrance => bossEntrance;
        [Tooltip("Existing FMOD song event. A Stage plays one song across all waves.")]
        [SerializeField] private string musicEventPath;
        [Tooltip("Non-Boss stages only. Normal_End ends regular time; overtime clears only the current combat Wave.")]
        [SerializeField] private bool useNormalEndDeadline;
        public bool UseNormalEndDeadline => useNormalEndDeadline;
        [Tooltip("Only the last Combat Wave permits overtime. Earlier Combat Waves (including their transition gap) expire as defeat. Treasure Waves still settle immediately.")]
        [SerializeField] private bool overtimeOnlyOnFinalCombatWave;
        public bool OvertimeOnlyOnFinalCombatWave => overtimeOnlyOnFinalCombatWave;
        [Header("Overtime Pressure")]
        [Tooltip("Ordinary combat beats between reductions to attack recovery. Does not change music or animation speed.")]
        [SerializeField, Min(1)] private int overtimeAccelerationIntervalBeats = 20;
        [SerializeField, Min(0)] private int overtimeRecoveryReductionBeats = 1;
        [SerializeField, Min(0)] private int overtimeMinimumRecoveryBeats;
        public int OvertimeAccelerationIntervalBeats => Mathf.Max(1, overtimeAccelerationIntervalBeats);
        public int OvertimeRecoveryReductionBeats => Mathf.Max(0, overtimeRecoveryReductionBeats);
        public int OvertimeMinimumRecoveryBeats => Mathf.Max(0, overtimeMinimumRecoveryBeats);
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
            if (tutorial && (tutorialHero == null || tutorialHero.Team != FightUnitSlot.UnitTeam.Hero))
            { error = "Tutorial requires a hero prefab in Tutorial Hero."; return false; }
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
