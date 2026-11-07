using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightScenePresenter
    {
        [Header("Player / Developer UI Groups")]
        [SerializeField] private Canvas playerUiCanvas;
        [Tooltip("只控制開發者測試群組；玩家血量、護甲、節拍器與戰鬥操作選單不受影響。")]
        [SerializeField] private bool showDeveloperUi = true;
        private RectTransform playerUiRoot, developerUiRoot, battleMenuRoot;

        private void EnsureUiGroups()
        {
            if (playerUiRoot != null) return;
            if (playerUiCanvas == null && healthText != null)
                playerUiCanvas = healthText.GetComponentInParent<Canvas>(true);
            if (playerUiCanvas == null && beatProgress != null)
                playerUiCanvas = beatProgress.GetComponentInParent<Canvas>(true);
            if (playerUiCanvas == null) return;
            playerUiRoot = UiGroup("PlayerUI", playerUiCanvas.transform);
            developerUiRoot = UiGroup("DeveloperUI", playerUiCanvas.transform);
            battleMenuRoot = UiGroup("BattleMenus", playerUiCanvas.transform);
            MoveUi("TopHud", developerUiRoot);
            MoveUi("Bottom UI (Enable In Inspector)", developerUiRoot);
            developerUiRoot.gameObject.SetActive(showDeveloperUi);
        }

        private static RectTransform UiGroup(string name, Transform parent)
        {
            var root = parent.Find(name) as RectTransform;
            if (root == null) root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.localScale = Vector3.one;
            return root;
        }

        private void MoveUi(string name, Transform destination)
        {
            if (playerUiCanvas == null || destination == null) return;
            Transform item = playerUiCanvas.transform.Find(name);
            if (item != null) item.SetParent(destination, false);
        }
    }
}
