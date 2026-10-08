using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    /// <summary>Pre-battle formation UI; the roster owns the lock, not these buttons.</summary>
    public sealed class FightFormationPanel : MonoBehaviour
    {
        private FightCombatController fight;
        private RectTransform panel;
        private Text summary, lockedLabel;
        private readonly Text[] positions = new Text[3];
        private readonly Button[] buttons = new Button[4];
        private static readonly string[] Bindings = { "FRONT  X / Q", "MIDDLE  Y / W", "BACK  B / E" };

        public void Configure(FightCombatController controller, Canvas canvas, Font font)
        {
            if (panel != null) return;
            fight = controller;
            panel = FightMenuUi.Rect("BattlePreparation", canvas.transform, Vector2.zero, new Vector2(870, 310));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.gameObject.AddComponent<Image>().color = new Color(.02f, .04f, .07f, .97f);
            FightMenuUi.Label(panel, "CHOOSE YOUR FORMATION", new Vector2(20, -8), new Vector2(830, 34), font, 24);
            FightMenuUi.Label(panel, "Formation stays locked until this battle ends, across every wave.",
                new Vector2(20, -43), new Vector2(830, 24), font, 16);
            for (int i = 0; i < positions.Length; i++)
                positions[i] = FightMenuUi.Label(panel, "", new Vector2(20 + i * 280, -76), new Vector2(270, 72), font, 18);
            buttons[0] = SwapButton(0, 1, "Front / Middle", 20, font);
            buttons[1] = SwapButton(0, 2, "Front / Back", 300, font);
            buttons[2] = SwapButton(1, 2, "Middle / Back", 580, font);
            summary = FightMenuUi.Label(panel, "", new Vector2(20, -198), new Vector2(550, 42), font, 18);
            buttons[3] = FightMenuUi.Button(panel, "START BATTLE", new Vector2(590, -204), new Vector2(260, 36), font,
                () => { fight.TryBeginBattle(); Refresh(); });
            FightMenuUi.Label(panel, "Team Skill: 60 MP, then A / R. Heroes cast in formation order. ◆ x2 = Fragile.",
                new Vector2(20, -255), new Vector2(830, 32), font, 15);
            lockedLabel = FightMenuUi.Label(canvas.transform, "", Vector2.zero, new Vector2(870, 30), font, 16);
            lockedLabel.name = "FormationLockStatus";
            lockedLabel.rectTransform.anchorMin = lockedLabel.rectTransform.anchorMax = new Vector2(.5f, 0);
            lockedLabel.rectTransform.pivot = new Vector2(.5f, 0);
            lockedLabel.rectTransform.anchoredPosition = new Vector2(0, 136);
            Refresh();
        }

        private Button SwapButton(int first, int second, string label, float x, Font font) =>
            FightMenuUi.Button(panel, label, new Vector2(x, -152), new Vector2(270, 34), font, () =>
            {
                fight.RosterManager.TrySwapHeroes((FightRosterManager.PartyPosition)first,
                    (FightRosterManager.PartyPosition)second);
                Refresh();
            });

        private void Update() => Refresh();

        public void Refresh()
        {
            if (panel == null || fight == null) return;
            bool preparing = fight.AwaitingBattleStart;
            // Keep pause/calibration menus accessible above the formation screen.
            panel.gameObject.SetActive(preparing && !fight.IsPaused && !fight.TimingCalibrationActive);
            lockedLabel.gameObject.SetActive(!preparing);
            lockedLabel.text = "FORMATION LOCKED  |  Changes available before the next battle";
            if (!preparing) return;
            bool complete = true;
            for (int i = 0; i < positions.Length; i++)
            {
                var actor = fight.GetHeroForCommand((FightInputRouter.HeroCommand)i).UnitSlot;
                var data = actor != null ? actor.CharacterDefinition : null;
                complete &= data != null;
                positions[i].text = Bindings[i] + "\n" + (data != null
                    ? $"{data.DisplayName}\nHP {data.MaxHp:0.#}  DEF {data.Defense}  MP {data.TeamSkillManaCost}"
                    : "EMPTY");
            }
            summary.text = $"TEAM  HP {fight.MaxPartyHp:0.#}   DEF {fight.MaxPartyArmor:0.#}   MP {fight.TeamSkillGauge}/{fight.TeamSkillGaugeMax}";
            buttons[3].interactable = complete;
        }

        private void OnDestroy()
        {
            if (panel != null) DestroyUi(panel.gameObject);
            if (lockedLabel != null) DestroyUi(lockedLabel.gameObject);
        }

        private static void DestroyUi(GameObject item)
        {
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }
    }
}
