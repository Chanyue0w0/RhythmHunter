using System;
using System.Collections.Generic;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>Presentation only. All pulses follow the battle's FMOD beat events.</summary>
    [DefaultExecutionOrder(-100), DisallowMultipleComponent]
    public sealed class FightEnvironmentController : MonoBehaviour
    {
        public enum ScrollDirection { Right = 1, Left = -1 }
        [Serializable]
        public sealed class Layer
        {
            public LoopingBackgroundScroller scroller;
            public bool motionEnabled = true;
            [Min(0)] public float speedMultiplier = 1;
            public bool reverseDirection;
        }
        [Serializable]
        public sealed class Pulse
        {
            public Transform target;
            [Min(0)] public float amount = .03f;
            [Range(.05f, 1)] public float durationInBeats = .45f;
            [Tooltip("0: every beat, 1: odd, 2: even, 3: third beat of each four.")]
            [Range(0, 3)] public int parity;
            [NonSerialized] public Vector3 restScale;
            [NonSerialized] public bool verticalOnly;
        }

        [SerializeField] private FightCombatController beatSource;
        [Header("Environment Motion (does not affect combat or music)")]
        [SerializeField] private bool motionEnabled = true;
        [SerializeField] private bool scrollingEnabled = true;
        [SerializeField] private ScrollDirection direction = ScrollDirection.Right;
        [SerializeField, Min(0)] private float speedMultiplier = 1;
        [SerializeField] private bool beatPulseEnabled = true;
        [SerializeField, Range(0, 3)] private float pulseStrength = 1;
        [SerializeField] private Layer[] layers = Array.Empty<Layer>();
        [SerializeField] private Pulse[] pulses = Array.Empty<Pulse>();

        private double anchorTime, secondsPerBeat = .5;
        private long anchorBeat;
        private bool hasBeat;
        private FightCombatController subscribedSource;
        public int PulseCount => pulses.Length;
        public bool MotionEnabled { get => motionEnabled; set => motionEnabled = value; }
        public bool ScrollingEnabled { get => scrollingEnabled; set => scrollingEnabled = value; }
        public bool BeatPulseEnabled { get => beatPulseEnabled; set => beatPulseEnabled = value; }
        public ScrollDirection Direction { get => direction; set => direction = value; }
        public float SpeedMultiplier { get => speedMultiplier; set => speedMultiplier = Mathf.Max(0, value); }

        public void Configure(FightCombatController source, Layer[] scrollLayers, Pulse[] pulseTargets)
        {
            Unsubscribe();
            RestoreScales();
            beatSource = source; layers = scrollLayers; pulses = pulseTargets;
            CaptureScales();
            if (Application.isPlaying && isActiveAndEnabled) Subscribe();
        }
        void Awake() => CaptureScales();
        void OnEnable() { Subscribe(); }
        void OnDisable()
        {
            Unsubscribe(); RestoreScales();
            foreach (var layer in layers)
                if (layer.scroller != null) layer.scroller.SetMotionMultiplier(0);
        }
        void CaptureScales()
        {
            foreach (var pulse in pulses)
            {
                if (pulse.target == null) continue;
                pulse.restScale = pulse.target.localScale;
                pulse.verticalOnly = pulse.target.GetComponent<LoopingBackgroundScroller>() != null;
                var old = pulse.target.GetComponent<BeatBounce>();
                if (old != null) old.enabled = false;
            }
        }
        void RestoreScales()
        {
            foreach (var pulse in pulses)
                if (pulse.target != null && pulse.restScale != Vector3.zero) pulse.target.localScale = pulse.restScale;
        }
        void Subscribe()
        {
            if (beatSource == null || subscribedSource == beatSource) return;
            subscribedSource = beatSource; subscribedSource.FightBeat += OnBeat;
        }
        void Unsubscribe()
        {
            if (subscribedSource != null) subscribedSource.FightBeat -= OnBeat;
            subscribedSource = null;
        }
        void OnBeat(FmodBeatClock.BeatSnapshot beat)
        {
            anchorTime = Time.timeAsDouble; anchorBeat = beat.GlobalBeat;
            secondsPerBeat = 60d / Math.Max(1d, beat.Tempo); hasBeat = true;
        }
        void Update()
        {
            bool paused = beatSource != null && beatSource.IsPaused;
            foreach (var layer in layers)
            {
                if (layer.scroller == null) continue;
                float speed = motionEnabled && scrollingEnabled && layer.motionEnabled && !paused
                    ? Mathf.Max(0, speedMultiplier) * Mathf.Max(0, layer.speedMultiplier) * (int)direction * (layer.reverseDirection ? -1 : 1) : 0;
                layer.scroller.SetMotionMultiplier(speed);
            }
            if (paused) return;
            if (!motionEnabled || !beatPulseEnabled || !hasBeat) { RestoreScales(); return; }
            double position = anchorBeat + Math.Max(0, Time.timeAsDouble - anchorTime) / secondsPerBeat;
            long index = (long)Math.Floor(position);
            float progress = (float)(position - index);
            foreach (var pulse in pulses)
            {
                if (pulse.target == null) continue;
                bool active = pulse.parity == 0 || pulse.parity == 1 && index % 2 != 0 ||
                    pulse.parity == 2 && index % 2 == 0 || pulse.parity == 3 && index % 4 == 3;
                float scale = active && progress < pulse.durationInBeats
                    ? 1 + Mathf.Sin(progress / Mathf.Max(.05f, pulse.durationInBeats) * Mathf.PI) * pulse.amount * pulseStrength : 1;
                pulse.target.localScale = pulse.verticalOnly
                    ? new Vector3(pulse.restScale.x, pulse.restScale.y * scale, pulse.restScale.z) : pulse.restScale * scale;
            }
        }

        // Compatibility for the original art scene. Integrated scenes serialize explicit bindings.
        public static void EnsureLegacy(FightCombatController source)
        {
            if (source.gameObject.scene.name != "FightScene" || source.GetComponent<FightEnvironmentController>() != null) return;
            var scroll = new List<Layer>(); var targets = new List<Pulse>();
            foreach (var root in source.gameObject.scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var scroller = t.GetComponent<LoopingBackgroundScroller>();
                if (scroller != null) scroll.Add(new Layer { scroller = scroller });
                var pulse = CreateAuthoredPulse(t);
                if (pulse != null) targets.Add(pulse);
            }
            source.gameObject.AddComponent<FightEnvironmentController>().Configure(source, scroll.ToArray(), targets.ToArray());
        }
        public static Pulse CreateAuthoredPulse(Transform t)
        {
            if (t.name == "SlotGround") return new Pulse { target = t, amount = .04f, durationInBeats = .6f };
            if (t.name == "Background_00") return new Pulse { target = t, amount = .012f, parity = 3 };
            if (t.name == "Background_05" || t.name == "Background_07" || t.name == "StoneLayer (Beat Pulse)")
                return new Pulse { target = t, amount = .015f, parity = 3 };
            if (t.name.StartsWith("Grass_", StringComparison.Ordinal) && int.TryParse(t.name.Substring(6), out int i))
                return new Pulse { target = t, parity = i % 2 == 0 ? 1 : 2 };
            return null;
        }
    }
}
