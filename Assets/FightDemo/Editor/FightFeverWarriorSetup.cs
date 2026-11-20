using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using RhythmHunter.FightDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightFeverWarriorSetup
    {
        static FightFeverWarriorSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/FeverWarriorSetup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Configure(); File.WriteAllText("Temp/FeverWarriorSetup.result", "PASS: Multi-Slash authored attack frames wired downL then upR; unrelated animation settings preserved."); }
            catch (Exception e) { File.WriteAllText("Temp/FeverWarriorSetup.result", "FAIL: " + e); }
        }
        [MenuItem("Rhythm Hunter/Fever/Configure Warrior Slash Frames")]
        static void Configure()
        {
            const string path = "Assets/FightDemo/Prefabs/ArtBattle/LongSwordWarrior_Weapon.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var animator = root.GetComponentInChildren<FightCharacterCombatAnimator>(true);
                var sequence = animator.FindSequence("Multi-Slash");
                if (sequence == null) throw new Exception("Missing Multi-Slash animation");
                var marked = Enumerable.Range(0, sequence.Frames.Count).Where(sequence.PlaysAttackEffectAt).ToArray();
                if (marked.Length != 2) throw new Exception("Expected exactly two marked attack-effect frames; got " + marked.Length);
                var prefabs = new[] {
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/TeamSkill/LightMultiSlash_downL.prefab"),
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/TeamSkill/LightMultiSlash_upR.prefab") };
                if (prefabs.Any(p => !p)) throw new Exception("Missing slash prefab");
                var data = new SerializedObject(animator); var sequences = data.FindProperty("sequences");
                int index = animator.Sequences.ToList().IndexOf(sequence);
                var frames = sequences.GetArrayElementAtIndex(index).FindPropertyRelative("frameEffects");
                frames.arraySize = Math.Max(frames.arraySize, sequence.Frames.Count);
                for (int i = 0; i < marked.Length; i++)
                {
                    var effects = frames.GetArrayElementAtIndex(marked[i]).FindPropertyRelative("effects");
                    int effectIndex = -1;
                    for (int j = 0; j < effects.arraySize; j++)
                        if (prefabs.Contains(effects.GetArrayElementAtIndex(j).FindPropertyRelative("prefab").objectReferenceValue)) { effectIndex = j; break; }
                    if (effectIndex < 0) { effectIndex = effects.arraySize; effects.arraySize++; }
                    var effect = effects.GetArrayElementAtIndex(effectIndex);
                    effect.FindPropertyRelative("prefab").objectReferenceValue = prefabs[i];
                    effect.FindPropertyRelative("location").enumValueIndex = (int)FightCharacterCombatAnimator.EffectLocation.AttackTarget;
                    effect.FindPropertyRelative("selfAnchor").objectReferenceValue = null;
                    effect.FindPropertyRelative("positionOffset").vector3Value = Vector3.zero;
                    effect.FindPropertyRelative("rotationOffset").vector3Value = Vector3.zero;
                    effect.FindPropertyRelative("followAnchor").boolValue = false;
                    effect.FindPropertyRelative("limitLifetime").boolValue = true;
                    effect.FindPropertyRelative("lifetimeSeconds").floatValue = .7f;
                    effect.FindPropertyRelative("soundEventPath").stringValue = "";
                }
                data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
