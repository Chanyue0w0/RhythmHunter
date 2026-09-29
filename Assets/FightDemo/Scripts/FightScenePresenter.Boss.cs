using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightScenePresenter
    {
        private RectTransform bossPanel;
        private Text bossLabel;
        private void UpdateBossHud()
        {
            bool visible = fight != null && fight.BossActive;
            if (!visible) { if (bossPanel != null) bossPanel.gameObject.SetActive(false); return; }
            if (bossPanel == null)
            {
                if (healthText == null || healthText.canvas == null) return;
                bossPanel = FightMenuUi.Rect("BossState", healthText.canvas.transform, new Vector2(0, -140), new Vector2(720, 84));
                bossPanel.anchorMin = bossPanel.anchorMax = bossPanel.pivot = new Vector2(.5f, 1);
                bossPanel.gameObject.AddComponent<Image>().color = new Color(.01f, .04f, .06f, .9f);
                bossLabel = FightMenuUi.Label(bossPanel, "", new Vector2(12, -6), new Vector2(696, 72), healthText.font, 16);
                foreach (var graphic in bossPanel.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            }
            bossPanel.gameObject.SetActive(true);
            bossLabel.color = fight.CurrentBossPhase == FightCombatController.BossPhase.Rage ? Red : Gold;
            bossLabel.text = $"BOSS  {fight.CurrentBossPhase.ToString().ToUpperInvariant()}  •  {fight.BossBeatsRemaining} BEATS" +
                (fight.CurrentBossPhase == FightCombatController.BossPhase.Break ? "" : $"  •  BREAK {fight.BossBreakProgress}/{fight.BossBreakThreshold}") +
                $"\n{fight.BossHint}";
            // Counters can change phase between FMOD callbacks; refresh immediately.
            if (warningText != null && beatClock != null && beatClock.HasTimingAnchor)
            {
                int remaining = fight.GetEnemyBeatsUntilAttack(beatClock.LatestBeat.GlobalBeat);
                warningText.text = remaining < 0 ? "BOSS — " + fight.CurrentBossPhase.ToString().ToUpperInvariant()
                    : remaining == 0 ? "ENEMY ATTACK  •  GUARD THIS BEAT"
                    : $"ENEMY ATTACK IN {remaining} BEAT{(remaining == 1 ? "" : "S")}";
                warningText.color = remaining == 0 ? Gold : Color.white;
            }
        }
    }
}
