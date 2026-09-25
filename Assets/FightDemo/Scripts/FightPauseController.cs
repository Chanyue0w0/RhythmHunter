using RhythmHunter.RhythmDemo;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    [DefaultExecutionOrder(-200)]
    public sealed class FightPauseController : MonoBehaviour
    {
        FmodBeatClock clock;
        GameObject overlay, toggle;
        float previousTimeScale;
        public bool IsPaused { get; private set; }

        public void Configure(FmodBeatClock source, Canvas canvas, Font font)
        {
            clock = source;
            var button = FightMenuUi.Button(canvas.transform, "PAUSE  [START / ESC]", new Vector2(-24,-236),new Vector2(270,36),font,TogglePause);
            toggle = button.gameObject;
            var r = (RectTransform)button.transform;
            r.anchorMin = r.anchorMax = r.pivot = Vector2.one;
            var panel = FightMenuUi.Rect("PauseOverlay",canvas.transform,Vector2.zero,Vector2.zero);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(.01f,.025f,.05f,.9f);
            overlay = panel.gameObject;
            var title = FightMenuUi.Label(panel,"PAUSED",new Vector2(0,70),new Vector2(720,70),font,48);
            Center(title.rectTransform);
            var hint = FightMenuUi.Label(panel,"Music and battle paused. Press START / ESC / F8 to continue.",new Vector2(0,0),new Vector2(900,40),font,22);
            Center(hint.rectTransform);
            var resume = FightMenuUi.Button(panel,"RESUME",new Vector2(0,-80),new Vector2(260,54),font,TogglePause);
            Center((RectTransform)resume.transform);
            overlay.SetActive(false);
        }

        static void Center(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
        }

        void Update()
        {
            if ((Gamepad.current?.startButton.wasPressedThisFrame ?? false) ||
                (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false) ||
                (Keyboard.current?.f8Key.wasPressedThisFrame ?? false)) TogglePause();
        }

        public void TogglePause() => SetPaused(!IsPaused);

        public void SetPaused(bool value)
        {
            if (clock == null || value == IsPaused || !clock.SetPaused(value)) return;
            if (value) { previousTimeScale = Time.timeScale; Time.timeScale = 0; }
            else Time.timeScale = previousTimeScale;
            IsPaused = value;
            if (overlay != null) { overlay.SetActive(value); if (value) overlay.transform.SetAsLastSibling(); }
        }

        void OnDisable()
        {
            if (!IsPaused) return;
            if (clock != null) clock.SetPaused(false);
            Time.timeScale = previousTimeScale;
            IsPaused = false;
            if (overlay != null) overlay.SetActive(false);
        }

        void OnDestroy()
        {
            if (overlay != null) { if(Application.isPlaying) Destroy(overlay); else DestroyImmediate(overlay); }
            if (toggle != null) { if(Application.isPlaying) Destroy(toggle); else DestroyImmediate(toggle); }
        }
    }
}
