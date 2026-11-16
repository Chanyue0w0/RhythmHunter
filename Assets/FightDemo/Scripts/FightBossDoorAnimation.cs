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
        private Sprite closedSprite;
        public bool HasOpeningFrames => openingFrames.Count > 0;
        private void Awake() { if (doorRenderer != null) closedSprite = doorRenderer.sprite; }
        public void Open(FightCombatController fight, float beats)
        {
            if (!doorRenderer || !animator || openingFrames.Count == 0) return;
            animator.BindBeatSource(fight);
            var sequence = new FightCharacterCombatAnimator.Sequence();
            sequence.Configure(FightCharacterCombatAnimator.CombatAnimation.NormalAttack, openingFrames, 16,-1,-1,-1);
            var durations = new float[openingFrames.Count];
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
