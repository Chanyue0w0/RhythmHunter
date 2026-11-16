using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    // Positions are visual actor-root offsets; party slots and input bindings remain unchanged.
    // Runs before Art2's contact-shadow update so feet and shadows move together.
    [DefaultExecutionOrder(80), DisallowMultipleComponent]
    public sealed class FightBossPhaseTwoTransition : MonoBehaviour
    {
        [SerializeField] private FightCombatController fight;
        [Header("World-space foot destinations")]
        [SerializeField] private Transform bossDestination;
        [Tooltip("Left / middle / right correspond to the current front / middle / rear party members.")]
        [SerializeField] private Transform[] heroDestinations = new Transform[3];
        [Header("Beat movement")]
        [SerializeField, Min(.05f)] private float bossJumpBeats = 4;
        [SerializeField, Min(0)] private float bossJumpHeight = 5;
        [SerializeField, Min(.05f)] private float heroesJumpBeats = 2;
        [SerializeField, Min(0)] private float heroesJumpHeight = 1.5f;
        [SerializeField, Min(.05f)] private float bossDestinationScale = .6f;
        [Header("Existing beat animation names")]
        [SerializeField] private string bossJumpAnimation = "Jump";
        [SerializeField] private string bossIdleAnimation = "IdlePh2";
        [SerializeField] private string heroIdleAnimation = "Idle_back";
        public enum TransitionPhase { Inactive, WaitingForMusic, BossAnimation, BossJump, HeroesJump, Ready }
        public TransitionPhase Phase { get; private set; }
        public bool Ready => Phase == TransitionPhase.Ready;
        public event Action Completed;
        private sealed class Actor
        {
            public FightUnitSlot slot;
            public Transform root, target;
            public Vector3 originalLocal, originalScale, from, footOffset;
            public float scale = 1;
        }
        private readonly List<Actor> bosses = new(), heroes = new();
        private double elapsed;
        private int previousMs;
        private bool hasTime;
        private void OnEnable()
        {
            if (!fight) fight = GetComponent<FightCombatController>();
            if (fight) fight.BossPhaseTwoStarted += Begin;
        }
        private void Begin()
        {
            Restore();
            if (!bossDestination || heroDestinations == null || heroDestinations.Length != 3 ||
                Array.Exists(heroDestinations, t => !t))
            { Debug.LogError("[Boss Phase Two] Assign the Boss and all three hero foot destinations.", this); return; }
            foreach (var slot in fight.RosterManager.ActiveEnemies)
                if (slot && slot.BossPhaseOneProtected) Add(bosses, slot, bossDestination, bossDestinationScale);
            Add(heroes, fight.FrontHero.UnitSlot, heroDestinations[0], 1);
            Add(heroes, fight.SecondHero.UnitSlot, heroDestinations[1], 1);
            Add(heroes, fight.ThirdHero.UnitSlot, heroDestinations[2], 1);
            Phase = TransitionPhase.WaitingForMusic;
            fight.RecoverPartyArmor(FightCharacterDefinition.ArmorRestoreMode.Full);
        }
        private static void Add(List<Actor> list, FightUnitSlot slot, Transform target, float scale)
        {
            if (!slot || !slot.ActorRoot || !slot.ActorInstance) return;
            var root = slot.ActorRoot;
            var foot = slot.ActorInstance.transform.Find("FootAnchor");
            list.Add(new Actor { slot = slot, root = root, target = target, originalLocal = root.localPosition,
                originalScale = root.localScale, from = root.position,
                footOffset = (foot ? foot.position : slot.ActorInstance.transform.position) - root.position, scale = scale });
        }
        private void LateUpdate()
        {
            if (Phase == TransitionPhase.Inactive) return;
            if (!fight || !fight.BossPhaseTwoActive) { Restore(); return; }
            var clock = fight.BeatClock;
            bool paused = fight.IsPaused || fight.TimingCalibrationActive || !clock || !clock.IsPlaying || clock.BossSectionJumpInProgress;
            SetPaused(bosses, paused); SetPaused(heroes, paused);
            if (!clock || !clock.TryGetTimelinePositionMs(out int now)) return;
            double delta = hasTime ? Math.Max(0, now - previousMs) / Math.Max(1d, clock.MillisecondsPerBeat) : 0;
            previousMs = now; hasTime = true;
            if (paused) return;
            if (Phase == TransitionPhase.WaitingForMusic)
            {
                foreach (var actor in bosses)
                {
                    var animator = actor.slot.CombatAnimator;
                    animator?.EndOpeningPresentation();
                    var sequence = animator?.FindSequence(bossJumpAnimation);
                    if (sequence != null && sequence.Frames.Count > 0)
                        animator.PlayVisualSequence(animator.TargetRenderer, sequence, false);
                    else Debug.LogWarning("[Boss Phase Two] Missing Jump animation; proceeding with movement.", actor.slot);
                }
                Phase = TransitionPhase.BossAnimation;
                return;
            }
            if (Phase == TransitionPhase.BossAnimation)
            {
                foreach (var actor in bosses)
                {
                    var animator = actor.slot.CombatAnimator;
                    if (animator && animator.IsPlaying && !animator.VisualSequenceComplete) return;
                }
                elapsed = 0; Phase = TransitionPhase.BossJump;
            }
            else elapsed += delta;
            if (Phase == TransitionPhase.BossJump)
            {
                float t = Mathf.Clamp01((float)(elapsed / Math.Max(.05f, bossJumpBeats)));
                Move(bosses, t, bossJumpHeight);
                if (t < 1) return;
                elapsed = 0; Phase = TransitionPhase.HeroesJump;
            }
            if (Phase == TransitionPhase.HeroesJump)
            {
                Move(bosses, 1, 0);
                float t = Mathf.Clamp01((float)(elapsed / Math.Max(.05f, heroesJumpBeats)));
                Move(heroes, t, heroesJumpHeight);
                if (t < 1) return;
                foreach (var actor in heroes) Idle(actor, heroIdleAnimation);
                foreach (var actor in bosses) Idle(actor, bossIdleAnimation);
                Phase = TransitionPhase.Ready; Completed?.Invoke();
            }
            if (Ready) { Move(bosses, 1, 0); Move(heroes, 1, 0); }
        }
        private static void Idle(Actor actor, string name)
        {
            var animator = actor.slot.CombatAnimator;
            if (!animator) return;
            animator.InterruptPlayback(); animator.EndOpeningPresentation(); animator.SetIdleOverride(name);
        }
        private static void SetPaused(List<Actor> actors, bool paused)
        { foreach (var actor in actors) if (actor.slot) actor.slot.CombatAnimator?.SetPlaybackPaused(paused); }
        private static void Move(List<Actor> actors, float t, float height)
        {
            foreach (var actor in actors)
            {
                if (!actor.root || !actor.target) continue;
                float scale = Mathf.Lerp(1, actor.scale, t);
                Vector3 destination = actor.target.position - actor.footOffset * actor.scale;
                actor.root.localScale = actor.originalScale * scale;
                actor.root.position = Vector3.Lerp(actor.from, destination, Mathf.SmoothStep(0, 1, t)) +
                    Vector3.up * (4 * t * (1 - t) * height);
            }
        }
        private void Restore()
        {
            foreach (var list in new[] { bosses, heroes })
            {
                foreach (var actor in list)
                {
                    if (actor.root) { actor.root.localPosition = actor.originalLocal; actor.root.localScale = actor.originalScale; }
                    if (actor.slot) { actor.slot.CombatAnimator?.SetPlaybackPaused(false); actor.slot.CombatAnimator?.InterruptPlayback(); actor.slot.CombatAnimator?.SetIdleOverride(null); }
                }
                list.Clear();
            }
            Phase = TransitionPhase.Inactive; hasTime = false; elapsed = 0;
        }
        private void OnDisable()
        { if (fight) fight.BossPhaseTwoStarted -= Begin; Restore(); }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            if (bossDestination) Gizmos.DrawWireSphere(bossDestination.position, .3f);
            Gizmos.color = Color.cyan;
            if (heroDestinations != null) foreach (var target in heroDestinations) if (target) Gizmos.DrawWireSphere(target.position, .3f);
        }
    }
}
