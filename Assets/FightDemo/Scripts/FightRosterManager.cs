using System;
using System.Collections.Generic;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Scene-level roster authoring point. Prefab entries are intentionally nullable,
    /// so a level can spawn any lineup from one enemy up to three per side.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class FightRosterManager : MonoBehaviour
    {
        public enum PartyPosition { Front, Middle, Back }

        [Header("Battle Preparation")]
        [Tooltip("Require an explicit start and lock the hero formation for the entire battle. Legacy labs can leave this off.")]
        [SerializeField] private bool requireBattlePreparation;
        public bool RequiresBattlePreparation => requireBattlePreparation;
        public bool BattleStarted { get; private set; }
        public bool FormationLocked => requireBattlePreparation && BattleStarted;

        [Header("Runtime Dependencies")]
        [SerializeField] private FightCombatController fightController;
        [SerializeField] private FmodBeatClock beatClock;

        [Header("Hero Spawn Points")]
        [SerializeField] private FightUnitSlot[] heroSpawnSlots = new FightUnitSlot[3];
        [Header("Hero Prefabs (Entries May Be Empty)")]
        [SerializeField] private FightCharacterDefinition[] heroPrefabs = new FightCharacterDefinition[3];

        [Header("Enemy Spawn Points")]
        [SerializeField] private FightUnitSlot[] enemySpawnSlots = new FightUnitSlot[3];
        [Header("Enemy Prefabs (Entries May Be Empty)")]
        [SerializeField] private FightCharacterDefinition[] enemyPrefabs = new FightCharacterDefinition[3];

        [Serializable]
        public sealed class EnemyWave
        {
            public FightCharacterDefinition[] enemies = new FightCharacterDefinition[3];
        }
        [Tooltip("Optional waves after the initial Enemy Prefabs. Hero formation stays locked.")]
        [SerializeField] private EnemyWave[] additionalWaves = Array.Empty<EnemyWave>();
        private FightCharacterDefinition[][] stageWaves;
        private FightEnemyPressureSettings[] stagePressure;
        private FightStageDefinition.WaveKind[] stageWaveKinds;
        public bool UsesStageDeadline { get; private set; }
        public FightStageDefinition.WaveKind GetWaveKind(int zeroBasedWave) =>
            stageWaveKinds != null && zeroBasedWave >= 0 && zeroBasedWave < stageWaveKinds.Length
                ? stageWaveKinds[zeroBasedWave] : FightStageDefinition.WaveKind.Combat;
        // Return a copy so Inspector/asset edits cannot mutate the active battle snapshot.
        public FightEnemyPressureSettings GetWavePressureSettings(int zeroBasedWave) =>
            stagePressure != null && zeroBasedWave >= 0 && zeroBasedWave < stagePressure.Length
                ? stagePressure[zeroBasedWave].Copy() : new FightEnemyPressureSettings();
        public int WaveCount => stageWaves != null ? stageWaves.Length : 1 + (additionalWaves?.Length ?? 0);

        public bool TryConfigureStage(FightStageDefinition stage, out string error)
        {
            if (BattleStarted) { error = "Cannot change Stage during battle."; return false; }
            if (!requireBattlePreparation) { error = "Stage waves require the existing battle preparation lifecycle."; return false; }
            if (stage == null) { error = "Stage is missing."; return false; }
            if (!stage.Validate(out error)) return false;
            if (enemySpawnSlots == null || enemySpawnSlots.Length != 3 || Array.Exists(enemySpawnSlots, slot => slot == null))
            { error = "Stage requires three enemy spawn positions."; return false; }
            // Existing battle targets sort enemy SlotIndex descending. Authoring uses
            // explicit front/middle/back rather than exposing that legacy ordering.
            int[] order = { 0, 1, 2 };
            Array.Sort(order, (a, b) => enemySpawnSlots[b].SlotIndex.CompareTo(enemySpawnSlots[a].SlotIndex));
            var snapshot = new FightCharacterDefinition[stage.Waves.Count][];
            var pressureSnapshot = new FightEnemyPressureSettings[stage.Waves.Count];
            var kindSnapshot = new FightStageDefinition.WaveKind[stage.Waves.Count];
            for (int w = 0; w < snapshot.Length; w++)
            {
                snapshot[w] = new FightCharacterDefinition[3];
                kindSnapshot[w] = stage.Waves[w].kind;
                pressureSnapshot[w] = stage.Waves[w].pressure?.Copy() ?? new FightEnemyPressureSettings();
                for (int p = 0; p < 3; p++) snapshot[w][order[p]] = stage.Waves[w].At(p);
            }
            stageWaves = snapshot; // Do not mutate shared assets or re-read edits mid-battle.
            stagePressure = pressureSnapshot;
            stageWaveKinds = kindSnapshot;
            UsesStageDeadline = stage.UseNormalEndDeadline;
            enemyPrefabs = (FightCharacterDefinition[])snapshot[0].Clone();
            error = null; return true;
        }

        internal bool SpawnNextEnemyWave(int waveIndex)
        {
            if (!FormationLocked || waveIndex < 1 || waveIndex >= WaveCount) return false;
            activeEnemies.Clear();
            SpawnSide(enemySpawnSlots, stageWaves != null ? stageWaves[waveIndex] : additionalWaves[waveIndex - 1]?.enemies,
                FightUnitSlot.UnitTeam.Enemy, activeEnemies);
            return true;
        }

        private readonly List<FightUnitSlot> activeHeroes = new();
        private readonly List<FightUnitSlot> activeEnemies = new();

        public event Action RosterChanged;

        public IReadOnlyList<FightUnitSlot> ActiveHeroes => activeHeroes;
        public IReadOnlyList<FightUnitSlot> ActiveEnemies => activeEnemies;
        public IReadOnlyList<FightCharacterDefinition> HeroPrefabs => heroPrefabs;
        public IReadOnlyList<FightCharacterDefinition> EnemyPrefabs => enemyPrefabs;

        public FightUnitSlot GetHeroAtPosition(PartyPosition position)
        {
            int index = (int)position;
            if (index < 0 || index >= heroSpawnSlots.Length)
                return null;
            FightUnitSlot slot = heroSpawnSlots[index];
            return slot != null && slot.HasCharacter ? slot : null;
        }

        public void Configure(
            FightCombatController controller,
            FmodBeatClock clock,
            FightUnitSlot[] heroSlots,
            FightCharacterDefinition[] heroes,
            FightUnitSlot[] enemySlots,
            FightCharacterDefinition[] enemies)
        {
            if (FormationLocked) return;
            fightController = controller;
            beatClock = clock;
            heroSpawnSlots = NormalizeSlots(heroSlots);
            heroPrefabs = NormalizePrefabs(heroes);
            enemySpawnSlots = NormalizeSlots(enemySlots);
            enemyPrefabs = NormalizePrefabs(enemies);
        }

        private void Awake()
        {
            SpawnConfiguredRoster();
        }

        /// <summary>
        /// Future level managers can inject a lineup, including null entries, then respawn it.
        /// </summary>
        public void SetRoster(
            IReadOnlyList<FightCharacterDefinition> heroes,
            IReadOnlyList<FightCharacterDefinition> enemies,
            bool spawnImmediately = true)
        {
            if (FormationLocked) return;
            CopyRoster(heroes, heroPrefabs);
            CopyRoster(enemies, enemyPrefabs);
            if (spawnImmediately)
                SpawnConfiguredRoster();
        }

        [ContextMenu("Respawn Configured Roster")]
        public void SpawnConfiguredRoster()
        {
            if (FormationLocked) return;
            activeHeroes.Clear();
            activeEnemies.Clear();
            SpawnSide(heroSpawnSlots, heroPrefabs, FightUnitSlot.UnitTeam.Hero, activeHeroes);
            SpawnSide(enemySpawnSlots, enemyPrefabs, FightUnitSlot.UnitTeam.Enemy, activeEnemies);
            RosterChanged?.Invoke();
        }

        public bool TrySwapHeroes(PartyPosition first, PartyPosition second)
        {
            int a = (int)first, b = (int)second;
            if ((fightController != null && fightController.BattleEntranceRunning) || FormationLocked || a < 0 || b < 0 || a >= heroPrefabs.Length ||
                b >= heroPrefabs.Length || a == b) return false;
            (heroPrefabs[a], heroPrefabs[b]) = (heroPrefabs[b], heroPrefabs[a]);
            SpawnConfiguredRoster();
            return true;
        }

        internal bool TryLockFormation()
        {
            if (!requireBattlePreparation || BattleStarted) return false;
            for (int i = 0; i < 3; i++)
                if (GetHeroAtPosition((PartyPosition)i) == null) return false;
            BattleStarted = true;
            return true;
        }

        private void SpawnSide(
            IReadOnlyList<FightUnitSlot> slots,
            IReadOnlyList<FightCharacterDefinition> prefabs,
            FightUnitSlot.UnitTeam expectedTeam,
            ICollection<FightUnitSlot> activeSlots)
        {
            int count = slots?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                FightUnitSlot slot = slots[i];
                FightCharacterDefinition prefab = prefabs != null && i < prefabs.Count ? prefabs[i] : null;
                if (slot == null)
                    continue;

                if (prefab == null)
                {
                    slot.ClearCharacter();
                    continue;
                }

                if (prefab.Team != expectedTeam)
                {
                    Debug.LogWarning(
                        $"[FightRosterManager] {prefab.name} is {prefab.Team} but was assigned to a {expectedTeam} slot. Slot left empty.",
                        this);
                    slot.ClearCharacter();
                    continue;
                }

                slot.SpawnCharacter(prefab, fightController);
                activeSlots.Add(slot);
            }
        }

        private static void CopyRoster(
            IReadOnlyList<FightCharacterDefinition> source,
            FightCharacterDefinition[] destination)
        {
            if (destination == null)
                return;

            for (int i = 0; i < destination.Length; i++)
                destination[i] = source != null && i < source.Count ? source[i] : null;
        }

        private static FightUnitSlot[] NormalizeSlots(FightUnitSlot[] source)
        {
            FightUnitSlot[] result = new FightUnitSlot[3];
            if (source != null)
                Array.Copy(source, result, Mathf.Min(source.Length, result.Length));
            return result;
        }

        private static FightCharacterDefinition[] NormalizePrefabs(FightCharacterDefinition[] source)
        {
            FightCharacterDefinition[] result = new FightCharacterDefinition[3];
            if (source != null)
                Array.Copy(source, result, Mathf.Min(source.Length, result.Length));
            return result;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (heroSpawnSlots == null || heroSpawnSlots.Length != 3)
                heroSpawnSlots = NormalizeSlots(heroSpawnSlots);
            if (heroPrefabs == null || heroPrefabs.Length != 3)
                heroPrefabs = NormalizePrefabs(heroPrefabs);
            if (enemySpawnSlots == null || enemySpawnSlots.Length != 3)
                enemySpawnSlots = NormalizeSlots(enemySpawnSlots);
            if (enemyPrefabs == null || enemyPrefabs.Length != 3)
                enemyPrefabs = NormalizePrefabs(enemyPrefabs);
        }
#endif
    }
}
