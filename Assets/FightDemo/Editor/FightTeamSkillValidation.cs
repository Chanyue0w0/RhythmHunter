using System;
using System.Collections.Generic;
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
    public static class FightTeamSkillValidation
    {
        const string Request = "Temp/FightTeamSkillValidation.request";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static FightTeamSkillValidation() => EditorApplication.update += Tick;
        static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
        static void SetInt(UnityEngine.Object target, string field, int value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).intValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            File.Delete(Request);
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/FightScene3.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                var roster = fight.RosterManager;
                var heroes = roster.HeroPrefabs.ToArray(); var enemies = roster.EnemyPrefabs.ToArray();
                // Resource/effect regression uses a neutral enemy; boss interactions have their own suite.
                for (int i = 0; i < enemies.Length; i++) if (enemies[i] != null)
                    enemies[i] = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Killer.prefab");
                var order = new List<FightUnitSlot>(); fight.TeamSkillStepResolved += order.Add;
                int now = 0;
                void Beat(int beat) { now = beat; Call(fight, "OnBeat", new FmodBeatClock.BeatSnapshot(beat, beat / 4 + 1, beat % 4 + 1, beat * 500, 120, 4, 4)); }
                void Reset(FightCharacterDefinition[] party)
                {
                    roster.SetRoster(party, enemies); Call(fight, "RebuildRosterAndResetCombat");
                    order.Clear(); Beat(0);
                }
                void Basic(int beat, FmodRhythmJudge.Grade grade = FmodRhythmJudge.Grade.Perfect)
                {
                    var result = new FmodRhythmJudge.Result(grade, 0, beat * 500, beat * 500,
                        new FmodBeatClock.NearestBeat(beat, beat / 4 + 1, beat % 4 + 1, beat * 500, 0), false, "test");
                    var command = fight.FrontHero.UnitSlot != null ? FightInputRouter.HeroCommand.Front :
                        fight.SecondHero.UnitSlot != null ? FightInputRouter.HeroCommand.Middle : FightInputRouter.HeroCommand.Back;
                    Call(fight, "ApplyHeroJudgement", fight.GetHeroForCommand(command), command, result);
                }
                void Fill()
                {
                    // Use the successful-Basic charging hook; action effects are separately
                    // covered by the existing Basic suite and the default chain below.
                    for (int i = 0; i < fight.TeamSkillGaugeMax + 2; i++)
                    {
                        Beat(now + 1);
                        Call(fight, "GainTeamSkill", new FmodRhythmJudge.Result(FmodRhythmJudge.Grade.Perfect, 0, 0, 0,
                            new FmodBeatClock.NearestBeat(now, 1, 1, 0, 0), false, "test"));
                    }
                }
                void VerifyChain()
                {
                    var slots = roster.ActiveHeroes.ToArray();
                    int start = now + 1;
                    Check(fight.TryStartTeamSkill() && fight.TeamSkillGauge == 0, "Activation consumes the shared mana.");
                    Check(!fight.TryStartTeamSkill() && order.Count == 0, "Cannot activate twice or off beat.");
                    for (int index = 0; index < slots.Length; index++)
                    {
                        int duration = slots[index].CharacterDefinition.TeamSkillPerformanceBeats;
                        Beat(start);
                        Check(order.Count == index + 1 && order[index] == slots[index], "Step must follow current formation.");
                        Check(fight.TeamSkillPerformingHero == slots[index] && fight.TeamSkillBeatsRemaining == duration, "Per-character performance window must start with its authored duration.");
                        Beat(start); Check(order.Count == index + 1, "Duplicate callback cannot repeat a skill.");
                        for (int offset = 1; offset < duration; offset++)
                        {
                            Beat(start + offset);
                            Check(fight.TeamSkillRunning && order.Count == index + 1, "No advance before the full performance completes.");
                        }
                        Basic(start + duration - 1);
                        Check(fight.TeamSkillGauge == 0, "Performance beats cannot recharge mana.");
                        start += duration;
                    }
                    Check(fight.TeamSkillRunning, "Final actor must retain its complete performance window.");
                    Beat(start);
                    Check(!fight.TeamSkillRunning && fight.TeamSkillPerformingHero == null, "Chain ends on the final performance boundary.");
                    Basic(start); Check(fight.TeamSkillGauge == 1, "Charging resumes at completion.");
                }
                Reset(heroes);
                Check(fight.TeamSkillGaugeMax == 30, "Default cost is 10+10+10.");
                Check(!fight.TryStartTeamSkill(), "Empty mana cannot activate.");
                Basic(0, FmodRhythmJudge.Grade.Miss); Check(fight.TeamSkillGauge == 0, "Miss cannot charge.");
                Basic(0); Basic(0); Check(fight.TeamSkillGauge == 1, "Same beat cannot charge twice.");
                Fill(); Check(fight.TeamSkillGauge == 30 && fight.TeamSkillReady, "Shared mana clamps to summed requirement.");
                float hp = fight.ActiveEnemySlot.CurrentHp; fight.ApplyPartyDamage(4, now);
                VerifyChain();
                Check(fight.ActiveEnemySlot.CurrentHp == hp - 2, "Counter and burst each deal one damage.");
                Check(fight.PartyHp == fight.MaxPartyHp, "Bard restores shared HP.");

                Reset(new[] { heroes[2], heroes[0], heroes[1] });
                int[] costs = {12, 15, 10}, durations = {2, 5, 3};
                for (int i = 0; i < 3; i++)
                {
                    SetInt(roster.ActiveHeroes[i].CharacterDefinition, "teamSkillManaCost", costs[i]);
                    SetInt(roster.ActiveHeroes[i].CharacterDefinition, "teamSkillPerformanceBeats", durations[i]);
                }
                Check(fight.TeamSkillGaugeMax == 37, "Different character costs must sum dynamically.");
                Fill(); VerifyChain();
                for (int mask = 1; mask < 8; mask++)
                {
                    Reset(heroes.Select((hero, index) => (mask & (1 << index)) != 0 ? hero : null).ToArray());
                    Check(fight.TeamSkillGaugeMax == roster.ActiveHeroes.Count * 10, "Only occupied positions contribute cost.");
                    Fill(); VerifyChain();
                }
                Reset(new FightCharacterDefinition[3]);
                Check(fight.TeamSkillGaugeMax == 0 && !fight.TeamSkillReady && !fight.TryStartTeamSkill(), "Empty team cannot activate.");
                Reset(heroes); Fill(); Check(fight.TryStartTeamSkill(), "Full party activates.");
                Beat(now + 1); fight.SetTimingCalibrationActive(true); Beat(now + 1);
                Check(!fight.TeamSkillRunning && fight.TeamSkillGauge == 0, "Calibration cancels performance.");
                fight.SetTimingCalibrationActive(false);
                Reset(heroes); Fill(); Check(fight.TryStartTeamSkill(), "New charge activates.");
                Reset(heroes); Check(!fight.TeamSkillRunning && fight.TeamSkillGauge == 0, "Roster replacement cancels performance.");
                FightTeamUltimateValidation.Run(fight);
                File.WriteAllText("Temp/FightTeamSkillValidation.result", "PASS: summed costs 30/37, 4+4+4 and 2+5+3 performances, final performance, sparse/empty teams, skill effects and cancellation.\nPASS: Ultimate charges only on completed chains, charge cap, separate resources, mutual exclusion, one damage packet per enemy at impact, duplicate callbacks, cancellation and completion.");
            }
            catch (Exception e) { File.WriteAllText("Temp/FightTeamSkillValidation.result", "FAIL: " + e); Debug.LogException(e); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}

