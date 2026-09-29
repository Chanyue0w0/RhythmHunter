using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightBossValidation
    {
        const string Request = "Temp/FightBossValidation.request";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static FightBossValidation() => EditorApplication.update += Tick;
        static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Flags).Invoke(o, args);
        static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, Flags).GetValue(o);
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            File.Delete(Request);
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/FightScene3.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                void Beat(int n) => Call(fight, "OnBeat", new FmodBeatClock.BeatSnapshot(n, n / 4 + 1, n % 4 + 1, n * 500, 120, 4, 4));
                void Resolve()
                {
                    if (fight.HasPendingEnemyAttack) Call(fight, "ResolvePendingEnemyAttack", Field<int>(fight, "pendingAttackRosterVersion"),
                        Field<long>(fight, "pendingEnemyActionId"), Field<FightUnitSlot>(fight, "pendingEnemyAttacker"));
                }
                void To(int from, int to) { for (int n = from; n <= to; n++) { Beat(n); Resolve(); } }
                void Reset() { fight.RosterManager.SpawnConfiguredRoster(); Call(fight, "RebuildRosterAndResetCombat"); Beat(0); }
                void Basic(FightInputRouter.HeroCommand command, int n)
                {
                    var result = new FmodRhythmJudge.Result(FmodRhythmJudge.Grade.Perfect, 0, n * 500, n * 500,
                        new FmodBeatClock.NearestBeat(n, n / 4 + 1, n % 4 + 1, n * 500, 0), false, "boss-test");
                    Call(fight, "ApplyHeroJudgement", fight.GetHeroForCommand(command), command, result);
                }
                Reset(); Check(fight.BossActive && fight.CurrentBossPhase == FightCombatController.BossPhase.Neutral, "Scene must spawn the boss prefab.");
                To(1, 3); Beat(4); Check(fight.HasPendingEnemyAttack, "Attack phase queues attack.");
                float armor = fight.PartyArmor;
                Basic(FightInputRouter.HeroCommand.Front, 4); Resolve(); Check(fight.PartyArmor == armor, "Same-beat Paladin guards boss attack.");
                Beat(4); Check(!fight.HasPendingEnemyAttack, "Duplicate beat cannot queue another attack.");
                Beat(5); var boss = fight.ActiveEnemySlot; float hp = boss.CurrentHp;
                Basic(FightInputRouter.HeroCommand.Back, 5); Check(boss.CurrentHp == hp - .5f && fight.BossBreakProgress == 1, "Armor reduces damage and Mage contributes break.");
                Beat(6); Basic(FightInputRouter.HeroCommand.Back, 6);
                Beat(7); Basic(FightInputRouter.HeroCommand.Back, 7);
                Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Break && !fight.HasPendingEnemyAttack, "Three Mage abilities break armor.");
                hp = boss.CurrentHp; Beat(8); Basic(FightInputRouter.HeroCommand.Back, 8);
                Check(boss.CurrentHp == hp - 1.5f, "Break provides bonus damage.");
                To(9, 10); Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Break, "Full Break window.");
                Beat(11); Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Neutral, "Break returns to Neutral.");
                Reset(); To(1, 5); Basic(FightInputRouter.HeroCommand.Back, 5); Beat(6);
                var mage = fight.GetHeroForCommand(FightInputRouter.HeroCommand.Back);
                Call(fight, "PerformConfiguredAbility", mage, FightCombatController.ActionType.Skill,
                    mage.UnitSlot.CharacterDefinition.SkillType, mage.UnitSlot.CharacterDefinition.SkillPower, 6L);
                Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Break, "Mage Team Skill contributes two break power.");
                Reset(); To(1, 3); Beat(4);
                Call(fight, "ApplyHeroDamage", Field<int>(fight, "rosterVersion"), fight.FrontHero.UnitSlot, fight.ActiveEnemySlot, 100f);
                Check(!fight.BossActive && !fight.HasPendingEnemyAttack, "A defeated boss cannot finish a queued attack.");
                Reset(); To(1, 8);
                // The input judge can accept the next beat before its FMOD callback.
                Basic(FightInputRouter.HeroCommand.Back, 9);
                Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Charge && fight.BossBreakProgress == 1, "Early boundary input uses judged state.");
                Beat(9); Beat(10); Basic(FightInputRouter.HeroCommand.Back, 10); Beat(11); Basic(FightInputRouter.HeroCommand.Back, 11);
                Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Break, "Charge is interruptible.");
                Beat(12); Check(!fight.HasPendingEnemyAttack, "Interrupted charge cannot hit.");
                Reset(); To(1, 11); Beat(12); Check(Field<float>(fight, "pendingBossDamage") == 2, "Failed charge queues authored heavy damage."); Resolve();
                To(13, 18); Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Rage, "Rage has a separate pattern.");
                Beat(19); Basic(FightInputRouter.HeroCommand.Front, 19); Resolve();
                Check(fight.BossBreakProgress == 1, "Successful Rage guard adds progress.");
                Beat(20); Basic(FightInputRouter.HeroCommand.Back, 20);
                Beat(21); Basic(FightInputRouter.HeroCommand.Back, 21);
                Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Break && !fight.HasPendingEnemyAttack, "Mixed Guard/Mage counters cancel a queued Rage hit.");
                Reset(); To(1, 23); Beat(24); Check(Field<float>(fight, "pendingBossDamage") == 3, "Unresolved Rage ends in finisher."); Resolve(); Beat(25);
                Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Neutral, "Failed Rage returns to Normal after finisher.");
                fight.SetTimingCalibrationActive(true); Check(!fight.BossActive && !fight.HasPendingEnemyAttack, "Calibration clears boss and pending actions.");
                fight.SetTimingCalibrationActive(false); Beat(26); Check(fight.CurrentBossPhase == FightCombatController.BossPhase.Neutral, "Resuming resets the pattern.");
                fight.SetCombatMode(FightCombatController.CombatMode.FrontHero); Check(!fight.BossActive, "Legacy modes opt out.");
                File.WriteAllText("Temp/FightBossValidation.result", "PASS: prefab opt-in, state cycle, same-beat guard, armor, Mage break, vulnerability, early input, charge interrupt/failure, Rage counters/finisher, duplicate beats, pending attack cancellation, calibration and legacy isolation.");
            }
            catch (Exception e) { File.WriteAllText("Temp/FightBossValidation.result", "FAIL: " + e); Debug.LogException(e); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
