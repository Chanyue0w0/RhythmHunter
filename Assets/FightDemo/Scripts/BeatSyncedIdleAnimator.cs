using System;
using System.Collections.Generic;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Plays a sprite sequence on the musical timeline exposed by FightCombatController.
    /// The sequence and its musical speed stay editable on every unit in the Inspector.
    /// </summary>
    public sealed class BeatSyncedIdleAnimator : MonoBehaviour
    {
        [Header("Beat Source")]
        [SerializeField] private FightCombatController beatSource;
        [SerializeField, Min(1f)] private float fallbackBpm = 120f;

        [Header("Editable Idle Sequence")]
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private List<Sprite> frames = new();
        [Tooltip("Complete animation loops per beat. 1 = one loop each beat; 0.5 = one loop every two beats.")]
        [SerializeField, Min(0.01f)] private float cyclesPerBeat = 1f;
        [Tooltip("Plays the sequence forward, then backward, without duplicating its end frames.")]
        [SerializeField] private bool pingPong;
        [Tooltip("Offsets this character within the beat while keeping it synchronized.")]
        [SerializeField, Range(0f, 1f)] private float phaseOffset;

        private double beatAnchorTime;
        private long beatAnchorIndex;
        private double secondsPerBeat;
        private bool hasBeatAnchor;
        private int shownFrame = -1;

        public FightCombatController BeatSource => beatSource;
        public SpriteRenderer TargetRenderer => targetRenderer;
        public IReadOnlyList<Sprite> Frames => frames;
        public int FrameCount => frames?.Count ?? 0;
        public float CyclesPerBeat => cyclesPerBeat;
        public bool PingPong => pingPong;

        public void Configure(
            FightCombatController source,
            SpriteRenderer renderer,
            IEnumerable<Sprite> idleFrames,
            float loopsPerBeat = 1f,
            bool usePingPong = false,
            float offset = 0f)
        {
            Unsubscribe();
            beatSource = source;
            targetRenderer = renderer;
            frames = idleFrames != null ? new List<Sprite>(idleFrames) : new List<Sprite>();
            cyclesPerBeat = Mathf.Max(0.01f, loopsPerBeat);
            pingPong = usePingPong;
            phaseOffset = Mathf.Repeat(offset, 1f);
            shownFrame = -1;
            ShowFrame(0);
            Subscribe();
        }

        private void Awake()
        {
            secondsPerBeat = 60d / Mathf.Max(1f, fallbackBpm);
            ShowFrame(0);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (targetRenderer == null || frames == null || frames.Count == 0)
                return;

            double now = Time.unscaledTimeAsDouble;
            double beatPosition = hasBeatAnchor
                ? beatAnchorIndex + Math.Max(0d, now - beatAnchorTime) / Math.Max(0.001d, secondsPerBeat)
                : now / Math.Max(0.001d, secondsPerBeat);
            double cyclePosition = beatPosition * Math.Max(0.01d, cyclesPerBeat) + phaseOffset;
            int sequenceLength = pingPong && frames.Count > 2 ? frames.Count * 2 - 2 : frames.Count;
            int sequenceFrame = PositiveModulo((int)Math.Floor(cyclePosition * sequenceLength), sequenceLength);
            int frameIndex = pingPong && sequenceFrame >= frames.Count
                ? sequenceLength - sequenceFrame
                : sequenceFrame;
            ShowFrame(frameIndex);
        }

        private void OnBeat(FmodBeatClock.BeatSnapshot beat)
        {
            beatAnchorTime = Time.unscaledTimeAsDouble;
            beatAnchorIndex = beat.GlobalBeat;
            secondsPerBeat = 60d / Math.Max(1d, beat.Tempo);
            hasBeatAnchor = true;
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || beatSource == null)
                return;

            beatSource.FightBeat -= OnBeat;
            beatSource.FightBeat += OnBeat;
        }

        private void Unsubscribe()
        {
            if (beatSource != null)
                beatSource.FightBeat -= OnBeat;
        }

        private void ShowFrame(int index)
        {
            if (targetRenderer == null || frames == null || index < 0 || index >= frames.Count || shownFrame == index)
                return;

            targetRenderer.sprite = frames[index];
            shownFrame = index;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            if (modulus <= 0)
                return 0;

            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            fallbackBpm = Mathf.Max(1f, fallbackBpm);
            cyclesPerBeat = Mathf.Max(0.01f, cyclesPerBeat);
            phaseOffset = Mathf.Repeat(phaseOffset, 1f);
            shownFrame = -1;
            ShowFrame(0);
        }
#endif
    }
}
