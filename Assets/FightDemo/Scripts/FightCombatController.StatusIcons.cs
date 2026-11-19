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
        [SerializeField] private FightStatusIcon deathEffectIcon;
        [SerializeField] private FightStatusIcon aimedEffectIcon;
        [SerializeField] private FightStatusIcon weakenEffectIcon;
        [SerializeField, Min(1)] private int statusIconsPerRow = 3;
        [SerializeField, Min(0)] private float statusIconGap = .08f;
        [SerializeField, Min(0)] private float statusIconRowSpacing = .6f;
        [SerializeField, Min(0)] private int statusIconPrewarmPerType = 4;
        [SerializeField, Min(1)] private int maxBlockedEffectIcons = 32;
        [SerializeField, Min(0)] private float statusIconSpacing = .55f;
        [Tooltip("3D 場景指定顯示相機：圖示朝向相機，偏移與彈跳使用畫面方向。留空維持原 2D 模式。")]
        [SerializeField] private Camera statusIconCamera;
        [Tooltip("額外圖示偏移；指定相機時 X=畫面右、Y=畫面上、Z=遠離相機。")]
        [SerializeField] private Vector3 statusIconOffset;
        private Transform statusIconRoot, statusIconStorage;
        private readonly Stack<FightStatusIcon>[] idleStatusIcons = { new(), new(), new(), new(), new(), new() };
        private readonly List<FightStatusIcon> statusLayout = new();
        private long lastWeakenPulseBeat = long.MinValue;
        private readonly List<FightStatusIcon> activeStatusIcons = new();
        public int ActiveStatusIconCount => activeStatusIcons.Count;
        public int CreatedStatusIconCount { get; private set; }

        private FightStatusIcon IconPrefab(int kind) => kind == 0 ? blockedEffectIcon : kind == 1 ? weakEffectIcon : kind == 2 ? bleedEffectIcon : kind == 3 ? deathEffectIcon : kind == 4 ? aimedEffectIcon : weakenEffectIcon;

        private void EnsureStatusIconPool()
        {
            if (statusIconRoot != null || !showStatusIcons) return;
            statusIconRoot = new GameObject("StatusIconPool").transform;
            statusIconRoot.SetParent(transform, false);
            statusIconStorage = new GameObject("Inactive").transform;
            statusIconStorage.SetParent(statusIconRoot, false);
            statusIconStorage.gameObject.SetActive(false);
            for (int kind = 0; kind < idleStatusIcons.Length; kind++)
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
            icon.Begin(target, lane, statusIconCamera, statusIconOffset);
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
            bool aimed = visible && target.HasAimed;
            int count = (weak ? 1 : 0) + (bleed ? 1 : 0) + (aimed ? 1 : 0), lane = 0;
            SyncStatusIcon(target, 1, weak, weak ? (lane++ - (count - 1) * .5f) * statusIconSpacing : 0);
            SyncStatusIcon(target, 2, bleed, bleed ? (lane++ - (count - 1) * .5f) * statusIconSpacing : 0);
            SyncStatusIcon(target, 4, aimed, aimed ? (lane - (count - 1) * .5f) * statusIconSpacing : 0);
            SyncStatusIcon(target, 5, visible && HasMageWeaken(target), 0);
            ArrangeStatusIcons(target);
            foreach (var icon in activeStatusIcons)
                if (icon != null && icon.Target == target && icon.Kind == 4) icon.SetAimedAppearance(target.HasGoldAimed);
        }

        private void ArrangeStatusIcons(FightUnitSlot target)
        {
            statusLayout.Clear();
            // Stable ordering, including a consumed icon until its final pulse has finished.
            for (int kind = 1; kind <= 5; kind++)
                foreach (var icon in activeStatusIcons)
                    if (icon != null && icon.Target == target && icon.Kind == kind && !icon.IsPopup) statusLayout.Add(icon);
            float rowY = 0;
            int columns = Mathf.Max(1, statusIconsPerRow);
            for (int start = 0; start < statusLayout.Count; start += columns)
            {
                int end = Mathf.Min(start + columns, statusLayout.Count);
                float width = 0, height = statusIconRowSpacing;
                for (int i = start; i < end; i++)
                { width += Mathf.Max(statusIconSpacing, statusLayout[i].LayoutWidth + statusIconGap); height = Mathf.Max(height, statusLayout[i].LayoutHeight + statusIconGap); }
                float x = -width * .5f;
                for (int i = start; i < end; i++)
                {
                    var icon = statusLayout[i]; float cell = Mathf.Max(statusIconSpacing, icon.LayoutWidth + statusIconGap);
                    icon.Lane = x + cell * .5f; icon.Row = rowY; x += cell;
                }
                rowY += height;
            }
            foreach (var icon in activeStatusIcons)
                if (icon != null && icon.Target == target && icon.IsPopup) icon.Row = rowY;
        }

        public void PulseAimedIcon(FightUnitSlot target)
        {
            RefreshStatusIcons(target);
            foreach (var icon in activeStatusIcons)
                if (icon != null && icon.Target == target && icon.Kind == 4) { icon.Trigger(false); return; }
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
            if (!showStatusIcons) { ClearStatusIcons(); return; }
            if (battleEnded || AwaitingNextWave) ClearStatusIconsExceptDeath(true);
            else foreach (var enemy in fightScene2Enemies) RefreshStatusIcons(enemy);
            if (!IsPaused && !TimingCalibrationActive && beatClock != null && beatClock.HasTimingAnchor && lastWeakenPulseBeat != beatClock.LatestBeat.GlobalBeat)
            {
                lastWeakenPulseBeat = beatClock.LatestBeat.GlobalBeat;
                foreach (var icon in activeStatusIcons)
                    if (icon != null && icon.Kind == 5 && !icon.Finishing) icon.Trigger(false);
            }
            float delta = IsPaused || TimingCalibrationActive ? 0 : Time.deltaTime;
            AdvanceStatusIconVisuals(delta);
        }

        private void AdvanceStatusIconVisuals(float delta)
        {
            float beatSeconds = beatClock != null && beatClock.HasTimingAnchor ? (float)(beatClock.MillisecondsPerBeat / 1000d)
                : beatClock != null && beatClock.HasOpeningTempo ? (float)beatClock.OpeningSecondsPerBeat : .5f;
            float beats = delta * BeatPresentationDeltaScale * (beatClock != null ? beatClock.MusicPitch : 1) / Mathf.Max(.001f, beatSeconds);
            for (int i = activeStatusIcons.Count - 1; i >= 0; i--)
                if (activeStatusIcons[i] == null || !activeStatusIcons[i].Advance(delta, beats)) ReturnStatusIcon(activeStatusIcons[i]);
        }

        internal void ShowDeathEffectIcon(FightUnitSlot target)
        {
            if (!showStatusIcons || !isActiveAndEnabled || target == null || target.Team != FightUnitSlot.UnitTeam.Enemy) return;
            RentStatusIcon(3, target);
        }

        private void ClearStatusIcons() => ClearStatusIconsExceptDeath(false);

        private void ClearStatusIconsExceptDeath(bool keepDeathPopups)
        {
            for (int i = activeStatusIcons.Count - 1; i >= 0; i--)
                if (!keepDeathPopups || activeStatusIcons[i] == null || activeStatusIcons[i].Kind != 3)
                    ReturnStatusIcon(activeStatusIcons[i]);
        }
    }
}
