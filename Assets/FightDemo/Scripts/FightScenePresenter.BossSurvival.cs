using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightScenePresenter
    {
        [Header("Boss Phase Two")]
        [SerializeField] private bool showBossSurvivalHud = true;
        private RectTransform bossSurvivalRoot;
        private Image bossSurvivalFill;
        private Text bossSurvivalLabel;
        private void UpdateBossSurvivalHud()
        {
            bool visible = showBossSurvivalHud && fight && fight.BossPhaseTwoActive && !fight.BattleEnded;
            if (!visible) { if (bossSurvivalRoot) bossSurvivalRoot.gameObject.SetActive(false); return; }
            EnsureUiGroups(); if (!playerUiRoot) return;
            if (!bossSurvivalRoot)
            {
                bossSurvivalRoot = new GameObject("BossPhaseTwoHealth", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                bossSurvivalRoot.SetParent(playerUiRoot, false);
                bossSurvivalRoot.anchorMin = bossSurvivalRoot.anchorMax = new Vector2(.5f, 1);
                bossSurvivalRoot.sizeDelta = new Vector2(560, 12); bossSurvivalRoot.anchoredPosition = new Vector2(0, -50);
                var track = bossSurvivalRoot.GetComponent<Image>(); track.color = new Color(.12f, .02f, .02f, .85f); track.raycastTarget = false;
                bossSurvivalFill = new GameObject("Remaining", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                bossSurvivalFill.transform.SetParent(bossSurvivalRoot, false); bossSurvivalFill.raycastTarget = false;
                bossSurvivalFill.color = new Color(.95f, .15f, .08f, 1);
                bossSurvivalLabel = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                bossSurvivalLabel.transform.SetParent(bossSurvivalRoot, false);
                bossSurvivalLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); bossSurvivalLabel.fontSize = 20;
                bossSurvivalLabel.alignment = TextAnchor.MiddleCenter; bossSurvivalLabel.raycastTarget = false;
                bossSurvivalLabel.rectTransform.sizeDelta = new Vector2(600, 34); bossSurvivalLabel.rectTransform.anchoredPosition = new Vector2(0, -27);
            }
            bossSurvivalRoot.gameObject.SetActive(true);
            float amount = Mathf.Clamp01(fight.BossPhaseTwoSecondsRemaining / Mathf.Max(.01f, fight.BossPhaseTwoDurationSeconds));
            var fill = bossSurvivalFill.rectTransform; fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(amount, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            var battle = fight.BossRhythm;
            bossSurvivalLabel.text = battle ? $"GOBLIN KING  ·  BLOCK {battle.Hits}  /  MISS {battle.MissedNotes}" : "GOBLIN KING";
        }
    }
}
