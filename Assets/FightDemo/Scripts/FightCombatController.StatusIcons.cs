using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        [Header("Pooled Status Icons")]
        [SerializeField] private bool showStatusIcons = true;
        [SerializeField] private FightStatusIcon blockedEffectIcon;
        [SerializeField] private FightStatusIcon weakEffectIcon;
        [SerializeField] private FightStatusIcon bleedEffectIcon;
        [SerializeField, Min(0)] private int statusIconPrewarmPerType = 4;
        [SerializeField, Min(1)] private int maxBlockedEffectIcons = 32;
        [SerializeField, Min(0)] private float statusIconSpacing = .55f;
        private Transform statusIconRoot, statusIconStorage;
        private readonly Stack<FightStatusIcon>[] idleStatusIcons = { new(), new(), new() };
        private readonly List<FightStatusIcon> activeStatusIcons = new();
        public int ActiveStatusIconCount => activeStatusIcons.Count;
        public int CreatedStatusIconCount { get; private set; }

        private FightStatusIcon IconPrefab(int kind) => kind == 0 ? blockedEffectIcon : kind == 1 ? weakEffectIcon : bleedEffectIcon;

        private void EnsureStatusIconPool()
        {
            if (statusIconRoot != null || !showStatusIcons) return;
            statusIconRoot = new GameObject("StatusIconPool").transform;
            statusIconRoot.SetParent(transform, false);
            statusIconStorage = new GameObject("Inactive").transform;
            statusIconStorage.SetParent(statusIconRoot, false);
            statusIconStorage.gameObject.SetActive(false);
            for (int kind = 0; kind < 3; kind++)
                if (IconPrefab(kind) != null)
                    for (int i = 0; i < Mathf.Clamp(statusIconPrewarmPerType, 0, 32); i++)
                        idleStatusIcons[kind].Push(CreateStatusIcon(kind));
        }

        private FightStatusIcon CreateStatusIcon(int kind)
        {
            var icon = Instantiate(IconPrefab(kind), statusIconStorage);
            icon.gameObject.SetActive(false);
            icon.Initialize(kind);
            CreatedStatusIconCount++;
            return icon;
        }

        private void RentStatusIcon(int kind, FightUnitSlot target, float lane = 0)
        {
            if (IconPrefab(kind) == null) return;
            EnsureStatusIconPool();
            var icon = idleStatusIcons[kind].Count > 0 ? idleStatusIcons[kind].Pop() : CreateStatusIcon(kind);
            icon.transform.SetParent(statusIconRoot, false);
            icon.Begin(target, lane);
            activeStatusIcons.Add(icon);
        }

        private void ReturnStatusIcon(FightStatusIcon icon)
        {
            activeStatusIcons.Remove(icon);
            if (icon == null) return;
            icon.Release();
            icon.transform.SetParent(statusIconStorage, false);
            idleStatusIcons[icon.Kind].Push(icon);
        }

        public void RefreshStatusIcons(FightUnitSlot target)
        {
            if (this == null || target == null) return;
            bool visible = isActiveAndEnabled && showStatusIcons && !battleEnded && target.gameObject.activeInHierarchy && target.HasCharacter && target.CurrentHp > 0;
            bool weak = visible && target.IsFragile, bleed = visible && target.BleedBeatsRemaining > 0;
            SyncStatusIcon(target, 1, weak, weak && bleed ? -statusIconSpacing * .5f : 0);
            SyncStatusIcon(target, 2, bleed, weak && bleed ? statusIconSpacing * .5f : 0);
        }

        private void SyncStatusIcon(FightUnitSlot target, int kind, bool wanted, float lane)
        {
            for (int i = activeStatusIcons.Count - 1; i >= 0; i--)
            {
                var icon = activeStatusIcons[i];
                if (icon == null) { activeStatusIcons.RemoveAt(i); continue; }
                if (icon.Target != target || icon.Kind != kind) continue;
                if (icon.Finishing && target.HasCharacter && target.CurrentHp > 0 && target.gameObject.activeInHierarchy) continue;
                if (!wanted) ReturnStatusIcon(icon); else icon.Lane = lane;
                return;
            }
            if (wanted) RentStatusIcon(kind, target, lane);
        }

        public void PlayStatusIconTrigger(FightUnitSlot target, bool bleeding, bool consumed)
        {
            if (this == null || !isActiveAndEnabled || !showStatusIcons || battleEnded || target == null) return;
            RefreshStatusIcons(target);
            int kind = bleeding ? 2 : 1;
            foreach (var icon in activeStatusIcons)
                if (icon != null && icon.Target == target && icon.Kind == kind && !icon.Finishing)
                { icon.Trigger(consumed); return; }
        }

        private void ShowBlockedEffectIcon()
        {
            if (!showStatusIcons || !isActiveAndEnabled || battleEnded || tankSlot == null || !tankSlot.HasCharacter) return;
            int count = 0;
            FightStatusIcon oldest = null;
            foreach (var icon in activeStatusIcons)
                if (icon.Kind == 0) { count++; if (oldest == null) oldest = icon; }
            if (count >= Mathf.Max(1, maxBlockedEffectIcons) && oldest != null) ReturnStatusIcon(oldest);
            RentStatusIcon(0, tankSlot);
        }

        private void LateUpdate()
        {
            if (!showStatusIcons || battleEnded || AwaitingNextWave) { ClearStatusIcons(); return; }
            foreach (var enemy in fightScene2Enemies) RefreshStatusIcons(enemy);
            float delta = IsPaused || TimingCalibrationActive ? 0 : Time.deltaTime;
            AdvanceStatusIconVisuals(delta);
        }

        private void AdvanceStatusIconVisuals(float delta)
        {
            float beatSeconds = beatClock != null && beatClock.HasTimingAnchor ? (float)(beatClock.MillisecondsPerBeat / 1000d)
                : beatClock != null && beatClock.HasOpeningTempo ? (float)beatClock.OpeningSecondsPerBeat : .5f;
            float beats = delta * (beatClock != null ? beatClock.MusicPitch : 1) / Mathf.Max(.001f, beatSeconds);
            for (int i = activeStatusIcons.Count - 1; i >= 0; i--)
                if (activeStatusIcons[i] == null || !activeStatusIcons[i].Advance(delta, beats)) ReturnStatusIcon(activeStatusIcons[i]);
        }

        private void ClearStatusIcons()
        {
            while (activeStatusIcons.Count > 0) ReturnStatusIcon(activeStatusIcons[activeStatusIcons.Count - 1]);
        }
    }
}
