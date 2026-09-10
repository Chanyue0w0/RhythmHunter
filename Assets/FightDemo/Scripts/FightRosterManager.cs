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

        private readonly List<FightUnitSlot> activeHeroes = new();
        private readonly List<FightUnitSlot> activeEnemies = new();

        public event Action RosterChanged;

        public IReadOnlyList<FightUnitSlot> ActiveHeroes => activeHeroes;
        public IReadOnlyList<FightUnitSlot> ActiveEnemies => activeEnemies;
        public IReadOnlyList<FightCharacterDefinition> HeroPrefabs => heroPrefabs;
        public IReadOnlyList<FightCharacterDefinition> EnemyPrefabs => enemyPrefabs;

        public void Configure(
            FightCombatController controller,
            FmodBeatClock clock,
            FightUnitSlot[] heroSlots,
            FightCharacterDefinition[] heroes,
            FightUnitSlot[] enemySlots,
            FightCharacterDefinition[] enemies)
        {
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
            CopyRoster(heroes, heroPrefabs);
            CopyRoster(enemies, enemyPrefabs);
            if (spawnImmediately)
                SpawnConfiguredRoster();
        }

        [ContextMenu("Respawn Configured Roster")]
        public void SpawnConfiguredRoster()
        {
            activeHeroes.Clear();
            activeEnemies.Clear();
            SpawnSide(heroSpawnSlots, heroPrefabs, FightUnitSlot.UnitTeam.Hero, activeHeroes);
            SpawnSide(enemySpawnSlots, enemyPrefabs, FightUnitSlot.UnitTeam.Enemy, activeEnemies);
            RosterChanged?.Invoke();
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
