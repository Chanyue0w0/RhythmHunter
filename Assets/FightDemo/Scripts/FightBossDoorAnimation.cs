using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    [DisallowMultipleComponent]
    public sealed class FightBossDoorAnimation : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer doorRenderer;
        [SerializeField] private FightCharacterCombatAnimator animator;
        [Tooltip("Opening sprites, in order. The last sprite stays visible. Empty keeps the current artwork until supplied.")]
        [SerializeField] private List<Sprite> openingFrames = new();
        [Tooltip("Empty reverses the opening frames and ends on the original closed sprite.")]
        [SerializeField] private List<Sprite> closingFrames = new();
        private Sprite closedSprite;
        public bool HasOpeningFrames => openingFrames.Count > 0;
        private void Awake() { if (doorRenderer != null) closedSprite = doorRenderer.sprite; }
        public void Open(FightCombatController fight, float beats)
        { Play(fight, openingFrames, beats); }
        public void Close(FightCombatController fight, float beats)
        {
            var frames = new List<Sprite>(closingFrames);
            if(frames.Count == 0) { frames.AddRange(openingFrames); frames.Reverse(); }
            if(closedSprite && (frames.Count == 0 || frames[frames.Count-1] != closedSprite)) frames.Add(closedSprite);
            Play(fight, frames, beats);
        }
        private void Play(FightCombatController fight, List<Sprite> frames, float beats)
        {
            if (!doorRenderer || !animator || frames.Count == 0) return;
            animator.BindBeatSource(fight);
            var sequence = new FightCharacterCombatAnimator.Sequence();
            sequence.Configure(FightCharacterCombatAnimator.CombatAnimation.NormalAttack, frames, 16,-1,-1,-1);
            var durations = new float[frames.Count];
            for(int i=0;i<durations.Length;i++) durations[i]=Mathf.Max(.01f,beats)/durations.Length;
            sequence.ConfigureFrameBeats(durations);
            animator.PlayVisualSequence(doorRenderer,sequence,false);
        }
        public void SetPaused(bool paused) { if(animator) animator.SetPlaybackPaused(paused); }
        public void ResetDoor()
        {
            if(animator) { animator.SetPlaybackPaused(false); animator.InterruptPlayback(); }
            if(doorRenderer && closedSprite) doorRenderer.sprite=closedSprite;
        }
    }
}
