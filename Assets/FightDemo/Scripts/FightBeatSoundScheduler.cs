using System;
using System.Collections.Generic;
using FMOD;
using FMOD.Studio;

namespace RhythmHunter.FightDemo
{
    // Uses the caller's music position, never a separate gameplay clock. Studio event starts
    // have mixer/update latency; this is timeline scheduling, not sample-accurate DSP scheduling.
    public sealed class FightBeatSoundScheduler : IDisposable
    {
        private sealed class Voice
        {
            public EventInstance instance;
            public ChannelGroup group;
            public bool active, pending, paused;
            public double target;
        }
        private readonly List<Voice> voices = new();
        private EventDescription description;
        private bool loaded;
        public int ScheduledCount { get; private set; }
        public int ImmediateCount { get; private set; }
        public string LastResult { get; private set; } = "Preparing";
        public bool Ready => loaded;
        public double LastTargetMs { get; private set; }
        public FightBeatSoundScheduler(FMOD.Studio.System system, string path)
        {
            if (system.getEvent(path, out description) != RESULT.OK)
            { LastResult = "Event unavailable: " + path; return; }
            loaded = description.loadSampleData() == RESULT.OK;
            LastResult = loaded ? "Ready" : "Sample preload failed";
        }
        public void Tick(double songMs, float pitch, bool paused)
        {
            foreach (var voice in voices)
            {
                if (!voice.active || !voice.instance.isValid()) continue;
                if (voice.pending)
                {
                    if (!paused && songMs >= voice.target) Start(voice);
                    continue;
                }
                if (voice.paused != paused)
                { voice.instance.setPaused(paused); voice.paused = paused; }
                voice.instance.getChannelGroup(out voice.group);
                if (voice.instance.getPlaybackState(out var state) == RESULT.OK && state == PLAYBACK_STATE.STOPPED)
                    voice.active = false;
            }
        }
        private bool Start(Voice voice)
        {
            voice.pending = false;
            voice.paused = false;
            voice.instance.setPaused(false);
            var result = voice.instance.start();
            if (result == RESULT.OK) return true;
            voice.active = false;
            LastResult = "Start failed: " + result;
            return false;
        }
        public bool PlayAt(double targetSongMs, double nowSongMs, float pitch, float volume, bool align, float minimumLeadMs)
        {
            if (!loaded || volume <= 0) return false;
            var voice = voices.Find(v => !v.active && v.instance.isValid());
            if (voice == null)
            {
                if (voices.Count >= 32) { LastResult = "Voice limit"; return false; }
                if (description.createInstance(out var instance) != RESULT.OK) return false;
                voice = new Voice { instance = instance }; voices.Add(voice);
            }
            bool scheduled = align && (targetSongMs - nowSongMs) / Math.Max(.01f, pitch) >= minimumLeadMs;
            voice.target = targetSongMs;
            voice.active = true;
            voice.pending = scheduled;
            voice.instance.setVolume(volume);
            LastTargetMs = targetSongMs;
            if (scheduled) { ScheduledCount++; LastResult = "Queued to music timeline"; return true; }
            if (!Start(voice)) return false;
            ImmediateCount++;
            LastResult = align ? "Immediate (late / insufficient lead)" : "Immediate mode";
            return true;
        }
        public void Dispose()
        {
            foreach (var voice in voices)
                if (voice.instance.isValid()) { voice.instance.stop(STOP_MODE.IMMEDIATE); voice.instance.release(); }
            voices.Clear();
            if (loaded && description.isValid()) description.unloadSampleData();
            loaded = false;
        }
    }
}
