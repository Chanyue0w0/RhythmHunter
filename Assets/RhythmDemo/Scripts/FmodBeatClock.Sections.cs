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
        [Header("Music Sections")]
        [Tooltip("Testing only. Off leaves battle music unchanged until stage integration.")]
        [SerializeField] private SectionPreview sectionPreview;
        [Tooltip("Fade duration when stopping music. Native overtime loops keep full volume.")]
        [SerializeField, Min(0)] private float sectionFadeOutSeconds = .15f;
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
        private bool stageManagedSections, stageSectionsConfigured;
        [Tooltip("Song seconds for each side of a Boss overtime/debug jump. Natural continuation and overtime loops never fade.")]
        [SerializeField, Min(0)] private float bossSectionJumpFadeSeconds = .08f;
        private int bossJumpTarget = -1, bossJumpFadeStart;
        private bool bossJumpFadingIn;
        private bool bossJumpAwaitingNative;
        public bool BossSectionJumpInProgress => bossJumpTarget >= 0 || bossJumpFadingIn || bossJumpAwaitingNative;

        public bool DisableOvertimeMusic()
        {
            if (!musicInstance.isValid()) return false;
            var result = musicInstance.setParameterByName(overtimeParameter, 0, true);
            if (result != RESULT.OK) { ReportError("Cannot leave Overtime: " + result); return false; }
            OvertimeMusicEnabled = false;
            return true;
        }

        // Called outside Beat callbacks. Raw FMOD position changes; gameplay time remains monotonic.
        public bool JumpToBossSection()
        {
            if (!IsPlaying || paused || fadeStopping || BossSectionJumpInProgress ||
                !TryGetMarkerPosition("Normal_End", out int start) ||
                !TryGetMarkerPosition("Boss_End", out int end) || end <= start ||
                !TryGetTimelinePositionMs(out bossJumpFadeStart)) return false;
            bossJumpTarget = start;
            return true;
        }

        private bool ExecuteBossSectionJump()
        {
            // Flush once at the explicit seek, not every frame. Drain old callbacks before remapping time.
            if (musicInstance.getSystem(out var system) != RESULT.OK) return false;
            system.flushCommands();
            if (!TryGetTimelinePositionMs(out int before)) return false;
            if (!DisableOvertimeMusic() || musicInstance.setTimelinePosition(bossJumpTarget) != RESULT.OK) return false;
            system.flushCommands();
            while (pendingBeats.TryDequeue(out _)) { }
            while (pendingMarkers.TryDequeue(out _)) { }
            loopOffsetMs = before - bossJumpTarget;
            hasAnchor = false;
            normalEndSent = true;
            bossJumpFadeStart = before;
            bossJumpAwaitingNative = true;
            return true;
        }
        public void ConfigureStageSections(bool enabled)
        {
            stageSectionsConfigured = true;
            stageManagedSections = enabled;
        }

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
            if (bossJumpAwaitingNative) return bossJumpFadeStart;
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
            // Arm before the endpoint; a frame-late gameplay timeout cannot miss the native loop.
            if (stageManagedSections || !stageSectionsConfigured && sectionPreview == SectionPreview.LoopOvertime) EnableOvertimeMusic();
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
            => FadeOutAndStop(sectionFadeOutSeconds);

        // Duration in song seconds, so a pitched-down event fades over the same musical beats.
        public void FadeOutAndStop(float songSeconds)
        {
            if (!IsPlaying || fadeStopping || !TryGetTimelinePositionMs(out fadeStopStart)) return;
            fadeStopping = true;
            fadeStopDuration = Mathf.Max(0, songSeconds * 1000);
            fadeStopGain = sectionGain;
            if (fadeStopDuration == 0) StopMusic();
        }
        private void UpdateMusicSections()
        {
            if (!IsPlaying || paused || !TryGetRawTimelinePositionMs(out int raw)) return;
            if (bossJumpAwaitingNative)
            {
                // flushCommands acknowledges command submission, not the mixer timeline snapshot.
                if (raw < bossJumpTarget) { musicInstance.setVolume(0); return; }
                while (pendingBeats.TryDequeue(out _)) { }
                while (pendingMarkers.TryDequeue(out _)) { }
                lastRawBeat = bossJumpTarget;
                bossJumpTarget = -1;
                bossJumpAwaitingNative = false;
                bossJumpFadingIn = true;
            }
            while (pendingMarkers.TryDequeue(out var marker)) MarkerReached?.Invoke(marker);
            if (!normalEndSent && TryGetMarkerPosition("Normal_End", out int normalEnd) &&
                (raw >= normalEnd || CompletedMusicLoops > 0))
            { normalEndSent = true; NormalEndReached?.Invoke(); }
            if (!IsPlaying) return;
            float gain = 1;
            if (BossSectionJumpInProgress && !fadeStopping)
            {
                float elapsed = Mathf.Max(0, ContinuousPosition(raw) - bossJumpFadeStart);
                float t = bossSectionJumpFadeSeconds <= 0 ? 1 : Mathf.Clamp01(elapsed / (bossSectionJumpFadeSeconds * 1000));
                if (bossJumpTarget >= 0)
                {
                    gain = 1 - t;
                    if (t >= 1 && !ExecuteBossSectionJump())
                    { bossJumpTarget = -1; ReportError("Boss music seek failed. Check FMOD section markers and parameter."); }
                }
                else
                {
                    gain = t;
                    if (t >= 1) bossJumpFadingIn = false;
                }
            }
            if (fadeStopping)
            {
                gain = fadeStopGain * (1 - Mathf.Clamp01((ContinuousPosition(raw) - fadeStopStart) / Mathf.Max(1, fadeStopDuration)));
                if (gain <= 0) { StopMusic(); return; }
            }
            else if (!OvertimeMusicEnabled && !stageSectionsConfigured &&
                sectionPreview == SectionPreview.StopAtNormalEnd && HasSectionMarkers)
            {
                TryGetMarkerPosition("Normal_End", out int normalEndMs);
                if (sectionFadeOutSeconds > 0) gain *= Mathf.Clamp01((normalEndMs - raw) / (sectionFadeOutSeconds * 1000));
                if (raw >= normalEndMs) { StopMusic(); return; }
            }
            sectionGain = gain;
            musicInstance.setVolume(musicVolume * sectionGain);
        }
        private void ResetMusicSections()
        {
            OvertimeMusicEnabled = false; CompletedMusicLoops = 0;
            bossJumpTarget = -1; bossJumpFadingIn = bossJumpAwaitingNative = false;
            loopOffsetMs = 0; lastRawBeat = -1; normalEndSent = false; fadeStopping = false; sectionGain = 1;
            while (pendingMarkers.TryDequeue(out _)) { }
        }
    }
}
