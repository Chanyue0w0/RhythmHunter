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
        [SerializeField, Min(0)] private float pressureBarGap = 5;
        [SerializeField] private Color pressureBarColor = new(1, .18f, .12f, 1);
        [SerializeField] private Color pressureBarEmptyColor = new(.12f, .025f, .025f, .8f);
        [SerializeField, Range(0, 1)] private float pressureWarningOpacity = .6f;
        [SerializeField, Range(.01f, 1)] private float pressureRevealBeats = .2f;
        [SerializeField] private Vector2 pressureBeatShake = new(0, 2);
        [SerializeField] private Vector2 pressureBreakShake = new(10, 5);
        [SerializeField, Range(.05f, 1)] private float pressureShakeBeats = .4f;
        private RectTransform pressureBarRoot;
        private CanvasGroup pressureBarGroup;
        private Image pressureBarIcon;
        private readonly Image[] pressureBarCells = new Image[10];
        private GameObject pressureBarIconSource;
        private int pressureBarRevision, pressureBarWave = -1;
        private double pressureBreakBeat = double.NegativeInfinity;

        private void BuildPressureBar()
        {
            pressureBarRoot = new GameObject("PlayerPressureDurationBar", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            pressureBarRoot.SetParent(playerUiRoot != null ? playerUiRoot : playerUiCanvas.transform, false);
            pressureBarGroup = pressureBarRoot.GetComponent<CanvasGroup>();
            pressureBarGroup.blocksRaycasts = pressureBarGroup.interactable = false;
            for (int i = 0; i < pressureBarCells.Length; i++)
            {
                var cell = new GameObject($"PressureCell_{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                cell.transform.SetParent(pressureBarRoot, false);
                cell.raycastTarget = false;
                pressureBarCells[i] = cell;
            }
            pressureBarIcon = new GameObject("PressuredEffectIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            pressureBarIcon.transform.SetParent(pressureBarRoot, false);
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
            }
            if (pressureBarRevision != fight.PressureBreakRevision)
            {
                pressureBarRevision = fight.PressureBreakRevision;
                pressureBreakBeat = fight.PressureLastBreakSongBeat;
            }
            // Keep an emptied bar just long enough to show the final player-caused break.
            bool finishing = fight.PressureRestActive && timing && beat < pressureBreakBeat + pressureShakeBeats;
            bool visible = showPressureDurationBar && fight.isActiveAndEnabled && (fight.PressureVisualsActive || finishing);
            if (!visible)
            {
                if (pressureBarRoot != null) pressureBarRoot.gameObject.SetActive(false);
                return;
            }
            if (pressureBarRoot == null) BuildPressureBar();
            pressureBarRoot.gameObject.SetActive(true);
            pressureBarRoot.anchorMin = pressureBarRoot.anchorMax = pressureBarAnchor;
            pressureBarRoot.sizeDelta = pressureBarSize;
            double phase = timing ? beat - Math.Floor(beat) : 1;
            float ordinaryPulse = fight.PressurePhaseActive ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((float)phase / pressureShakeBeats)) : 0;
            double breakAge = beat - pressureBreakBeat;
            float strongPulse = timing && breakAge >= 0 && breakAge < pressureShakeBeats
                ? Mathf.Sin((float)(breakAge / pressureShakeBeats) * Mathf.PI * 4) * (1 - (float)(breakAge / pressureShakeBeats)) : 0;
            pressureBarRoot.anchoredPosition = pressureBarOffset + pressureBeatShake * ordinaryPulse + pressureBreakShake * strongPulse;
            pressureBarGroup.alpha = 1;
            if (fight.PressureWarningActive)
            {
                double remaining = fight.PressureNextStartBeat - fight.WaveCombatBeatCount - phase;
                float progress = Mathf.Clamp01(1 - (float)remaining / Mathf.Max(1, fight.PressureWarningLeadBeats));
                pressureBarGroup.alpha = remaining <= pressureRevealBeats
                    ? Mathf.Lerp(pressureWarningOpacity, 1, 1 - (float)remaining / pressureRevealBeats)
                    : pressureWarningOpacity * progress;
                pressureBarRoot.anchoredPosition = pressureBarOffset;
            }
            float width = Mathf.Max(1, (pressureBarSize.x - pressureBarGap * 9) / 10);
            int pairs = finishing ? 0 : fight.PressureVisiblePairs;
            for (int i = 0; i < 10; i++)
            {
                pressureBarCells[i].rectTransform.sizeDelta = new Vector2(width, pressureBarSize.y);
                pressureBarCells[i].rectTransform.anchoredPosition = new Vector2((i - 4.5f) * (width + pressureBarGap), 0);
                pressureBarCells[i].color = i >= 5 - pairs && i < 5 + pairs ? pressureBarColor : pressureBarEmptyColor;
            }
            if (pressureBarIconSource != pressureStoneIconPrefab)
            {
                pressureBarIconSource = pressureStoneIconPrefab;
                var source = pressureStoneIconPrefab != null ? pressureStoneIconPrefab.GetComponentInChildren<SpriteRenderer>(true) : null;
                pressureBarIcon.sprite = source != null ? source.sprite : null;
                pressureBarIcon.color = source != null ? source.color : Color.white;
            }
            pressureBarIcon.enabled = showPressureStoneIcon && pressureBarIcon.sprite != null;
            float iconPulse = fight.PressurePhaseActive ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((float)phase / pressureStonePulseBeats)) : 0;
            pressureBarIcon.rectTransform.sizeDelta = pressureStoneIconSize;
            pressureBarIcon.rectTransform.anchoredPosition = pressureStoneIconOffset + Vector2.up *
                (pressureBarSize.y * .5f + iconPulse * pressureStoneJumpHeight);
            pressureBarIcon.rectTransform.localScale = Vector3.one * (1 + iconPulse * pressureStonePulseAmount);
        }
    }
}
