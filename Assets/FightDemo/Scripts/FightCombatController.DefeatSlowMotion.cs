using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        [Header("Defeat Slow Motion (Prototype)")]
        [SerializeField] private bool enableDefeatSlowMotion = true;
        [SerializeField, Range(.25f, 1)] private float defeatPlaybackScale = .5f;
        [SerializeField, Min(.1f)] private float defeatSlowMotionBeats = 2;
        [SerializeField, Min(0)] private float defeatInputGraceBeats = 1;
        private float defeatElapsedBeats, defeatSavedPitch, defeatSavedTimeScale, defeatSavedFixedDelta;
        private float defeatEffectiveScale = 1;
        public bool DefeatSlowMotionActive { get; private set; }
        public bool DefeatInputGraceActive => DefeatSlowMotionActive && defeatElapsedBeats < Mathf.Min(defeatInputGraceBeats, defeatSlowMotionBeats);
        public float DefeatPresentationProgress => Mathf.Clamp01(defeatElapsedBeats / Mathf.Max(.1f, defeatSlowMotionBeats));
        // Beat animations already multiply by MusicPitch; remove only our global slowdown.
        public float BeatPresentationDeltaScale => DefeatSlowMotionActive ? 1 / Mathf.Max(.01f, defeatEffectiveScale) : 1;

        private void BeginDefeatSlowMotion()
        {
            defeatElapsedBeats = 0;
            defeatSavedPitch = beatClock.MusicPitch;
            defeatSavedTimeScale = Time.timeScale;
            defeatSavedFixedDelta = Time.fixedDeltaTime;
            DefeatSlowMotionActive = true;
            BattleWon = false;
            AwaitingNextWave = false;
            stageOvertimeActive = false;
            beatClock.SetPlaybackPitch(defeatSavedPitch * defeatPlaybackScale);
            defeatEffectiveScale = beatClock.MusicPitch / Mathf.Max(.01f, defeatSavedPitch);
            Time.timeScale = defeatSavedTimeScale * defeatEffectiveScale;
            Time.fixedDeltaTime = defeatSavedFixedDelta * defeatEffectiveScale;
            float secondsPerBeat = (float)(beatClock.MillisecondsPerBeat > 0 ? beatClock.MillisecondsPerBeat / 1000d : beatClock.OpeningSecondsPerBeat);
            beatClock.FadeOutAndStop(Mathf.Max(.01f, secondsPerBeat) * defeatSlowMotionBeats);
            // Latch results immediately; the grace window cannot turn this defeat into victory.
            BattleLost?.Invoke();
        }

        private void AdvanceDefeatSlowMotion(float realSeconds)
        {
            if (!DefeatSlowMotionActive || IsPaused) return;
            float secondsPerBeat = (float)(beatClock.MillisecondsPerBeat > 0 ? beatClock.MillisecondsPerBeat / 1000d : beatClock.OpeningSecondsPerBeat);
            defeatElapsedBeats += Mathf.Max(0, realSeconds) * beatClock.MusicPitch / Mathf.Max(.01f, secondsPerBeat);
            if (DefeatPresentationProgress < 1) return;
            battleEnded = true;
            rosterVersion++;
            ResetSwordCharges();
            ResetTeamSkill();
            CancelEnemyCombatWork();
            beatClock.StopMusic();
            RestoreDefeatPlayback();
        }

        private void RestoreDefeatPlayback()
        {
            if (!DefeatSlowMotionActive) return;
            DefeatSlowMotionActive = false;
            beatClock?.SetPlaybackPitch(defeatSavedPitch);
            Time.timeScale = defeatSavedTimeScale;
            Time.fixedDeltaTime = defeatSavedFixedDelta;
        }
    }
}
