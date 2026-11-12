using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightScenePresenter
    {
        private RectTransform teamSkillPanel;
        private Text teamSkillLabel;
        private Image teamSkillFill;
        private RectTransform burstPanel;
        private Image burstFill;
        private Text burstLabel;
        private void UpdateTeamSkillHud()
        {
            EnsureUiGroups();
            UpdateBurstHud();
            if (!EqualBeats || !showTeamSkillGauge)
            {
                if (teamSkillPanel != null) teamSkillPanel.gameObject.SetActive(false);
                if (ultimateFlash != null) ultimateFlash.color = Color.clear;
                return;
            }
            if (teamSkillPanel == null)
            {
                if (healthText == null || playerUiCanvas == null) return;
                teamSkillPanel = FightMenuUi.Rect("TeamSkillGauge", playerUiCanvas.transform, new Vector2(-24, -286), new Vector2(540, 144));
                EnsureUiGroups();
                if (developerUiRoot != null) teamSkillPanel.SetParent(developerUiRoot, false);
                teamSkillPanel.anchorMin = teamSkillPanel.anchorMax = teamSkillPanel.pivot = Vector2.one;
                teamSkillPanel.gameObject.AddComponent<Image>().color = new Color(.01f, .04f, .06f, .88f);
                teamSkillLabel = FightMenuUi.Label(teamSkillPanel, "", new Vector2(12, -4), new Vector2(516, 56), healthText.font, 16);
                var track = FightMenuUi.Rect("Track", teamSkillPanel, new Vector2(12, -66), new Vector2(516, 8));
                track.gameObject.AddComponent<Image>().color = Dim;
                var fill = FightMenuUi.Rect("Fill", track, Vector2.zero, new Vector2(516, 8));
                teamSkillFill = fill.gameObject.AddComponent<Image>();
                foreach (var graphic in teamSkillPanel.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            }
            teamSkillPanel.gameObject.SetActive(true);
            UpdateTeamUltimateHud();
            string title = fight.TeamSkillRunning ? $"TEAM SKILL — {fight.TeamSkillBeatsRemaining} BEATS"
                : fight.TeamSkillReady ? $"TEAM SKILL READY  [A / R]  MP {fight.TeamSkillGaugeMax}"
                : $"TEAM MANA  {fight.TeamSkillGaugeExact:0.#} / {fight.TeamSkillGaugeMax}";
            teamSkillLabel.text = title + "\n" + (fight.TeamUltimateRunning ? "Waiting for Ultimate performance" : fight.TeamSkillStatus);
            teamSkillFill.color = fight.TeamSkillReady ? Gold : Cyan;
            teamSkillFill.rectTransform.localScale = new Vector3(fight.TeamSkillGaugeExact / Mathf.Max(1, fight.TeamSkillGaugeMax), 1, 1);
        }

        private void UpdateBurstHud()
        {
            bool visible = EqualBeats && fight != null && fight.TeamSkillBurstActive;
            if (burstPanel == null && visible)
            {
                if (healthText == null || playerUiCanvas == null) return;
                burstPanel = FightMenuUi.Rect("TeamSkillRemaining", playerUiCanvas.transform,
                    new Vector2(0, -48), new Vector2(460, 64));
                EnsureUiGroups();
                if (playerUiRoot != null) burstPanel.SetParent(playerUiRoot, false);
                burstPanel.anchorMin = burstPanel.anchorMax = burstPanel.pivot = new Vector2(.5f, 1);
                burstLabel = FightMenuUi.Label(burstPanel, "", new Vector2(0, -4), new Vector2(460, 30), healthText.font, 20);
                burstLabel.alignment = TextAnchor.MiddleCenter;
                burstLabel.color = Gold;
                var track = FightMenuUi.Rect("Track", burstPanel, new Vector2(0, -40), new Vector2(460, 14));
                track.gameObject.AddComponent<Image>().color = new Color(.15f, .1f, .02f, .9f);
                var fill = FightMenuUi.Rect("GoldRemaining", track, Vector2.zero, new Vector2(460, 14));
                burstFill = fill.gameObject.AddComponent<Image>();
                burstFill.color = Gold;
                foreach (var graphic in burstPanel.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            }
            if (burstPanel == null) return;
            burstPanel.gameObject.SetActive(visible);
            if (!visible) return;
            burstFill.rectTransform.localScale = new Vector3(fight.TeamSkillBurstProgress, 1, 1);
            burstLabel.text = $"TEAM SKILL  {fight.TeamSkillBurstRemaining:0.0} BEATS  [A / R]";
        }
    }
}
