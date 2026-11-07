using System;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>Loop scenery frames on the song grid, without accumulating frame delta time.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class SceneBeatSpriteAnimator : MonoBehaviour
    {
        [Serializable]
        public sealed class Frame
        {
            public Sprite sprite;
            [Min(.001f)] public float durationInBeats = .33f;
        }

        [Tooltip("與戰鬥共用的總節拍時鐘。未指定或歌曲尚未開始時顯示第一張。")]
        [SerializeField] private FmodBeatClock beatClock;
        [SerializeField] private Frame[] frames = Array.Empty<Frame>();
        private SpriteRenderer spriteRenderer;

        private void OnEnable()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            ShowFirstFrame();
        }

        private void Update()
        {
            if (beatClock == null || !beatClock.isActiveAndEnabled || !beatClock.IsPlaying ||
                !beatClock.HasTimingAnchor)
            {
                ShowFirstFrame();
                return;
            }

            // Use the latest FMOD anchor and timeline to support loops longer than one beat too.
            if (!beatClock.TryGetTimelinePositionMs(out int timelineMs) || beatClock.MillisecondsPerBeat <= 0) return;
            double position = beatClock.LatestBeat.GlobalBeat +
                (timelineMs - beatClock.LatestBeat.TimelinePositionMs) / beatClock.MillisecondsPerBeat;
            double total = 0;
            foreach (var frame in frames) total += Duration(frame);
            if (total <= 0) return;
            double cursor = ((position % total) + total) % total;
            foreach (var frame in frames)
            {
                double duration = Duration(frame);
                if (cursor < duration)
                {
                    if (frame.sprite != null) spriteRenderer.sprite = frame.sprite;
                    return;
                }
                cursor -= duration;
            }
        }

        private static double Duration(Frame frame) => frame == null ? 0 :
            Math.Max(.001, Math.Round(frame.durationInBeats, 6));

        private void OnDisable() => ShowFirstFrame();

        private void ShowFirstFrame()
        {
            if (spriteRenderer != null && frames.Length > 0 && frames[0]?.sprite != null)
                spriteRenderer.sprite = frames[0].sprite;
        }
    }
}
