using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Frame-driven combat animation player kept separate from character stats and beat-synced idle.
    /// Each prefab owns editable sequences and decides which frame raises warning, VFX, and damage events.
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
            [Tooltip("Frame that applies gameplay damage. Clamped to an existing frame.")]
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
        [SerializeField] private BeatSyncedIdleAnimator idleAnimator;
        [SerializeField] private List<Sequence> sequences = new();

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
            BeatSyncedIdleAnimator idle,
            IEnumerable<Sequence> animationSequences)
        {
            targetRenderer = renderer;
            idleAnimator = idle;
            sequences = animationSequences != null
                ? new List<Sequence>(animationSequences)
                : new List<Sequence>();
            foreach (Sequence sequence in sequences)
                sequence?.ClampEventFrames();
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
            if (sequence == null || sequence.Frames.Count == 0 || targetRenderer == null)
                return false;

            FinishActiveSequence(true);
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
            if (idleAnimator != null)
                idleAnimator.enabled = false;
            EnterFrame(0);
            return true;
        }

        private void Update()
        {
            if (activeSequence == null)
                return;

            frameElapsed += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, activeSequence.FramesPerSecond);
            while (activeSequence != null && frameElapsed >= frameDuration)
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
            FinishActiveSequence(false);
        }

        private void EnterFrame(int frameIndex)
        {
            activeFrame = frameIndex;
            targetRenderer.sprite = activeSequence.Frames[frameIndex];
            if (!warningRaised && frameIndex == activeSequence.WarningFrame)
            {
                warningRaised = true;
                WarningEventCount++;
                warningCallback?.Invoke();
            }

            if (!effectRaised && frameIndex == activeSequence.AttackEffectFrame)
            {
                effectRaised = true;
                AttackEffectEventCount++;
                attackEffectCallback?.Invoke();
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

            if (flushGameplayEvents)
            {
                if (!effectRaised)
                {
                    AttackEffectEventCount++;
                    attackEffectCallback?.Invoke();
                }
                if (!damageRaised)
                {
                    DamageEventCount++;
                    damageCallback?.Invoke();
                }
            }

            Action finished = completedCallback;
            activeSequence = null;
            warningCallback = null;
            attackEffectCallback = null;
            damageCallback = null;
            completedCallback = null;
            if (idleAnimator != null)
                idleAnimator.enabled = true;
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
            foreach (Sequence sequence in sequences)
                sequence?.ClampEventFrames();
        }
#endif
    }
}
