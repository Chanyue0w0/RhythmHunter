using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace RhythmHunter.RhythmDemo
{
    /// <summary>
    /// Owns the FMOD music instance and exposes a main-thread beat clock.
    /// The FMOD audio callback only copies plain timing data into a thread-safe queue.
    /// </summary>
    public sealed class FmodBeatClock : MonoBehaviour
    {
        public readonly struct BeatSnapshot
        {
            public BeatSnapshot(
                long globalBeat,
                int bar,
                int beat,
                int timelinePositionMs,
                float tempo,
                int timeSignatureUpper,
                int timeSignatureLower)
            {
                GlobalBeat = globalBeat;
                Bar = bar;
                Beat = beat;
                TimelinePositionMs = timelinePositionMs;
                Tempo = tempo;
                TimeSignatureUpper = timeSignatureUpper;
                TimeSignatureLower = timeSignatureLower;
            }

            public long GlobalBeat { get; }
            public int Bar { get; }
            public int Beat { get; }
            public int TimelinePositionMs { get; }
            public float Tempo { get; }
            public int TimeSignatureUpper { get; }
            public int TimeSignatureLower { get; }
        }

        public readonly struct NearestBeat
        {
            public NearestBeat(
                long globalBeat,
                int bar,
                int beat,
                double timelinePositionMs,
                double deltaMs)
            {
                GlobalBeat = globalBeat;
                Bar = bar;
                Beat = beat;
                TimelinePositionMs = timelinePositionMs;
                DeltaMs = deltaMs;
            }

            public long GlobalBeat { get; }
            public int Bar { get; }
            public int Beat { get; }
            public double TimelinePositionMs { get; }
            public double DeltaMs { get; }
        }

        private readonly struct CallbackBeatData
        {
            public CallbackBeatData(TIMELINE_BEAT_PROPERTIES properties)
            {
                Bar = properties.bar;
                Beat = properties.beat;
                TimelinePositionMs = properties.position;
                Tempo = properties.tempo;
                TimeSignatureUpper = properties.timesignatureupper;
                TimeSignatureLower = properties.timesignaturelower;
            }

            public int Bar { get; }
            public int Beat { get; }
            public int TimelinePositionMs { get; }
            public float Tempo { get; }
            public int TimeSignatureUpper { get; }
            public int TimeSignatureLower { get; }
        }

        [Header("FMOD Music")]
        [SerializeField] private string musicEventPath = "event:/Combat soundtracks/Combat 01";
        [SerializeField, Min(0f)] private float musicStartDelaySeconds = 1f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
        [SerializeField, Range(0.25f, 4f)] private float musicPitch = 1f;
        [SerializeField] private bool playOnStart = true;

        [Header("Beat Diagnostics")]
        [Tooltip("Play BeatTapping on the existing FMOD beat grid. Can be toggled during Play Mode; does not analyse the song or change input calibration.")]
        [SerializeField] private bool enableBeatTapping;
        [SerializeField] private string beatTappingEventPath = "event:/SoundEffects/BeatTapping";
        [SerializeField, Range(0f, 1f)] private float beatTappingVolume = 0.5f;

        private readonly ConcurrentQueue<CallbackBeatData> pendingBeats = new();
        private EventInstance musicInstance;
        private EventInstance beatTappingInstance;
        private string activeBeatTappingPath;
        private EVENT_CALLBACK timelineBeatCallback;
        private GCHandle selfHandle;
        private bool selfHandleAllocated;
        private bool initialized;
        private bool playbackStarted;
        private bool paused;
        private bool hasAnchor;
        private BeatSnapshot latestBeat;
        private long receivedBeatCount;
        private string lastError = string.Empty;
        [SerializeField, HideInInspector] private float openingSongTempo;
        [SerializeField, HideInInspector] private string openingTempoEventPath;
        private float openingTempo;
        public bool OpeningTempoFailed { get; private set; }
        public bool HasOpeningTempo => openingTempo > 0;
        public double OpeningSecondsPerBeat => HasOpeningTempo ? 60d / openingTempo : 0;
#if UNITY_EDITOR
        public static Func<string, float> EditorOpeningTempoResolver;
#endif
        public void ConfigureOpeningTempo(float tempo)
        {
            openingSongTempo = tempo;
            openingTempoEventPath = musicEventPath;
        }

        public void PrepareOpeningTempo()
        {
            // Do not start a muted event: FMOD may virtualize it and never send beats.
            ShutdownMusic();
            receivedBeatCount = 0;
#if UNITY_EDITOR
            if (EditorOpeningTempoResolver != null)
                ConfigureOpeningTempo(EditorOpeningTempoResolver(musicEventPath));
#endif
            openingTempo = openingTempoEventPath == musicEventPath ? openingSongTempo : 0;
            OpeningTempoFailed = openingTempo <= 0;
            if (OpeningTempoFailed)
                ReportError("Opening tempo metadata missing. Check the FMOD event Tempo Marker and rebuild.");
        }

        public event Action<BeatSnapshot> Beat;
        public event Action<string> PlaybackError;

        public string MusicEventPath => musicEventPath;
        public float MusicStartDelaySeconds => musicStartDelaySeconds;
        public float MusicVolume => musicVolume;
        public float MusicPitch => musicPitch;
        public bool IsReady => initialized && musicInstance.isValid();
        public bool IsPlaying => playbackStarted && musicInstance.isValid();
        public bool IsPaused => paused;
        public bool HasTimingAnchor => hasAnchor;
        public BeatSnapshot LatestBeat => latestBeat;
        public long ReceivedBeatCount => receivedBeatCount;
        public string LastError => lastError;
        public double MillisecondsPerBeat => hasAnchor && latestBeat.Tempo > 0f
            ? 60000.0 / latestBeat.Tempo
            : 0.0;

        private void Awake()
        {
            InitializeMusic();
        }

        private IEnumerator Start()
        {
            if (!playOnStart || !IsReady)
                yield break;

            if (musicStartDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(musicStartDelaySeconds);

            if (playOnStart) StartMusic();
        }

        public void SetAutomaticPlayback(bool value) => playOnStart = value;

        private void Update()
        {
            if (!enableBeatTapping || paused || !IsPlaying || activeBeatTappingPath != beatTappingEventPath)
                ReleaseBeatTapping();
            if (beatTappingInstance.isValid())
                beatTappingInstance.setVolume(Mathf.Clamp01(beatTappingVolume));
            if (!paused) ProcessPendingBeats();
        }

        private void OnDisable()
        {
            ReleaseBeatTapping();
        }

        public bool SetPaused(bool value)
        {
            if (musicInstance.isValid())
            {
                RESULT result = musicInstance.setPaused(value);
                if (result != RESULT.OK) { ReportError("FMOD pause failed: " + result); return false; }
            }
            paused = value;
            if (paused) ReleaseBeatTapping();
            return true;
        }

        private void OnDestroy()
        {
            ShutdownMusic();
        }

        public void Configure(
            string eventPath,
            float startDelaySeconds,
            bool shouldPlayOnStart = true,
            float configuredMusicVolume = 1f)
        {
            musicEventPath = eventPath;
            musicStartDelaySeconds = Mathf.Max(0f, startDelaySeconds);
            musicVolume = Mathf.Clamp01(configuredMusicVolume);
            playOnStart = shouldPlayOnStart;
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (!musicInstance.isValid())
                return;

            RESULT result = musicInstance.setVolume(musicVolume);
            if (result != RESULT.OK)
                ReportError($"FMOD failed to set music volume for '{musicEventPath}': {result}");
        }

        public void SetPlaybackPitch(float pitch)
        {
            musicPitch = Mathf.Clamp(pitch, 0.25f, 4f);
            if (!musicInstance.isValid())
                return;

            RESULT result = musicInstance.setPitch(musicPitch);
            if (result != RESULT.OK)
                ReportError($"FMOD failed to set music pitch for '{musicEventPath}': {result}");
        }

        public void StartMusic()
        {
            if (!IsReady || playbackStarted)
                return;

            musicInstance.setPaused(paused);

            RESULT result = musicInstance.start();
            if (result != RESULT.OK)
            {
                ReportError($"FMOD failed to start '{musicEventPath}': {result}");
                return;
            }

            playbackStarted = true;
        }

        public void StopMusic()
        {
            ShutdownMusic();
        }

        public void RestartMusic()
        {
            ShutdownMusic();
            receivedBeatCount = 0;
            InitializeMusic();
            StartMusic();
        }

        public bool TryGetTimelinePositionMs(out int timelinePositionMs)
        {
            timelinePositionMs = 0;
            if (!musicInstance.isValid())
                return false;

            RESULT result = musicInstance.getTimelinePosition(out timelinePositionMs);
            return result == RESULT.OK;
        }

        public bool TryGetNearestBeat(double evaluatedTimelineMs, out NearestBeat nearestBeat)
        {
            nearestBeat = default;
            if (!hasAnchor || latestBeat.Tempo <= 0f)
                return false;

            double intervalMs = 60000.0 / latestBeat.Tempo;
            double beatDistance = (evaluatedTimelineMs - latestBeat.TimelinePositionMs) / intervalMs;
            long beatOffset = (long)Math.Round(beatDistance, MidpointRounding.AwayFromZero);
            double targetPositionMs = latestBeat.TimelinePositionMs + beatOffset * intervalMs;
            double deltaMs = evaluatedTimelineMs - targetPositionMs;

            AdvanceMusicalPosition(
                latestBeat.Bar,
                latestBeat.Beat,
                latestBeat.TimeSignatureUpper,
                beatOffset,
                out int targetBar,
                out int targetBeat);

            nearestBeat = new NearestBeat(
                latestBeat.GlobalBeat + beatOffset,
                targetBar,
                targetBeat,
                targetPositionMs,
                deltaMs);
            return true;
        }

        public bool TryGetBeatPhase(out float phase)
        {
            phase = 0f;
            if (!TryGetTimelinePositionMs(out int timelinePositionMs) || !hasAnchor || latestBeat.Tempo <= 0f)
                return false;

            double intervalMs = 60000.0 / latestBeat.Tempo;
            double beatPosition = (timelinePositionMs - latestBeat.TimelinePositionMs) / intervalMs;
            phase = (float)(beatPosition - Math.Floor(beatPosition));
            return true;
        }

        private void InitializeMusic()
        {
            if (string.IsNullOrWhiteSpace(musicEventPath))
            {
                ReportError("FMOD music event path is empty.");
                return;
            }

            musicInstance = RuntimeManager.CreateInstance(musicEventPath);
            if (!musicInstance.isValid())
            {
                ReportError($"FMOD event was not found: {musicEventPath}");
                return;
            }

            RESULT volumeResult = musicInstance.setVolume(musicVolume);
            if (volumeResult != RESULT.OK)
            {
                ReportError($"FMOD failed to set music volume for '{musicEventPath}': {volumeResult}");
                musicInstance.release();
                musicInstance.clearHandle();
                return;
            }

            RESULT pitchResult = musicInstance.setPitch(musicPitch);
            if (pitchResult != RESULT.OK)
            {
                ReportError($"FMOD failed to set music pitch for '{musicEventPath}': {pitchResult}");
                musicInstance.release();
                musicInstance.clearHandle();
                return;
            }

            selfHandle = GCHandle.Alloc(this, GCHandleType.Normal);
            selfHandleAllocated = true;

            RESULT userDataResult = musicInstance.setUserData(GCHandle.ToIntPtr(selfHandle));
            if (userDataResult != RESULT.OK)
            {
                ReportError($"FMOD failed to set callback user data: {userDataResult}");
                ShutdownMusic();
                return;
            }

            timelineBeatCallback = TimelineBeatCallback;
            RESULT callbackResult = musicInstance.setCallback(
                timelineBeatCallback,
                EVENT_CALLBACK_TYPE.TIMELINE_BEAT);

            if (callbackResult != RESULT.OK)
            {
                ReportError($"FMOD failed to register timeline callback: {callbackResult}");
                ShutdownMusic();
                return;
            }

            initialized = true;
        }

        private void ProcessPendingBeats()
        {
            bool receivedBeat = false;
            while (pendingBeats.TryDequeue(out CallbackBeatData data))
            {
                int upper = Mathf.Max(1, data.TimeSignatureUpper);
                int lower = Mathf.Max(1, data.TimeSignatureLower);
                float tempo = Mathf.Max(1f, data.Tempo);

                latestBeat = new BeatSnapshot(
                    receivedBeatCount,
                    Mathf.Max(1, data.Bar),
                    Mathf.Clamp(data.Beat, 1, upper),
                    data.TimelinePositionMs,
                    tempo,
                    upper,
                    lower);

                receivedBeatCount++;
                hasAnchor = true;
                receivedBeat = true;
                Beat?.Invoke(latestBeat);
            }

            // Never replay a burst of old clicks after a frame stall or pause.
            // Gameplay still receives every queued beat above.
            if (receivedBeat && enableBeatTapping && IsPlaying && !paused && isActiveAndEnabled &&
                TryGetTimelinePositionMs(out int positionMs))
            {
                double ageMs = positionMs - latestBeat.TimelinePositionMs;
                if (ageMs >= 0 && ageMs <= Math.Min(100.0, MillisecondsPerBeat * 0.25))
                    PlayBeatTapping();
            }
        }

        private void PlayBeatTapping()
        {
            try
            {
                if (!beatTappingInstance.isValid())
                {
                    beatTappingInstance = RuntimeManager.CreateInstance(beatTappingEventPath);
                    activeBeatTappingPath = beatTappingEventPath;
                }

                RESULT result = beatTappingInstance.setVolume(Mathf.Clamp01(beatTappingVolume));
                if (result == RESULT.OK) result = beatTappingInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                if (result == RESULT.OK) result = beatTappingInstance.start();
                if (result != RESULT.OK)
                    DisableBeatTapping(result.ToString());
            }
            catch (EventNotFoundException exception)
            {
                DisableBeatTapping(exception.Message);
            }
        }

        private void DisableBeatTapping(string reason)
        {
            enableBeatTapping = false;
            ReleaseBeatTapping();
            // A diagnostic sound failure must not stop music or gameplay.
            UnityEngine.Debug.LogWarning($"[FmodBeatClock] Beat tapping disabled: {reason}", this);
        }

        private void ReleaseBeatTapping()
        {
            if (beatTappingInstance.isValid())
            {
                beatTappingInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                beatTappingInstance.release();
                beatTappingInstance.clearHandle();
            }
            activeBeatTappingPath = null;
        }

        private void ShutdownMusic()
        {
            ReleaseBeatTapping();
            initialized = false;
            playbackStarted = false;
            paused = false;
            hasAnchor = false;

            if (musicInstance.isValid())
            {
                musicInstance.setCallback(null, EVENT_CALLBACK_TYPE.ALL);
                musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                musicInstance.setUserData(IntPtr.Zero);
                musicInstance.release();
                musicInstance.clearHandle();
            }

            if (selfHandleAllocated)
            {
                selfHandle.Free();
                selfHandleAllocated = false;
            }

            while (pendingBeats.TryDequeue(out _))
            {
            }
        }

        private void ReportError(string message)
        {
            lastError = message;
            UnityEngine.Debug.LogError($"[FmodBeatClock] {message}", this);
            PlaybackError?.Invoke(message);
        }

        private static void AdvanceMusicalPosition(
            int anchorBar,
            int anchorBeat,
            int beatsPerBar,
            long beatOffset,
            out int targetBar,
            out int targetBeat)
        {
            int safeBeatsPerBar = Mathf.Max(1, beatsPerBar);
            long zeroBasedBeat = anchorBeat - 1L + beatOffset;
            long barOffset = FloorDiv(zeroBasedBeat, safeBeatsPerBar);
            long beatInBar = zeroBasedBeat - barOffset * safeBeatsPerBar;

            targetBar = (int)Math.Max(1L, anchorBar + barOffset);
            targetBeat = (int)beatInBar + 1;
        }

        private static long FloorDiv(long value, long divisor)
        {
            long quotient = value / divisor;
            long remainder = value % divisor;
            if (remainder != 0 && value < 0)
                quotient--;
            return quotient;
        }

        [AOT.MonoPInvokeCallback(typeof(EVENT_CALLBACK))]
        private static RESULT TimelineBeatCallback(
            EVENT_CALLBACK_TYPE callbackType,
            IntPtr eventInstancePointer,
            IntPtr parameterPointer)
        {
            if (callbackType != EVENT_CALLBACK_TYPE.TIMELINE_BEAT || parameterPointer == IntPtr.Zero)
                return RESULT.OK;

            EventInstance callbackInstance = new(eventInstancePointer);
            RESULT userDataResult = callbackInstance.getUserData(out IntPtr userDataPointer);
            if (userDataResult != RESULT.OK || userDataPointer == IntPtr.Zero)
                return RESULT.OK;

            GCHandle handle = GCHandle.FromIntPtr(userDataPointer);
            if (!(handle.Target is FmodBeatClock clock))
                return RESULT.OK;

            TIMELINE_BEAT_PROPERTIES properties =
                Marshal.PtrToStructure<TIMELINE_BEAT_PROPERTIES>(parameterPointer);
            clock.pendingBeats.Enqueue(new CallbackBeatData(properties));
            return RESULT.OK;
        }
    }
}
