using System.Collections.Generic;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;
using Debug = UnityEngine.Debug;

namespace RhythmHunter.FightDemo
{
    /// <summary>Scene-owned battle SFX. Music and diagnostic clicks remain owned by FmodBeatClock.</summary>
    [DisallowMultipleComponent]
    public sealed class FightAudioPlayer : MonoBehaviour
    {
        [SerializeField] private bool soundEnabled = true;
        [SerializeField, Range(0, 1)] private float masterVolume = 1;
        [SerializeField, Min(1)] private int maxActiveSounds = 64;
        private struct Voice { public EventInstance instance; public float volume; public bool enemy; }
        private readonly List<Voice> voices = new();
        private readonly HashSet<string> reportedErrors = new();
        private FightCombatController fight;
        public int ActiveSoundCount => voices.Count;

        private void Awake() => fight = GetComponent<FightCombatController>();
        private bool Paused(bool enemy) => fight != null &&
            (fight.IsPaused || fight.TimingCalibrationActive || (enemy && fight.EnemyActionsPaused));

        public bool Play(string eventPath, float volume, Vector3 position, bool enemy = false)
        {
            if (!isActiveAndEnabled || !soundEnabled || Paused(enemy) ||
                (fight != null && fight.BattleEnded) || string.IsNullOrWhiteSpace(eventPath) ||
                volume <= 0 || float.IsNaN(volume) || float.IsInfinity(volume)) return false;
            EventInstance instance = default;
            try
            {
                instance = RuntimeManager.CreateInstance(eventPath);
                RESULT result = instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
                if (result == RESULT.OK) result = instance.setVolume(Mathf.Clamp01(volume) * Mathf.Clamp01(masterVolume));
                if (result == RESULT.OK) result = instance.start();
                if (result != RESULT.OK) { Report(eventPath, result.ToString()); return false; }
                while (voices.Count >= Mathf.Max(1, maxActiveSounds)) ReleaseAt(0);
                voices.Add(new Voice { instance = instance, volume = Mathf.Clamp01(volume), enemy = enemy });
                instance.clearHandle(); // Ownership transferred to the scene list.
                return true;
            }
            catch (EventNotFoundException exception) { Report(eventPath, exception.Message); return false; }
            finally
            {
                if (instance.isValid()) { instance.stop(STOP_MODE.IMMEDIATE); instance.release(); }
            }
        }

        private void Update()
        {
            if (!soundEnabled || (fight != null && fight.BattleEnded)) { StopAll(); return; }
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                var voice = voices[i];
                if (!voice.instance.isValid() || voice.instance.getPlaybackState(out var state) != RESULT.OK || state == PLAYBACK_STATE.STOPPED)
                { ReleaseAt(i); continue; }
                voice.instance.setPaused(Paused(voice.enemy));
                voice.instance.setVolume(voice.volume * Mathf.Clamp01(masterVolume));
            }
        }

        private void Report(string path, string reason)
        {
            if (reportedErrors.Add(path)) Debug.LogWarning($"[FightAudioPlayer] {path}: {reason}", this);
        }
        private void ReleaseAt(int index)
        {
            var instance = voices[index].instance;
            voices.RemoveAt(index);
            if (!instance.isValid()) return;
            instance.stop(STOP_MODE.IMMEDIATE);
            instance.release();
        }
        public void StopAll() { while (voices.Count > 0) ReleaseAt(voices.Count - 1); }
        private void OnDisable() => StopAll();
        private void OnDestroy() => StopAll();
    }

    public sealed partial class FightCombatController
    {
        private FightAudioPlayer battleAudio;
        public FightAudioPlayer AudioPlayer
        {
            get
            {
                if (battleAudio == null) battleAudio = GetComponent<FightAudioPlayer>();
                if (battleAudio == null) battleAudio = gameObject.AddComponent<FightAudioPlayer>();
                return battleAudio;
            }
        }
        private void StopBattleAudio()
        {
            if (battleAudio == null) battleAudio = GetComponent<FightAudioPlayer>();
            if (battleAudio != null) battleAudio.StopAll();
        }
    }
}
