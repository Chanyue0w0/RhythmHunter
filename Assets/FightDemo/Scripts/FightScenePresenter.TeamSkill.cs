using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightScenePresenter
    {
        private RectTransform teamSkillPanel;
        private Text teamSkillLabel;
        private Image teamSkillFill;
        private void UpdateTeamSkillHud()
        {
            if (!EqualBeats)
            {
                if (teamSkillPanel != null) teamSkillPanel.gameObject.SetActive(false);
                if (ultimateFlash != null) ultimateFlash.color = Color.clear;
                return;
            }
            if (teamSkillPanel == null)
            {
                if (healthText == null || healthText.canvas == null) return;
                teamSkillPanel = FightMenuUi.Rect("TeamSkillGauge", healthText.canvas.transform, new Vector2(-24, -286), new Vector2(540, 144));
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
                : $"TEAM MANA  {fight.TeamSkillGauge} / {fight.TeamSkillGaugeMax}";
            teamSkillLabel.text = title + "\n" + (fight.TeamUltimateRunning ? "Waiting for Ultimate performance" : fight.TeamSkillStatus);
            teamSkillFill.color = fight.TeamSkillReady ? Gold : Cyan;
            teamSkillFill.rectTransform.localScale = new Vector3((float)fight.TeamSkillGauge / Mathf.Max(1, fight.TeamSkillGaugeMax), 1, 1);
        }
    }
}
