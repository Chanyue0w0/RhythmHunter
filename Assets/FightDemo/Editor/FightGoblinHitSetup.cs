using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightGoblinHitSetup
    {
        static FightGoblinHitSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/GoblinHitSetup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Configure(); }
            catch (Exception e) { File.WriteAllText("Temp/GoblinHitSetup.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Characters/Configure Goblin GetHurt Animations")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play first.");
            foreach (var kind in new[] { "Normal", "Killer", "Giant" })
            {
                string path = "Assets/FightDemo/Prefabs/ArtBattle/VisualPrefab/Goblin" + kind + "_Visual.prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var animator = root.GetComponentInChildren<FightCharacterCombatAnimator>(true);
                    if (animator == null) throw new Exception(kind + " animator missing");
                    string folder = "Assets/FightDemo/Arts/敵人/" + (kind == "Normal" ? "goblin_normal" : "Goblin_" + kind) + "/attacked/";
                    var frames = new[] { 1, 2 }.Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(folder + "attacked_" + i + ".png")).ToArray();
                    if (frames.Any(f => f == null)) throw new Exception(kind + " attacked sprite missing");
                    var hit = new FightCharacterCombatAnimator.Sequence();
                    hit.Configure(FightCharacterCombatAnimator.CombatAnimation.Hit, frames, 16, -1, -1, -1);
                    hit.ConfigureFrameBeats(new[] { .5f, .5f });
                    var sequences = animator.Sequences.Where(s => s.Animation != FightCharacterCombatAnimator.CombatAnimation.Hit).ToList();
                    sequences.Add(hit);
                    animator.Configure(animator.TargetRenderer, sequences);
                    animator.EnsureAttachmentFrameIds();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    // Playing GetHurt must not emit attack/warning/damage frame events.
                    if (!animator.Play(FightCharacterCombatAnimator.CombatAnimation.Hit) || animator.ActiveFrame != 0 ||
                        hit.GetFrameBeats(0) != .5f || hit.GetFrameBeats(1) != .5f || hit.Frames.Count != 2)
                        throw new Exception(kind + " GetHurt configuration invalid");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            ValidateDamageReactions();
            File.WriteAllText("Temp/GoblinHitSetup.result", "PASS: Normal/Killer/Giant two-frame GetHurt at 0.5 beats each; actual damage triggers only during rest; zero damage and DoT excluded; attacks and callbacks preserved; second frame, half-beat fast ascent/apex shake/return verified.");
        }

        static void ValidateDamageReactions()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            void Set(object o, string name, object value) => o.GetType().GetField(name, flags).SetValue(o, value);
            object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, flags).Invoke(o, args);
            void Check(bool value, string message) { if (!value) throw new Exception(message); }
            foreach (var kind in new[] { "Normal", "Killer", "Giant" })
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                try
                {
                    var fight = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FightCombatController>(true)).Single();
                    fight.RosterManager.SpawnConfiguredRoster();
                    Call(fight, "RebuildRosterAndResetCombat");
                    Set(fight, "beatClock", null); Set(fight, "waveEntrance", null);
                    Check(fight.TryBeginBattle(), "Could not begin validation battle");
                    var enemy = fight.RosterManager.ActiveEnemies[0];
                    enemy.SpawnCharacter(AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_" + kind + ".prefab"), fight);
                    Set(enemy, "maxHp", 100f); enemy.RestoreFullHealth();
                    var animator = enemy.CombatAnimator;
                    animator.InterruptPlayback();
                    enemy.TakeDamage(1);
                    Check(!animator.IsPlaying, kind + " reacted outside rest");
                    Set(fight, "pressureRestUntil", fight.WaveCombatBeatCount + 20);
                    Check(fight.PressureRestActive, "Rest fixture inactive");
                    enemy.TakeDamage(0);
                    enemy.TakeDamage(.5f, FightUnitSlot.DamageKind.DamageOverTime);
                    Check(!animator.IsPlaying, kind + " reacted to zero damage or DoT");
                    enemy.TakeDamage(1);
                    Check(animator.IsPlaying && animator.ActiveAnimation == FightCharacterCombatAnimator.CombatAnimation.Hit, kind + " missing rest hit reaction");
                    Vector3 origin = enemy.ActorInstance.transform.position;
                    float seconds = (float)animator.GetSequenceDurationSeconds(FightCharacterCombatAnimator.CombatAnimation.Hit);
                    Call(animator, "AdvancePlayback", seconds * .1f);
                    Call(enemy, "UpdateRestHitHop");
                    Check(enemy.ActorInstance.transform.position.x < origin.x && enemy.ActorInstance.transform.position.y > origin.y, kind + " missing leftward hop");
                    float apex = enemy.ActorInstance.transform.position.y;
                    Call(animator, "AdvancePlayback", seconds * .03f);
                    Call(enemy, "UpdateRestHitHop");
                    Check(Mathf.Abs(enemy.ActorInstance.transform.position.y - apex) < .0001f && Mathf.Abs(enemy.ActorInstance.transform.position.x - (origin.x - .12f)) > .001f, kind + " apex shake missing");
                    Call(animator, "AdvancePlayback", seconds * .38f);
                    Call(enemy, "UpdateRestHitHop");
                    Check(Vector3.Distance(enemy.ActorInstance.transform.position, origin) < .0001f, kind + " hop exceeded half beat");
                    Check(animator.ActiveFrame == 1, kind + " second hit frame missing");
                    Call(animator, "AdvancePlayback", seconds * .51f);
                    Call(enemy, "UpdateRestHitHop");
                    Check(Vector3.Distance(enemy.ActorInstance.transform.position, origin) < .0001f, kind + " hop did not return to origin");
                    Check(!animator.IsPlaying, kind + " did not return to idle");
                    int callbacks = 0;
                    // Preview scenes have no FMOD runtime; keep authored frame events from playing audio/VFX here.
                    animator.Play(FightCharacterCombatAnimator.CombatAnimation.NormalAttack, onCompleted: () => callbacks++, onFrameEffect: _ => { });
                    int frame = animator.ActiveFrame;
                    enemy.TakeDamage(1);
                    Check(animator.ActiveAnimation == FightCharacterCombatAnimator.CombatAnimation.NormalAttack && animator.ActiveFrame == frame && callbacks == 0,
                        kind + " hurt interrupted attack or flushed callback");
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }
    }
}
