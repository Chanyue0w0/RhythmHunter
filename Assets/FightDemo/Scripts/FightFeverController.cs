using System;
using UnityEngine;
using RhythmHunter.FightDemo.Art2;

namespace RhythmHunter.FightDemo
{
    [DisallowMultipleComponent, DefaultExecutionOrder(150)]
    public sealed partial class FightFeverController : MonoBehaviour
    {
        public enum Phase { Idle, Introduction, Flight, Charging, Released, Returning, Explanation, Performing }
        [SerializeField] private GameObject chargeEffectPrefab;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private FightFeverCharge charge = new();
        [SerializeField, Min(.1f)] private float flightSeconds = .85f;
        [SerializeField, Range(0, .4f)] private float cameraZoom = .22f;
        public Phase CurrentPhase { get; private set; }
        public bool Active => CurrentPhase == Phase.Flight || CurrentPhase == Phase.Charging || CurrentPhase == Phase.Released || CurrentPhase == Phase.Returning || CurrentPhase == Phase.Performing;
        public float Progress => charge.Progress;
        public bool TutorialCompleted { get; private set; }
        public Vector3 DamagePresentationOffset => Active && stage ? stageOffset : Vector3.zero;
        public event Action ChargeSucceeded;
        private FightCombatController fight;
        private FightScenePresenter presenter;
        private FightGamepadRumble rumble;
        private Art2CameraController cameraRig;
        private bool tutorial, introShown, lastSuccess;
        private int page, enteredFrame;
        private float phaseTime, pulse, overshoot, displayProgress;
        private double lastBeat;
        private Vector2 gemOrigin;

        private void Start()
        {
            fight = GetComponent<FightCombatController>(); presenter = GetComponent<FightScenePresenter>();
            rumble = GetComponent<FightGamepadRumble>() ?? gameObject.AddComponent<FightGamepadRumble>();
            if (!battleCamera) battleCamera = Camera.main;
            cameraRig = battleCamera ? battleCamera.GetComponent<Art2CameraController>() : null;
            tutorial = GetComponent<FightLevelPlanner>()?.ActiveStage?.TeachesFever == true;
            fight.BattleLost += Abort; fight.BattleVictory += Abort; fight.RosterRebuilt += Abort;
            BuildUi();
        }
        public bool ConsumeCommand(FightInputRouter.HeroCommand command)
        {
            if (!isActiveAndEnabled || !fight || fight.IsPaused || fight.TimingCalibrationActive) return false;
            bool a = command == FightInputRouter.HeroCommand.TeamSkill;
            if (CurrentPhase == Phase.Introduction || CurrentPhase == Phase.Explanation)
            {
                if (a && enteredFrame != Time.frameCount) AdvancePage();
                return true;
            }
            if (Active)
            {
                if (a && enteredFrame != Time.frameCount)
                {
                    if (CurrentPhase == Phase.Charging) Press();
                }
                return true;
            }
            if (a && !fight.TeamSkillActivationBlocked && fight.TeamSkillReady && !fight.WaveEntranceRunning)
            {
                Begin(); return true;
            }
            return false;
        }
        private void SetPhase(Phase phase)
        { CurrentPhase = phase; phaseTime = 0; enteredFrame = Time.frameCount; }
        private void Begin()
        {
            if (!battleCamera || !presenter || !chargeEffectPrefab) { Debug.LogError("Fever camera, presenter or charge effect is missing.", this); return; }
            gemOrigin = ScreenToUi(presenter.FeverGemScreenPosition);
            charge.Reset(); displayProgress = overshoot = pulse = 0; lastSuccess = false;
            fight.SetFeverMana(false); fight.SetFeverSuspended(true);
            presenter.SetFeverHudHidden(true);
            BeginStage(); SetPhase(Phase.Flight);
            show.gameObject.SetActive(true); dialogue.gameObject.SetActive(false);
            hint.rectTransform.anchoredPosition = new Vector2(0, -270);
            gem.sprite = presenter.FeverFullGem; gem.gameObject.SetActive(true);
            barRoot.gameObject.SetActive(false); flash.color = Color.clear;
        }
        private double BeatPosition()
        {
            var clock = fight.BeatClock;
            if (clock == null || !clock.HasTimingAnchor || clock.MillisecondsPerBeat <= 0 || !clock.TryGetTimelinePositionMs(out int ms)) return lastBeat;
            return clock.LatestBeat.GlobalBeat + (ms - clock.LatestBeat.TimelinePositionMs) / clock.MillisecondsPerBeat;
        }
        private void AdvanceChargeClock()
        {
            double now = BeatPosition();
            charge.Advance(Math.Max(0, now - lastBeat)); lastBeat = now;
            if (charge.Expired) BeginReturn();
        }
        private void Press()
        {
            AdvanceChargeClock();
            if (CurrentPhase != Phase.Charging || !charge.Press()) return;
            pulse = 1; overshoot = .035f + charge.Progress * .02f;
            rumble.Pulse(Mathf.Lerp(.2f, .65f, charge.Progress));
            if (charge.Won)
            {
                lastSuccess = true; SetPhase(Phase.Released); ReleaseToHeroes();
                rumble.Pulse(.8f, .16f); ChargeSucceeded?.Invoke();
                hint.text = "團隊共鳴！";
            }
        }
        private void Update()
        {
            if (!fight || !ui) return;
            bool paused = fight.IsPaused || fight.TimingCalibrationActive;
            ui.enabled = !paused;
            if (stageCamera) stageCamera.enabled = !paused;
            SetStagePaused(paused);
            if (paused) { rumble.Stop(); cameraRig?.SetFeverShake(Vector2.zero); return; }
            if (tutorial && !introShown && !fight.AwaitingBattleStart && !fight.WaveTransitionActive && !fight.BattleEntranceRunning && !fight.BattleEnded)
            {
                introShown = true; fight.SetFeverMana(true); fight.SetFeverSuspended(true);
                SetPhase(Phase.Introduction); page = 0; ShowPage();
            }
            phaseTime += Time.unscaledDeltaTime;
            if (CurrentPhase == Phase.Flight && phaseTime >= flightSeconds)
            { SetPhase(Phase.Charging); lastBeat = BeatPosition(); barRoot.gameObject.SetActive(true); }
            if (CurrentPhase == Phase.Charging) AdvanceChargeClock();
            if (CurrentPhase == Phase.Released && phaseTime >= .6f) BeginPerformances();
            if (CurrentPhase == Phase.Performing) AdvancePerformance();
            AdvancePerformanceEffects(Time.unscaledDeltaTime);
            if (CurrentPhase == Phase.Returning && phaseTime >= .45f) FinishReturn();
            pulse = Mathf.MoveTowards(pulse, 0, Time.unscaledDeltaTime * 7);
            overshoot = Mathf.MoveTowards(overshoot, 0, Time.unscaledDeltaTime * .25f);
            displayProgress = Mathf.MoveTowards(displayProgress, charge.Progress, Time.unscaledDeltaTime * 3);
        }
        private void LateUpdate()
        {
            if (!Active || !fight || fight.IsPaused || fight.TimingCalibrationActive) return;
            float weight = CurrentPhase == Phase.Flight ? Mathf.SmoothStep(0, 1, phaseTime / flightSeconds) :
                CurrentPhase == Phase.Returning ? 1 - Mathf.SmoothStep(0, 1, phaseTime / .45f) : 1;
            Present(weight);
        }
        private void BeginReturn()
        { SetPhase(Phase.Returning); barRoot.gameObject.SetActive(false); rumble.Stop(); }
        private void FinishReturn()
        {
            CleanupStage(); presenter.SetFeverHudHidden(false); show.gameObject.SetActive(false);
            fight.SetFeverMana(false);
            if (tutorial && lastSuccess && !TutorialCompleted)
            {
                TutorialCompleted = true; SetPhase(Phase.Idle);
                dialogue.gameObject.SetActive(false); hint.text = "";
                fight.SetFeverSuspended(false); fight.CompleteFeverTutorial();
            }
            else
            {
                SetPhase(Phase.Idle); fight.SetFeverSuspended(false);
                hint.text = tutorial ? "未能解放能量。呼叫勇者重新累積魔力，再按 A 挑戰。" : "";
            }
        }
        private void AdvancePage()
        {
            if (CurrentPhase == Phase.Explanation)
            {
                TutorialCompleted = true; SetPhase(Phase.Idle); dialogue.gameObject.SetActive(false);
                fight.SetFeverSuspended(false); hint.text = "Fever 教學完成！可繼續自由練習。"; return;
            }
            if (++page < 2) { enteredFrame = Time.frameCount; ShowPage(); return; }
            dialogue.gameObject.SetActive(false); SetPhase(Phase.Idle); fight.SetFeverSuspended(false);
            hint.rectTransform.anchoredPosition = new Vector2(0, 210);
            hint.text = "魔力已充滿！按 A 啟動 Fever，再連打 A 解放能量。";
        }
        private void Abort()
        {
            CleanupStage();
            if (presenter) presenter.SetFeverHudHidden(false);
            if (fight) fight.SetFeverSuspended(false);
            rumble?.Stop(); CurrentPhase = Phase.Idle;
            if (hint) hint.text = "";
            if (show) show.gameObject.SetActive(false);
            if (dialogue) dialogue.gameObject.SetActive(false);
        }
        private void OnDisable() => Abort();
        private void OnDestroy()
        {
            if (fight) { fight.BattleLost -= Abort; fight.BattleVictory -= Abort; fight.RosterRebuilt -= Abort; }
            if (ui) Destroy(ui.gameObject);
            if (font) Destroy(font);
            if (gradient) Destroy(gradient);
            if (gradientSprite) Destroy(gradientSprite);
        }
    }
}
