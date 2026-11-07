using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    /// <summary>Screen-space view of the existing shared party resources; owns no combat values.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class FightPartyResourceHud : MonoBehaviour
    {
        [SerializeField] private Sprite hpEmpty;
        [SerializeField] private Sprite hpFull;
        [SerializeField] private Sprite armorEmpty;
        [SerializeField] private Sprite armorFull;
        [SerializeField, Min(1)] private float iconSize = 64;
        [SerializeField, Min(0)] private float iconGap = 8;
        [SerializeField, Min(0)] private float rowGap = 12;
        [Header("Beat Bounce")]
        [SerializeField] private bool beatBounceEnabled = true;
        [Tooltip("輕跳高度，單位為 Canvas 參考解析度的像素。")]
        [SerializeField, Min(0)] private float bounceHeight = 4;
        [SerializeField, Range(.05f, 1)] private float bounceDurationBeats = .45f;
        private long bounceBeat;
        private float bouncePhase;
        private bool hasBounceTiming;
        private int bounceCycleLength;
        private FightCombatController fight;
        private readonly List<Image[]> hearts = new();
        private readonly List<Image[]> armor = new();

        public void Configure(FightCombatController controller)
        {
            fight = controller;
            Refresh();
        }

        private void Update() => Refresh();
        private void OnDisable()
        {
            for (int i = 0; i < hearts.Count; i++)
                if (hearts[i][0] != null) hearts[i][0].rectTransform.anchoredPosition = new Vector2(-i * (iconSize + iconGap), 0);
            for (int i = 0; i < armor.Count; i++)
                if (armor[i][0] != null) armor[i][0].rectTransform.anchoredPosition = new Vector2(-i * (iconSize + iconGap), -(iconSize + rowGap));
        }

        public void Refresh()
        {
            if (fight == null) return;
            // Rows are right-aligned: advance one shared column per beat, with
            // shorter rows joining only when the sweep reaches their columns.
            bounceCycleLength = Mathf.Max(Mathf.CeilToInt(Mathf.Max(0, fight.MaxPartyHp)),
                Mathf.CeilToInt(Mathf.Max(0, fight.MaxPartyArmor)));
            var clock = fight.BeatClock;
            hasBounceTiming = beatBounceEnabled && clock != null && clock.isActiveAndEnabled && clock.IsPlaying &&
                clock.HasTimingAnchor && clock.MillisecondsPerBeat > 0;
            if (hasBounceTiming)
            {
                hasBounceTiming = clock.TryGetTimelinePositionMs(out int timelineMs);
                double position = clock.LatestBeat.GlobalBeat + (timelineMs - clock.LatestBeat.TimelinePositionMs) / clock.MillisecondsPerBeat;
                bounceBeat = (long)System.Math.Floor(position);
                bouncePhase = (float)(position - System.Math.Floor(position));
            }
            DrawRow(hearts, "HP", hpEmpty, hpFull, fight.PartyHp, fight.MaxPartyHp, 0);
            DrawRow(armor, "Armor", armorEmpty, armorFull, fight.PartyArmor, fight.MaxPartyArmor, -(iconSize + rowGap));
        }

        public static float UnitFill(float value, int indexFromRight) =>
            Mathf.Clamp01(Mathf.Round(Mathf.Max(0, value) * 2f) * .5f - indexFromRight);

        private void DrawRow(List<Image[]> row, string label, Sprite empty, Sprite full, float current, float maximum, float y)
        {
            int count = Mathf.CeilToInt(Mathf.Max(0, maximum));
            while (row.Count < count)
            {
                var background = CreateImage(label + "_" + row.Count + "_Empty", transform);
                var fill = CreateImage("Full", background.transform);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Right;
                row.Add(new[] { background, fill });
            }
            for (int i = 0; i < row.Count; i++)
            {
                var pair = row[i];
                pair[0].gameObject.SetActive(i < count && fight.HealthSystemEnabled);
                if (i >= count) continue;
                pair[0].sprite = empty;
                pair[1].sprite = full;
                float jump = hasBounceTiming ? BounceOffset(i, bounceCycleLength, bounceBeat, bouncePhase, bounceHeight, bounceDurationBeats) : 0;
                pair[0].rectTransform.anchoredPosition = new Vector2(-i * (iconSize + iconGap), y + jump);
                pair[0].rectTransform.sizeDelta = pair[1].rectTransform.sizeDelta = Vector2.one * iconSize;
                pair[1].fillAmount = UnitFill(Mathf.Clamp(current, 0, maximum), i);
            }
        }

        public static float BounceOffset(int indexFromRight, int count, long beat, float phase, float height, float duration)
        {
            if (count <= 0 || phase < 0 || phase >= duration) return 0;
            int fromLeft = (int)((beat % count + count) % count);
            if (indexFromRight != count - 1 - fromLeft) return 0;
            float wave = Mathf.Sin(Mathf.PI * phase / Mathf.Max(.05f, duration));
            return Mathf.Max(0, height) * wave * wave;
        }

        private static Image CreateImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }
    }
}
