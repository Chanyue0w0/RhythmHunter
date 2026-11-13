using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightScenePresenter
    {
        [Header("Defeat Stone / Beat Presentation")]
        [SerializeField, Min(0)] private float defeatStoneJumpHeight = 65;
        [SerializeField, Min(.01f)] private float defeatStoneJumpBeats = .3f;
        [SerializeField, Min(.01f)] private float defeatStoneFallBeats = 1.2f;
        [SerializeField, Min(.01f)] private float defeatBeatFadeBeats = .5f;
        private bool defeatUiStarted, defeatStoneFalls;
        private float defeatUiElapsed, defeatUiSecondsPerBeat, defeatStoneExitY;
        private Vector2 defeatStoneOrigin;
        private Color[] defeatPointColors;

        private void BeginDefeatUi()
        {
            if (defeatUiStarted) return;
            defeatUiStarted = true;
            defeatStoneFalls = fight != null && fight.PartyHp <= 0;
            defeatUiElapsed = 0;
            defeatUiSecondsPerBeat = beatClock == null ? .5f : (float)(beatClock.MillisecondsPerBeat > 0
                ? beatClock.MillisecondsPerBeat / 1000d : beatClock.OpeningSecondsPerBeat);
            if (beatClock != null) defeatUiSecondsPerBeat /= Mathf.Max(.01f, beatClock.MusicPitch);
            defeatUiSecondsPerBeat = Mathf.Max(.01f, defeatUiSecondsPerBeat);
            if (approachingPoints != null)
            {
                defeatPointColors = new Color[approachingPoints.Length];
                for (int i = 0; i < approachingPoints.Length; i++) defeatPointColors[i] = approachingPoints[i].color;
            }
            if (timelineCenter == null) return;
            var stone = timelineCenter.rectTransform;
            defeatStoneOrigin = stone.anchoredPosition;
            // Convert the Canvas bottom to the stone parent's coordinates, preserving UI scale.
            var corners = new Vector3[4];
            ((RectTransform)playerUiCanvas.transform).GetWorldCorners(corners);
            float bottom = stone.parent.InverseTransformPoint(corners[0]).y;
            defeatStoneExitY = defeatStoneOrigin.y + bottom - stone.localPosition.y - stone.rect.height * 2 - 30;
        }

        private void AdvanceDefeatUi(float seconds)
        {
            defeatUiElapsed += Mathf.Max(0, seconds);
            float beats = defeatUiElapsed / defeatUiSecondsPerBeat;
            if (defeatPointColors != null)
                for (int i = 0; i < defeatPointColors.Length; i++)
                {
                    Color color = defeatPointColors[i];
                    color.a *= 1 - Mathf.Clamp01(beats / defeatBeatFadeBeats);
                    approachingPoints[i].color = color;
                }
            if (!defeatStoneFalls || timelineCenter == null) return;
            float y;
            if (beats < defeatStoneJumpBeats)
            {
                float t = Mathf.Clamp01(beats / defeatStoneJumpBeats);
                y = defeatStoneOrigin.y + defeatStoneJumpHeight * (1 - (1 - t) * (1 - t));
            }
            else
            {
                float t = Mathf.Clamp01((beats - defeatStoneJumpBeats) / defeatStoneFallBeats);
                y = Mathf.Lerp(defeatStoneOrigin.y + defeatStoneJumpHeight, defeatStoneExitY, t * t);
            }
            timelineCenter.rectTransform.anchoredPosition = new Vector2(defeatStoneOrigin.x, y);
            if (stoneEffectAnchor != null) stoneEffectAnchor.anchoredPosition = timelineCenter.rectTransform.anchoredPosition;
        }

        public void HideBattleUiAfterDefeat()
        {
            if (playerUiCanvas != null) playerUiCanvas.gameObject.SetActive(false);
        }
    }
}
