using System;
using System.Collections.Concurrent;
using FMOD;
using FMOD.Studio;
using UnityEngine;

namespace RhythmHunter.RhythmDemo
{
    public sealed partial class FmodBeatClock
    {
        [Serializable] public struct SongMarker
        {
            public string name;
            public int positionMs;
        }
        public enum SectionPreview { Off, StopAtNormalEnd, LoopOvertime }
        [Header("Music Sections (non-Boss)")]
        [Tooltip("Testing only. Off leaves battle music unchanged until stage integration.")]
        [SerializeField] private SectionPreview sectionPreview;
        [SerializeField, Min(0)] private float sectionFadeOutSeconds = .15f;
        [SerializeField, Min(0)] private float sectionFadeInSeconds = .15f;
        [SerializeField] private string overtimeParameter = "Overtime";
        [SerializeField] private SongMarker[] songMarkers = Array.Empty<SongMarker>();
        [SerializeField, HideInInspector] private string markerEventPath;
        public event Action<SongMarker> MarkerReached;
        public event Action NormalEndReached;
        public bool OvertimeMusicEnabled { get; private set; }
        public int CompletedMusicLoops { get; private set; }
        public bool HasSectionMarkers => markerEventPath == musicEventPath &&
            TryGetMarkerPosition("Normal_End", out _) && TryGetMarkerPosition("Overtime_Start", out int start) &&
            TryGetMarkerPosition("Overtime_End", out int end) && end > start;
#if UNITY_EDITOR
        public static Func<string, SongMarker[]> EditorMarkerResolver;
#endif
        private readonly ConcurrentQueue<SongMarker> pendingMarkers = new();
        private int lastRawBeat = -1;
        private double loopOffsetMs;
        private bool normalEndSent, fadeStopping;
        private int fadeStopStart;
        private float fadeStopDuration, sectionGain = 1, fadeStopGain;

        public void ConfigureSongMarkers(SongMarker[] markers)
        {
            songMarkers = markers ?? Array.Empty<SongMarker>();
            markerEventPath = musicEventPath;
        }
        public bool TryGetMarkerPosition(string name, out int milliseconds)
        {
            milliseconds = 0;
            if (markerEventPath != musicEventPath) return false;
            foreach (var marker in songMarkers)
                if (marker.name == name) { milliseconds = marker.positionMs; return true; }
            return false;
        }
        public bool TryGetRawTimelinePositionMs(out int ms)
        {
            ms = 0;
            return musicInstance.isValid() && musicInstance.getTimelinePosition(out ms) == RESULT.OK;
        }
        private double LoopLengthMs
        {
            get
            {
                if (!TryGetMarkerPosition("Overtime_Start", out int start) || !TryGetMarkerPosition("Overtime_End", out int end)) return 0;
                double length = end - start;
                // FMOD callbacks expose integer milliseconds. Avoid accumulating rounding error per loop.
                double beat = MillisecondsPerBeat;
                return beat > 0 && Math.Abs(length / beat - Math.Round(length / beat)) < .01
                    ? Math.Round(length / beat) * beat : length;
            }
        }

        // The FMOD timeline wraps; gameplay time does not. No second beat timer is introduced.
        private int ContinuousPosition(int raw)
        {
            double offset = loopOffsetMs;
            if (OvertimeMusicEnabled && lastRawBeat >= 0 && raw < lastRawBeat - 2 && LoopLengthMs > 0)
                offset += LoopLengthMs; // audio thread wrapped before its next beat callback was drained
            return (int)Math.Round(raw + offset);
        }
        private int MapBeatPosition(int raw)
        {
            if (OvertimeMusicEnabled && lastRawBeat >= 0 && raw < lastRawBeat - 2 && LoopLengthMs > 0)
            {
                loopOffsetMs += LoopLengthMs;
                CompletedMusicLoops++;
            }
            lastRawBeat = raw;
            return (int)Math.Round(raw + loopOffsetMs);
        }
        private void PrepareMusicSections()
        {
#if UNITY_EDITOR
            if (EditorMarkerResolver != null) ConfigureSongMarkers(EditorMarkerResolver(musicEventPath));
#endif
            if (sectionPreview == SectionPreview.LoopOvertime) EnableOvertimeMusic();
        }
        public bool EnableOvertimeMusic()
        {
            if (!musicInstance.isValid() || !HasSectionMarkers) return false;
            var result = musicInstance.setParameterByName(overtimeParameter, 1, true);
            if (result != RESULT.OK) { ReportError("Overtime parameter unavailable; rebuild FMOD banks: " + result); return false; }
            OvertimeMusicEnabled = true;
            return true;
        }
        [ContextMenu("Music Test/Enable Overtime Loop")]
        private void PreviewOvertime() { if (Application.isPlaying) EnableOvertimeMusic(); }
        [ContextMenu("Music Test/Fade Out And Stop")]
        private void PreviewFadeStop() { if (Application.isPlaying) FadeOutAndStop(); }
        public void FadeOutAndStop()
        {
            if (!IsPlaying || fadeStopping || !TryGetTimelinePositionMs(out fadeStopStart)) return;
            fadeStopping = true;
            fadeStopDuration = Mathf.Max(0, sectionFadeOutSeconds * 1000);
            fadeStopGain = sectionGain;
            if (fadeStopDuration == 0) StopMusic();
        }
        private void UpdateMusicSections()
        {
            if (!IsPlaying || paused || !TryGetRawTimelinePositionMs(out int raw)) return;
            while (pendingMarkers.TryDequeue(out var marker)) MarkerReached?.Invoke(marker);
            if (!normalEndSent && TryGetMarkerPosition("Normal_End", out int normalEnd) &&
                (raw >= normalEnd || CompletedMusicLoops > 0))
            { normalEndSent = true; NormalEndReached?.Invoke(); }
            if (!IsPlaying) return;
            float gain = 1;
            if (fadeStopping)
            {
                gain = fadeStopGain * (1 - Mathf.Clamp01((ContinuousPosition(raw) - fadeStopStart) / Mathf.Max(1, fadeStopDuration)));
                if (gain <= 0) { StopMusic(); return; }
            }
            else if (HasSectionMarkers)
            {
                TryGetMarkerPosition("Normal_End", out int normalEndMs);
                TryGetMarkerPosition("Overtime_Start", out int start);
                TryGetMarkerPosition("Overtime_End", out int end);
                int stop = OvertimeMusicEnabled ? end : normalEndMs;
                if (OvertimeMusicEnabled || sectionPreview == SectionPreview.StopAtNormalEnd)
                {
                    if (sectionFadeOutSeconds > 0) gain *= Mathf.Clamp01((stop - raw) / (sectionFadeOutSeconds * 1000));
                    if (OvertimeMusicEnabled && CompletedMusicLoops > 0 && sectionFadeInSeconds > 0)
                        gain *= Mathf.Clamp01((raw - start) / (sectionFadeInSeconds * 1000));
                    if (!OvertimeMusicEnabled && raw >= stop) { StopMusic(); return; }
                }
            }
            sectionGain = gain;
            musicInstance.setVolume(musicVolume * sectionGain);
        }
        private void ResetMusicSections()
        {
            OvertimeMusicEnabled = false; CompletedMusicLoops = 0;
            loopOffsetMs = 0; lastRawBeat = -1; normalEndSent = false; fadeStopping = false; sectionGain = 1;
            while (pendingMarkers.TryDequeue(out _)) { }
        }
    }
}
