using System;
using RhythmHunter.RhythmDemo;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    /// <summary>Audio-led personal calibration. The song keeps playing while combat is suspended.</summary>
    public sealed class FightTimingCalibration : MonoBehaviour
    {
        readonly RhythmTimingSamples samples = new();
        FightCombatController fight;
        FmodBeatClock clock;
        FmodRhythmJudge judge;
        FightInputRouter router;
        Font font;
        GameObject panel;
        GameObject toggleObject;
        Text status, readout, progress, cueLabel;
        Button previewButton, saveButton;
        Image progressFill;
        RectTransform tapMarker;
        bool measuring, trial, cues;
        float originalDelay;
        string profile, message = "";
        double lastTapTime;
        Action enterBattle;
        bool Standalone => enterBattle != null;
        public bool IsOpen => panel != null && panel.activeSelf;
        public bool HideRhythmCues => IsOpen && !cues;

        public void Configure(FightCombatController controller, FmodBeatClock beatClock, FmodRhythmJudge rhythmJudge,
            FightInputRouter input, Canvas canvas, Font uiFont, Action onEnterBattle = null)
        {
            fight = controller; clock = beatClock; judge = rhythmJudge; router = input; font = uiFont;
            enterBattle = onEnterBattle;
            Build(canvas.transform);
            if (router != null) router.CommandStarted += OnCommand;
        }

        void OnDisable()
        {
            if (IsOpen) Close();
        }

        void OnDestroy()
        {
            if (router != null) router.CommandStarted -= OnCommand;
            if (panel != null) { if (Application.isPlaying) Destroy(panel); else DestroyImmediate(panel); }
            if (toggleObject != null) { if (Application.isPlaying) Destroy(toggleObject); else DestroyImmediate(toggleObject); }
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused || !IsOpen || !measuring) return;
            samples.Clear(); lastTapTime = Time.realtimeSinceStartupAsDouble;
            message = "Focus lost: restart the warmup when you return to the game.";
        }

        void Update()
        {
            if (panel == null) return;
            if (!Standalone && ((Keyboard.current?.f8Key.wasPressedThisFrame ?? false) || (Gamepad.current?.startButton.wasPressedThisFrame ?? false)))
            {
                if (IsOpen) Close(); else Open();
            }
            if (!IsOpen) return;
            if (!Standalone && (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false)) { Close(); return; }
            if (measuring && Time.realtimeSinceStartupAsDouble - lastTapTime > 5)
            {
                samples.Clear(); lastTapTime = Time.realtimeSinceStartupAsDouble;
                message = "Paused tapping: sample set restarted. Follow the music, one tap per beat.";
            }
            Refresh();
        }

        public void Open()
        {
            if (IsOpen) return;
            originalDelay = judge.PersonalDelayMs; profile = judge.InputProfile;
            panel.SetActive(true); panel.transform.SetAsLastSibling();
            if (fight != null) fight.SetTimingCalibrationActive(true);
            Begin();
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (trial) judge.SetPersonalDelay(originalDelay, false);
            trial = measuring = false;
            panel.SetActive(false);
            if (fight != null) fight.SetTimingCalibrationActive(false);
        }

        public void Begin()
        {
            if (trial) judge.SetPersonalDelay(originalDelay, false);
            trial = false; samples.Clear(); measuring = true;
            lastTapTime = Time.realtimeSinceStartupAsDouble;
            message = "Listen and tap naturally. Warm up for 4 taps, then record 32.";
        }

        void OnCommand(FightInputRouter.HeroCommand command)
        {
            if (!IsOpen || command == FightInputRouter.HeroCommand.Ultimate) return;
            string device = router.CurrentInputProfile;
            if (device != profile)
            {
                if (trial) judge.SetPersonalDelay(originalDelay, false);
                judge.SetInputProfile(device); profile = device; originalDelay = judge.PersonalDelayMs;
                trial = false; Begin(); message = "Input device changed: starting a separate calibration.";
            }
            if (!judge.TryMeasureInput(router.CurrentInputAgeMs, out double delta, out long beat))
            { message = "Waiting for music / fresh input. Start playback to calibrate."; return; }
            lastTapTime = Time.realtimeSinceStartupAsDouble;
            double shownDelta = trial ? delta - judge.PersonalDelayMs : delta;
            tapMarker.anchoredPosition = new Vector2((float)Math.Clamp(shownDelta / 200, -1, 1) * 320, 0);
            status.text = $"{(trial ? "COMPENSATED" : "RAW TAP")}  {Direction(shownDelta)}";
            if (!measuring) return;
            if (!samples.Add(beat, delta, clock.MillisecondsPerBeat))
            { message = "Tap once per beat. Duplicate or far-off taps are ignored."; return; }
            message = samples.Complete
                ? samples.Reliable ? "Ready. Preview the compensation, then Save if it feels right."
                    : "Not stable enough to save. Retry; if the timing drifts, check the song's tempo map."
                : "Keep following the music at a steady pace.";
            if (samples.Complete) measuring = false;
        }

        public void Preview()
        {
            if (!samples.Reliable) return;
            judge.SetPersonalDelay((float)samples.MedianMs, false); trial = true;
            message = "Preview: keep tapping. The indicator now shows compensated error. Close cancels preview.";
        }

        public void Save()
        {
            if (!samples.Reliable) return;
            if (!judge.SetPersonalDelay((float)samples.MedianMs, true))
            { message = RhythmCalibrationStore.LastError; return; }
            originalDelay = judge.PersonalDelayMs; trial = true;
            message = Standalone ? "Saved. ENTER BATTLE will load FightScene3 with this calibration."
                : "Saved for this input device. Close to resume battle on the next beat.";
        }

        public void ResetProfile()
        {
            if (!judge.SetPersonalDelay(0, true)) { message = RhythmCalibrationStore.LastError; return; }
            originalDelay = 0; trial = false;
            samples.Clear(); measuring = false; status.text = "Profile reset to 0 ms.";
            message = "Saved offset cleared for this input device. Select Retry to measure again.";
        }

        static string Direction(double ms) => $"{Math.Abs(ms):0} ms {(ms < 0 ? "EARLY" : "LATE")}";

        void Refresh()
        {
            readout.text = $"{clock.MusicEventPath}\n{clock.LatestBeat.Tempo:0.00} BPM   |   {profile}   |   Applied habit: {Direction(judge.PersonalDelayMs)}\n" +
                $"Measured habit: {Direction(samples.MedianMs)}   |   Spread: {samples.SpreadMs:0} ms   |   Drift: {samples.DriftMs:+0;-0;0} ms\n" +
                "Positive habit = late taps; compensation moves INPUT only. Audio and visual beats stay fixed.";
            progress.text = (samples.WarmupRemaining > 0 && measuring ? $"WARMUP: {samples.WarmupRemaining} taps left" :
                $"SAMPLES {samples.Count}/{RhythmTimingSamples.TargetCount}  |  Consistent samples: {samples.InlierCount}") + "\n" + message;
            progressFill.rectTransform.sizeDelta = new Vector2(700f * samples.Count / RhythmTimingSamples.TargetCount, 6);
            previewButton.interactable = saveButton.interactable = samples.Reliable;
        }

        void Build(Transform parent)
        {
            if (!Standalone)
            {
                Button toggle = ButtonAt(parent, "TIMING  [F8 / START]", new Vector2(-24,-236), new Vector2(270,36), Open);
                toggleObject = toggle.gameObject;
                var toggleRect = (RectTransform)toggle.transform;
                toggleRect.anchorMin = toggleRect.anchorMax = toggleRect.pivot = Vector2.one;
            }
            var box = Rect("TimingCalibration", parent, Vector2.zero, new Vector2(780,490));
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(.5f,.5f);
            panel = box.gameObject;
            var background = panel.AddComponent<Image>(); background.color = new Color(.025f,.045f,.075f,.98f);
            Label(box, "TIMING CALIBRATION", new Vector2(30,-20), new Vector2(700,34), 26);
            Label(box, Standalone ? "Tap Q / W / E or X / Y / B once per beat. SAVE before entering battle."
                : "Battle paused. Tap Q / W / E or X / Y / B once per beat.   ESC / F8: close", new Vector2(30,-62),new Vector2(720,26),16);
            readout = Label(box,"",new Vector2(30,-102),new Vector2(720,108),17);
            progress = Label(box,"",new Vector2(30,-219),new Vector2(720,66),17);
            var bar=Rect("Progress",box,new Vector2(30,-290),new Vector2(700,6));
            bar.gameObject.AddComponent<Image>().color=new Color(.1f,.2f,.3f);
            progressFill=Rect("Fill",bar,Vector2.zero,new Vector2(0,6)).gameObject.AddComponent<Image>();
            progressFill.color=Color.cyan; progressFill.raycastTarget=false;
            status=Label(box,"RAW TAP",new Vector2(30,-307),new Vector2(720,26),19); status.alignment=TextAnchor.MiddleCenter;
            var axis=Rect("EarlyLateAxis",box,new Vector2(70,-350),new Vector2(640,2));
            axis.gameObject.AddComponent<Image>().color=new Color(.25f,.4f,.5f);
            var zero=Rect("Zero",axis,new Vector2(320,8),new Vector2(2,18));zero.gameObject.AddComponent<Image>().color=Color.white;
            tapMarker=Rect("Tap",axis,Vector2.zero,new Vector2(8,18));tapMarker.anchorMin=tapMarker.anchorMax=tapMarker.pivot=new Vector2(.5f,.5f);
            tapMarker.gameObject.AddComponent<Image>().color=Color.cyan;
            Label(box,"EARLY -200 ms",new Vector2(70,-368),new Vector2(200,24),16);
            Label(box,"ON TIME",new Vector2(290,-368),new Vector2(200,24),16).alignment=TextAnchor.MiddleCenter;
            Label(box,"LATE +200 ms",new Vector2(510,-368),new Vector2(200,24),16).alignment=TextAnchor.MiddleRight;
            ButtonAt(box,"RETRY",new Vector2(30,-408),new Vector2(104,36),Begin);
            previewButton=ButtonAt(box,"PREVIEW",new Vector2(144,-408),new Vector2(104,36),Preview);
            saveButton=ButtonAt(box,"SAVE",new Vector2(258,-408),new Vector2(104,36),Save);
            ButtonAt(box,"RESET",new Vector2(372,-408),new Vector2(104,36),ResetProfile);
            var cue=ButtonAt(box,"CUES: OFF",new Vector2(486,-408),new Vector2(124,36),()=>{cues=!cues;cueLabel.text=cues?"CUES: ON":"CUES: OFF";Begin();});
            cueLabel=cue.GetComponentInChildren<Text>();
            ButtonAt(box,Standalone ? "ENTER BATTLE" : "CLOSE",new Vector2(620,-408),new Vector2(124,36),()=>
            {
                if (Standalone) enterBattle(); else Close();
            });
            Label(box,"This measures habit + device/audio latency. Keep the same headphones and output device.",new Vector2(30,-454),new Vector2(720,24),14);
            panel.SetActive(false);
        }

        static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=size;return r;
        }
        Text Label(Transform parent,string text,Vector2 pos,Vector2 size,int fontSize)
        {
            var t=Rect("Label",parent,pos,size).gameObject.AddComponent<Text>();t.font=font;t.fontSize=fontSize;t.text=text;
            t.color=Color.white;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;return t;
        }
        Button ButtonAt(Transform parent,string text,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action)
        {
            var r=Rect(text,parent,pos,size);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.08f,.22f,.3f);
            var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;button.navigation=new Navigation{mode=Navigation.Mode.None};
            button.onClick.AddListener(action);var label=Label(r,text,Vector2.zero,size,16);label.alignment=TextAnchor.MiddleCenter;return button;
        }
    }
}
