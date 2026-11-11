using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightWeaponVisual
    {
        public enum ChargeStage { Uncharged, HalfCharged, FullCharged }
        [Serializable] public sealed class AnimationFrame
        {
            public Sprite sprite;
            [Min(.01f)] public float beats = .33f;
        }
        [Serializable] public sealed class AnimationState
        {
            public string stateId = "Normal";
            public bool loop = true;
            public List<AnimationFrame> frames = new();
            [Tooltip("Only active in this state. Keep these objects under the weapon root.")]
            public List<GameObject> visibleObjects = new();
            [Tooltip("Optional Animator state to enter on visible objects, e.g. Base Layer.GearSpin. Empty keeps the prefab default.")]
            public string animatorStateOnEnter;
            public FightCharacterCombatAnimator.Sequence BuildSequence()
            {
                var sprites = new List<Sprite>(); var durations = new List<float>();
                foreach (var frame in frames)
                    if (frame != null && frame.sprite != null) { sprites.Add(frame.sprite); durations.Add(Mathf.Max(.01f, frame.beats)); }
                var sequence = new FightCharacterCombatAnimator.Sequence();
                sequence.Configure(FightCharacterCombatAnimator.CombatAnimation.NormalAttack, sprites, 12, -1, -1, -1);
                sequence.ConfigureFrameBeats(durations);
                return sequence;
            }
        }
        [Header("Weapon Beat Animation")]
        [SerializeField] private List<AnimationState> animationStates = new();
        [SerializeField] private string initialStateId = "Normal";
        [SerializeField] private string unchargedStateId = "Normal";
        [SerializeField] private string halfChargedStateId = "Charge_1";
        [SerializeField] private string fullChargedStateId = "Charge_Full";
        [SerializeField] private Transform gearAnchor;
        private FightCharacterCombatAnimator visualPlayer, ownerAnimator;
        private string currentStateId;
        private AnimationState currentState;
        private bool effectsPaused;
        private readonly List<ParticleSystem> pausedParticles = new();
        private readonly Dictionary<Animator, float> pausedAnimators = new();
        public IReadOnlyList<AnimationState> AnimationStates => animationStates;
        public string CurrentStateId => currentStateId;
        public Transform GearAnchor => gearAnchor;
        public FightCharacterCombatAnimator VisualPlayer => visualPlayer;

        private void Awake() { EnsurePlayer(); }
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            EnsurePlayer();
            SetWeaponAnimationState(currentStateId ?? initialStateId, true);
        }
        private void EnsurePlayer()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            visualPlayer = GetComponent<FightCharacterCombatAnimator>() ?? gameObject.AddComponent<FightCharacterCombatAnimator>();
        }
        public void BindAnimationSource(FightCharacterCombatAnimator owner)
        {
            ownerAnimator = owner; EnsurePlayer();
            visualPlayer.BindBeatSource(owner != null ? owner.BeatSource : null);
            SyncPause();
            if (currentState == null) SetWeaponAnimationState(initialStateId);
        }
        public bool SetWeaponChargeStage(ChargeStage stage) => SetWeaponAnimationState(stage switch
        {
            ChargeStage.HalfCharged => halfChargedStateId,
            ChargeStage.FullCharged => fullChargedStateId,
            _ => unchargedStateId
        });
        public bool SetWeaponAnimationState(string stateId) => SetWeaponAnimationState(stateId, false);
        private bool SetWeaponAnimationState(string stateId, bool force)
        {
            if (!force && currentStateId == stateId && currentState != null) return true;
            var state = animationStates.Find(s => s != null && string.Equals(s.stateId, stateId, StringComparison.Ordinal));
            if (state == null) return false;
            var sequence = state.BuildSequence();
            if (sequence.Frames.Count == 0) return false; // Keep the previous valid state on bad input.
            EnsurePlayer();
            RestoreEffects();
            currentState = state; currentStateId = stateId;
            ApplyVisibility(state);
            visualPlayer.PlayVisualSequence(spriteRenderer, sequence, state.loop);
            SyncPause();
            return true;
        }
        private void ApplyVisibility(AnimationState state)
        {
            var objects = new HashSet<GameObject>();
            foreach (var entry in animationStates)
                if (entry != null) foreach (var go in entry.visibleObjects)
                    if (go != null && go != gameObject && go.transform.IsChildOf(transform)) objects.Add(go);
            foreach (var go in objects)
            {
                bool show = state.visibleObjects.Contains(go);
                if (!show && go.activeSelf)
                    foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                go.SetActive(show);
                if (show && Application.isPlaying && !string.IsNullOrEmpty(state.animatorStateOnEnter))
                    foreach (var animator in go.GetComponentsInChildren<Animator>())
                        if (animator.runtimeAnimatorController != null && animator.HasState(0, Animator.StringToHash(state.animatorStateOnEnter)))
                            animator.Play(state.animatorStateOnEnter, 0, 0);
            }
        }
        private void Update() { SyncPause(); }
        private void SyncPause()
        {
            var fight = ownerAnimator != null ? ownerAnimator.BeatSource : null;
            if (visualPlayer != null && visualPlayer.BeatSource != fight) visualPlayer.BindBeatSource(fight);
            bool paused = ownerAnimator != null && ownerAnimator.PlaybackPaused || fight != null && (fight.IsPaused || fight.TimingCalibrationActive);
            visualPlayer?.SetPlaybackPaused(paused);
            if (paused == effectsPaused) return;
            if (!paused) { RestoreEffects(); return; }
            effectsPaused = true;
            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
                if (ps.isPlaying) { pausedParticles.Add(ps); ps.Pause(false); }
            foreach (var animator in GetComponentsInChildren<Animator>())
            { pausedAnimators[animator] = animator.speed; animator.speed = 0; }
        }
        private void RestoreEffects()
        {
            foreach (var ps in pausedParticles) if (ps != null && ps.gameObject.activeInHierarchy) ps.Play(false);
            pausedParticles.Clear();
            foreach (var entry in pausedAnimators) if (entry.Key != null) entry.Key.speed = entry.Value;
            pausedAnimators.Clear(); effectsPaused = false;
        }
        private void OnDisable() { RestoreEffects(); }
    }
}
