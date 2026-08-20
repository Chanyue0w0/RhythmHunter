using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class OtterSurfaceSensor : MonoBehaviour
    {
        private readonly HashSet<AquariumSurfaceZone> activeZones = new();

        [SerializeField] private AquariumSurfaceType currentSurface = AquariumSurfaceType.Land;
        [SerializeField] private float currentSpeedMultiplier = 1f;

        public AquariumSurfaceType CurrentSurface => currentSurface;
        public float CurrentSpeedMultiplier => currentSpeedMultiplier;
        public bool IsInWater => currentSurface == AquariumSurfaceType.Water;
        public bool IsInShallowWater => currentSurface == AquariumSurfaceType.ShallowWater;

        public event Action<AquariumSurfaceType, AquariumSurfaceType> SurfaceChanged;

        private void OnTriggerEnter2D(Collider2D other)
        {
            AquariumSurfaceZone zone = other.GetComponent<AquariumSurfaceZone>();
            if (zone != null && activeZones.Add(zone))
                RefreshSurface();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            AquariumSurfaceZone zone = other.GetComponent<AquariumSurfaceZone>();
            if (zone != null && activeZones.Remove(zone))
                RefreshSurface();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // Stay also repairs state after teleports, scene reloads, or a physics
            // step in which nested land/water triggers are entered together.
            AquariumSurfaceZone zone = other.GetComponent<AquariumSurfaceZone>();
            if (zone != null && activeZones.Add(zone))
                RefreshSurface();
        }

        private void OnDisable()
        {
            activeZones.Clear();
            SetSurface(AquariumSurfaceType.Land, 1f);
        }

        private void RefreshSurface()
        {
            activeZones.RemoveWhere(zone => zone == null || !zone.isActiveAndEnabled);

            AquariumSurfaceZone selected = null;
            foreach (AquariumSurfaceZone zone in activeZones)
            {
                if (selected == null || zone.Priority > selected.Priority)
                    selected = zone;
            }

            SetSurface(
                selected != null ? selected.SurfaceType : AquariumSurfaceType.Land,
                selected != null ? selected.SpeedMultiplier : 1f);
        }

        private void SetSurface(AquariumSurfaceType next, float speedMultiplier)
        {
            AquariumSurfaceType previous = currentSurface;
            currentSurface = next;
            currentSpeedMultiplier = speedMultiplier;

            if (previous != next)
                SurfaceChanged?.Invoke(previous, next);
        }
    }
}
