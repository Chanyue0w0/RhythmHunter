using System;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    internal static class FightTeamUltimateValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        internal static void Run(FightCombatController fight)
        {
            var roster = fight.RosterManager;
            var heroes = roster.HeroPrefabs.ToArray(); var enemies = roster.EnemyPrefabs.ToArray();
            int now = 0;
            void Beat(int beat) { now = beat; Call(fight, "OnBeat", new FmodBeatClock.BeatSnapshot(beat, beat / 4 + 1, beat % 4 + 1, beat * 500, 120, 4, 4)); }
            void Gain() => Call(fight, "GainTeamSkill", new FmodRhythmJudge.Result(FmodRhythmJudge.Grade.Perfect, 0, 0, 0,
                new FmodBeatClock.NearestBeat(now, 1, 1, 0, 0), false, "test"));
            void Fill() { for (int i = 0; i < fight.TeamSkillGaugeMax; i++) { Beat(now + 1); Gain(); } }
            void Reset()
            {
                roster.SetRoster(heroes, enemies); Call(fight, "RebuildRosterAndResetCombat"); Beat(0);
            }
            try
            {
                Reset();
                var enemy = enemies.First(p => p != null);
                roster.SetRoster(heroes, new[] { enemy, enemy, enemy }); Call(fight, "RebuildRosterAndResetCombat"); Beat(0);
                Check(!fight.TryStartTeamUltimate(), "Uncharged Ultimate cannot activate.");
                for (int chain = 0; chain < 4; chain++)
                {
                    Fill(); int oldCharge = fight.TeamUltimateCharge;
                    Check(fight.TryStartTeamSkill(), "Ready Team Skill can start even when Ultimate charge is full.");
                    Check(!fight.TryStartTeamUltimate(), "Ultimate cannot overlap a Team Skill chain.");
                    int duration = roster.ActiveHeroes.Sum(s => s.CharacterDefinition.TeamSkillPerformanceBeats);
                    for (int i = 0; i <= duration; i++)
                    {
                        Beat(now + 1);
                        if (i < duration) Check(fight.TeamUltimateCharge == oldCharge, "Partial performances cannot award charge.");
                    }
                    int expected = Math.Min(chain + 1, fight.TeamUltimateRequiredCharges);
                    Check(fight.TeamUltimateCharge == expected, "Exactly one charge per completed chain, clamped to capacity.");
                    Beat(now); Check(fight.TeamUltimateCharge == expected, "Duplicate beat cannot grant another charge.");
                }
                Fill(); int mana = fight.TeamSkillGauge;
                float[] hp = roster.ActiveEnemies.Select(s => s.CurrentHp).ToArray();
                Check(fight.TryStartTeamUltimate() && fight.TeamUltimateCharge == 0, "Ultimate consumes its own charge.");
                Check(fight.TeamSkillGauge == mana && !fight.TryStartTeamUltimate() && !fight.TryStartTeamSkill(), "Ultimate preserves mana and rejects overlapping activations.");
                int start = now + 1;
                for (int i = 0; i < 4; i++)
                {
                    Beat(start + i); Gain();
                    Check(fight.TeamUltimateRunning && fight.TeamSkillGauge == mana, "Ultimate reserves the complete performance and blocks recharge.");
                    for (int enemyIndex = 0; enemyIndex < hp.Length; enemyIndex++)
                        Check(roster.ActiveEnemies[enemyIndex].CurrentHp == hp[enemyIndex] - (i == 3 ? 3 : 0), "Only impact beat applies one 3-damage team packet per enemy.");
                }
                Beat(now); Check(fight.TeamUltimateImpactCount == 1 && fight.TeamUltimateActivationCount == 1, "Duplicate callbacks cannot repeat the Ultimate.");
                Beat(now + 1);
                Check(!fight.TeamUltimateRunning && fight.TeamUltimateCharge == 0, "Ultimate completes without charging itself.");
                Check(fight.TeamSkillReady, "Stored Team Skill mana is available again after Ultimate.");

                Reset(); Fill(); fight.TryStartTeamSkill(); Beat(now + 1);
                fight.SetTimingCalibrationActive(true); Beat(now + 20);
                Check(fight.TeamUltimateCharge == 0, "Cancelled chain grants no charge."); fight.SetTimingCalibrationActive(false);
                Reset();
                for (int i = 0; i < 3; i++) Call(fight, "GainTeamUltimateCharge");
                Check(fight.TryStartTeamUltimate(), "Charged Ultimate starts.");
                fight.SetTimingCalibrationActive(true); Beat(now + 10);
                Check(!fight.TeamUltimateRunning && fight.TeamUltimateImpactCount == 0, "Calibration cancels a queued Ultimate before damage."); fight.SetTimingCalibrationActive(false);
                Reset();
                for (int i = 0; i < 3; i++) Call(fight, "GainTeamUltimateCharge");
                fight.TryStartTeamUltimate(); Beat(now + 1);
                Reset(); Beat(now + 10);
                Check(!fight.TeamUltimateRunning && fight.TeamUltimateCharge == 0 && fight.TeamUltimateImpactCount == 0, "Roster replacement cancels active Ultimate.");
            }
            finally { Reset(); }
        }
    }
}
