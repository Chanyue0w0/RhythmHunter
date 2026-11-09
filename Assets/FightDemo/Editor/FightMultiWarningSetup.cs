using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightMultiWarningSetup
    {
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        static FightMultiWarningSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string path = "Temp/MultiWarningSetup.request";
            if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(path); } catch (IOException) { return; }
            try
            {
                foreach (string kind in new[] { "Normal", "Killer", "Giant" }) Migrate(kind);
                Validate();
                File.WriteAllText("Temp/MultiWarningSetup.result", "PASS: migrated existing goblin warnings without changing frame timings; multiple warning callbacks/VFX/sound entries; skipped frames; interruption and replay.");
            }
            catch (Exception e) { File.WriteAllText("Temp/MultiWarningSetup.result", "FAIL: " + e); Debug.LogException(e); }
        }
        static void Migrate(string kind)
        {
            string path = $"Assets/FightDemo/Prefabs/ArtBattle/Goblin_{kind}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var animator = root.GetComponentInChildren<FightCharacterCombatAnimator>(true);
                var sequences = (List<FightCharacterCombatAnimator.Sequence>)typeof(FightCharacterCombatAnimator).GetField("sequences", Flags).GetValue(animator);
                bool changed = false;
                foreach (var sequence in sequences)
                {
                    var rows = (List<FightCharacterCombatAnimator.FrameEffects>)sequence.GetType().GetField("frameEffects", Flags).GetValue(sequence);
                    // Recover missing shared settings from the original visual prefab without
                    // changing any warning checkboxes the artist has since selected.
                    if ((sequence.WarningEffect == null || sequence.WarningEffect.prefab == null) &&
                        Enumerable.Range(0, sequence.Frames.Count).Any(sequence.PlaysWarningAt))
                    {
                        var original = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/FightDemo/Prefabs/ArtBattle/VisualPrefab/Goblin{kind}_Visual.prefab");
                        var originalSequence = original.GetComponentInChildren<FightCharacterCombatAnimator>(true).FindSequence(sequence.Animation);
                        if (originalSequence != null)
                            for (int frame = 0; frame < originalSequence.Frames.Count; frame++)
                            {
                                var effects = originalSequence.EffectsAt(frame);
                                if (effects == null) continue;
                                var template = effects.FirstOrDefault(e => e?.prefab != null && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(e.prefab)) == "2e1c2f459c628474c9a7928b57b34a9c");
                                if (template == null) continue;
                                sequence.GetType().GetField("warningEffect", Flags).SetValue(sequence, template);
                                changed = true;
                                break;
                            }
                    }
                    foreach (var row in rows)
                    {
                        if (row?.effects == null) continue;
                        for (int i = row.effects.Count - 1; i >= 0; i--)
                        {
                            var effect = row.effects[i];
                            if (effect?.prefab == null || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(effect.prefab)) != "2e1c2f459c628474c9a7928b57b34a9c") continue;
                            if (sequence.WarningEffect == null || sequence.WarningEffect.prefab == null)
                                sequence.GetType().GetField("warningEffect", Flags).SetValue(sequence, effect);
                            row.playWarning = true;
                            row.effects.RemoveAt(i);
                            changed = true;
                        }
                    }
                }
                if (changed)
                {
                    EditorUtility.SetDirty(animator);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void Validate()
        {
            var root = new GameObject("Multi Warning Validation") { hideFlags = HideFlags.HideAndDontSave };
            var dummy = new GameObject("Warning Dummy") { hideFlags = HideFlags.HideAndDontSave };
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>();
                var animator = root.AddComponent<FightCharacterCombatAnimator>();
                animator.ConfigureIdle(null, renderer, new[] { sprite }, 1, false, 0);
                var sequence = new FightCharacterCombatAnimator.Sequence();
                sequence.Configure(FightCharacterCombatAnimator.CombatAnimation.NormalAttack, new[] { sprite, sprite, sprite, sprite }, 16, 0, -1, -1);
                sequence.ConfigureFrameBeats(new[] { .5f, .5f, .5f, .5f });
                sequence.GetType().GetField("frameEffects", Flags).SetValue(sequence, new List<FightCharacterCombatAnimator.FrameEffects> {
                    new() { playWarning = true }, new(), new() { playWarning = true }, new() });
                sequence.GetType().GetField("warningEffect", Flags).SetValue(sequence, new FightCharacterCombatAnimator.FrameEffect {
                    prefab = dummy, soundEventPath = "event:/ZooGoblinFight/SoundEffects/Warning", soundVolume = 1 });
                animator.Configure(renderer, new[] { sequence });
                int warnings = 0, effects = 0;
                void Start() => animator.Play(sequence.Animation, onWarning: () => warnings++, onFrameEffect: effect => {
                    Check(effect.prefab == dummy && effect.soundEventPath.EndsWith("/Warning"), "Warning VFX/sound configuration lost"); effects++; });
                void Advance(float seconds) => typeof(FightCharacterCombatAnimator).GetMethod("AdvancePlayback", Flags).Invoke(animator, new object[] { seconds });
                Start();
                Check(warnings == 1 && effects == 1, "Legacy plus per-frame checkbox doubled the first warning");
                Advance(2);
                Check(warnings == 2 && effects == 2, "Skipped frames lost the second warning");
                Start(); animator.InterruptPlayback(); Advance(2);
                Check(warnings == 3 && effects == 3, "Interrupted action emitted its later warning");
                Start(); Advance(2);
                Check(warnings == 5 && effects == 5, "New playback failed to reset warning events");
                var killer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Killer.prefab");
                var migrated = killer.GetComponentInChildren<FightCharacterCombatAnimator>(true).FindSequence(sequence.Animation);
                Check(migrated.WarningEffect?.prefab != null && !string.IsNullOrEmpty(migrated.WarningEffect.soundEventPath), "Killer warning configuration missing");
                var savedInstance = UnityEngine.Object.Instantiate(killer);
                try
                {
                    var savedAnimator = savedInstance.GetComponentInChildren<FightCharacterCombatAnimator>(true);
                    var savedSequence = savedAnimator.FindSequence(sequence.Animation);
                    int expected = Enumerable.Range(0, savedSequence.Frames.Count).Count(savedSequence.PlaysWarningAt);
                    int actual = 0;
                    savedAnimator.Play(sequence.Animation, onFrameEffect: effect => {
                        if (effect != savedSequence.WarningEffect) return;
                        Check(effect.prefab != null && effect.soundVolume > 0 && effect.soundEventPath == "event:/ZooGoblinFight/SoundEffects/Warning", "Saved Killer warning lost VFX/audio");
                        actual++;
                    });
                    typeof(FightCharacterCombatAnimator).GetMethod("AdvancePlayback", Flags).Invoke(savedAnimator, new object[] { 100f });
                    Check(expected > 0 && actual == expected, "Saved Killer prefab did not dispatch every checked warning");
                    Debug.Log($"KILLER_WARNING_SAVED_PREFAB: expected={expected}, VFX+audio dispatches={actual}");
                }
                finally { UnityEngine.Object.DestroyImmediate(savedInstance); }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(dummy); UnityEngine.Object.DestroyImmediate(sprite); }
        }
    }
}
