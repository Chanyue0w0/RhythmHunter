using UnityEngine;
using UnityEngine.UI;
namespace RhythmHunter.FightDemo
{
    public sealed partial class FightTutorialController
    {
        [Header("Tutorial position buttons")]
        [SerializeField] private bool showTutorialPositionButtons = true;
        [SerializeField] private Vector2 positionButtonOffset = new(0, -34);
        [SerializeField] private Vector2 positionButtonSize = new(44, 44);
        private readonly RectTransform[] positionButtons = new RectTransform[3];
        private RectTransform buttonCanvasRect;
        private Canvas buttonCanvas;
        private void ConfigurePositionBadges(RectTransform parent, Font font)
        {
            buttonCanvasRect = parent;
            buttonCanvas = parent.GetComponentInParent<Canvas>();
            string[] names = { "X", "Y", "B" };
            Color[] colors = { new(.2f, .65f, 1), new(1, .85f, .15f), new(1, .3f, .3f) };
            for (int i = 0; i < 3; i++)
            {
                var rect = FightMenuUi.Rect("PositionButton_" + names[i], parent, Vector2.zero, positionButtonSize);
                Center(rect); positionButtons[i] = rect;
                var background = rect.gameObject.AddComponent<Image>(); background.color = new Color(.015f, .025f, .04f, .85f); background.raycastTarget = false;
                var label = Label(rect, font, Vector2.zero, positionButtonSize, 30);
                label.text = names[i]; label.color = colors[i]; label.fontStyle = FontStyle.Bold; label.raycastTarget = false;
                rect.gameObject.SetActive(false);
            }
        }
        private void LateUpdate()
        {
            if (buttonCanvasRect == null) return;
            var controller = fight != null ? fight : GetComponent<FightCombatController>();
            var planner = GetComponent<FightLevelPlanner>();
            bool visible = showTutorialPositionButtons && isActiveAndEnabled && planner != null && planner.ActiveStage != null &&
                planner.ActiveStage.Tutorial && controller != null && !controller.BattleEnded;
            var camera = Camera.main;
            for (int i = 0; i < 3; i++)
            {
                var rect = positionButtons[i]; if (rect == null) continue;
                var slot = visible ? controller.RosterManager?.GetHeroAtPosition((FightRosterManager.PartyPosition)i) : null;
                if (!visible || camera == null || slot == null || !slot.HasCharacter || slot.ActorInstance == null)
                { rect.gameObject.SetActive(false); continue; }
                var foot = slot.ActorInstance.transform.Find("FootAnchor");
                var screen = camera.WorldToScreenPoint(foot != null ? foot.position : slot.ActorInstance.transform.position);
                rect.gameObject.SetActive(screen.z > 0);
                if (screen.z <= 0) continue;
                Camera uiCamera = buttonCanvas != null && buttonCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? buttonCanvas.worldCamera : null;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(buttonCanvasRect, screen, uiCamera, out var local))
                    rect.anchoredPosition = local - buttonCanvasRect.rect.center + positionButtonOffset;
                rect.sizeDelta = positionButtonSize;
            }
        }
    }
}
