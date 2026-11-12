using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightScenePresenter
    {
        [Header("Beat Timeline Artwork")]
        [Tooltip("整組節拍器縮放，包含底條、拍點與中央石頭。")]
        [SerializeField, Min(.1f)] private float timelineScale = 1;
        [SerializeField] private Sprite timelineBarSprite;
        [Tooltip("只控制節拍器底圖；拍點、中央石頭與判定保持運作。")]
        [SerializeField] private bool showTimelineBar = true;
        [SerializeField] private Sprite timelinePointSprite;
        [Tooltip("密集攻擊期間的拍點圖片；結束後還原。未指定時使用紅色染色。")]
        [SerializeField] private Sprite pressureTimelinePointSprite;
        [SerializeField] private Sprite stoneEmptySprite;
        [SerializeField] private Sprite stoneFillSprite;
        [SerializeField] private Sprite[] stoneReadyFrames;
        [SerializeField] private Vector2 timelinePointArtSize = new(6, 34);
        [SerializeField] private Vector2 stoneArtSize = new(70, 90);
        [Tooltip("底條高度，0 表示依原圖比例隨 Timeline Width 縮放。")]
        [SerializeField, Min(0)] private float timelineBarHeight;
        [SerializeField, Min(.05f)] private float stoneReadyLoopBeats = 1;
        [Header("Successful Beat Stone Feedback")]
        [UnityEngine.Serialization.FormerlySerializedAs("stoneShakeEnabled")]
        [SerializeField] private bool stoneBounceEnabled = true;
        [Tooltip("成功時最大放大比例；0.15 表示放大到 115%。")]
        [SerializeField, Range(0, .5f)] private float stoneBounceAmount = .15f;
        [UnityEngine.Serialization.FormerlySerializedAs("stoneShakeBeats")]
        [SerializeField, Range(.05f, 1)] private float stoneBounceBeats = .3f;
        [Tooltip("Success-only effect behind the stone. Particle prefabs use a reusable UI rendering stage; RectTransform prefabs use the UI anchor.")]
        [SerializeField] private GameObject stoneSuccessEffectPrefab;
        [Tooltip("Seconds until the UI effect is removed. Zero lets the prefab manage its own lifetime.")]
        [SerializeField, Min(0)] private float stoneSuccessEffectLifetime = .5f;
        [SerializeField] private Vector2 stoneSuccessEffectSize = new(240, 240);
        [SerializeField, Min(.1f)] private float stoneSuccessEffectViewSize = 1.4f;
        private FightUiParticleBurst stoneParticleBurst;
        [Header("Accepted Beat Point Feedback")]
        [SerializeField] private bool removeAcceptedBeatPoints = true;
        [Tooltip("拍點消除特效：UI RectTransform Prefab。早按時在左右拍點位置生成；拍點已抵達時在中央生成。")]
        [SerializeField] private GameObject beatPointHitEffectPrefab;
        [SerializeField, Min(0)] private float beatPointHitEffectLifetime = .5f;
        private readonly HashSet<long> consumedTimelineTicks = new();
        private readonly Queue<long> consumedTimelineOrder = new();
        private long[] timelinePointTicks;
        private Image stoneFillImage, timelineTrack;
        private RectTransform stoneEffectAnchor;
        private double stoneHitTimelineMs = double.NegativeInfinity;
        private double stoneHitDurationMs = 150;
        private bool HasTimelineArt => timelineBarSprite != null && timelinePointSprite != null &&
            stoneEmptySprite != null && stoneFillSprite != null;

        private void BuildTimelineArtwork()
        {
            if (!HasTimelineArt) return;
            timelineTrack.sprite = timelineBarSprite;
            timelineTrack.enabled = showTimelineBar;
            timelineTrack.color = Color.white;
            timelineTrack.rectTransform.sizeDelta = new Vector2(timelineWidth,
                timelineWidth * timelineBarSprite.rect.height / timelineBarSprite.rect.width);
            timelineFrame.enabled = false;
            timelineCenter.sprite = stoneEmptySprite;
            timelineCenter.color = Color.white;
            timelineCenter.rectTransform.sizeDelta = stoneArtSize;
            timelineCenter.transform.SetAsLastSibling();
            stoneFillImage = new GameObject("StoneTeamSkillFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            stoneFillImage.transform.SetParent(timelineCenter.transform, false);
            stoneFillImage.rectTransform.anchorMin = Vector2.zero;
            stoneFillImage.rectTransform.anchorMax = Vector2.one;
            stoneFillImage.rectTransform.sizeDelta = Vector2.zero;
            stoneFillImage.sprite = stoneFillSprite;
            stoneFillImage.raycastTarget = false;
            stoneFillImage.type = Image.Type.Filled;
            stoneFillImage.fillMethod = Image.FillMethod.Vertical;
            stoneFillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            stoneEffectAnchor = new GameObject("StoneSuccessEffectAnchor", typeof(RectTransform)).GetComponent<RectTransform>();
            stoneEffectAnchor.SetParent(timelineCenter.transform.parent, false);
            stoneEffectAnchor.anchorMin = timelineCenter.rectTransform.anchorMin;
            stoneEffectAnchor.anchorMax = timelineCenter.rectTransform.anchorMax;
            stoneEffectAnchor.anchoredPosition = timelineCenter.rectTransform.anchoredPosition;
            stoneEffectAnchor.SetSiblingIndex(timelineCenter.transform.GetSiblingIndex());
            stoneEffectAnchor.sizeDelta = stoneArtSize;
            if (stoneSuccessEffectPrefab != null && stoneSuccessEffectPrefab.GetComponentInChildren<ParticleSystem>(true) != null)
            {
                stoneParticleBurst = stoneEffectAnchor.gameObject.AddComponent<FightUiParticleBurst>();
                stoneParticleBurst.Initialize(stoneSuccessEffectPrefab, stoneSuccessEffectSize, stoneSuccessEffectViewSize, stoneSuccessEffectLifetime);
            }
            foreach (var point in approachingPoints)
            {
                point.sprite = timelinePointSprite;
                point.rectTransform.sizeDelta = timelinePointArtSize;
                point.rectTransform.localRotation = Quaternion.identity;
            }
            UpdateTimelineArtwork();
        }

        private bool TryGetStoneBeat(out double beat)
        {
            beat = 0;
            if (beatClock == null || !beatClock.IsPlaying || !beatClock.HasTimingAnchor || beatClock.MillisecondsPerBeat <= 0 ||
                !beatClock.TryGetTimelinePositionMs(out int ms)) return false;
            beat = beatClock.LatestBeat.GlobalBeat + (ms - beatClock.LatestBeat.TimelinePositionMs) / beatClock.MillisecondsPerBeat;
            return true;
        }

        private void UpdateTimelineArtwork()
        {
            if (stoneFillImage == null || fight == null) return;
            timelineRoot.anchoredPosition = new Vector2(0, timelineBottomOffset);
            timelineTrack.enabled = showTimelineBar;
            timelineRoot.localScale = Vector3.one * Mathf.Max(.1f, timelineScale);
            timelineRoot.sizeDelta = new Vector2(timelineWidth, 64);
            timelineTrack.rectTransform.sizeDelta = new Vector2(timelineWidth, timelineBarHeight > 0 ? timelineBarHeight :
                timelineWidth * timelineBarSprite.rect.height / timelineBarSprite.rect.width);
            timelineCenter.rectTransform.sizeDelta = stoneArtSize;
            stoneEffectAnchor.sizeDelta = stoneParticleBurst != null ? stoneSuccessEffectSize : stoneArtSize;
            foreach (var point in approachingPoints) point.rectTransform.sizeDelta = timelinePointArtSize;
            float fill = Mathf.Clamp01((float)fight.TeamSkillGauge / Mathf.Max(1, fight.TeamSkillGaugeMax));
            bool timing = TryGetStoneBeat(out double beat);
            RenderStoneArtwork(fill, beat, timing);
            if (beatClock != null && beatClock.TryGetTimelinePositionMs(out int timelineMs))
                RenderStoneBounce(Mathf.Max(0, (float)(timelineMs - stoneHitTimelineMs)) / stoneHitDurationMs);
            else
                RenderStoneBounce(double.PositiveInfinity);
        }

        private void RenderStoneArtwork(float fill, double beat, bool timing)
        {
            bool animated = fill >= 1 && stoneReadyFrames != null && stoneReadyFrames.Length > 0;
            int frame = 0;
            if (animated && timing)
            {
                double phase = beat / Mathf.Max(.05f, stoneReadyLoopBeats);
                frame = Mathf.Min(stoneReadyFrames.Length - 1, (int)((phase - System.Math.Floor(phase)) * stoneReadyFrames.Length));
            }
            timelineCenter.sprite = animated && stoneReadyFrames[frame] != null ? stoneReadyFrames[frame] : stoneEmptySprite;
            timelineCenter.color = Color.white;
            timelineCenter.rectTransform.localScale = Vector3.one;
            stoneFillImage.enabled = !animated;
            stoneFillImage.fillAmount = fill;
        }

        private void RenderStoneBounce(double progress)
        {
            float bounce = 0;
            if (stoneBounceEnabled && progress >= 0 && progress < 1)
            {
                float p = (float)progress;
                // Fast expansion, a small undershoot, then return to the authored size.
                bounce = p < .35f
                    ? Mathf.Lerp(.5f, 1, Mathf.Sin(p / .35f * Mathf.PI * .5f))
                    : p < .75f
                        ? Mathf.Lerp(1, -.2f, Mathf.SmoothStep(0, 1, (p - .35f) / .4f))
                        : Mathf.Lerp(-.2f, 0, Mathf.SmoothStep(0, 1, (p - .75f) / .25f));
            }
            timelineCenter.rectTransform.localScale = Vector3.one * (1 + bounce * stoneBounceAmount);
            timelineCenter.rectTransform.anchoredPosition = Vector2.zero;
        }

        private void OnSuccessfulStoneBeat()
        {
            if (stoneFillImage == null || fight == null || fight.IsPaused || beatClock == null ||
                !beatClock.TryGetTimelinePositionMs(out int timelineMs)) return;
            stoneHitTimelineMs = timelineMs;
            stoneHitDurationMs = System.Math.Max(1, beatClock.MillisecondsPerBeat * stoneBounceBeats);
            // Give immediate visible feedback even when several successful calls share a frame.
            RenderStoneBounce(.15);
            if (stoneSuccessEffectPrefab == null || stoneEffectAnchor == null) return;
            if (stoneParticleBurst != null) { stoneParticleBurst.Play(); return; }
            var effect = Instantiate(stoneSuccessEffectPrefab, stoneEffectAnchor, false);
            effect.SetActive(true);
            if (stoneSuccessEffectLifetime > 0) Destroy(effect, stoneSuccessEffectLifetime);
        }

        private void OnTeamSkillPointAccepted(double acceptedBeat) =>
            ConsumeTimelinePoint((long)System.Math.Round(acceptedBeat * 2, System.MidpointRounding.AwayFromZero));

        private void ResetConsumedTimelinePoints()
        {
            stoneHitTimelineMs = double.NegativeInfinity;
            consumedTimelineTicks.Clear();
            consumedTimelineOrder.Clear();
        }

        private float TimelinePointDistance(int index, float phase, int count, bool burst)
        {
            return (index + 1f - phase) / count * timelineWidth * .5f;
        }

        private void ConsumeTimelinePoint(long halfBeatTick)
        {
            if (!consumedTimelineTicks.Add(halfBeatTick)) return;
            consumedTimelineOrder.Enqueue(halfBeatTick);
            while (consumedTimelineOrder.Count > 64) consumedTimelineTicks.Remove(consumedTimelineOrder.Dequeue());
            if (timelineRoot == null || approachingPoints == null) return;
            bool found = false;
            for (int i = 0; timelinePointTicks != null && i < approachingPoints.Length; i++)
            {
                if (timelinePointTicks[i] != halfBeatTick || !approachingPoints[i].enabled) continue;
                found = true;
                SpawnBeatPointHit(approachingPoints[i].rectTransform.anchoredPosition);
                if (removeAcceptedBeatPoints) approachingPoints[i].enabled = false;
            }
            // Late accepted input refers to a beat that has already left the scrolling pool.
            if (!found) SpawnBeatPointHit(Vector2.zero);
        }

        private void SpawnBeatPointHit(Vector2 position)
        {
            if (beatPointHitEffectPrefab == null) return;
            var effect = Instantiate(beatPointHitEffectPrefab, timelineRoot, false);
            if (effect.transform is RectTransform rect)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
                rect.anchoredPosition = position;
            }
            effect.SetActive(true);
            if (beatPointHitEffectLifetime > 0) Destroy(effect, beatPointHitEffectLifetime);
        }
    }
}
