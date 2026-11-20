using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightPauseController
    {
        RectTransform pauseContent;
        Font guideFont;
        void BuildHeroGuide(Transform parent, Font font)
        {
            var guide = Resources.Load<FightHeroGuide>("FightHeroGuide");
            var title = FightMenuUi.Label(parent, "勇者情報", new Vector2(0, 320), new Vector2(1160, 42), font, 28);
            Center(title.rectTransform); title.color = new Color(1, .84f, .5f);
            for (int i = 0; i < 6; i++)
            {
                var entry = guide && i < guide.heroes.Length ? guide.heroes[i] : null;
                var card = FightMenuUi.Rect("HeroGuide" + i, parent, new Vector2((i - 2.5f) * 196, 147), new Vector2(184, 286));
                Center(card);
                var background = card.gameObject.AddComponent<Image>();
                background.color = new Color(.045f, .10f, .15f, .97f); background.raycastTarget = false;
                var portraitRect = FightMenuUi.Rect("Portrait", card, new Vector2(0, 80), new Vector2(100, 100)); Center(portraitRect);
                var portrait = portraitRect.gameObject.AddComponent<Image>();
                portrait.sprite = entry?.portrait; portrait.preserveAspect = true; portrait.raycastTarget = false;
                portrait.enabled = portrait.sprite;
                void Line(string name, string value, float y, int size, Color color, float height = 28)
                {
                    var label = FightMenuUi.Label(card, value ?? "???", new Vector2(0, y), new Vector2(172, height), font, size);
                    label.name = name; Center(label.rectTransform); label.color = color;
                }
                Line("CharacterName", entry?.characterName, 10, 22, new Color(1, .86f, .56f));
                Line("Profession", entry?.profession, -24, 19, Color.white);
                Line("Role", entry?.role, -55, 19, new Color(.48f, .83f, 1));
                Line("Abilities", entry?.abilities, -105, 18, new Color(.85f, .9f, .95f), 64);
            }
        }
        void FitPauseContent()
        {
            if (!pauseContent || !overlay) return;
            var bounds = ((RectTransform)overlay.transform).rect.size;
            pauseContent.localScale = Vector3.one * Mathf.Min(bounds.x / 1280f, bounds.y / 720f);
        }
    }
}
