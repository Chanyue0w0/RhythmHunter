using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    internal static class FightBasicGuardValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static void Check(bool passed, string message) { if (!passed) throw new InvalidOperationException(message); }

        internal static void Run(FightCombatController fight)
        {
            var clock = Get<FmodBeatClock>(fight, "beatClock");
            var judge = Get<FmodRhythmJudge>(fight, "rhythmJudge");
            var roster = fight.RosterManager;
            var heroes = roster.HeroPrefabs.ToArray();
            var enemies = roster.EnemyPrefabs.ToArray();
            var originalAnchor = clock.LatestBeat;
            bool hadAnchor = clock.HasTimingAnchor;
            typeof(FmodBeatClock).GetField("hasAnchor", Private).SetValue(clock, true);
            typeof(FmodBeatClock).GetField("latestBeat", Private).SetValue(clock, new FmodBeatClock.BeatSnapshot(0, 1, 1, 1000, 120, 4, 4));

            void Reset()
            {
                Call(fight, "RebuildRosterAndResetCombat");
                judge.ResetDuplicateTracking();
                foreach (var enemy in roster.ActiveEnemies) enemy.RestoreFullHealth();
            }
            FmodRhythmJudge.Result Input(FightInputRouter.HeroCommand command, int beat, int delta = 0)
            {
                int rawMs = 1000 + beat * 500 + delta - Mathf.RoundToInt(judge.JudgementOffsetMs);
                var result = (FmodRhythmJudge.Result)Call(judge, "JudgeTimelinePosition", rawMs);
                Call(fight, "ApplyHeroJudgement", fight.GetHeroForCommand(command), command, result);
                return result;
            }
            void Queue(int beat) => Call(fight, "QueueEnemyAttack", new FmodBeatClock.BeatSnapshot(beat, beat / 4 + 1, beat % 4 + 1, 1000 + beat * 500, 120, 4, 4));
            void Resolve() => Call(fight, "ResolvePendingEnemyAttack", Get<int>(fight, "pendingAttackRosterVersion"), Get<long>(fight, "pendingEnemyActionId"), Get<FightUnitSlot>(fight, "pendingEnemyAttacker"));

            try
            {
                // Both edges of the actual calibrated judgement window protect the same beat.
                foreach (int delta in new[] { -Mathf.RoundToInt(judge.PerfectWindowMs), 0, Mathf.RoundToInt(judge.PerfectWindowMs) })
                {
                    Reset();
                    if (delta >= 0) Queue(3);
                    Check(Input(FightInputRouter.HeroCommand.Front, 3, delta).Judgement == FmodRhythmJudge.Grade.Perfect, "Guard should accept both early and late window boundaries.");
                    if (delta < 0) Queue(3);
                    Resolve();
                    Check(fight.BlockedAttackCount == 1 && fight.PartyArmor == 3 && fight.PartyHp == 5, "Same-beat Guard must protect armor and HP.");
                    Resolve();
                    Check(fight.BlockedAttackCount == 1, "Enemy damage must resolve only once.");
                }

                Reset();
                Input(FightInputRouter.HeroCommand.Front, 2);
                Queue(3); Resolve();
                Check(fight.BlockedAttackCount == 0 && fight.PartyArmor == 2, "Guard must not be stored for a future attack.");

                Reset();
                Queue(3);
                Input(FightInputRouter.HeroCommand.Front, 4, -120);
                Resolve();
                Check(fight.BlockedAttackCount == 0 && fight.PartyArmor == 2, "Early input for the next beat must not guard a pending previous beat.");

                Reset();
                Queue(3);
                Input(FightInputRouter.HeroCommand.Front, 3, 120);
                Input(FightInputRouter.HeroCommand.Front, 4, -120);
                Resolve();
                Queue(4); Resolve();
                Check(fight.BlockedAttackCount == 2, "Adjacent accepted Guard beats must not overwrite each other.");
                Queue(5); Resolve();
                Check(fight.PartyArmor == 2, "A consumed Guard cannot protect subsequent beats.");

                Reset();
                Check(Input(FightInputRouter.HeroCommand.Front, 3, 121).Judgement == FmodRhythmJudge.Grade.Miss, "Out-of-window input must miss.");
                Queue(3); Resolve();
                Check(fight.PartyArmor == 2 && fight.BlockedAttackCount == 0, "Missed Guard must have no defensive effect.");

                Reset();
                var enemyTarget = fight.ActiveEnemySlot;
                float initialHp = enemyTarget.CurrentHp;
                Input(FightInputRouter.HeroCommand.Back, 3);
                var duplicate = Input(FightInputRouter.HeroCommand.Front, 3);
                Check(duplicate.DuplicateBeat && duplicate.Judgement == FmodRhythmJudge.Grade.Miss, "A second character cannot use the same accepted beat.");
                Queue(3); Resolve();
                Check(enemyTarget.CurrentHp == initialHp - 1 && fight.PartyArmor == 2, "Mage damage must resolve immediately; duplicate Guard must not execute.");

                Reset();
                fight.ApplyPartyDamage(3.5f, 0);
                Input(FightInputRouter.HeroCommand.Middle, 1);
                Check(fight.PartyHp == 5 && fight.PartyArmor == 0, "Bard heals 0.5 HP without regenerating armor.");
                Input(FightInputRouter.HeroCommand.Middle, 2);
                Check(fight.PartyHp == 5, "Consecutive Bard Basics are allowed and cannot overheal.");
                Input(FightInputRouter.HeroCommand.Front, 3);
                Queue(3); Resolve();
                Check(fight.PartyHp == 5, "Guard must protect HP when armor is empty.");
                fight.AdvanceArmorRecovery(4);
                Check(fight.PartyArmor == 0.5f, "A blocked attack must not reset armor recovery.");

                roster.SetRoster(new[] { heroes[2], heroes[0], heroes[1] }, enemies);
                Reset();
                Input(FightInputRouter.HeroCommand.Front, 3);
                Queue(3); Resolve();
                Check(fight.BlockedAttackCount == 0 && fight.PartyArmor == 0, "Mage in front must not gain Guard from position.");
                Reset();
                Input(FightInputRouter.HeroCommand.Middle, 3);
                Queue(3); Resolve();
                Check(fight.BlockedAttackCount == 1 && fight.PartyArmor == 1, "Paladin in middle must still guard via Y.");

                roster.SetRoster(heroes, enemies);
                fight.SetCombatMode(FightCombatController.CombatMode.FrontHero);
                Reset();
                Input(FightInputRouter.HeroCommand.Front, 2);
                Queue(2 + fight.GetEnemyBeatsUntilAttack(2)); Resolve();
                Check(fight.BlockedAttackCount == 1, "Older FrontHero scenes retain their existing stored-Guard behavior.");
                File.WriteAllText("Temp/FightBasicGuardValidation.result", "PASS: early/late same-beat Guard; wrong-beat and missed Guard rejected; adjacent guards retained; duplicate Basic rejected; Mage immediate damage; Bard capped healing; character-owned Guard after swapping; armor recovery after block; legacy compatibility.");
            }
            finally
            {
                fight.SetCombatMode(FightCombatController.CombatMode.EqualBeat);
                roster.SetRoster(heroes, enemies);
                typeof(FmodBeatClock).GetField("hasAnchor", Private).SetValue(clock, hadAnchor);
                typeof(FmodBeatClock).GetField("latestBeat", Private).SetValue(clock, originalAnchor);
                Reset();
            }
        }
    }
}
