using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    // Player-facing UI on its own canvas; developer HUD visibility never hides results.
    [DisallowMultipleComponent]
    public sealed class FightVictoryPanel : MonoBehaviour
    {
        [SerializeField] private FightStageResults results;
        [SerializeField] private bool showVictoryPanel = true;
        [SerializeField] private bool showDefeatPanel = true;
        [SerializeField] private Font font;
        [SerializeField] private Color backdropColor = new(.015f, .025f, .04f, .88f);
        [SerializeField] private Color accentColor = new(1f, .8f, .28f);
        [SerializeField] private Color defeatAccentColor = new(1f, .35f, .3f);
        [Header("Defeat Presentation")]
        [Tooltip("Wait after defeat before fading in results. Uses the song tempo at defeat.")]
        [SerializeField, Min(0)] private float defeatDelayBeats = 2;
        [SerializeField, Min(0)] private float defeatFadeSeconds = .35f;
        private GameObject canvasObject;
        private CanvasGroup presentationGroup;
        private float presentationDelay, presentationFadeElapsed;
        private float shownAt;
        public bool IsVisible => canvasObject != null && canvasObject.activeSelf;
        private void OnEnable()
        {
            if (results == null) return;
            results.ResultReady += Show;
            if (results.Completed) Show();
        }
        private void OnDisable()
        {
            if (results != null) results.ResultReady -= Show;
            if (canvasObject != null) canvasObject.SetActive(false);
        }
        private void OnDestroy()
        {
            if (canvasObject == null) return;
            if (Application.isPlaying) Destroy(canvasObject); else DestroyImmediate(canvasObject);
        }
        private void Update()
        {
            if (IsVisible && presentationGroup != null && presentationGroup.alpha < 1)
            {
                if (results != null && results.PresentationPaused) return;
                AdvancePresentation(Time.unscaledDeltaTime);
            }
            if (presentationGroup != null && !presentationGroup.interactable) return;
            if (!IsVisible || Time.unscaledTime - shownAt < .4f) return;
            if (Gamepad.current?.buttonSouth.wasPressedThisFrame ?? false) Restart();
        }
        private static void Center(RectTransform rect) => rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        private Text Label(Transform parent, string text, float y, int size, Font labelFont)
        {
            var label = FightMenuUi.Label(parent, text, new Vector2(0, y), new Vector2(960, 64), labelFont, size);
            Center(label.rectTransform);
            // Font size is not line height: the 64pt title needs room for ascent/descent.
            // Unity Text's default vertical truncation can otherwise discard the whole line.
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(64, label.preferredHeight + 8));
            return label;
        }
        private static string Duration(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"mm\:ss\.f");
        private void Show()
        {
            if (results == null || !results.Completed || !(results.Won ? showVictoryPanel : showDefeatPanel)) return;
            if (canvasObject != null) { canvasObject.SetActive(true); return; }
            shownAt = Time.unscaledTime;
            canvasObject = new GameObject("PlayerResultCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            presentationGroup = canvasObject.AddComponent<CanvasGroup>();
            float secondsPerBeat = 0;
            var clock = results.BeatClock;
            if (clock != null)
            {
                secondsPerBeat = (float)(clock.MillisecondsPerBeat > 0 ? clock.MillisecondsPerBeat / 1000d : clock.OpeningSecondsPerBeat);
                secondsPerBeat /= Mathf.Max(.01f, clock.MusicPitch);
            }
            // Presentation only: combat/input and result statistics are already locked at defeat.
            // Snapshot tempo because the music may finish fading out during this wait.
            presentationDelay = results.Won ? 0 : defeatDelayBeats * secondsPerBeat;
            presentationFadeElapsed = 0;
            presentationGroup.alpha = results.Won ? 1 : 0;
            presentationGroup.interactable = results.Won;
            presentationGroup.blocksRaycasts = results.Won;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var panel = FightMenuUi.Rect(results.Won ? "VictoryPanel" : "DefeatPanel", canvas.transform, Vector2.zero, Vector2.zero);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = backdropColor;
            var labelFont = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Color resultColor = results.Won ? accentColor : defeatAccentColor;
            Label(panel, results.Won ? "VICTORY" : "DEFEAT", 230, 64, labelFont).color = resultColor;
            Label(panel, string.IsNullOrEmpty(results.ChapterName) ? results.StageName : results.ChapterName + " / " + results.StageName, 145, 30, labelFont);
            Label(panel, $"WAVES CLEARED   {results.ClearedWaves} / {results.TotalWaves}", 80, 28, labelFont);
            Label(panel, "STAGE TIME   " + Duration(results.ElapsedSeconds), 15, 30, labelFont);
            Label(panel, results.SongLengthSeconds > 0 ? "SONG LENGTH   " + Duration(results.SongLengthSeconds) : "SONG LENGTH   —", -45, 26, labelFont);
            Label(panel, results.SongLengthSeconds <= 0 ? "" : results.OvertimeSeconds > 0 ? "OVERTIME   +" + Duration(results.OvertimeSeconds) : "WITHIN SONG TIME", -105, 26, labelFont).color = resultColor;
            var restart = FightMenuUi.Button(panel, "RESTART BATTLE  [A / F9]", new Vector2(0, -210), new Vector2(400, 60), labelFont, Restart);
            Center((RectTransform)restart.transform);
            // FightMenuUi labels use top-left anchors: centre the button caption explicitly.
            var caption = restart.GetComponentInChildren<Text>().rectTransform;
            Center(caption); caption.anchoredPosition = Vector2.zero;
        }
        private void Restart()
        {
            if (presentationGroup != null && !presentationGroup.interactable) return;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                var pause = root.GetComponentInChildren<FightPauseController>(true);
                if (pause != null) { pause.RestartBattle(); return; }
            }
        }
        private void AdvancePresentation(float seconds)
        {
            float delayConsumed = Mathf.Min(presentationDelay, seconds);
            presentationDelay -= delayConsumed;
            seconds -= delayConsumed;
            if (presentationDelay > 0) return;
            presentationFadeElapsed += seconds;
            presentationGroup.alpha = defeatFadeSeconds <= 0 ? 1 : Mathf.Clamp01(presentationFadeElapsed / defeatFadeSeconds);
            if (presentationGroup.alpha < 1) return;
            presentationGroup.interactable = presentationGroup.blocksRaycasts = true;
            if (results != null && !results.Won) GetComponent<FightScenePresenter>()?.HideBattleUiAfterDefeat();
            shownAt = Time.unscaledTime;
        }
    }
}
