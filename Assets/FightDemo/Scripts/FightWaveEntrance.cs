using System.Collections.Generic;
using UnityEngine;
using RhythmHunter.RhythmDemo;

namespace RhythmHunter.FightDemo
{
    /// <summary>Visual-only offset on actor roots. Formation slots and logical positions stay fixed.</summary>
    [DefaultExecutionOrder(-100), DisallowMultipleComponent]
    public sealed class FightWaveEntrance : MonoBehaviour
    {
        [SerializeField] private FightCombatController fight;
        [SerializeField] private FightRosterManager roster;
        [SerializeField] private FmodBeatClock beatClock;
        [SerializeField] private Camera view;
        [SerializeField] private bool slideEnabled = true;
        [SerializeField, Min(.05f)] private float durationBeats = 1;
        [Tooltip("Hop immediately on arrival, using each slot's existing On Beat Jump Height/Duration.")]
        [SerializeField] private bool hopOnArrival = true;
        [Tooltip("Extra space beyond the viewport, as a fraction of screen width.")]
        [SerializeField, Min(0)] private float edgeMargin = .08f;
        [Header("Enemy Entry Invulnerability")]
        [SerializeField] private bool showInvulnerabilityLabel = true;
        [SerializeField] private string invulnerabilityLabel = "IMMUNE";
        [SerializeField] private Color invulnerabilityColor = new(.35f, .9f, 1f, 1f);
        [SerializeField, Min(.01f)] private float invulnerabilityTextSize = .12f;
        [SerializeField] private float invulnerabilityHeadOffset = .25f;
        private sealed class Entry
        {
            public Transform root;
            public FightUnitSlot slot;
            public GameObject actor;
            public Vector3 offset, appliedLocal;
        }
        private readonly List<Entry> entries = new();
        private bool running, hasTime;
        private bool alignToFirstBeat;
        private int previousMs;
        private double elapsedBeats;
        private bool initialStarted;
        public bool IsRunning => running;

        private void OnEnable() { if (fight != null) fight.WaveStarted += OnWaveStarted; }
        private void Start() { if (fight != null && fight.AutoPlayBattleEntrance) BeginFirstWave(); }
        public void BeginFirstWave()
        {
            if (initialStarted || roster == null) return;
            initialStarted = true; Begin(true);
        }
        private void OnWaveStarted(int wave) => Begin(false);
        private void Update() => RefreshProgress();

        private void Begin(bool includeHeroes)
        {
            Clear();
            if (!slideEnabled || !view || roster == null || beatClock == null) return;
            if (includeHeroes) AddSide(roster.ActiveHeroes);
            AddSide(roster.ActiveEnemies);
            running = entries.Count > 0;
            elapsedBeats = 0;
            alignToFirstBeat = true;
            hasTime = beatClock.IsPlaying && beatClock.TryGetTimelinePositionMs(out previousMs);
            Apply(1);
        }

        private void AddSide(IReadOnlyList<FightUnitSlot> slots)
        {
            float average = 0; int count = 0;
            foreach (var slot in slots)
                if (slot != null && slot.ActorInstance != null) { average += view.WorldToViewportPoint(slot.ActorInstance.transform.position).x; count++; }
            bool fromRight = count > 0 && average / count >= .5f;
            foreach (var slot in slots)
            {
                if (slot == null || slot.ActorInstance == null || slot.ActorRoot == null) continue;
                var root = slot.ActorRoot;
                Bounds bounds = new Bounds(slot.ActorInstance.transform.position, Vector3.zero);
                foreach (var renderer in slot.ActorInstance.GetComponentsInChildren<SpriteRenderer>())
                    if (renderer.enabled && renderer.sprite != null) bounds.Encapsulate(renderer.bounds);
                entries.Add(new Entry { root = root, slot = slot, actor = slot.ActorInstance,
                    offset = CalculateStartOffset(view, bounds, root.position, fromRight, edgeMargin) });
                if (showInvulnerabilityLabel && slot.Team == FightUnitSlot.UnitTeam.Enemy)
                {
                    var label = slot.ActorInstance.GetComponent<FightTransitionInvulnerabilityVisual>() ?? slot.ActorInstance.AddComponent<FightTransitionInvulnerabilityVisual>();
                    label.Initialize(slot, view, invulnerabilityLabel, invulnerabilityColor, invulnerabilityTextSize, invulnerabilityHeadOffset);
                }
            }
        }

        private static Vector3 CalculateStartOffset(Camera camera, Bounds bounds, Vector3 origin, bool right, float margin)
        {
            Vector3 total = Vector3.zero;
            // Perspective bounds corners can lie at different depths from the root.
            // Refine the ground-plane translation until the entire card clears the edge.
            for (int i = 0; i < 8; i++)
            {
                Vector3 step = CalculateEdgeStep(camera, bounds, origin, right, margin);
                total += step; bounds.center += step; origin += step;
                if (step.sqrMagnitude < .000001f) break;
            }
            return total;
        }

        private static Vector3 CalculateEdgeStep(Camera camera, Bounds bounds, Vector3 origin, bool right, float margin)
        {
            var projected = camera.WorldToViewportPoint(origin);
            // All eight bounds corners matter with perspective and tilted cameras.
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                float x = camera.WorldToViewportPoint(corner).x;
                min = Mathf.Min(min, x); max = Mathf.Max(max, x);
            }
            projected.x += right ? 1 + margin - min : -margin - max;
            Ray ray = camera.ViewportPointToRay(projected);
            var plane = new Plane(Vector3.up, origin);
            if (plane.Raycast(ray, out float distance)) return Vector3.ProjectOnPlane(ray.GetPoint(distance) - origin, Vector3.up);
            return Vector3.ProjectOnPlane(camera.ViewportToWorldPoint(projected) - origin, Vector3.up);
        }

        public void RefreshProgress()
        {
            if (!running) return;
            if (!slideEnabled || fight == null || fight.BattleEnded) { Clear(); return; }
            if (!beatClock.IsPlaying || !beatClock.TryGetTimelinePositionMs(out int now)) return;
            int delta = hasTime ? Mathf.Max(0, now - previousMs) : 0;
            previousMs = now; hasTime = true;
            if (fight.IsPaused || fight.TimingCalibrationActive) return;
            double msPerBeat = beatClock.HasTimingAnchor ? beatClock.MillisecondsPerBeat : beatClock.OpeningSecondsPerBeat * 1000;
            if (msPerBeat <= 0) return;
            if (alignToFirstBeat)
            {
                // Anchor the slide to the actual FMOD beat, rather than the frame
                // that first observed playback. Integer durations arrive on that beat grid.
                if (!beatClock.HasTimingAnchor) return;
                elapsedBeats = System.Math.Max(0, now - beatClock.LatestBeat.TimelinePositionMs) / msPerBeat;
                alignToFirstBeat = false;
            }
            else elapsedBeats += delta / msPerBeat;
            float t = Mathf.Clamp01((float)(elapsedBeats / Mathf.Max(.05f, durationBeats)));
            Apply(1 - Mathf.SmoothStep(0, 1, t));
            if (t >= 1) CompleteArrival();
        }

        private void Apply(float amount)
        {
            foreach (var entry in entries)
            {
                if (!entry.root) continue;
                entry.root.localPosition -= entry.appliedLocal;
                var world = entry.offset * amount;
                entry.appliedLocal = entry.root.parent ? entry.root.parent.InverseTransformVector(world) : world;
                entry.root.localPosition += entry.appliedLocal;
            }
        }
        private void Clear() { Apply(0); entries.Clear(); running = hasTime = false; }
        private void CompleteArrival()
        {
            if (!running) return;
            Apply(0); // Restore the destination before starting any hop, in this same frame.
            running = hasTime = false;
            foreach (var entry in entries)
                if (hopOnArrival && entry.slot != null && entry.slot.ActorInstance == entry.actor)
                    entry.slot.PlayArrivalHop();
            entries.Clear();
        }
        private void OnDisable()
        {
            if (fight != null) fight.WaveStarted -= OnWaveStarted;
            Clear();
        }
    }
}
