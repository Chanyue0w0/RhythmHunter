using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightScenePresenter
    {
        private readonly List<Image> ultimateChargeIcons = new();
        private Text ultimateLabel;
        private Image ultimateFlash;
        private int shownUltimateActivation, shownUltimateImpact;
        private float ultimateFlashTime;

        private void UpdateTeamUltimateHud()
        {
            if (ultimateLabel == null)
            {
                ultimateLabel = FightMenuUi.Label(teamSkillPanel, "", new Vector2(92, -86), new Vector2(432, 48), healthText.font, 14);
                var overlay = FightMenuUi.Rect("TeamUltimateFlash", healthText.canvas.transform, Vector2.zero, Vector2.zero);
                overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
                overlay.offsetMin = overlay.offsetMax = Vector2.zero; overlay.SetAsFirstSibling();
                ultimateFlash = overlay.gameObject.AddComponent<Image>(); ultimateFlash.raycastTarget = false;
                ultimateFlash.color = Color.clear;
            }
            if (ultimateChargeIcons.Count != fight.TeamUltimateRequiredCharges)
            {
                foreach (var icon in ultimateChargeIcons) Destroy(icon.gameObject);
                ultimateChargeIcons.Clear();
                for (int i = 0; i < fight.TeamUltimateRequiredCharges; i++)
                {
                    var rect = FightMenuUi.Rect("UltimateCharge" + i, teamSkillPanel, new Vector2(22 + 20 * i, -110), new Vector2(10, 10));
                    rect.pivot = new Vector2(.5f, .5f); rect.localRotation = Quaternion.Euler(0, 0, 45);
                    var icon = rect.gameObject.AddComponent<Image>(); icon.raycastTarget = false;
                    ultimateChargeIcons.Add(icon);
                }
                float left = 24 + fight.TeamUltimateRequiredCharges * 20;
                ultimateLabel.rectTransform.anchoredPosition = new Vector2(left, -86);
                ultimateLabel.rectTransform.sizeDelta = new Vector2(528 - left, 48);
            }
            for (int i = 0; i < ultimateChargeIcons.Count; i++) ultimateChargeIcons[i].color = i < fight.TeamUltimateCharge ? Gold : Dim;
            string title = fight.TeamUltimateRunning ? $"ULTIMATE — {fight.TeamUltimateBeatsRemaining} BEATS"
                : $"ULTIMATE  {fight.TeamUltimateCharge} / {fight.TeamUltimateRequiredCharges}";
            string status = fight.TeamSkillRunning && fight.TeamUltimateCharge >= fight.TeamUltimateRequiredCharges
                ? "Charged — waiting for Team Skill" : fight.TeamUltimateStatus;
            ultimateLabel.text = title + "\n" + status;
            if (fight.TeamUltimateActivationCount != shownUltimateActivation)
            {
                shownUltimateActivation = fight.TeamUltimateActivationCount;
                if (shownUltimateActivation > 0) SetResult("TEAM ULTIMATE", Gold, "Full party finisher", 1.2f);
            }
            if (fight.TeamUltimateImpactCount != shownUltimateImpact)
            {
                shownUltimateImpact = fight.TeamUltimateImpactCount;
                if (shownUltimateImpact > 0) { ultimateFlashTime = .22f; SetResult("TEAM FINISHER", Gold, fight.TeamUltimateStatus, 1.2f); }
                else ultimateFlashTime = 0;
            }
            if (!fight.IsPaused) ultimateFlashTime = Mathf.Max(0, ultimateFlashTime - Time.deltaTime);
            float alpha = ultimateFlashTime > 0 ? .2f * ultimateFlashTime / .22f : fight.TeamUltimatePerforming ? .045f : 0;
            ultimateFlash.color = new Color(1, .72f, .22f, alpha);
        }
    }
}
