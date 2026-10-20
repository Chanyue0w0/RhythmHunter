using RhythmHunter.RhythmDemo;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    [DefaultExecutionOrder(-200)]
    public sealed class FightPauseController : MonoBehaviour
    {
        FmodBeatClock clock;
        GameObject overlay, toggle;
        float previousTimeScale;
        bool restarting;
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
            var restart = FightMenuUi.Button(panel, "RESTART BATTLE  [F9]", new Vector2(0,-150),new Vector2(300,54),font,RestartBattle);
            Center((RectTransform)restart.transform);
            overlay.SetActive(false);
        }

        static void Center(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
        }

        void Update()
        {
            if (restarting) return;
            if (Keyboard.current?.f9Key.wasPressedThisFrame ?? false)
            {
                RestartBattle();
                return;
            }
            if ((Gamepad.current?.startButton.wasPressedThisFrame ?? false) ||
                (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false) ||
                (Keyboard.current?.f8Key.wasPressedThisFrame ?? false)) TogglePause();
        }

        public void TogglePause() => SetPaused(!IsPaused);

        public void SetPaused(bool value)
        {
            if (restarting) return;
            if (clock == null || value == IsPaused || !clock.SetPaused(value)) return;
            if (value) { previousTimeScale = Time.timeScale; Time.timeScale = 0; }
            else Time.timeScale = previousTimeScale;
            IsPaused = value;
            if (overlay != null) { overlay.SetActive(value); if (value) overlay.transform.SetAsLastSibling(); }
        }

        public void RestartBattle()
        {
            if (!Application.isPlaying || restarting) return;
            Scene scene = gameObject.scene;
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning("Save the battle scene before restarting it.", this);
                return;
            }
#if !UNITY_EDITOR
            if (!Application.CanStreamedLevelBeLoaded(scene.path))
            {
                Debug.LogError("Cannot restart: the battle scene is missing from the build scene list.", this);
                return;
            }
#endif
            restarting = true;
            // Restore global time before unloading. OnDisable must not restore it twice.
            if (IsPaused) Time.timeScale = previousTimeScale;
            IsPaused = false;
            clock?.StopMusic();
            // Reload authored formation, resources, waves and entrance/debug settings.
            // In the Editor this also supports saved scenes outside the build list.
#if UNITY_EDITOR
            // Runtime scene objects are about to be destroyed. Do not leave their
            // built-in inspectors targeting the old instances during scene reload.
            var selected = UnityEditor.Selection.objects;
            var retained = new System.Collections.Generic.List<Object>();
            foreach (var item in selected)
                if (item != null && UnityEditor.EditorUtility.IsPersistent(item)) retained.Add(item);
            if (retained.Count != selected.Length)
            {
                UnityEditor.Selection.objects = retained.ToArray();
                UnityEditor.ActiveEditorTracker.sharedTracker.ForceRebuild();
            }
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.path, LoadSceneMode.Single);
#endif
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
