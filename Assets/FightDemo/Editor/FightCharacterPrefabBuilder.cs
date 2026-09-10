using System.Collections.Generic;
using System.Linq;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    /// <summary>
    /// Creates editable character/effect prefabs and wires the default FightScene2 roster.
    /// Safe to rerun after changing source art.
    /// </summary>
    public static class FightCharacterPrefabBuilder
    {
        private const string Root = "Assets/FightDemo/Prefabs";
        private const string HeroRoot = Root + "/Heroes";
        private const string EnemyRoot = Root + "/Enemies";
        private const string EffectRoot = Root + "/SkillEffects";

        [MenuItem("Rhythm Hunter/Build Fight Character Prefabs And Roster")]
        public static void BuildPrefabsAndRoster()
        {
            EnsureFolders();
            Sprite fallbackEffectSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            FightCharacterDefinition paladin = BuildCharacter(
                "Paladin", "PALADIN", FightUnitSlot.UnitTeam.Hero, FightUnitSlot.UnitRole.Tank,
                120, 12, "Radiant Smite", 36, 1, 4, new Color(0.12f, 0.65f, 0.9f, 1f), 1.72f,
                new[]
                {
                    "Assets/FightDemo/Arts/角色/Paladin/Idle/Idle1.png",
                    "Assets/FightDemo/Arts/角色/Paladin/Idle/Idle2.png",
                    "Assets/FightDemo/Arts/角色/Paladin/Idle/Idle3.png"
                }, fallbackEffectSprite, HeroRoot);
            FightCharacterDefinition bard = BuildCharacter(
                "Bard", "BARD", FightUnitSlot.UnitTeam.Hero, FightUnitSlot.UnitRole.Support,
                85, 8, "Final Chorus", 20, 2, 4, new Color(0.2f, 0.78f, 0.48f, 1f), 1.72f,
                new[]
                {
                    "Assets/FightDemo/Arts/角色/Bard/Idle/Idle1.png",
                    "Assets/FightDemo/Arts/角色/Bard/Idle/Idle2.png",
                    "Assets/FightDemo/Arts/角色/Bard/Idle/Idle3.png"
                }, fallbackEffectSprite, HeroRoot);
            FightCharacterDefinition mage = BuildCharacter(
                "Mage", "MAGE", FightUnitSlot.UnitTeam.Hero, FightUnitSlot.UnitRole.Damage,
                75, 24, "Arcane Burst", 84, 4, 3, new Color(0.72f, 0.3f, 0.88f, 1f), 1.72f,
                new[]
                {
                    "Assets/FightDemo/Arts/角色/Mage/Idle/Idle1.png",
                    "Assets/FightDemo/Arts/角色/Mage/Idle/Idle2.png",
                    "Assets/FightDemo/Arts/角色/Mage/Idle/Idle3.png"
                }, fallbackEffectSprite, HeroRoot);

            FightCharacterDefinition goblinMage = BuildCharacter(
                "Goblin_Mage", "GOBLIN MAGE", FightUnitSlot.UnitTeam.Enemy, FightUnitSlot.UnitRole.Enemy,
                80, 12, "Goblin Hex", 30, 4, 4, new Color(0.55f, 0.16f, 0.2f, 1f), 1.65f,
                new[]
                {
                    "Assets/FightDemo/Arts/敵人/Goblin_Mage/idle/idle_1.png",
                    "Assets/FightDemo/Arts/敵人/Goblin_Mage/idle/idle_2.png"
                }, fallbackEffectSprite, EnemyRoot);
            FightCharacterDefinition goblinMercenary = BuildCharacter(
                "Goblin_Mercenary", "GOBLIN MERCENARY", FightUnitSlot.UnitTeam.Enemy, FightUnitSlot.UnitRole.Enemy,
                120, 18, "Mercenary Cleave", 45, 4, 4, new Color(0.85f, 0.2f, 0.25f, 1f), 1.65f,
                new[]
                {
                    "Assets/FightDemo/Arts/敵人/Goblin_Mercenary/idle/idle_1.png",
                    "Assets/FightDemo/Arts/敵人/Goblin_Mercenary/idle/idle_2.png"
                }, fallbackEffectSprite, EnemyRoot);
            FightCharacterDefinition goblinShield = BuildCharacter(
                "Goblin_Shield", "GOBLIN SHIELD", FightUnitSlot.UnitTeam.Enemy, FightUnitSlot.UnitRole.Enemy,
                90, 14, "Shield Crash", 28, 4, 4, new Color(0.55f, 0.16f, 0.2f, 1f), 1.65f,
                new[]
                {
                    "Assets/FightDemo/Arts/敵人/Goblin_Shield/idle_Holding/holding_1.png",
                    "Assets/FightDemo/Arts/敵人/Goblin_Shield/idle_Holding/holding_2.png"
                }, fallbackEffectSprite, EnemyRoot);

            ConfigureFightScene2(
                new[] { paladin, bard, mage },
                new[] { goblinMage, goblinMercenary, goblinShield });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FIGHT_CHARACTER_PREFABS_AND_ROSTER_BUILT");
        }

        [MenuItem("Rhythm Hunter/Remove Scene-Authored FightScene2 Characters")]
        public static void RemoveSceneAuthoredFightScene2Characters()
        {
            Scene scene = EditorSceneManager.OpenScene(FightSceneBuilder.Scene2Path, OpenSceneMode.Single);
            FightUnitSlot[] slots = Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None);
            int removedCount = 0;

            foreach (FightUnitSlot slot in slots)
            {
                if (slot.gameObject.scene != scene || slot.ActorRoot == null)
                    continue;

                BeatSyncedIdleAnimator[] sceneCharacters =
                    slot.ActorRoot.GetComponentsInChildren<BeatSyncedIdleAnimator>(true);
                foreach (BeatSyncedIdleAnimator sceneCharacter in sceneCharacters)
                {
                    Object.DestroyImmediate(sceneCharacter.gameObject);
                    removedCount++;
                }

                EditorUtility.SetDirty(slot);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, FightSceneBuilder.Scene2Path);
            Debug.Log($"FIGHT_SCENE2_SCENE_CHARACTERS_REMOVED:{removedCount}");
        }

        private static FightCharacterDefinition BuildCharacter(
            string id,
            string displayName,
            FightUnitSlot.UnitTeam team,
            FightUnitSlot.UnitRole role,
            int hp,
            int attack,
            string skillName,
            int skillDamage,
            int attackIntervalBeats,
            int maxMana,
            Color accent,
            float actorHeight,
            IEnumerable<string> idlePaths,
            Sprite fallbackEffectSprite,
            string outputFolder)
        {
            Sprite[] frames = idlePaths.Select(LoadSprite).Where(sprite => sprite != null).ToArray();
            if (frames.Length == 0)
                throw new MissingReferenceException($"No idle sprites found for {id}.");

            GameObject skillEffect = BuildSkillEffect(id, accent, fallbackEffectSprite);
            GameObject root = new(id, typeof(SpriteRenderer), typeof(BeatSyncedIdleAnimator), typeof(FightCharacterDefinition));
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.color = Color.white;
            renderer.sortingOrder = 12;
            float sourceHeight = Mathf.Max(0.01f, frames[0].bounds.size.y);
            float scale = actorHeight / sourceHeight;
            root.transform.localScale = new Vector3(scale, scale, 1f);

            BeatSyncedIdleAnimator animator = root.GetComponent<BeatSyncedIdleAnimator>();
            animator.Configure(null, renderer, frames, 1f, false);
            FightCharacterDefinition definition = root.GetComponent<FightCharacterDefinition>();
            definition.Configure(
                id,
                displayName,
                team,
                role,
                hp,
                attack,
                skillName,
                skillDamage,
                skillEffect,
                attackIntervalBeats,
                maxMana,
                accent,
                renderer,
                animator,
                frames);

            string path = $"{outputFolder}/{id}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null ? prefab.GetComponent<FightCharacterDefinition>() : null;
        }

        private static GameObject BuildSkillEffect(string id, Color accent, Sprite sprite)
        {
            GameObject effect = new($"{id}_SkillEffect", typeof(SpriteRenderer), typeof(FightAttackEffect));
            SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.Lerp(accent, new Color(1f, 0.76f, 0.18f, 1f), 0.45f);
            renderer.sortingOrder = 34;
            string path = $"{EffectRoot}/{id}_SkillEffect.prefab";
            PrefabUtility.SaveAsPrefabAsset(effect, path);
            Object.DestroyImmediate(effect);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void ConfigureFightScene2(
            FightCharacterDefinition[] heroPrefabs,
            FightCharacterDefinition[] enemyPrefabs)
        {
            Scene scene = EditorSceneManager.OpenScene(FightSceneBuilder.Scene2Path, OpenSceneMode.Single);
            FightCombatController fight = Object.FindFirstObjectByType<FightCombatController>();
            FmodBeatClock clock = Object.FindFirstObjectByType<FmodBeatClock>();
            if (fight == null || clock == null)
                throw new MissingReferenceException("FightScene2 controller or beat clock is missing.");

            FightUnitSlot[] allSlots = Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None);
            FightUnitSlot[] heroes = allSlots
                .Where(slot => slot.Team == FightUnitSlot.UnitTeam.Hero)
                .OrderBy(slot => slot.SlotIndex)
                .ToArray();
            FightUnitSlot[] enemies = allSlots
                .Where(slot => slot.Team == FightUnitSlot.UnitTeam.Enemy)
                .OrderBy(slot => slot.SlotIndex)
                .ToArray();
            if (heroes.Length != 3 || enemies.Length != 3)
                throw new MissingReferenceException("FightScene2 must contain three hero and three enemy spawn slots.");

            FightRosterManager manager = fight.GetComponent<FightRosterManager>();
            if (manager == null)
                manager = fight.gameObject.AddComponent<FightRosterManager>();
            manager.Configure(fight, clock, heroes, heroPrefabs, enemies, enemyPrefabs);
            fight.ConfigureRoster(manager);

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(fight);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, FightSceneBuilder.Scene2Path);
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null)
                Debug.LogError($"[FightCharacterPrefabBuilder] Missing sprite: {path}");
            return sprite;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/FightDemo", "Prefabs");
            EnsureFolder(Root, "Heroes");
            EnsureFolder(Root, "Enemies");
            EnsureFolder(Root, "SkillEffects");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
