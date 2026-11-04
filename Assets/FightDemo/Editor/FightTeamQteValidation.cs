using System;
using System.Collections;
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
    public static class FightTeamQteValidation
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static int checks;
        static FightTeamQteValidation() => EditorApplication.update += TickRequest;
        static void TickRequest()
        {
            const string path = "Temp/FightTeamQteValidation.request";
            if (!File.Exists(path) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            File.Delete(path);
            try { Run(); }
            catch (Exception ex) { File.WriteAllText("Temp/FightTeamQteValidation.result", "FAIL: " + ex); Debug.LogException(ex); }
        }
        static object Get(object obj, string field) => obj.GetType().GetField(field, Flags).GetValue(obj);
        static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
        static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);
        static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }

        [MenuItem("Rhythm Hunter/Validate Team Skill QTE")]
        public static void Run()
        {
            checks = 0;
            var note = new FightQteNote(1, .5, .2);
            Check(!note.Press(.4, .4), "A hidden prompt cannot be hit.");
            Check(note.Press(1, 1) && note.Outcome == FightQteNote.Result.Hit, "Exact beat succeeds.");
            Check(!note.Press(1, 1) && !note.Expire(2), "A prompt resolves only once.");
            note = new FightQteNote(1, .5, .2);
            Check(note.Press(.5, .5) && note.Outcome == FightQteNote.Result.Miss, "Early presses cannot be held for success.");
            note = new FightQteNote(1, .5, .2);
            Check(note.Expire(1.21) && note.Outcome == FightQteNote.Result.Miss, "Missing a prompt expires it.");

            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Main_FightScene.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                Set(fight.RosterManager, "requireBattlePreparation", false);
                Set(fight, "showEnemyDamageNumbers", false); Set(fight, "showStatusIcons", false);
                var heroes = new[] { "LongswordWarrior", "Mage", "Skeleton" }.Select(n =>
                    AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>($"Assets/FightDemo/Prefabs/ArtBattle/{n}.prefab")).ToArray();
                var enemyData = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Killer.prefab");
                fight.RosterManager.SetRoster(heroes, new[] { enemyData, enemyData, enemyData });
                Call(fight, "RebuildRosterAndResetCombat");
                Set(fight.BeatClock, "hasAnchor", true);
                Set(fight.BeatClock, "latestBeat", new FmodBeatClock.BeatSnapshot(0, 1, 1, 0, 120, 4, 4));
                Set(Get(fight, "rhythmJudge"), "perfectWindowMs", 100f);
                var enemies = fight.RosterManager.ActiveEnemies.OrderByDescending(e => e.SlotIndex).ToArray();
                var front = enemies[0]; var middle = enemies[1]; var back = enemies[2];
                foreach (var hero in fight.RosterManager.ActiveHeroes) Set(hero, "combatAnimator", null);
                void Reset()
                {
                    Call(fight, "ResetBattleStateForRoster");
                    foreach (var enemy in enemies)
                    { enemy.CharacterDefinition.BossPattern.enabled = false; Set(enemy, "maxHp", 100f); enemy.RestoreFullHealth(); }
                    Set(fight, "latestCombatBeat", 0L);
                }
                void Begin(FightCombatController.HeroBeatSettings hero)
                {
                    Set(fight, "nextTeamSkillBeat", 2L); Set(fight, "pendingTeamSkillHero", hero);
                    Call(fight, "BeginEnemyPause");
                    Check((bool)Call(fight, "BeginTeamSkillQte", hero, 0L), "Prefab enables role-specific QTE.");
                }
                void Press(double beat) => Call(fight, "SubmitTeamSkillQteAt", beat, beat);
                void Advance(double beat) => Call(fight, "UpdateTeamSkillQteAt", beat, beat);
                void Impact() => Call(fight, "OnQteImpact");
                int Flights() => ((IList)Get(fight, "skillFlights")).Count;

                Reset(); Begin(fight.FrontHero); Press(1);
                Check(front.CurrentHp == 100, "Successful sword QTE waits for animation impact.");
                Impact(); Check(front.CurrentHp == 98 && front.IsFragile, "Successful sword slash deals full damage and Fragile.");
                Impact(); Check(front.CurrentHp == 98, "Repeated animation marker does not double-hit.");
                Reset(); Begin(fight.FrontHero); Advance(1.4); Impact();
                Check(front.CurrentHp == 99 && front.IsFragile, "Failed sword QTE halves damage, retains Fragile.");
                Reset(); Set(fight.FrontHero.UnitSlot.CharacterDefinition, "qteFailureAppliesStatus", false);
                Begin(fight.FrontHero); Advance(1.4); Impact();
                Check(front.CurrentHp == 99 && !front.IsFragile, "Failure status policy is configurable.");
                Set(fight.FrontHero.UnitSlot.CharacterDefinition, "qteFailureAppliesStatus", true);

                Reset(); Begin(fight.SecondHero); Press(1); Advance(1.34);
                Check(!front.HasSlimeFragment && Flights() == 1, "Slime attaches on landing, not on QTE input.");
                Advance(1.36);
                Check(front.HasSlimeFragment && front.CurrentHp == 100 && Get(front, "slimeFloorVisual") != null,
                    "Slime landing attaches a floor visual without damage.");
                Press(3); Advance(3.36);
                Check(front.HasSlimeFragment && !middle.HasSlimeFragment && back.HasSlimeFragment,
                    "Missed middle QTE does not shift the successful back shot to middle.");
                var floor = (GameObject)Get(front, "slimeFloorVisual");
                front.TakeDamage(1);
                Check(front.SlimeDetonationPending && floor.activeSelf, "Floor remains during the half-beat detonation delay.");
                front.AdvanceFragmentDetonations(.5f);
                Check(front.CurrentHp == 98 && !floor.activeSelf, "Detonation damages once and returns floor visual to pool.");
                Check(fight.NormalBattleBeatCount == 0, "QTE, flight and explosion do not spend normal status duration beats.");
                Check(fight.TeamSkillGauge == 0, "QTE inputs never grant MP.");

                Reset(); Begin(fight.ThirdHero); Press(1); Press(3);
                Check(Flights() == 0 && enemies.All(e => e.CurrentHp == 100), "Bomb successes are recorded, not launched immediately.");
                Advance(4); Impact(); Check(Flights() == 2, "Throw marker launches all successful bombs simultaneously.");
                Advance(4.36);
                Check(front.CurrentHp == 99 && middle.CurrentHp == 100 && back.CurrentHp == 99,
                    "Bombs damage their fixed successful targets only.");
                Check(front.IsStunned && !middle.IsStunned && back.IsStunned, "Only bomb victims are stunned.");
                Reset(); Begin(fight.ThirdHero); Advance(4); Impact(); Advance(5);
                Check(enemies.All(e => e.CurrentHp == 100 && !e.IsStunned) && Flights() == 0, "All misses produce no bombs or effects.");

                Reset(); front.CharacterDefinition.BossPattern.enabled = true;
                Begin(fight.ThirdHero); Press(1); Advance(4); Impact(); Advance(4.36);
                Check(front.CurrentHp == 99 && !front.IsStunned, "Boss super armor keeps damage but rejects stun.");
                Reset(); Begin(fight.SecondHero); Press(1); front.TakeDamage(200); Advance(1.4);
                Check(!front.HasSlimeFragment && !middle.HasSlimeFragment, "Flight to a dead target never retargets another enemy.");
                Reset(); Begin(fight.SecondHero); Press(1); Call(fight, "ResetTeamSkill");
                Check(Flights() == 0 && !fight.TeamQteActive && ((IList)Get(fight, "activeSkillVisuals")).Count == 0,
                    "Restart clears all prompts, flights and pooled effects.");
                string message = $"PASS: {checks} QTE timing, skill outcomes, target order, projectiles, floor lifecycle and cleanup checks.";
                File.WriteAllText("Temp/FightTeamQteValidation.result", message); Debug.Log(message);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
