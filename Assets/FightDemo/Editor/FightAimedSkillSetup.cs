using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightAimedSkillSetup
    {
        const string Root = "Assets/FightDemo/";
        static FightAimedSkillSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/AimedSkillSetup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Run(); } catch (Exception e) { Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Abilities/Configure and Validate Aimed Shooter")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before configuring assets.");
            try
            {
                string bullet = Root + "Prefabs/VFX/AimedGoldBullet.prefab";
                string preparation = Root + "Prefabs/VFX/AimedPreparation.prefab";
                MakeGoldEffect(Root + "Prefabs/VFX/BleedBullet.prefab", bullet);
                MakeGoldEffect(Root + "Prefabs/VFX/ChargeEffectPixel.prefab", preparation);
                EditPrefab(Root + "Prefabs/ArtBattle/Skeleton.prefab", go =>
                {
                    var data = new SerializedObject(go.GetComponent<FightCharacterDefinition>());
                    data.FindProperty("enableAimedFollowUp").boolValue = true;
                    data.FindProperty("basicAppliesBleeding").boolValue = false;
                    data.FindProperty("basicInterruptsEnemy").boolValue = false;
                    data.FindProperty("consecutiveInputBeats").intValue = 2;
                    data.FindProperty("normalAbilityPower").floatValue = 1;
                    data.FindProperty("aimedApplication").enumValueIndex = 0;
                    data.FindProperty("aimedActivation").enumValueIndex = 0;
                    data.FindProperty("aimedGreenBeats").intValue = 4;
                    data.FindProperty("aimedGoldBeats").intValue = 8;
                    data.FindProperty("aimedGoldProjectile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(bullet);
                    data.FindProperty("normalAbilityEffectPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(bullet);
                    data.FindProperty("aimedPreparationEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(preparation);
                    data.ApplyModifiedPropertiesWithoutUndo();
                });
                string iconPath = Root + "Prefabs/StatusIcons/AimedEffect.prefab";
                EditPrefab(iconPath, go =>
                {
                    var data = new SerializedObject(go.GetComponent<FightStatusIcon>());
                    var sprite = go.GetComponentInChildren<SpriteRenderer>().sprite;
                    if (data.FindProperty("aimedGreenSprite").objectReferenceValue == null) data.FindProperty("aimedGreenSprite").objectReferenceValue = sprite;
                    if (data.FindProperty("aimedGoldSprite").objectReferenceValue == null) data.FindProperty("aimedGoldSprite").objectReferenceValue = sprite;
                    data.ApplyModifiedPropertiesWithoutUndo();
                });
                var scene = SceneManager.GetSceneByPath(Root + "Scenes/Art2_FightScene.unity");
                if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(Root + "Scenes/Art2_FightScene.unity", OpenSceneMode.Additive);
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).First();
                var settings = new SerializedObject(fight);
                settings.FindProperty("aimedEffectIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(iconPath).GetComponent<FightStatusIcon>();
                settings.FindProperty("suspendFeverActivation").boolValue = true;
                settings.FindProperty("normalAttackImpactBeats").floatValue = .4f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Validate();
            }
            catch (Exception e) { File.WriteAllText("Temp/AimedSkillValidation.result", "FAIL: " + e); throw; }
        }

        static void MakeGoldEffect(string source, string destination)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destination) != null) return;
            if (!AssetDatabase.CopyAsset(source, destination)) throw new IOException("Cannot copy " + source);
            EditPrefab(destination, go =>
            {
                go.name = Path.GetFileNameWithoutExtension(destination);
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main; main.startColor = new Color(1, .78f, .15f, 1);
                    main.stopAction = ParticleSystemStopAction.None;
                }
            });
        }
        static void EditPrefab(string path, Action<GameObject> edit)
        {
            var go = PrefabUtility.LoadPrefabContents(path);
            try { edit(go); PrefabUtility.SaveAsPrefabAsset(go, path); }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        [MenuItem("Rhythm Hunter/Abilities/Validate Aimed Shooter")]
        public static void Validate()
        {
            var report = new List<string>();
            var root = new GameObject("AimedValidation") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            try
            {
                var fight = root.AddComponent<FightCombatController>();
                Set(fight, "showStatusIcons", false); Set(fight, "showEnemyDamageNumbers", false);
                Set(fight, "combatMode", FightCombatController.CombatMode.EqualBeat);
                Set(fight, "enableHealthSystemInFrontHeroMode", true);
                var shooter = Slot("shooter", false);
                var warrior = Slot("warrior", false);
                var enemy = Slot("enemy", true);
                Set(shooter.CharacterDefinition, "enableAimedFollowUp", true);
                Set(shooter.CharacterDefinition, "consecutiveInputBeats", 2);
                Set(shooter, "normalAbilityBehavior", FightCharacterDefinition.AbilityBehavior.AimedShot);
                ((List<FightUnitSlot>)Get(fight, "fightScene2Enemies")).Add(enemy);
                Set(Get(fight, "frontHero"), "unitSlot", warrior);
                Set(Get(fight, "secondHero"), "unitSlot", shooter);
                Check(!fight.TryStartTeamSkill() && !fight.TryStartTeamUltimate(), "Fever activation suspended");
                Invoke(fight, "PerformAimedShot", shooter, 1f, 10L);
                Check(fight.GetAimedShotProgress(shooter) == 1 && !enemy.HasAimed, "First input prepares without marking");
                Invoke(fight, "PerformAimedShot", shooter, 1f, 10L);
                Check(fight.GetAimedShotProgress(shooter) == 1, "Duplicate beat cannot fire");
                Invoke(fight, "PerformAimedShot", shooter, 1f, 12L);
                Check(fight.GetAimedShotProgress(shooter) == 1, "Missed beat restarts preparation");
                Invoke(fight, "PerformAimedShot", shooter, 1f, 13L);
                Check(fight.GetAimedShotProgress(shooter) == 0 && !enemy.HasAimed, "Second consecutive input launches; no early mark");
                Invoke(fight, "AdvanceNormalImpacts", .39d);
                Check(!enemy.HasAimed && enemy.CurrentHp == 20, "No damage before shared impact beat");
                Invoke(fight, "AdvanceNormalImpacts", .011d);
                Check(enemy.HasGoldAimed && enemy.CurrentHp == 19, "Shot impact deals 1 HP and applies gold");
                int count = ((IList)Get(fight, "normalImpacts")).Count;
                Invoke(fight, "TriggerAimedFollowUp", shooter);
                Check(((IList)Get(fight, "normalImpacts")).Count == count, "Shooter cannot trigger follow-up");
                Invoke(fight, "TriggerAimedFollowUp", warrior);
                Check(((IList)Get(fight, "normalImpacts")).Count == count + 1, "One teammate call creates one follow-up");
                Invoke(fight, "AdvanceNormalImpacts", .401d);
                Check(enemy.CurrentHp == 18 && ((IList)Get(fight, "normalImpacts")).Count == 0, "Follow-up deals 1 HP without recursion");
                Set(fight, "<NormalBattleBeatCount>k__BackingField", 8L); enemy.AdvanceTimedStatuses(8);
                Check(enemy.HasGoldAimed, "Gold survives its eighth normal beat");
                Set(fight, "<NormalBattleBeatCount>k__BackingField", 9L); enemy.AdvanceTimedStatuses(9);
                Check(!enemy.HasAimed, "Gold expires after eight protected intervals");
                enemy.ApplyAimed(shooter, false);
                Invoke(enemy, "NotifyAimedDirectHit", warrior);
                Check(!enemy.HasGoldAimed, "Default green activation is shooter-only");
                Invoke(enemy, "NotifyAimedDirectHit", shooter);
                Check(enemy.HasGoldAimed && enemy.AimedBeatsRemaining == 8, "Green activation starts a fresh eight beats");
                enemy.ClearTimedStatuses(); enemy.ApplyAimed(shooter, false);
                Set(shooter.CharacterDefinition, "aimedActivation", FightCharacterDefinition.AimedActivationSource.AnyHeroDirectHit);
                Invoke(enemy, "NotifyAimedDirectHit", warrior);
                Check(enemy.HasGoldAimed, "Optional any-hero activation works");
                Invoke(fight, "TriggerAimedFollowUp", warrior);
                Invoke(fight, "ClearNormalImpacts"); enemy.ClearTimedStatuses();
                Check(!enemy.HasAimed && ((IList)Get(fight, "normalImpacts")).Count == 0, "Cleanup removes marks and pending hits");
                enemy.CharacterDefinition.BossPattern.enabled = true;
                enemy.CharacterDefinition.BossPattern.armorReduction = 10;
                Set(fight, "bossActor", enemy);
                Set(fight, "<CurrentBossPhase>k__BackingField", FightCombatController.BossPhase.Armor);
                float before = enemy.CurrentHp;
                Invoke(fight, "LaunchAimedShot", shooter, enemy, 1f, false);
                Invoke(fight, "AdvanceNormalImpacts", .401d);
                Check(enemy.CurrentHp == before && enemy.HasGoldAimed, "Zero-damage armored contact still applies gold");
                enemy.ClearTimedStatuses();
                Set(fight, "<AwaitingNextWave>k__BackingField", true);
                Invoke(fight, "LaunchAimedShot", shooter, enemy, 1f, false);
                Invoke(fight, "AdvanceNormalImpacts", .401d);
                Check(!enemy.HasAimed && enemy.CurrentHp == before, "Wave invulnerability prevents status application");
                Set(fight, "<AwaitingNextWave>k__BackingField", false);
                Set(fight, "bossActor", null);
                enemy.ApplyAimed(shooter, true);
                int hits = 0;
                Invoke(fight, "ScheduleNormalImpact", warrior, enemy, (Action)(() => hits++), null, -1f, null);
                Invoke(fight, "TriggerAimedFollowUp", warrior);
                Invoke(fight, "AdvanceNormalImpacts", .39d);
                Check(hits == 0 && enemy.CurrentHp == before, "Melee and follow-up both wait for impact");
                Invoke(fight, "AdvanceNormalImpacts", .011d);
                Check(hits == 1 && enemy.CurrentHp == before - 1, "Melee and follow-up resolve at the same beat delay");
                enemy.ApplyAimed(shooter, true);
                enemy.TakeDamage(100);
                Check(!enemy.HasAimed, "Death removes Aimed");

                FightUnitSlot Slot(string name, bool hostile)
                {
                    var go = new GameObject(name); go.transform.SetParent(root.transform);
                    var slot = go.AddComponent<FightUnitSlot>(); var data = go.AddComponent<FightCharacterDefinition>();
                    Set(slot, "characterDefinition", data); Set(slot, "hasCharacter", true);
                    Set(slot, "team", hostile ? FightUnitSlot.UnitTeam.Enemy : FightUnitSlot.UnitTeam.Hero);
                    Set(slot, "currentHp", 20f); Set(slot, "maxHp", 20f);
                    Set(slot, "<CombatController>k__BackingField", fight);
                    Set(slot, "runtimeCastEffectAnchor", go.transform); Set(slot, "runtimeImpactEffectAnchor", go.transform);
                    return slot;
                }
                void Check(bool passed, string label) { if (!passed) throw new Exception(label); report.Add("PASS: " + label); }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            File.WriteAllLines("Temp/AimedSkillValidation.result", report);
        }
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static object Get(object o, string field) => o.GetType().GetField(field, Flags).GetValue(o);
        static void Set(object o, string field, object value) => o.GetType().GetField(field, Flags).SetValue(o, value);
        static object Invoke(object o, string method, params object[] args) => o.GetType().GetMethod(method, Flags).Invoke(o, args);
    }
}
