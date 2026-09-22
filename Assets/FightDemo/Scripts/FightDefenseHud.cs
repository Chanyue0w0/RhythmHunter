using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    /// <summary>Compact live defense HUD; all values are read from the existing combat/character data.</summary>
    public sealed class FightDefenseHud : MonoBehaviour
    {
        private FightCombatController fight;
        private Font font;
        private RectTransform root;
        private Text hpText, armorText, recoveryText;
        private HeartRow hpHearts, armorHearts;
        private Text[] values, enemyLabels;
        private Image[] enemyFills;
        private GameObject[] enemyRows;
        private GameObject developerPanel;
        private Text developerToggleText;
        private static readonly string[] Positions = { "FRONT X", "MIDDLE Y", "BACK B" };
        private readonly Color red = new(1f, 0.22f, 0.3f);
        private readonly Color blue = new(0.35f, 0.8f, 1f);

        public void Configure(FightCombatController controller, Canvas canvas, Font uiFont)
        {
            if (root != null)
                return;
            fight = controller;
            font = uiFont;
            root = Rect("DefenseHud", canvas.transform, Vector2.zero, Vector2.zero);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.sizeDelta = Vector2.zero;

            RectTransform party = Panel("TeamDefense", root, new Vector2(-24, -24), new Vector2(560, 204), true);
            Label(party, "TEAM DEFENSE", new Vector2(16, -10), new Vector2(528, 28), 20);
            hpText = Label(party, "", new Vector2(16, -42), new Vector2(528, 22));
            hpHearts = new HeartRow(this, party, new Vector2(16, -68), red);
            armorText = Label(party, "", new Vector2(16, -118), new Vector2(528, 22));
            armorHearts = new HeartRow(this, party, new Vector2(16, -144), blue);
            recoveryText = Label(party, "", new Vector2(16, -178), new Vector2(528, 20), 15);

            RectTransform enemies = Panel("EnemyHealth", root, new Vector2(24, -24), new Vector2(540, 204));
            Label(enemies, "ENEMIES", new Vector2(16, -10), new Vector2(508, 28), 20);
            enemyLabels = new Text[3];
            enemyFills = new Image[3];
            enemyRows = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform row = Rect($"Enemy{i}", enemies, new Vector2(16, -44 - i * 50), new Vector2(508, 44));
                enemyRows[i] = row.gameObject;
                enemyLabels[i] = Label(row, "", Vector2.zero, new Vector2(508, 24));
                Image bar = Graphic("HealthTrack", row, new Vector2(0, -28), new Vector2(508, 9), new Color(0.22f, 0.09f, 0.12f));
                enemyFills[i] = Graphic("HealthFill", bar.transform, Vector2.zero, new Vector2(508, 9), red);
            }

            RectTransform dev = Panel("DeveloperValues", root, new Vector2(24, -276), new Vector2(640, 218));
            developerPanel = dev.gameObject;
            Label(dev, "DEV  |  BASE HP / DEF / BASIC", new Vector2(16, -10), new Vector2(608, 26), 18);
            values = new Text[6];
            for (int i = 0; i < values.Length; i++)
                values[i] = Label(dev, "", new Vector2(16, -44 - i * 27), new Vector2(608, 24), 16);

            Image toggleImage = Graphic("DeveloperToggle", root, new Vector2(24, -236), new Vector2(182, 32), new Color(0.08f, 0.16f, 0.22f, 0.95f));
            toggleImage.raycastTarget = true;
            Button toggle = toggleImage.gameObject.AddComponent<Button>();
            toggle.targetGraphic = toggleImage;
            developerToggleText = Label(toggleImage.transform, "DEV VALUES: ON", new Vector2(8, -4), new Vector2(166, 24), 16);
            toggle.onClick.AddListener(() =>
            {
                developerPanel.SetActive(!developerPanel.activeSelf);
                developerToggleText.text = developerPanel.activeSelf ? "DEV VALUES: ON" : "DEV VALUES: OFF";
            });
            Refresh();
        }

        private void Update() => Refresh();

        public void Refresh()
        {
            if (root == null || fight == null)
                return;
            root.gameObject.SetActive(fight.HealthSystemEnabled);
            hpText.text = $"HP  {fight.PartyHp:0.#} / {fight.MaxPartyHp:0.#}";
            armorText.text = $"ARMOR  {fight.PartyArmor:0.#} / {fight.MaxPartyArmor:0.#}";
            hpHearts.Set(fight.PartyHp, fight.MaxPartyHp);
            armorHearts.Set(fight.PartyArmor, fight.MaxPartyArmor);
            recoveryText.text = fight.BattleEnded ? "DEFEATED" : fight.MaxPartyArmor <= 0 ? "No frontline armor"
                : fight.PartyArmor >= fight.MaxPartyArmor ? "Armor full"
                : $"Armor recovery in {fight.ArmorRecoveryBeatsRemaining} beat(s)";
            var enemies = fight.RosterManager != null ? fight.RosterManager.ActiveEnemies : null;
            for (int i = 0; i < 3; i++)
            {
                if (developerPanel.activeSelf)
                    values[i].text = StatLine(Positions[i], fight.GetHeroForCommand((FightInputRouter.HeroCommand)i).UnitSlot);
                FightUnitSlot enemy = enemies != null && i < enemies.Count ? enemies[i] : null;
                enemyRows[i].SetActive(enemy != null && enemy.HasCharacter);
                if (enemy != null)
                {
                    enemyLabels[i].text = $"{enemy.DisplayName}   HP {enemy.CurrentHp:0.#} / {enemy.MaxHp:0.#}";
                    enemyFills[i].rectTransform.sizeDelta = new Vector2(508 * Mathf.Clamp01(enemy.CurrentHp / enemy.MaxHp), 9);
                }
                if (developerPanel.activeSelf)
                    values[i + 3].text = StatLine($"ENEMY {i + 1}", enemy);
            }
        }

        private static string StatLine(string position, FightUnitSlot slot)
        {
            if (slot == null || !slot.HasCharacter)
                return $"{position}  —";
            var data = slot.CharacterDefinition;
            string ability = slot.Team == FightUnitSlot.UnitTeam.Enemy ? $"ATK {slot.AttackPower:0.#}"
                : data != null ? data.BasicAbilityName + (data.BasicAbilityPower > 0 ? $" {data.BasicAbilityPower:0.#}" : "") : "Basic";
            return $"{position}  {slot.DisplayName}   HP {slot.MaxHp:0.#}  DEF {(data != null ? data.Defense : 0)}  |  {ability}";
        }

        private void OnDestroy()
        {
            if (root != null)
            {
                if (Application.isPlaying) Destroy(root.gameObject);
                else DestroyImmediate(root.gameObject);
            }
        }

        private RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size, bool right = false)
        {
            RectTransform panel = Graphic(name, parent, position, size, new Color(0.025f, 0.04f, 0.065f, 0.92f)).rectTransform;
            if (right)
                panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            return panel;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private Image Graphic(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            Image image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize = 18)
        {
            Text text = Rect("Label", parent, position, size).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.text = value;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        private sealed class HeartRow
        {
            private readonly FightDefenseHud owner;
            private readonly RectTransform root;
            private readonly Color filled;
            private readonly List<Image> pixels = new();
            private readonly List<float> thresholds = new();
            private int capacity = -1;
            public HeartRow(FightDefenseHud hud, Transform parent, Vector2 position, Color color)
            {
                owner = hud;
                filled = color;
                root = Rect("Hearts", parent, position, new Vector2(528, 48));
            }
            public void Set(float current, float maximum)
            {
                int count = Mathf.CeilToInt(maximum);
                if (capacity != count)
                {
                    foreach (Image pixel in pixels)
                    {
                        pixel.gameObject.SetActive(false);
                        if (Application.isPlaying) Destroy(pixel.gameObject);
                        else DestroyImmediate(pixel.gameObject);
                    }
                    pixels.Clear();
                    thresholds.Clear();
                    capacity = count;
                    string[] pattern = { ".XX.XX.", "XXXXXXX", "XXXXXXX", ".XXXXX.", "..XXX..", "...X..." };
                    for (int heart = 0; heart < count; heart++)
                    for (int y = 0; y < pattern.Length; y++)
                    for (int x = 0; x < pattern[y].Length; x++)
                    {
                        if (pattern[y][x] != 'X') continue;
                        pixels.Add(owner.Graphic("Pixel", root, new Vector2(heart % 20 * 26 + x * 3, -heart / 20 * 24 - y * 3), Vector2.one * 3, filled));
                        thresholds.Add(heart + (x <= 2 ? 0.5f : 1f));
                    }
                }
                for (int i = 0; i < pixels.Count; i++)
                    pixels[i].color = current >= thresholds[i] ? filled : new Color(0.18f, 0.2f, 0.25f);
            }
        }
    }
}
