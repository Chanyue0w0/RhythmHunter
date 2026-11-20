using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    [DisallowMultipleComponent]
    public sealed class FightVictoryPanel : MonoBehaviour
    {
        [SerializeField] private FightStageResults results;
        [SerializeField] private bool showVictoryPanel = true;
        [SerializeField] private bool showDefeatPanel = true;
        [SerializeField] private Font font;
        [SerializeField] private Color backdropColor = new(.015f,.025f,.04f,.88f);
        [SerializeField] private Color accentColor = new(1f,.8f,.28f);
        [SerializeField] private Color defeatAccentColor = new(1f,.35f,.3f);
        [Header("Defeat Presentation")]
        [SerializeField, Min(0)] private float defeatDelayBeats = 2;
        [SerializeField, Min(0)] private float defeatFadeSeconds = .35f;
        [Header("Result Reveal")]
        [SerializeField, Min(3)] private float minimumResultSeconds = 3;
        [SerializeField, Min(1)] private float charactersPerSecond = 35;
        [SerializeField, Min(0)] private float itemDelaySeconds = .15f;
        [SerializeField, Min(0)] private float ratingHoldSeconds = .6f;
        [SerializeField] private bool showDeveloperInfo = true;
        GameObject canvasObject, developerInfo;
        CanvasGroup presentationGroup;
        FightMenuNavigation navigation;
        Font runtimeFont;
        Text actionHint;
        float presentationDelay, presentationFadeElapsed, revealElapsed;
        bool slowMotionPresentation, presentationComplete;
        readonly List<Text> values = new();
        readonly List<string> finalValues = new();
        public bool IsVisible => canvasObject && canvasObject.activeSelf;
        public bool ActionsReady => IsVisible && navigation && navigation.Ready && navigation.Armed;
        void OnEnable()
        {
            if (!results) results = GetComponent<FightStageResults>();
            if (!results) return;
            results.ResultReady += Show;
            if (results.Completed) Show();
        }
        void OnDisable() { if (results) results.ResultReady -= Show; if (canvasObject) canvasObject.SetActive(false); }
        void OnDestroy()
        {
            if (canvasObject) { if (Application.isPlaying) Destroy(canvasObject); else DestroyImmediate(canvasObject); }
            if (runtimeFont) { if (Application.isPlaying) Destroy(runtimeFont); else DestroyImmediate(runtimeFont); }
        }
        void Update()
        {
            if (!IsVisible || (results && results.PresentationPaused)) return;
            if (!presentationComplete)
            {
                if (slowMotionPresentation)
                {
                    presentationGroup.alpha = results.DefeatPresentationProgress;
                    if (presentationGroup.alpha >= 1) CompletePresentation();
                }
                else AdvancePresentation(Time.unscaledDeltaTime);
                return;
            }
            AdvanceReveal(Time.unscaledDeltaTime);
            if (developerInfo) developerInfo.SetActive(showDeveloperInfo);
        }
        static void Center(RectTransform r) => r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f,.5f);
        Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize, Font labelFont)
        {
            var label = FightMenuUi.Label(parent,text,position,size,labelFont,fontSize);
            Center(label.rectTransform); label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }
        static string Duration(double seconds) => TimeSpan.FromSeconds(Math.Max(0,seconds)).ToString(@"mm\:ss\.f");
        void Show()
        {
            if (!results || !results.Completed || !(results.Won ? showVictoryPanel : showDefeatPanel)) return;
            if (canvasObject) { canvasObject.SetActive(true); return; }
            canvasObject = new GameObject("PlayerResultCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform,false);
            presentationGroup = canvasObject.AddComponent<CanvasGroup>();
            var clock = results.BeatClock;
            float secondsPerBeat = clock ? (float)(clock.MillisecondsPerBeat > 0 ? clock.MillisecondsPerBeat / 1000d : clock.OpeningSecondsPerBeat) / Mathf.Max(.01f,clock.MusicPitch) : .5f;
            presentationDelay = results.Won ? 0 : defeatDelayBeats * secondsPerBeat;
            slowMotionPresentation = !results.Won && results.DefeatSlowMotionActive;
            presentationGroup.alpha = results.Won ? 1 : 0;
            presentationGroup.interactable = true;
            presentationGroup.blocksRaycasts = true;
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            var panel = FightMenuUi.Rect(results.Won ? "VictoryPanel" : "DefeatPanel",canvas.transform,Vector2.zero,Vector2.zero);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = backdropColor;
            Font labelFont = font;
            if (!labelFont)
            {
                runtimeFont = Font.CreateDynamicFontFromOSFont(new[]{"Microsoft JhengHei","PingFang TC","Noto Sans CJK TC","Arial Unicode MS"},28);
                labelFont = runtimeFont ? runtimeFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            Color accent = results.Won ? accentColor : defeatAccentColor;
            Label(panel,results.Won ? "VICTORY" : "DEFEAT",new Vector2(0,350),new Vector2(800,90),64,labelFont).color = accent;
            string[] headings = {"完成波數","戰鬥時間","成功格擋","受擊次數","剩餘愛心","最終評價"};
            string[] content = {
                $"{results.ClearedWaves} / {results.TotalWaves}", Duration(results.ElapsedSeconds),
                results.SuccessfulBlocks + " 次", results.ReceivedHits + " 次",
                $"{results.RemainingHp / FightCombatUnits.PointsPerIcon:0.#} / {results.MaximumHp / FightCombatUnits.PointsPerIcon:0.#}", results.RatingText };
            for (int i = 0; i < headings.Length; i++)
            {
                float y = 235 - i * 70;
                Label(panel,headings[i],new Vector2(-170,y),new Vector2(260,58),28,labelFont);
                var value = Label(panel,"",new Vector2(160,y),new Vector2(350,58),i == 5 ? 44 : 30,labelFont);
                value.color = i == 5 ? accent : Color.white; values.Add(value); finalValues.Add(content[i]);
            }
            var dev = Label(panel,$"DEVELOPER\n{results.ChapterName}\n{results.StageName}\nWAVES {results.ClearedWaves}/{results.TotalWaves}\nSTAGE {Duration(results.ElapsedSeconds)}\nSONG {Duration(results.SongLengthSeconds)}\nPROGRESS {Duration(results.SongProgressSeconds)}\nOVERTIME {(results.UsedOvertime ? "YES" : "NO")}  +{Duration(results.OvertimeSeconds)}\nTOOK DAMAGE {results.TookDamage}",new Vector2(690,130),new Vector2(420,420),20,labelFont);
            dev.alignment = TextAnchor.UpperLeft; dev.color = new Color(.65f,.72f,.8f); developerInfo = dev.gameObject; developerInfo.SetActive(showDeveloperInfo);
            var buttons = FightMenuUi.Rect("ResultActions",panel,Vector2.zero,Vector2.zero); Center(buttons);
            navigation = buttons.gameObject.AddComponent<FightMenuNavigation>(); navigation.Ready = false;
            buttons.GetComponent<CanvasGroup>().interactable = false;
            var planner = GetComponent<FightLevelPlanner>();
            bool hasNext = results.Won && planner && planner.TryFindNextStage(out _,out _);
            if (hasNext) AddButton(buttons,"下一場戰鬥",new Vector2(-210,-245),labelFont,Next);
            AddButton(buttons,"再來一場",new Vector2(hasNext ? 210 : 0,-245),labelFont,Restart);
            actionHint = Label(panel,"結算中…",new Vector2(0,-335),new Vector2(880,55),22,labelFont);
            if (results.Won) CompletePresentation();
        }
        void AddButton(Transform parent,string caption,Vector2 position,Font labelFont,UnityEngine.Events.UnityAction action)
        {
            var button = FightMenuUi.Button(parent,caption,position,new Vector2(360,62),labelFont,action);
            Center((RectTransform)button.transform);
            var text = button.GetComponentInChildren<Text>(); Center(text.rectTransform); text.rectTransform.anchoredPosition = Vector2.zero; text.fontSize = 27;
        }
        void Next()
        {
            if (!ActionsReady || !results.Won) return;
            var planner = GetComponent<FightLevelPlanner>(); var pause = GetComponent<FightPauseController>();
            if (planner && pause && planner.TryFindNextStage(out var chapter,out int index) && !pause.TryLoadStage(chapter,index,out string error))
                actionHint.text = error;
        }
        void Restart() { if (ActionsReady) GetComponent<FightPauseController>()?.RestartBattle(); }
        private void AdvancePresentation(float seconds)
        {
            float consumed = Mathf.Min(presentationDelay,seconds); presentationDelay -= consumed; seconds -= consumed;
            if (presentationDelay > 0) return;
            presentationFadeElapsed += seconds;
            presentationGroup.alpha = defeatFadeSeconds <= 0 ? 1 : Mathf.Clamp01(presentationFadeElapsed / defeatFadeSeconds);
            if (presentationGroup.alpha >= 1) CompletePresentation();
        }
        private void CompletePresentation()
        {
            if (presentationComplete) return;
            presentationComplete = true; revealElapsed = 0;
            if (results && !results.Won) GetComponent<FightScenePresenter>()?.HideBattleUiAfterDefeat();
        }
        private void AdvanceReveal(float seconds)
        {
            revealElapsed += Mathf.Max(0,seconds);
            float cursor = 0;
            for (int i = 0; i < values.Count; i++)
            {
                string content = finalValues[i];
                int length = Mathf.Clamp(Mathf.FloorToInt((revealElapsed-cursor) * Mathf.Max(1,charactersPerSecond)),0,content.Length);
                values[i].text = content.Substring(0,length);
                cursor += content.Length / Mathf.Max(1,charactersPerSecond) + (i == values.Count-1 ? ratingHoldSeconds : itemDelaySeconds);
            }
            navigation.Ready = revealElapsed >= Mathf.Max(3,minimumResultSeconds) && revealElapsed >= cursor;
            actionHint.text = navigation.Ready ? "方向鍵／搖桿選擇　A 確認" : "結算中…";
        }
    }
}
