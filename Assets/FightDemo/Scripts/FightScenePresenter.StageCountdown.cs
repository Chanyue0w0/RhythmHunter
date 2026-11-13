using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightScenePresenter
    {
        [Header("Player Stage Countdown Line")]
        [SerializeField] private bool showStageCountdownLine = true;
        [Tooltip("Height in Canvas reference-resolution units.")]
        [SerializeField, Min(1)] private float stageCountdownHeight = 10;
        [SerializeField, Min(0)] private float stageCountdownBottomOffset;
        [SerializeField] private Color stageCountdownColor = new(.8f, .9f, 1, .95f);
        [SerializeField] private Color stageCountdownTrackColor = new(.1f, .15f, .2f, .3f);
        private RectTransform stageCountdownRoot;
        private Image stageCountdownFill, stageCountdownTrack;

        private void UpdateStageCountdownLine()
        {
            bool visible = showStageCountdownLine && fight != null && fight.isActiveAndEnabled &&
                fight.StageDeadlineEnabled && !fight.AwaitingBattleStart && !fight.BattleEnded && fight.StageTimeLimitSeconds > 0;
            if (!visible)
            {
                if (stageCountdownRoot != null) stageCountdownRoot.gameObject.SetActive(false);
                return;
            }
            EnsureUiGroups();
            if (playerUiRoot == null) return;
            if (stageCountdownRoot == null)
            {
                stageCountdownTrack = new GameObject("PlayerStageCountdownLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                stageCountdownRoot = stageCountdownTrack.rectTransform;
                stageCountdownRoot.SetParent(playerUiRoot, false);
                stageCountdownTrack.raycastTarget = false;
                stageCountdownFill = new GameObject("RemainingTime", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                stageCountdownFill.transform.SetParent(stageCountdownRoot, false);
                stageCountdownFill.raycastTarget = false;
            }
            stageCountdownRoot.gameObject.SetActive(true);
            // Stretch over the complete player Canvas, independent of the beat timeline scale.
            stageCountdownRoot.anchorMin = new Vector2(0, 0);
            stageCountdownRoot.anchorMax = new Vector2(1, 0);
            stageCountdownRoot.pivot = new Vector2(.5f, 0);
            stageCountdownRoot.anchoredPosition = new Vector2(0, Mathf.Max(0, stageCountdownBottomOffset));
            stageCountdownRoot.sizeDelta = new Vector2(0, Mathf.Max(1, stageCountdownHeight));
            stageCountdownTrack.color = stageCountdownTrackColor;
            float remaining = fight.StageTimeExpired ? 0 : Mathf.Clamp01(fight.StageSecondsRemaining / fight.StageTimeLimitSeconds);
            float inset = (1 - remaining) * .5f;
            var fill = stageCountdownFill.rectTransform;
            fill.anchorMin = new Vector2(inset, 0);
            fill.anchorMax = new Vector2(1 - inset, 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            stageCountdownFill.color = stageCountdownColor;
            stageCountdownFill.enabled = remaining > 0;
        }
    }
}
