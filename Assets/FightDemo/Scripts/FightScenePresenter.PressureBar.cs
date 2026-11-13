using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightScenePresenter
    {
        [Header("Player Pressure Duration Bar")]
        [SerializeField] private bool showPressureDurationBar = true;
        [Tooltip("Normalized screen anchor; independent of the bottom timeline scale.")]
        [SerializeField] private Vector2 pressureBarAnchor = new(.5f, .76f);
        [SerializeField] private Vector2 pressureBarOffset = Vector2.zero;
        [SerializeField] private Vector2 pressureBarSize = new(360, 18);
        [SerializeField] private Vector2 pressureEdgeSize = new(5, 28);
        [SerializeField] private Color pressureEdgeColor = new(1, .65f, .45f, 1);
        [Header("Pressure Reduction Trail")]
        [SerializeField] private Color pressureTrailColor = new(1, .85f, .1f, 1);
        [SerializeField, Min(0)] private float pressureTrailHoldBeats = 2;
        [SerializeField, Min(.01f)] private float pressureTrailFadeBeats = .5f;
        [SerializeField, Min(.01f)] private float pressureReductionSlideBeats = .15f;
        [SerializeField] private Color pressureBarColor = new(1, .18f, .12f, 1);
        [SerializeField] private Color pressureBarEmptyColor = new(.12f, .025f, .025f, .8f);
        [SerializeField] private Vector2 pressureBeatShake = new(0, 2);
        [SerializeField] private Vector2 pressureBreakShake = new(10, 5);
        [SerializeField, Range(.05f, 1)] private float pressureShakeBeats = .4f;
        [Header("Pressure UI Warning (Bar + Icon)")]
        [SerializeField, Min(0), InspectorName("UI Warning Lead Beats")] private float pressureIconLeadBeats = 1;
        [SerializeField, Min(.01f), InspectorName("UI Reveal Beats")] private float pressureIconRevealBeats = .15f;
        private CanvasGroup pressureIconGroup;
        private RectTransform pressureBarRoot;
        private CanvasGroup pressureBarGroup;
        private Image pressureBarIcon;
        private Image pressureTrack, pressureFill, pressureTrail, pressureLeftEdge, pressureRightEdge;
        private float pressureSeenRemoved, pressureTrailDebt, pressureSlideDebt;
        private double pressureTrailStarted, pressureSlideStarted;
        private double pressureVisualBeat, pressureLastSongBeat = double.NaN;
        private float pressureLastPhase;

        private Image CreatePressureImage(string name)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            item.transform.SetParent(pressureBarRoot, false);
            item.raycastTarget = false;
            return item;
        }
        private GameObject pressureBarIconSource;
        private int pressureBarRevision, pressureBarWave = -1;
        private double pressureBreakBeat = double.NegativeInfinity;

        private void BuildPressureBar()
        {
            pressureBarRoot = new GameObject("PlayerPressureDurationBar", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            pressureBarRoot.SetParent(playerUiRoot != null ? playerUiRoot : playerUiCanvas.transform, false);
            pressureBarGroup = pressureBarRoot.GetComponent<CanvasGroup>();
            pressureBarGroup.blocksRaycasts = pressureBarGroup.interactable = false;
            pressureTrack = CreatePressureImage("PressureTrack");
            pressureTrail = CreatePressureImage("PressureReductionTrail_Yellow");
            pressureFill = CreatePressureImage("PressureRemaining");
            pressureLeftEdge = CreatePressureImage("PressureLeftEdge");
            pressureRightEdge = CreatePressureImage("PressureRightEdge");
            pressureBarIcon = new GameObject("PressuredEffectIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            pressureBarIcon.transform.SetParent(pressureBarRoot, false);
            pressureIconGroup = pressureBarIcon.gameObject.AddComponent<CanvasGroup>();
            pressureIconGroup.ignoreParentGroups = false;
            pressureIconGroup.blocksRaycasts = pressureIconGroup.interactable = false;
            pressureBarIcon.raycastTarget = false;
            pressureBarIcon.preserveAspect = true;
            pressureBarIcon.rectTransform.pivot = new Vector2(.5f, 0);
        }

        private void UpdatePressureBar()
        {
            bool timing = TryGetStoneBeat(out double beat);
            RenderPressureBar(beat, timing);
        }

        private void RenderPressureBar(double beat, bool timing)
        {
            if (fight == null || playerUiCanvas == null) return;
            if (pressureBarWave != fight.CurrentWave || fight.PressureBreakRevision < pressureBarRevision)
            {
                pressureBarWave = fight.CurrentWave;
                pressureBarRevision = 0;
                pressureBreakBeat = double.NegativeInfinity;
                pressureSeenRemoved = 0;
                pressureTrailDebt = pressureSlideDebt = 0;
                pressureVisualBeat = 0;
                pressureLastSongBeat = double.NaN;
            }
            bool frozen = fight.IsPaused || fight.TimingCalibrationActive || fight.EnemyActionsPaused || fight.TeamSkillRunning;
            if (timing && !double.IsNaN(pressureLastSongBeat) && !frozen)
                pressureVisualBeat += Math.Max(0, beat - pressureLastSongBeat);
            if (timing) pressureLastSongBeat = beat;
            if (timing && !frozen) pressureLastPhase = (float)(beat - Math.Floor(beat));
            float trailFactor = 1 - Mathf.Clamp01((float)(pressureVisualBeat - pressureTrailStarted - pressureTrailHoldBeats) / Mathf.Max(.01f, pressureTrailFadeBeats));
            float slideFactor = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((float)(pressureVisualBeat - pressureSlideStarted) / Mathf.Max(.01f, pressureReductionSlideBeats)));
            float trail = pressureTrailDebt * trailFactor;
            float slide = pressureSlideDebt * slideFactor;
            if (pressureBarRevision != fight.PressureBreakRevision)
            {
                pressureBarRevision = fight.PressureBreakRevision;
                pressureBreakBeat = fight.PressureLastBreakSongBeat;
                float removed = Mathf.Max(0, fight.PressureRemovedBeats - pressureSeenRemoved) / fight.PressureDurationBeats;
                pressureSeenRemoved = fight.PressureRemovedBeats;
                pressureTrailDebt = trail = Mathf.Clamp01(trail + removed);
                pressureSlideDebt = slide = Mathf.Clamp01(slide + removed);
                pressureTrailStarted = pressureSlideStarted = pressureVisualBeat;
                trailFactor = 1;
            }
            // Keep an emptied bar just long enough to show the final player-caused break.
            bool finishing = fight.PressureRestActive && (trail > .0001f || timing && beat < pressureBreakBeat + pressureShakeBeats);
            double untilStart = fight.PressureNextStartBeat - fight.WaveCombatBeatCount - pressureLastPhase;
            bool uiWarning = fight.PressureWarningActive && untilStart <= pressureIconLeadBeats;
            bool visible = showPressureDurationBar && fight.isActiveAndEnabled && (fight.PressurePhaseActive || uiWarning || finishing);
            if (!visible)
            {
                if (pressureBarRoot != null) pressureBarRoot.gameObject.SetActive(false);
                pressureTrailDebt = pressureSlideDebt = 0;
                return;
            }
            if (pressureBarRoot == null) BuildPressureBar();
            pressureBarRoot.gameObject.SetActive(true);
            pressureBarRoot.anchorMin = pressureBarRoot.anchorMax = pressureBarAnchor;
            pressureBarRoot.sizeDelta = pressureBarSize;
            double phase = pressureLastPhase;
            float ordinaryPulse = fight.PressurePhaseActive ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((float)phase / pressureShakeBeats)) : 0;
            double breakAge = beat - pressureBreakBeat;
            float strongPulse = timing && breakAge >= 0 && breakAge < pressureShakeBeats
                ? Mathf.Sin((float)(breakAge / pressureShakeBeats) * Mathf.PI * 4) * (1 - (float)(breakAge / pressureShakeBeats)) : 0;
            pressureBarRoot.anchoredPosition = pressureBarOffset + pressureBeatShake * ordinaryPulse + pressureBreakShake * strongPulse;
            pressureBarGroup.alpha = 1;
            if (fight.PressureWarningActive)
            {
                pressureBarGroup.alpha = Mathf.Clamp01((pressureIconLeadBeats - (float)untilStart) / Mathf.Max(.01f, pressureIconRevealBeats));
                pressureBarRoot.anchoredPosition = pressureBarOffset;
            }
            float remaining = fight.StageOvertimeActive || fight.PressureWarningActive ? 1 : fight.PressurePhaseActive
                ? Mathf.Clamp01((fight.PressureRemainingExact - (float)phase) / fight.PressureDurationBeats) : 0;
            float fill = Mathf.Clamp01(remaining + slide);
            float ghost = Mathf.Clamp01(remaining + trail);
            pressureTrack.rectTransform.sizeDelta = pressureBarSize;
            pressureTrack.color = pressureBarEmptyColor;
            pressureFill.rectTransform.sizeDelta = new Vector2(pressureBarSize.x * fill, pressureBarSize.y);
            pressureFill.color = pressureBarColor;
            pressureTrail.rectTransform.sizeDelta = new Vector2(pressureBarSize.x * ghost, pressureBarSize.y);
            Color trailColor = pressureTrailColor;
            trailColor.a *= trailFactor;
            pressureTrail.color = trailColor;
            pressureTrail.enabled = trail > .0001f;
            pressureLeftEdge.rectTransform.sizeDelta = pressureRightEdge.rectTransform.sizeDelta = pressureEdgeSize;
            pressureLeftEdge.color = pressureRightEdge.color = pressureEdgeColor;
            pressureLeftEdge.rectTransform.anchoredPosition = Vector2.left * (pressureBarSize.x * fill * .5f);
            pressureRightEdge.rectTransform.anchoredPosition = Vector2.right * (pressureBarSize.x * fill * .5f);
            pressureLeftEdge.enabled = pressureRightEdge.enabled = fill > .0001f;
            if (pressureBarIconSource != pressureStoneIconPrefab)
            {
                pressureBarIconSource = pressureStoneIconPrefab;
                var source = pressureStoneIconPrefab != null ? pressureStoneIconPrefab.GetComponentInChildren<SpriteRenderer>(true) : null;
                pressureBarIcon.sprite = source != null ? source.sprite : null;
                pressureBarIcon.color = source != null ? source.color : Color.white;
            }
            pressureBarIcon.enabled = showPressureStoneIcon && pressureBarIcon.sprite != null;
            pressureIconGroup.alpha = 1;
            float iconPulse = fight.PressurePhaseActive ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((float)phase / pressureStonePulseBeats)) : 0;
            pressureBarIcon.rectTransform.sizeDelta = pressureStoneIconSize;
            pressureBarIcon.rectTransform.anchoredPosition = new Vector2(pressureStoneIconOffset.x,
                pressureBarSize.y * .5f + Mathf.Max(0, pressureStoneIconOffset.y));
            pressureBarIcon.rectTransform.localScale = Vector3.one * (1 + iconPulse * pressureStonePulseAmount);
        }
    }
}
