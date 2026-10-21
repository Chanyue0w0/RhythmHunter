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
