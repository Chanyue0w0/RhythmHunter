using System;
using System.Collections.Generic;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Owns all character sprite playback: beat-synced idle and frame-driven combat.
    /// Sequence events time presentation callbacks only; FightCombatController owns beat-authoritative gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FightCharacterCombatAnimator : MonoBehaviour
    {
        public enum CombatAnimation
        {
            NormalAttack,
            LightAttack,
            HeavyAttack,
            Guard,
            Skill,
            Hit,
            Death
        }

        [Serializable]
        public sealed class Sequence
        {
            [SerializeField] private CombatAnimation animation;
            [SerializeField] private List<Sprite> frames = new();
            [SerializeField, Min(1f)] private float framesPerSecond = 16f;
            [Tooltip("-1 disables the warning event for this sequence.")]
            [SerializeField] private int warningFrame = -1;
            [Tooltip("-1 disables the attack VFX event for this sequence.")]
            [SerializeField] private int attackEffectFrame;
            [Tooltip("Legacy presentation event frame. Party-mode damage is resolved by the beat judgement, not this frame.")]
            [SerializeField] private int damageFrame;

            public CombatAnimation Animation => animation;
            public IReadOnlyList<Sprite> Frames => frames;
            public float FramesPerSecond => framesPerSecond;
            public int WarningFrame => warningFrame;
            public int AttackEffectFrame => attackEffectFrame;
            public int DamageFrame => damageFrame;

            public void Configure(
                CombatAnimation animationType,
                IEnumerable<Sprite> sprites,
                float fps,
                int warningAtFrame,
                int effectAtFrame,
                int damageAtFrame)
            {
                animation = animationType;
                frames = sprites != null ? new List<Sprite>(sprites) : new List<Sprite>();
                framesPerSecond = Mathf.Max(1f, fps);
                warningFrame = warningAtFrame;
                attackEffectFrame = effectAtFrame;
                damageFrame = damageAtFrame;
                ClampEventFrames();
            }

            public void ClampEventFrames()
            {
                framesPerSecond = Mathf.Max(1f, framesPerSecond);
                int lastFrame = Mathf.Max(0, frames.Count - 1);
                warningFrame = Mathf.Clamp(warningFrame, -1, lastFrame);
                attackEffectFrame = Mathf.Clamp(attackEffectFrame, -1, lastFrame);
                damageFrame = Mathf.Clamp(damageFrame, 0, lastFrame);
            }
        }

        [SerializeField] private SpriteRenderer targetRenderer;
        [Header("Beat Source")]
        [SerializeField] private FightCombatController beatSource;
        [SerializeField, Min(1f)] private float fallbackBpm = 120f;

        [Header("Editable Idle Sequence")]
        [SerializeField] private List<Sprite> frames = new();
        [Tooltip("Complete animation loops per beat. 1 = one loop each beat; 0.5 = one loop every two beats.")]
        [SerializeField, Min(0.01f)] private float cyclesPerBeat = 1f;
        [Tooltip("Plays the sequence forward, then backward, without duplicating its end frames.")]
        [SerializeField] private bool pingPong;
        [Tooltip("Offsets this character within the beat while keeping it synchronized.")]
        [SerializeField, Range(0f, 1f)] private float phaseOffset;

        [Header("Combat Sequences")]
        [SerializeField] private List<Sequence> sequences = new();

        private double beatAnchorTime;
        private long beatAnchorIndex;
        private double secondsPerBeat;
        private bool hasBeatAnchor;
        private int shownFrame = -1;
        private int playbackVersion;

        private Sequence activeSequence;
        private int activeFrame;
        private float frameElapsed;
        private Action warningCallback;
        private Action attackEffectCallback;
        private Action damageCallback;
        private Action completedCallback;
        private bool warningRaised;
        private bool effectRaised;
        private bool damageRaised;

        public FightCombatController BeatSource => beatSource;
        public SpriteRenderer TargetRenderer => targetRenderer;
        public IReadOnlyList<Sprite> Frames => frames;
        public int FrameCount => frames?.Count ?? 0;
        public float CyclesPerBeat => cyclesPerBeat;
        public bool PingPong => pingPong;

        public int WarningEventCount { get; private set; }
        public int AttackEffectEventCount { get; private set; }
        public int DamageEventCount { get; private set; }

        public bool IsPlaying => activeSequence != null;
        public CombatAnimation ActiveAnimation => activeSequence != null
            ? activeSequence.Animation
            : CombatAnimation.NormalAttack;
        public int ActiveFrame => activeFrame;
        public IReadOnlyList<Sequence> Sequences => sequences;

        public void Configure(
            SpriteRenderer renderer,
            IEnumerable<Sequence> animationSequences)
        {
            targetRenderer = renderer;
            sequences = animationSequences != null
                ? new List<Sequence>(animationSequences)
                : new List<Sequence>();
            foreach (Sequence sequence in sequences)
                sequence?.ClampEventFrames();
        }

        public void ConfigureIdle(
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
            hasBeatAnchor = false;
            secondsPerBeat = 60d / Mathf.Max(1f, fallbackBpm);
            if (!IsPlaying) ShowFrame(0);
            Subscribe();
        }

        public void BindBeatSource(FightCombatController source)
        {
            Unsubscribe();
            beatSource = source;
            hasBeatAnchor = false;
            secondsPerBeat = 60d / Mathf.Max(1f, fallbackBpm);
            Subscribe();
        }

        private void Awake()
        {
            secondsPerBeat = 60d / Mathf.Max(1f, fallbackBpm);
            ShowFrame(0);
        }

        private void OnEnable()
        {
            shownFrame = -1;
            Subscribe();
        }

        private void UpdateIdle()
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


        public bool HasSequence(CombatAnimation animation)
        {
            Sequence sequence = FindSequence(animation);
            return sequence != null && sequence.Frames.Count > 0;
        }

        public bool Play(
            CombatAnimation animation,
            Action onWarning = null,
            Action onAttackEffect = null,
            Action onDamage = null,
            Action onCompleted = null)
        {
            Sequence sequence = FindSequence(animation);
            if (!isActiveAndEnabled || sequence == null || sequence.Frames.Count == 0 || targetRenderer == null)
                return false;

            FinishActiveSequence(true);
            // A callback may have started another animation or disabled this component.
            if (!isActiveAndEnabled || activeSequence != null)
                return false;

            playbackVersion++;
            activeSequence = sequence;
            activeFrame = 0;
            frameElapsed = 0f;
            warningCallback = onWarning;
            attackEffectCallback = onAttackEffect;
            damageCallback = onDamage;
            completedCallback = onCompleted;
            warningRaised = false;
            effectRaised = false;
            damageRaised = false;
            EnterFrame(0);
            return true;
        }

        private void Update()
        {
            if (activeSequence == null)
            {
                UpdateIdle();
                return;
            }

            int version = playbackVersion;
            frameElapsed += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, activeSequence.FramesPerSecond);
            while (activeSequence != null && playbackVersion == version && frameElapsed >= frameDuration)
            {
                frameElapsed -= frameDuration;
                int nextFrame = activeFrame + 1;
                if (nextFrame >= activeSequence.Frames.Count)
                {
                    FinishActiveSequence(true);
                    return;
                }

                EnterFrame(nextFrame);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            FinishActiveSequence(false);
        }

        private void EnterFrame(int frameIndex)
        {
            int version = playbackVersion;
            activeFrame = frameIndex;
            targetRenderer.sprite = activeSequence.Frames[frameIndex];
            if (!warningRaised && frameIndex == activeSequence.WarningFrame)
            {
                warningRaised = true;
                WarningEventCount++;
                warningCallback?.Invoke();
                if (version != playbackVersion) return;
            }

            if (!effectRaised && frameIndex == activeSequence.AttackEffectFrame)
            {
                effectRaised = true;
                AttackEffectEventCount++;
                attackEffectCallback?.Invoke();
                if (version != playbackVersion) return;
            }

            if (!damageRaised && frameIndex == activeSequence.DamageFrame)
            {
                damageRaised = true;
                DamageEventCount++;
                damageCallback?.Invoke();
            }
        }

        private void FinishActiveSequence(bool flushGameplayEvents)
        {
            if (activeSequence == null)
                return;

            // Detach before invoking callbacks: damage can synchronously start a hit animation.
            Action effect = flushGameplayEvents && !effectRaised && activeSequence.AttackEffectFrame >= 0
                ? attackEffectCallback : null;
            Action damage = flushGameplayEvents && !damageRaised ? damageCallback : null;
            Action finished = flushGameplayEvents ? completedCallback : null;
            bool raiseEffect = flushGameplayEvents && !effectRaised && activeSequence.AttackEffectFrame >= 0;
            bool raiseDamage = flushGameplayEvents && !damageRaised;
            playbackVersion++;
            activeSequence = null;
            warningCallback = null;
            attackEffectCallback = null;
            damageCallback = null;
            completedCallback = null;
            shownFrame = -1;
            if (isActiveAndEnabled) UpdateIdle();
            if (raiseEffect) AttackEffectEventCount++;
            if (raiseDamage) DamageEventCount++;
            effect?.Invoke();
            damage?.Invoke();
            finished?.Invoke();
        }

        private Sequence FindSequence(CombatAnimation animation)
        {
            foreach (Sequence sequence in sequences)
            {
                if (sequence != null && sequence.Animation == animation)
                    return sequence;
            }

            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            fallbackBpm = Mathf.Max(1f, fallbackBpm);
            cyclesPerBeat = Mathf.Max(0.01f, cyclesPerBeat);
            phaseOffset = Mathf.Repeat(phaseOffset, 1f);
            shownFrame = -1;
            if (!IsPlaying) ShowFrame(0);
            foreach (Sequence sequence in sequences)
                sequence?.ClampEventFrames();
        }
#endif
    }
}
