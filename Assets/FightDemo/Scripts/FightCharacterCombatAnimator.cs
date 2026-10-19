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
            [InspectorName("Normal")] NormalAttack = 0,
            [InspectorName("Light Attack (Legacy)")] LightAttack = 1,
            HeavyAttack = 2,
            Guard = 3,
            [InspectorName("TeamSkill")] Skill = 4,
            [InspectorName("GetHurt")] Hit = 5,
            [InspectorName("Die")] Death = 6,
            [InspectorName("Opening Idle")] OpeningIdle = 7,
            [InspectorName("Drink")] Drink = 8
        }

        [Serializable]
        public sealed class Sequence
        {
            [SerializeField] private CombatAnimation animation;
            [SerializeField] private List<Sprite> frames = new();
            [SerializeField, Min(1f)] private float framesPerSecond = 16f;
            [Tooltip("Use individual frame durations in beats instead of FPS.")]
            [SerializeField] private bool useFrameBeatDurations;
            [Tooltip("Matches Frames by index. 0.5 = half a beat. Missing entries default to 0.5.")]
            [SerializeField] private List<float> frameBeatDurations = new();
            [Tooltip("-1 disables the warning event for this sequence.")]
            [SerializeField] private int warningFrame = -1;
            [Tooltip("-1 disables the attack VFX event for this sequence.")]
            [SerializeField] private int attackEffectFrame;
            [Tooltip("Legacy presentation event frame. Party-mode damage is resolved by the beat judgement, not this frame.")]
            [SerializeField] private int damageFrame;

            public CombatAnimation Animation => animation;
            public IReadOnlyList<Sprite> Frames => frames;
            public float FramesPerSecond => framesPerSecond;
            public bool UseFrameBeatDurations => useFrameBeatDurations;
            public float GetFrameBeats(int index) => FrameBeats(frameBeatDurations, index);

            public void ConfigureFrameBeats(IEnumerable<float> durations)
            {
                useFrameBeatDurations = true;
                frameBeatDurations = durations != null ? new List<float>(durations) : new List<float>();
                ClampEventFrames();
            }
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
                NormalizeDurations(frameBeatDurations, frames.Count);
                int lastFrame = Mathf.Max(0, frames.Count - 1);
                warningFrame = Mathf.Clamp(warningFrame, -1, lastFrame);
                attackEffectFrame = Mathf.Clamp(attackEffectFrame, -1, lastFrame);
                damageFrame = Mathf.Clamp(damageFrame, -1, lastFrame);
            }
        }

        [SerializeField] private SpriteRenderer targetRenderer;
        [Header("Beat Source")]
        [SerializeField] private FightCombatController beatSource;
        [SerializeField, Min(1f)] private float fallbackBpm = 120f;

        [Header("Editable Idle Sequence")]
        [SerializeField] private List<Sprite> frames = new();
        [Tooltip("Use individual idle frame durations instead of Cycles Per Beat. Ping Pong still applies.")]
        [SerializeField] private bool useIdleFrameBeatDurations;
        [SerializeField] private List<float> idleFrameBeatDurations = new();
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
        private bool playbackPaused;
        private bool openingIdle;
        private float presentationSpeed = 1f;
        private double openingIdleStartBeat;
        public bool IsOpeningIdle => openingIdle;

        // Entry presentation is opt-in. Existing enemies and prefabs keep their normal idle.
        public void BeginOpeningIdle()
        {
            FinishActiveSequence(false);
            openingIdle = true;
            openingIdleStartBeat = CurrentIdleBeatPosition() - idlePausedBeats;
            shownFrame = -1;
            UpdateIdle();
        }

        public bool PlayDrink(Action onCompleted = null)
        {
            return Play(CombatAnimation.Drink, onCompleted: () =>
            {
                openingIdle = false;
                shownFrame = -1;
                UpdateIdle();
                onCompleted?.Invoke();
            });
        }

        public void EndOpeningPresentation()
        {
            FinishActiveSequence(false);
            openingIdle = false;
            presentationSpeed = 1f;
            shownFrame = -1;
            UpdateIdle();
        }

        public void PlayDrinkForSeconds(float seconds)
        {
            Sequence drink = FindSequence(CombatAnimation.Drink);
            if (drink == null || drink.Frames.Count == 0) return;
            float duration = 0;
            for (int i = 0; i < drink.Frames.Count; i++)
                duration += drink.UseFrameBeatDurations ? drink.GetFrameBeats(i) * (float)secondsPerBeat
                    : 1f / Mathf.Max(1f, drink.FramesPerSecond);
            if (PlayDrink()) presentationSpeed = duration / Mathf.Max(0.01f, seconds);
        }
        private double idlePausedBeats, pauseBeatPosition;
        public bool PlaybackPaused => playbackPaused;

        public void SetPlaybackPaused(bool paused)
        {
            if (playbackPaused == paused) return;
            if (paused) pauseBeatPosition = CurrentIdleBeatPosition();
            else idlePausedBeats += Math.Max(0d, CurrentIdleBeatPosition() - pauseBeatPosition);
            playbackPaused = paused;
        }

        // Interruptions discard callbacks; they must never flush a suspended attack.
        public void InterruptPlayback() => FinishActiveSequence(false);

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
            Sequence entry = openingIdle ? FindSequence(CombatAnimation.OpeningIdle) : null;
            if (entry != null && entry.Frames.Count > 0 && targetRenderer != null)
            {
                double totalBeats = 0;
                for (int i = 0; i < entry.Frames.Count; i++)
                    totalBeats += entry.UseFrameBeatDurations ? entry.GetFrameBeats(i)
                        : 1d / Math.Max(1f, entry.FramesPerSecond) / Math.Max(0.001d, secondsPerBeat);
                double position = (CurrentIdleBeatPosition() - idlePausedBeats - openingIdleStartBeat) % totalBeats;
                if (position < 0) position += totalBeats;
                for (int i = 0; i < entry.Frames.Count; i++)
                {
                    position -= entry.UseFrameBeatDurations ? entry.GetFrameBeats(i)
                        : 1d / Math.Max(1f, entry.FramesPerSecond) / Math.Max(0.001d, secondsPerBeat);
                    if (position < 0)
                    {
                        targetRenderer.sprite = entry.Frames[i];
                        shownFrame = -1;
                        return;
                    }
                }
            }
            if (targetRenderer == null || frames == null || frames.Count == 0)
                return;

            double beatPosition = CurrentIdleBeatPosition() - idlePausedBeats;
            double cyclePosition = beatPosition * Math.Max(0.01d, cyclesPerBeat) + phaseOffset;
            int sequenceLength = pingPong && frames.Count > 2 ? frames.Count * 2 - 2 : frames.Count;
            if (useIdleFrameBeatDurations)
            {
                double totalBeats = 0;
                for (int i = 0; i < sequenceLength; i++)
                    totalBeats += FrameBeats(idleFrameBeatDurations, i < frames.Count ? i : sequenceLength - i);
                double position = (beatPosition + phaseOffset * totalBeats) % totalBeats;
                if (position < 0) position += totalBeats;
                for (int i = 0; i < sequenceLength; i++)
                {
                    int index = i < frames.Count ? i : sequenceLength - i;
                    position -= FrameBeats(idleFrameBeatDurations, index);
                    if (position < 0) { ShowFrame(index); return; }
                }
                return;
            }
            int sequenceFrame = PositiveModulo((int)Math.Floor(cyclePosition * sequenceLength), sequenceLength);
            int frameIndex = pingPong && sequenceFrame >= frames.Count
                ? sequenceLength - sequenceFrame
                : sequenceFrame;
            ShowFrame(frameIndex);
        }

        private double CurrentIdleBeatPosition()
        {
            FmodBeatClock clock = beatSource != null ? beatSource.BeatClock : null;
            if (clock != null && clock.HasTimingAnchor && clock.TryGetTimelinePositionMs(out int timelineMs))
                return clock.LatestBeat.GlobalBeat +
                    (timelineMs - clock.LatestBeat.TimelinePositionMs) / Math.Max(1d, clock.MillisecondsPerBeat);
            double now = Time.timeAsDouble;
            return hasBeatAnchor
                ? beatAnchorIndex + Math.Max(0d, now - beatAnchorTime) / Math.Max(0.001d, secondsPerBeat)
                : now / Math.Max(0.001d, secondsPerBeat);
        }

        private void OnBeat(FmodBeatClock.BeatSnapshot beat)
        {
            beatAnchorTime = Time.timeAsDouble;
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

            // Hits and other presentation requests cannot replace the held frame.
            // Gameplay interruption is explicit via InterruptPlayback().
            if (playbackPaused) return true;

            FinishActiveSequence(true);
            // A callback may have started another animation or disabled this component.
            if (!isActiveAndEnabled || activeSequence != null)
                return false;

            playbackVersion++;
            activeSequence = sequence;
            presentationSpeed = 1f;
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
            AdvancePlayback(Time.deltaTime);
        }

        private void AdvancePlayback(float deltaTime)
        {
            if (playbackPaused) return;
            deltaTime *= presentationSpeed;
            if (activeSequence == null)
            {
                UpdateIdle();
                return;
            }

            int version = playbackVersion;
            // Reuse the tempo delivered by the combat beat source. Accumulating beat
            // fractions preserves progress if the music tempo changes mid-frame.
            float pitch = beatSource != null && beatSource.BeatClock != null ? beatSource.BeatClock.MusicPitch : 1f;
            if (beatSource != null && beatSource.BeatClock != null && beatSource.BeatClock.HasTimingAnchor)
                secondsPerBeat = beatSource.BeatClock.MillisecondsPerBeat / 1000d;
            frameElapsed += activeSequence.UseFrameBeatDurations
                ? deltaTime * pitch / (float)Math.Max(0.001d, secondsPerBeat)
                : deltaTime;
            while (activeSequence != null && playbackVersion == version)
            {
                float frameDuration = activeSequence.UseFrameBeatDurations
                    ? activeSequence.GetFrameBeats(activeFrame)
                    : 1f / Mathf.Max(1f, activeSequence.FramesPerSecond);
                if (frameElapsed < frameDuration) break;
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
            playbackPaused = false;
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
            Action damage = flushGameplayEvents && !damageRaised && activeSequence.DamageFrame >= 0 ? damageCallback : null;
            Action finished = flushGameplayEvents ? completedCallback : null;
            bool raiseEffect = flushGameplayEvents && !effectRaised && activeSequence.AttackEffectFrame >= 0;
            bool raiseDamage = flushGameplayEvents && !damageRaised && activeSequence.DamageFrame >= 0;
            playbackVersion++;
            activeSequence = null;
            warningCallback = null;
            attackEffectCallback = null;
            damageCallback = null;
            completedCallback = null;
            shownFrame = -1;
            if (isActiveAndEnabled && !playbackPaused) UpdateIdle();
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

        private static float FrameBeats(List<float> durations, int index)
        {
            float value = durations != null && index >= 0 && index < durations.Count ? durations[index] : 0.5f;
            return float.IsNaN(value) || float.IsInfinity(value) ? 0.5f : Mathf.Max(0.01f, value);
        }

        private static void NormalizeDurations(List<float> durations, int count)
        {
            if (durations == null) return;
            while (durations.Count < count) durations.Add(0.5f);
            for (int i = 0; i < durations.Count; i++) durations[i] = FrameBeats(durations, i);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            fallbackBpm = Mathf.Max(1f, fallbackBpm);
            cyclesPerBeat = Mathf.Max(0.01f, cyclesPerBeat);
            phaseOffset = Mathf.Repeat(phaseOffset, 1f);
            NormalizeDurations(idleFrameBeatDurations, frames.Count);
            shownFrame = -1;
            if (!IsPlaying) ShowFrame(0);
            foreach (Sequence sequence in sequences)
                sequence?.ClampEventFrames();
        }
#endif
    }
}
