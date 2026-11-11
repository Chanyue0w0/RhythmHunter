using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightSwordChargeSetup
    {
        static FightSwordChargeSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            if (File.Exists("Temp/SwordAuraValidation.request") && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete("Temp/SwordAuraValidation.request");
                try { ValidateAura(); }
                catch (Exception e) { File.WriteAllText("Temp/SwordAuraValidation.result", "FAIL: " + e); }
            }
            const string request = "Temp/SwordChargeSetup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Run(); }
            catch (Exception e) { File.WriteAllText("Temp/SwordChargeValidation.result", "FAIL: " + e); Debug.LogException(e); }
        }
        public static void ValidateAura()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/ArtBattle/LongSwordWarrior_Weapon.prefab");
            var root = UnityEngine.Object.Instantiate(source);
            try
            {
                var data = root.GetComponent<FightCharacterDefinition>();
                var aura = (GameObject)Get(data, "swordChargeAura");
                if (aura == null || aura.name != "YellowAura") throw new Exception("Missing child aura reference");
                Invoke(data, "ResetSwordAura");
                if (aura.activeSelf) throw new Exception("Aura visible without charge");
                data.SetSwordChargeAura(true, null);
                if (!aura.activeSelf) throw new Exception("Aura did not activate");
                var particles = aura.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particles) ps.Simulate(.3f, false, false);
                data.SetSwordChargeAura(false, null);
                if (!aura.activeSelf) throw new Exception("Aura disabled before particles finish");
                foreach (var ps in particles)
                {
                    if (ps.isEmitting) throw new Exception("Aura kept emitting after charge ended");
                    ps.Simulate(120, false, false);
                }
                Invoke(data, "Update");
                if (aura.activeSelf)
                {
                    string detail = "";
                    foreach (var ps in particles) detail += $" {ps.name}: alive={ps.IsAlive(false)} count={ps.particleCount} emitting={ps.isEmitting} time={ps.time} lifetime={ps.main.startLifetime.constantMax}";
                    throw new Exception("Aura not disabled after completion" + detail);
                }
                data.SetSwordChargeAura(true, null);
                if (!aura.activeSelf || aura.GetComponentsInChildren<ParticleSystem>(true).Length != particles.Length)
                    throw new Exception("Aura cannot be reused");
                foreach (var component in aura.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    var property = new SerializedObject(component).FindProperty("clearBehavior");
                    if (property != null && property.intValue != 0) throw new Exception("Aura still has automatic destructive cleanup");
                }
                File.WriteAllText("Temp/SwordAuraValidation.result", "PASS: reference, initially hidden, full-charge activation, stop emission, natural drain, disable, reuse, non-destructive cleanup");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [MenuItem("Rhythm Hunter/Abilities/Configure and Validate Resonance Warrior")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            const string path = "Assets/FightDemo/Prefabs/ArtBattle/LongSwordWarrior_Weapon.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var data = new SerializedObject(root.GetComponent<FightCharacterDefinition>());
                data.FindProperty("enableSwordCharge").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Validate();
            FightAimedSkillSetup.Validate();
            FightWeaponAnimationSetup.Validate();
        }
        [MenuItem("Rhythm Hunter/Abilities/Validate Resonance Warrior")]
        public static void Validate()
        {
            var lines = new List<string>();
            var root = new GameObject("SwordChargeValidation") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            try
            {
                var fight = root.AddComponent<FightCombatController>();
                Set(fight, "showStatusIcons", false); Set(fight, "showEnemyDamageNumbers", false);
                Set(fight, "combatMode", FightCombatController.CombatMode.EqualBeat);
                Set(fight, "enableHealthSystemInFrontHeroMode", true);
                var warrior = Slot("warrior", false); var enemy = Slot("enemy", true);
                Set(warrior.CharacterDefinition, "enableSwordCharge", true);
                // Exercise partial-streak resets with an explicit multi-hit configuration,
                // independently of the live prefab's single-hit / five-beat tuning.
                Set(warrior.CharacterDefinition, "swordHitsPerStage", 4);
                Set(warrior.CharacterDefinition, "swordFullChargeBeats", 8);
                Set(Get(fight, "frontHero"), "unitSlot", warrior);
                ((List<FightUnitSlot>)Get(fight, "fightScene2Enemies")).Add(enemy);
                Check(fight.GetSwordChargeStage(warrior) == 1, "Initial stage is 1");
                Invoke(fight, "PerformSwordAttack", warrior, 1f, 1L);
                Step(.39); Check(fight.GetSwordChargeHits(warrior) == 0 && enemy.CurrentHp == 100, "Input alone never charges; existing impact delay retained");
                Step(.011); Check(fight.GetSwordChargeHits(warrior) == 1 && enemy.CurrentHp == 99, "Actual first contact charges once");
                Invoke(fight, "PerformSwordAttack", warrior, 1f, 1L); Step(.5);
                Check(fight.GetSwordChargeHits(warrior) == 1 && enemy.CurrentHp == 99, "Same-beat duplicate cannot attack or charge");
                Attack(2); Attack(4);
                Check(fight.GetSwordChargeHits(warrior) == 1, "Missed next warrior beat resets partial streak");
                Attack(5); Attack(6); Attack(7);
                Check(fight.GetSwordChargeStage(warrior) == 2 && fight.GetSwordChargeHits(warrior) == 0, "Four consecutive contacts reach stage 2");
                Attack(8); Invoke(fight, "BreakSwordStreaks");
                Check(fight.GetSwordChargeStage(warrior) == 2 && fight.GetSwordChargeHits(warrior) == 0, "Breaking streak preserves earned stage");
                Set(fight, "<NormalBattleBeatCount>k__BackingField", 100L);
                Attack(10); Attack(11); Attack(12); Attack(13);
                Check(fight.GetSwordChargeStage(warrior) == 3 && fight.GetSwordChargeBeatsRemaining(warrior) == 8, "Next four contacts begin eight-beat full charge");
                float hp = enemy.CurrentHp;
                Invoke(fight, "PerformSwordAttack", warrior, 1f, 14L);
                Step(.401); Check(enemy.CurrentHp == hp - 2, "Full charge deals 2 on the usual impact point");
                Step(.498); Check(enemy.CurrentHp == hp - 2, "Extra damage waits half a beat");
                Step(.002); Check(enemy.CurrentHp == hp - 3 && ((IList)Get(fight, "normalImpacts")).Count == 0, "Exactly one delayed 1-damage tick, without recursion");
                Invoke(fight, "BreakSwordStreaks");
                Check(fight.GetSwordChargeStage(warrior) == 3, "Missing input does not cancel active full charge");
                Set(fight, "<NormalBattleBeatCount>k__BackingField", 107L); Invoke(fight, "AdvanceSwordChargeBeat");
                Check(fight.GetSwordChargeStage(warrior) == 3, "Full charge remains before eight beats elapse");
                Set(fight, "<NormalBattleBeatCount>k__BackingField", 108L); Invoke(fight, "AdvanceSwordChargeBeat");
                Check(fight.GetSwordChargeStage(warrior) == 1 && fight.GetSwordChargeHits(warrior) == 0, "Eight-beat expiry returns to stage 1");
                hp = enemy.CurrentHp; Attack(20);
                Check(enemy.CurrentHp == hp - 1, "Expired charge restores ordinary damage");
                Invoke(fight, "PerformSwordAttack", warrior, 1f, 21L);
                Set(fight, "rosterVersion", (int)Get(fight, "rosterVersion") + 1); Step(.5);
                Check(enemy.CurrentHp == hp - 1 && fight.GetSwordChargeHits(warrior) == 0, "Old-wave pending hit is cancelled and cannot charge");
                Set(fight, "<AwaitingNextWave>k__BackingField", true);
                Attack(22); Check(enemy.CurrentHp == hp - 1 && fight.GetSwordChargeHits(warrior) == 0, "Transition attacks neither damage nor charge");
                Set(fight, "<AwaitingNextWave>k__BackingField", false);
                enemy.CharacterDefinition.BossPattern.enabled = true; enemy.CharacterDefinition.BossPattern.armorReduction = 10;
                Set(fight, "bossActor", enemy); Set(fight, "<CurrentBossPhase>k__BackingField", FightCombatController.BossPhase.Armor);
                Attack(23); Check(enemy.CurrentHp == hp - 1 && fight.GetSwordChargeHits(warrior) == 1, "Blocked contact counts as hit, independently of HP damage");
                Set(fight, "bossActor", null); Invoke(fight, "ResetSwordCharges");
                for (long beat = 30; beat < 38; beat++) Attack(beat);
                Set(enemy, "currentHp", 2f); Invoke(fight, "PerformSwordAttack", warrior, 1f, 38L); Step(.401); Step(.5);
                Check(enemy.CurrentHp == 0 && ((IList)Get(fight, "normalImpacts")).Count == 0, "Lethal first tick cancels delayed tick without retargeting");
                Invoke(fight, "ResetSwordCharges");
                Check(fight.GetSwordChargeStage(warrior) == 1 && ((IDictionary)Get(fight, "swordCharges")).Count == 0, "Battle cleanup clears all warrior state");
                void Attack(long beat) { Invoke(fight, "PerformSwordAttack", warrior, 1f, beat); Step(.401); }
                void Step(double beats) => Invoke(fight, "AdvanceNormalImpacts", beats);
                void Check(bool ok, string label) { if (!ok) throw new Exception(label); lines.Add("PASS: " + label); }
                FightUnitSlot Slot(string name, bool hostile)
                {
                    var go = new GameObject(name); go.transform.SetParent(root.transform);
                    var slot = go.AddComponent<FightUnitSlot>(); var data = go.AddComponent<FightCharacterDefinition>();
                    Set(data, "useFallbackAbilityEffects", false);
                    Set(slot, "characterDefinition", data); Set(slot, "hasCharacter", true);
                    Set(slot, "team", hostile ? FightUnitSlot.UnitTeam.Enemy : FightUnitSlot.UnitTeam.Hero);
                    Set(slot, "currentHp", 100f); Set(slot, "maxHp", 100f);
                    Set(slot, "<CombatController>k__BackingField", fight);
                    Set(slot, "runtimeCastEffectAnchor", go.transform); Set(slot, "runtimeImpactEffectAnchor", go.transform);
                    return slot;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            File.WriteAllLines("Temp/SwordChargeValidation.result", lines);
        }
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static object Get(object obj, string field) => obj.GetType().GetField(field, Flags).GetValue(obj);
        static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
        static object Invoke(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);
    }
}
