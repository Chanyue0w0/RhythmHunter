using System;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    public static class FightNormalGoblinValidation
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
        private static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Flags).GetValue(o);
        private static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

        [MenuItem("Rhythm Hunter/Validate Killer And Giant")]
        public static void RunKillerAndGiant()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
            ValidateBackRowEnemyTurns();
            foreach (string kind in new[] { "Killer", "Giant" })
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/FightScene3.unity");
                try
                {
                    var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                    Set(fight.RosterManager, "requireBattlePreparation", false);
                    fight.RosterManager.SpawnConfiguredRoster();
                    Call(fight, "RebuildRosterAndResetCombat");
                    var enemy = fight.ActiveEnemySlot;
                    enemy.SpawnCharacter(AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>($"Assets/FightDemo/Prefabs/ArtBattle/Goblin_{kind}.prefab"), fight);
                    var sequence = enemy.CombatAnimator.FindSequence(FightCharacterCombatAnimator.CombatAnimation.NormalAttack);
                    bool killer = kind == "Killer";
                    Check(sequence.Frames.All(f => f != null), kind + " missing sprite");
                    Check(sequence.Frames.Count == (killer ? 10 : 5), kind + " frame count");
                    Check(sequence.WarningFrame == (killer ? 1 : 3), kind + " warning");
                    Check(Mathf.Abs(Enumerable.Range(0, sequence.Frames.Count).Sum(sequence.GetFrameBeats) - 5) < .0001f, kind + " duration");
                    if (killer)
                        Check(Mathf.Abs(sequence.GetFrameBeats(9) - .34f) < .0001f && sequence.PlaysAttackEffectAt(6) && sequence.PlaysAttackEffectAt(9), "Killer frame events");
                    else Check(sequence.Frames[2] == sequence.Frames[3] && sequence.PlaysAttackEffectAt(4), "Giant repeated frame");
                    Check((bool)Call(fight, "TryStartAuthoredEnemyAttack", enemy), kind + " failed to start");
                    Check(fight.ScheduledEnemyHitCount == (killer ? 2 : 1), kind + " hit count");
                    float before = fight.PartyHp + fight.PartyArmor;
                    Set(fight, "beatClock", null); // deterministic synthetic beat timestamps below
                    Call(fight, "BeginEnemyPause");
                    Call(fight, "ResolveEnemyCombatWork", 99999);
                    Check(fight.PartyHp + fight.PartyArmor == before, "Paused action dealt damage");
                    Call(fight, "EndEnemyPause", true);
                    Set(fight, "<NormalBattleBeatCount>k__BackingField", 4L);
                    Call(fight, "AdvanceEnemyCombatWork", new FmodBeatClock.BeatSnapshot(4, 2, 1, 2000, 120, 4, 4));
                    Call(fight, "ResolveEnemyCombatWork", 2200);
                    Check(Mathf.Abs(fight.PartyHp + fight.PartyArmor - (before - (killer ? 1 : 2))) < .001f, kind + " damage");
                    if (killer)
                    {
                        Check(fight.ScheduledEnemyHitCount == 1 && fight.TryInterruptEnemyAttack(enemy), "Killer interruption");
                        Check(fight.ScheduledEnemyHitCount == 0, "Remaining slash survived interruption");
                        float afterInterrupt = fight.PartyHp + fight.PartyArmor;
                        Check((bool)Call(fight, "TryStartAuthoredEnemyAttack", enemy), "Killer second action");
                        Set(fight, "<NormalBattleBeatCount>k__BackingField", 9L);
                        Call(fight, "AdvanceEnemyCombatWork", new FmodBeatClock.BeatSnapshot(9, 3, 2, 4500, 120, 4, 4));
                        Call(fight, "ResolveEnemyCombatWork", 5000);
                        Check(fight.ScheduledEnemyHitCount == 0 && Mathf.Abs(fight.PartyHp + fight.PartyArmor - (afterInterrupt - 2)) < .001f, "Killer full combo must hit twice");
                        Call(fight, "ResolveEnemyCombatWork", 5500);
                        Check(Mathf.Abs(fight.PartyHp + fight.PartyArmor - (afterInterrupt - 2)) < .001f, "Duplicate combo damage");
                    }
                    else Check(!fight.TryInterruptEnemyAttack(enemy), "Recovery counted as successful interruption");
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            Debug.Log("KILLER_GIANT_VALIDATION_PASS");
        }

        private static void ValidateBackRowEnemyTurns()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/FightScene3.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                var roster = fight.RosterManager;
                Set(roster, "requireBattlePreparation", false);
                Set(roster, "enemyPrefabs", new[] { "Giant", "Killer", "Normal" }.Select(kind =>
                    AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>($"Assets/FightDemo/Prefabs/ArtBattle/Goblin_{kind}.prefab")).ToArray());
                roster.SpawnConfiguredRoster();
                Call(fight, "RebuildRosterAndResetCombat");
                var killer = roster.ActiveEnemies.Single(e => e.CharacterDefinition.CharacterId == "Goblin_Killer");
                var giant = roster.ActiveEnemies.Single(e => e.CharacterDefinition.CharacterId == "Goblin_Giant");
                Check(fight.ActiveEnemySlot.CharacterDefinition.CharacterId == "Goblin_Normal", "Normal must remain in front");
                void Beat(int n) => Call(fight, "OnBeat", new FmodBeatClock.BeatSnapshot(n, 1 + n / 4, 1 + n % 4, n * 500, 120, 4, 4));
                for (int n = 0; n < 15; n++) Beat(n);
                Check(fight.ScheduledEnemyHitCount == 0, "Rear enemies attacked before interval");
                Beat(15);
                Check(fight.HasPendingEnemyAttack, "Front normal attack must coexist with rear attacks");
                Check(fight.ScheduledEnemyHitCount == 3 && killer.CombatAnimator.IsPlaying && giant.CombatAnimator.IsPlaying,
                    "Rear Killer and Giant must both animate and schedule their hits");
                Beat(15);
                Check(fight.ScheduledEnemyHitCount == 3, "Duplicate beat scheduled duplicate attacks");
                Check(fight.TryInterruptEnemyAttack(killer) && fight.ScheduledEnemyHitCount == 1, "Interrupt must only cancel Killer's two hits");
                Check(giant.CombatAnimator.IsPlaying, "Interrupting Killer stopped Giant");
                Call(fight, "CancelEnemyCombatWork");
                Check(fight.ScheduledEnemyHitCount == 0 && !giant.CombatAnimator.IsPlaying, "Wave cleanup left rear attack active");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [MenuItem("Rhythm Hunter/Validate Normal Goblin")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/FightScene3.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                var roster = fight.RosterManager;
                Set(roster, "requireBattlePreparation", false);
                roster.SpawnConfiguredRoster();
                Call(fight, "RebuildRosterAndResetCombat");
                var enemy = fight.ActiveEnemySlot;
                Check(enemy.CharacterDefinition.CharacterId == "Goblin_Normal", "Scene must spawn new normal goblin.");
                Check(enemy.MaxHp == 10 && enemy.AttackPower == .5f && enemy.AttackIntervalBeats == 16, "Goblin stats.");
                var animator = enemy.CombatAnimator;
                var sequence = Get<System.Collections.Generic.List<FightCharacterCombatAnimator.Sequence>>(animator, "sequences").Single();
                Check(sequence.WarningFrame == 1 && sequence.AttackEffectFrame == 2 && sequence.Frames.Count == 3, "Attack frame mapping.");
                Check(Enumerable.Range(0, 3).All(i => sequence.GetFrameBeats(i) == 1), "One beat per attack frame.");
                Check(Mathf.Abs(Get<System.Collections.Generic.List<float>>(animator, "idleFrameBeatDurations").Sum() - 1) < .0001f, "Idle loop is one beat.");
                void Beat(int n) => Call(fight, "OnBeat", new FmodBeatClock.BeatSnapshot(n, 1+n/4, 1+n%4, n*500, 120, 4, 4));
                void Resolve() => Call(fight, "ResolvePendingEnemyAttack", Get<int>(fight, "pendingAttackRosterVersion"), Get<long>(fight, "pendingEnemyActionId"), enemy);
                for (int n = 0; n < 15; n++) Beat(n);
                Check(!fight.HasPendingEnemyAttack, "No attack before interval.");
                float resources = fight.PartyHp + fight.PartyArmor;
                Beat(15); Resolve();
                Check(fight.HasPendingEnemyAttack && fight.PartyHp + fight.PartyArmor == resources, "Attack 1 cannot hit.");
                Beat(16); Resolve();
                Check(fight.PartyHp + fight.PartyArmor == resources, "Warning beat cannot hit.");
                Call(fight, "BeginEnemyPause");
                for (int n = 17; n <= 22; n++) Beat(n);
                Resolve();
                Check(fight.PartyHp + fight.PartyArmor == resources, "Team pause cannot consume windup.");
                Call(fight, "EndEnemyPause", true);
                Beat(23); Resolve();
                Check(fight.PartyHp + fight.PartyArmor == resources - .5f && !fight.HasPendingEnemyAttack, "Third ordinary beat hits exactly half a heart.");
                Call(fight, "QueueEnemyAttack", new FmodBeatClock.BeatSnapshot(24, 7, 1, 12000, 120, 4, 4));
                Check(fight.TryInterruptEnemyAttack(enemy) && !fight.HasPendingEnemyAttack, "Windup can be interrupted.");
                Debug.Log("NORMAL_GOBLIN_VALIDATION_PASS");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
