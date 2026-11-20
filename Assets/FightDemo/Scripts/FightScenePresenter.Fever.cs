using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightScenePresenter
    {
        public Sprite FeverEmptyGem => stoneEmptySprite;
        public Sprite FeverFullGem => stoneReadyFrames != null && stoneReadyFrames.Length > 0 ? stoneReadyFrames[0] : stoneFillSprite;
        public Vector2 FeverGemScreenPosition => timelineCenter != null ? RectTransformUtility.WorldToScreenPoint(null, timelineCenter.transform.position) : new Vector2(Screen.width * .5f, 90);
        private CanvasGroup feverHudGroup;
        private float previousFeverHudAlpha;
        private bool feverHudHidden;
        public void SetFeverHudHidden(bool hidden)
        {
            if (playerUiRoot == null || feverHudHidden == hidden) return;
            feverHudHidden = hidden;
            if (!feverHudGroup) feverHudGroup = playerUiRoot.GetComponent<CanvasGroup>();
            if (!feverHudGroup) feverHudGroup = playerUiRoot.gameObject.AddComponent<CanvasGroup>();
            if (hidden) { previousFeverHudAlpha = feverHudGroup.alpha; feverHudGroup.alpha = 0; }
            else feverHudGroup.alpha = previousFeverHudAlpha;
        }
    }
}
