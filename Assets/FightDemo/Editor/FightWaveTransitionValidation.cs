using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightWaveTransitionValidation
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static FightWaveTransitionValidation() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/WaveTransitionValidation.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(request); } catch (IOException) { return; }
            try { Run(); } catch (Exception e) { File.WriteAllText("Temp/WaveTransitionValidation.result", "FAIL: " + e); Debug.LogException(e); }
        }
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
        static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Run()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                Set(fight, "enableSuccessfulInputSound", false);
                var roster = fight.RosterManager;
                roster.SpawnConfiguredRoster();
                Call(fight, "RebuildRosterAndResetCombat");
                Set(roster, "<BattleStarted>k__BackingField", true);
                Set(fight, "<AwaitingNextWave>k__BackingField", true);
                var enemy = roster.ActiveEnemies.First();
                float hp = enemy.CurrentHp;
                Set(enemy, "fragile", true);
                foreach (FightUnitSlot.DamageKind kind in Enum.GetValues(typeof(FightUnitSlot.DamageKind)))
                    Check(enemy.TakeDamage(1, kind) == 0 && enemy.CurrentHp == hp && enemy.IsFragile, "Transition damage consumed HP/fragile: " + kind);
                var mage = fight.GetHeroForCommand(FightInputRouter.HeroCommand.Middle);
                Set(fight, "partyHp", 1f); Set(fight, "partyArmor", 0f);
                Call(fight, "HealPartyHealth", mage.UnitSlot, 1f);
                Check(fight.PartyHp == 2, "Healing blocked during transition");
                Check(fight.RecoverPartyArmor(FightCharacterDefinition.ArmorRestoreMode.Full) > 0, "Armor recovery blocked");
                var accepted = new FmodRhythmJudge.Result(FmodRhythmJudge.Grade.Perfect, 0, 1000, 1000,
                    new FmodBeatClock.NearestBeat(100, 26, 1, 1000, 0), false, "Validation");
                int mana = fight.TeamSkillGauge;
                Call(fight, "ApplyHeroJudgement", mage, FightInputRouter.HeroCommand.Middle, accepted);
                Check(fight.TeamSkillGauge == Mathf.Min(mana + 1, fight.TeamSkillGaugeMax), "Ordinary input did not grant one MP");
                var guards = (System.Collections.Generic.HashSet<long>)typeof(FightCombatController).GetField("guardedBeats", Private).GetValue(fight);
                Check(guards.Contains(100), "Guard did not arm during transition");
                mana = fight.TeamSkillGauge;
                int feedback = 0; fight.HeroCalled += _ => feedback++;
                Call(fight, "ApplyTransitionRhythmJudgement", accepted);
                Check(feedback == 1 && fight.TeamSkillGauge == mana && !fight.TeamSkillRunning, "A activated skill, changed MP, or missed feedback");
                var shooter = fight.ThirdHero.UnitSlot;
                Call(fight, "PlayAnimatedHeroAction", shooter, enemy, FightCombatController.ActionType.LightAttack, 1f);
                Set(fight, "<AwaitingNextWave>k__BackingField", false);
                var fire = (Action)typeof(FightCharacterCombatAnimator).GetField("attackEffectCallback", Private).GetValue(shooter.CombatAnimator);
                fire?.Invoke();
                var projectile = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightBeatProjectile>(true)).FirstOrDefault();
                Check(projectile != null, "Transition shot did not spawn projectile");
                var impact = (Action)typeof(FightBeatProjectile).GetField("impact", Private).GetValue(projectile);
                impact.Invoke();
                Check(enemy.CurrentHp == hp && enemy.IsFragile && enemy.BleedBeatsRemaining == 0, "Delayed transition projectile damaged after immunity ended");
                Check(enemy.TakeDamage(.5f) > 0 && !enemy.IsFragile, "Damage did not resume after transition");
                File.WriteAllText("Temp/WaveTransitionValidation.result", "PASS: direct/DoT/secondary immunity preserves fragile; heal/armor/guard and ordinary MP work; A gives feedback without MP/skill; delayed shot remains harmless; normal damage resumes.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
