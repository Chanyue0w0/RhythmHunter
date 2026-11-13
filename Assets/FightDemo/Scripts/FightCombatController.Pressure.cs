using System;
using UnityEngine;
using System.Collections.Generic;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        private FightEnemyPressureSettings wavePressure = new();
        [UnityEngine.SerializeField, UnityEngine.Min(0)]
        [UnityEngine.Tooltip("密集攻擊視覺提示提前幾個普通戰鬥拍開始；不改變敵人實際攻擊時程。0 表示不提前。")]
        private int pressureWarningBeats = 1;
        private sealed class EnemyCadence
        {
            public double actionEnd;
            public double pressureReady;
            public double normalReady = -1;
        }
        private readonly Dictionary<FightUnitSlot, EnemyCadence> enemyCadences = new();
        private bool pressureCadenceActive;
        public long WaveCombatBeatCount { get; private set; }
        public bool PressurePhaseEnabled => StageOvertimeActive || wavePressure.enablePressurePhase;
        [Header("Pressure Break / Rest")]
        [SerializeField, Min(.5f), InspectorName("Damage Per Segment")] private float pressureDamagePerPair = 5;
        [SerializeField, Min(1), InspectorName("Blocks Per Segment")] private int pressureBlocksPerPair = 5;
        [Tooltip("每次達標削減的拍數 = 本次 Pressured 總持續拍數 / 此段數。20 拍除以 20 段 = 每次削減 1 拍；段數越大，每次削減越少。")]
        [SerializeField, Min(1), InspectorName("Reduction Segments")] private int pressureReductionSegments = 20;
        [SerializeField, Min(0)] private int pressureRestBeats = 20;
        [SerializeField, Min(0)] private float pressureManaMultiplier = 2;
        [Tooltip("Pressure 攻擊遭到格擋時仍造成的傷害，先扣護甲再扣 HP。未格擋不增傷；0 表示關閉。")]
        [SerializeField, Min(0)] private float pressureGuardChipDamage = .5f;
        [Header("Pressure Reward / Fever")]
        [SerializeField] private bool refillManaAfterPressure = true;
        [SerializeField] private bool blockFeverDuringPressure = true;
        public bool FeverBlockedByPressure => blockFeverDuringPressure && PressurePhaseActive;
        private bool pressureRewardPaid = true;
        private float CurrentPressureGuardChip => PressurePhaseActive ? Mathf.Max(0, pressureGuardChipDamage) : 0;
        private float pendingEnemyGuardChip;
        private readonly HashSet<long> pressureChipHitBeats = new();
        private float ApplyEnemyPartyDamage(float damage, bool blocked, long hitBeat)
        {
            if (blocked && pressureChipHitBeats.Contains(hitBeat)) return 0;
            int version = rosterVersion;
            float applied = ApplyPartyDamage(damage, hitBeat);
            // Only actual guarded damage consumes this beat's allowance. Late callbacks
            // retain their original hit beat; never deduplicate by frame or arrival order.
            if (blocked && applied > 0 && version == rosterVersion) pressureChipHitBeats.Add(hitBeat);
            return applied > 0 ? damage : 0;
        }
        private float ResolveEnemyHitDamage(float amount, bool blocked, float guardChip)
        {
            if (!HealthSystemEnabled) return 0;
            float normalDamage = QuantizeCombatValue(amount);
            return blocked ? Mathf.Min(normalDamage, QuantizeCombatValue(guardChip)) : normalDamage;
        }
        private double pressureRemaining;
        private long pressureNextStart = -1, pressureRestUntil;
        private float pressureDamageProgress;
        private int pressureBlockProgress;
        public int PressureBreakRevision { get; private set; }
        public float PressureRemovedBeats { get; private set; }
        public int PressureDurationBeats => Math.Max(1, wavePressure.durationBeats);
        public long PressureLastBreakSongBeat { get; private set; }
        private bool PressureBattleAvailable => !battleEnded && !AwaitingBattleStart && !WaveTransitionActive;
        public bool PressurePhaseActive => PressureBattleAvailable && (StageOvertimeActive || pressureRemaining > 0);
        public bool PressureRestActive => PressureBattleAvailable && !StageOvertimeActive && WaveCombatBeatCount < pressureRestUntil;
        public bool PressureWarningActive => PressureBattleAvailable && !PressurePhaseActive && !PressureRestActive &&
            pressureNextStart >= 0 && pressureNextStart > WaveCombatBeatCount &&
            pressureNextStart - WaveCombatBeatCount <= pressureWarningBeats;
        public bool PressureVisualsActive => PressurePhaseActive || PressureWarningActive;
        public long PressurePhaseRemainingBeats => PressurePhaseActive ? (long)Math.Ceiling(pressureRemaining) : 0;
        public float PressureRemainingExact => PressurePhaseActive ? (float)pressureRemaining : 0;
        public int PressureVisiblePairs => PressurePhaseActive ? Math.Min(Math.Max(1, pressureReductionSegments), (int)Math.Ceiling(pressureRemaining / PressurePairBeats - .00001)) : Math.Max(1, pressureReductionSegments);
        public float PressurePairBeats => (float)PressureDurationBeats / Math.Max(1, pressureReductionSegments);
        public int PressureWarningLeadBeats => pressureWarningBeats;
        public long PressureNextStartBeat => PressureBattleAvailable ? pressureNextStart : -1;
        public int PressureRecoveryBeats => StageOvertimeActive ? OvertimeRecoveryBeats : wavePressure.recoveryBeats;
        public IReadOnlyList<FightUnitSlot> PressureEnemySlots => fightScene2Enemies;

        private void ResetWavePressure()
        {
            pressureChipHitBeats.Clear();
            pressureRewardPaid = true;
            overtimePressureStartBeat = 0;
            lastOvertimeRecoveryBeats = 0;
            ClearPressureCadence();
            WaveCombatBeatCount = 0;
            wavePressure = rosterManager != null
                ? rosterManager.GetWavePressureSettings(CurrentWave - 1) : new FightEnemyPressureSettings();
            pressureRemaining = 0;
            pressureRestUntil = 0;
            pressureDamageProgress = 0;
            pressureBlockProgress = 0;
            PressureBreakRevision = 0;
            PressureRemovedBeats = 0;
            PressureLastBreakSongBeat = long.MinValue;
            pressureNextStart = wavePressure.enablePressurePhase && wavePressure.Validate(out _) ? wavePressure.startBeat : -1;
        }

        // Called only when the existing normal-battle clock accepts a new beat.
        private void AdvanceWavePressureBeat()
        {
            WaveCombatBeatCount++;
            if (StageOvertimeActive) { AdvanceOvertimePressureBeat(); return; }
            if (pressureRemaining > 0)
            {
                pressureRemaining = Math.Max(0, pressureRemaining - 1);
                if (pressureRemaining <= 0) EndPressurePhase();
            }
            else if (pressureNextStart >= 0 && WaveCombatBeatCount >= pressureNextStart)
            {
                pressureRewardPaid = false;
                pressureRemaining = Math.Max(1, wavePressure.durationBeats);
                pressureDamageProgress = 0;
                pressureBlockProgress = 0;
                pressureNextStart = -1;
            }
        }

        internal void RecordPressureDamage(FightUnitSlot target, float actualHpLoss)
        {
            if (StageOvertimeActive || !PressurePhaseActive || target == null || !fightScene2Enemies.Contains(target) || actualHpLoss <= 0) return;
            pressureDamageProgress += actualHpLoss;
            int pairs = (int)Math.Floor((pressureDamageProgress + .00001f) / Mathf.Max(.5f, pressureDamagePerPair));
            pressureDamageProgress -= pairs * Mathf.Max(.5f, pressureDamagePerPair);
            ReducePressurePairs(pairs);
        }

        private void RecordPressureBlock()
        {
            if (StageOvertimeActive || !PressurePhaseActive) return;
            pressureBlockProgress++;
            int threshold = Math.Max(1, pressureBlocksPerPair);
            int pairs = pressureBlockProgress / threshold;
            pressureBlockProgress %= threshold;
            ReducePressurePairs(pairs);
        }

        private void ReducePressurePairs(int pairs)
        {
            if (StageOvertimeActive || pairs <= 0 || !PressurePhaseActive) return;
            double removed = Math.Min(pressureRemaining, pairs * PressurePairBeats);
            PressureRemovedBeats += (float)removed;
            pressureRemaining = Math.Max(0, pressureRemaining - removed);
            PressureBreakRevision++;
            PressureLastBreakSongBeat = beatClock != null ? beatClock.LatestBeat.GlobalBeat + 1 : latestCombatBeat + 1;
            if (pressureRemaining <= .00001) EndPressurePhase();
        }

        private void EndPressurePhase()
        {
            if (StageOvertimeActive) return;
            pressureRemaining = 0;
            pressureDamageProgress = 0;
            pressureBlockProgress = 0;
            pressureRestUntil = WaveCombatBeatCount + Math.Max(0, pressureRestBeats);
            pressureNextStart = wavePressure.repeat ? pressureRestUntil + Math.Max(1, wavePressure.startBeat) : -1;
            // Cancel unexecuted attacks; launched projectiles retain their normal resolution.
            enemyHits.RemoveAll(hit => !hit.launched);
            pendingEnemyAttack = pendingEnemyAnimationDriven = pendingEnemyWindup = false;
            pendingEnemyAttacker = null;
            foreach (var enemy in fightScene2Enemies)
            {
                if (enemy == null) continue;
                enemy.CombatAnimator?.InterruptPlayback();
                CadenceFor(enemy).normalReady = NormalBattleBeatCount + Math.Max(0, pressureRestBeats);
            }
            pressureCadenceActive = false;
            if (!pressureRewardPaid && !battleEnded && partyHp > 0)
            {
                pressureRewardPaid = true;
                if (refillManaAfterPressure)
                {
                    teamSkillGauge = TeamSkillGaugeMax;
                    teamSkillGaugeFraction = 0;
                    TeamSkillStatus = "PRESSURE CLEARED — MP FULL";
                }
            }
        }

        private EnemyCadence CadenceFor(FightUnitSlot enemy)
        {
            if (!enemyCadences.TryGetValue(enemy, out var cadence))
                enemyCadences.Add(enemy, cadence = new EnemyCadence());
            return cadence;
        }

        private bool EnemyActionBusy(FightUnitSlot enemy) =>
            enemyHits.Exists(hit => hit.source == enemy && !hit.launched) ||
            (enemy.CombatAnimator != null && enemy.CombatAnimator.IsPlaying);

        private void RefreshPressureCadence()
        {
            bool active = PressurePhaseActive;
            if (active == pressureCadenceActive) return;
            pressureCadenceActive = active;
            foreach (var enemy in fightScene2Enemies)
            {
                if (enemy == null || !enemy.HasCharacter || enemy.CurrentHp <= 0) continue;
                var cadence = CadenceFor(enemy);
                if (active)
                    cadence.pressureReady = EnemyActionBusy(enemy)
                        ? Math.Max(NormalBattleBeatCount, cadence.actionEnd) + PressureRecoveryBeats
                        : NormalBattleBeatCount;
                else
                    // Exit never cuts a telegraphed action short or catches up an old modulo beat.
                    cadence.normalReady = Math.Max(NormalBattleBeatCount, cadence.actionEnd) + enemy.AttackIntervalBeats;
            }
        }

        private bool EnemyCadenceReady(FightUnitSlot enemy, long globalBeat)
        {
            if (PressureRestActive) return false;
            if (PressurePhaseEnabled)
            {
                var cadence = CadenceFor(enemy);
                if (PressurePhaseActive) return NormalBattleBeatCount + .00001 >= cadence.pressureReady;
                if (cadence.normalReady >= 0) return NormalBattleBeatCount + .00001 >= cadence.normalReady;
            }
            return IsScheduledAttackBeat(globalBeat - enemyPausedBeats, enemy.AttackIntervalBeats);
        }

        private void RecordEnemyActionCadence(FightUnitSlot enemy)
        {
            // Track action end even before pressure: overtime may begin during a normal attack.
            var sequence = enemy.CombatAnimator?.FindSequence(FightCharacterCombatAnimator.CombatAnimation.NormalAttack);
            double duration = 0;
            if (sequence != null)
                for (int i = 0; i < sequence.Frames.Count; i++)
                    duration += sequence.UseFrameBeatDurations ? sequence.GetFrameBeats(i)
                        : 1000d / Math.Max(1, sequence.FramesPerSecond) / (beatClock != null ? Math.Max(1, beatClock.MillisecondsPerBeat) : 500d);
            if (!enemy.CharacterDefinition.EnemyAttackUsesEffectFrames)
                duration = Math.Max(duration, enemy.CharacterDefinition.AttackWindupBeats);
            var cadence = CadenceFor(enemy);
            // Use the complete authored animation, including recovery frames, not the last damage frame.
            cadence.actionEnd = NormalBattleBeatCount + duration;
            cadence.pressureReady = cadence.actionEnd + PressureRecoveryBeats;
            if (cadence.normalReady >= 0) cadence.normalReady = NormalBattleBeatCount + enemy.AttackIntervalBeats;
        }

        private void EndEnemyCadenceEarly(FightUnitSlot enemy)
        {
            if (!enemyCadences.TryGetValue(enemy, out var cadence)) return;
            double now = NormalBattleBeatCount;
            if (beatClock != null && !EnemyActionsPaused && beatClock.HasTimingAnchor && beatClock.TryGetTimelinePositionMs(out int ms))
                now += Math.Max(0, Math.Min(1, (ms - beatClock.LatestBeat.TimelinePositionMs) / Math.Max(1d, beatClock.MillisecondsPerBeat)));
            cadence.actionEnd = Math.Min(cadence.actionEnd, now);
            cadence.pressureReady = now + PressureRecoveryBeats;
            if (!PressurePhaseActive && cadence.normalReady >= 0) cadence.normalReady = now + enemy.AttackIntervalBeats;
        }

        private void ClearPressureCadence()
        {
            enemyCadences.Clear();
            pressureCadenceActive = false;
        }
    }
}
