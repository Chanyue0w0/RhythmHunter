using System;
using System.IO;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class CharacterAnimationPreviewValidation
    {
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        static object Get(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
        static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
        static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
        static CharacterAnimationPreviewValidation() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/CharacterAnimationPreviewValidation.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(request);
            try { Run(); }
            catch (Exception e) { File.WriteAllText("Temp/CharacterAnimationPreviewValidation.result", "FAIL: " + e); }
        }
        [MenuItem("Rhythm Hunter/Animation/Validate Character Preview Editing")]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            const string original = "Assets/FightDemo/Prefabs/ArtBattle/LongswordWarrior.prefab";
            string copy = "Assets/__CharacterPreviewValidation_" + Guid.NewGuid().ToString("N") + ".prefab";
            var originalBytes = File.ReadAllBytes(original);
            WeaponAttachmentEditorWindow window = null;
            void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
            try
            {
                Check(AssetDatabase.CopyAsset(original, copy), "Failed to create isolated fixture");
                window = ScriptableObject.CreateInstance<WeaponAttachmentEditorWindow>();
                Set(window, "characterEditing", true);
                Call(window, "Load", AssetDatabase.LoadAssetAtPath<GameObject>(copy));
                var animator = (FightCharacterCombatAnimator)Get(window, "animator");
                Check(animator != null && Get(window, "animationInspector") is FightCharacterCombatAnimatorEditor, "Existing Inspector not reused");
                var serialized = new SerializedObject(animator);
                var sequences = serialized.FindProperty("sequences");
                SerializedProperty normal = null;
                for (int i = 0; i < sequences.arraySize; i++)
                    if (sequences.GetArrayElementAtIndex(i).FindPropertyRelative("animation").intValue == 0)
                        normal = sequences.GetArrayElementAtIndex(i);
                Check(normal != null, "Fixture missing normal animation");
                normal.FindPropertyRelative("useFrameBeatDurations").boolValue = true;
                normal.FindPropertyRelative("frameBeatDurations").GetArrayElementAtIndex(0).floatValue = .75f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Call(window, "RefreshSources");
                var sources = (System.Collections.Generic.List<int>)Get(window, "sources");
                Set(window, "sourceChoice", sources.IndexOf(0)); Set(window, "frame", 999);
                Call(window, "RefreshSources");
                Check((int)Get(window, "frame") < animator.FindSequence(FightCharacterCombatAnimator.CombatAnimation.NormalAttack).Frames.Count, "Frame cursor not clamped after editing");
                int warnings = animator.WarningEventCount, attacks = animator.AttackEffectEventCount, damage = animator.DamageEventCount;
                Set(window, "playing", true); Set(window, "lastTime", EditorApplication.timeSinceStartup - 2);
                Call(window, "Tick");
                Check(warnings == animator.WarningEventCount && attacks == animator.AttackEffectEventCount && damage == animator.DamageEventCount, "Preview emitted gameplay events");
                Set(window, "playing", false);
                Check((bool)Call(window, "SaveCharacter"), "Save failed");
                Call(window, "Load", AssetDatabase.LoadAssetAtPath<GameObject>(copy));
                animator = (FightCharacterCombatAnimator)Get(window, "animator");
                Check(Mathf.Approximately(animator.FindSequence(FightCharacterCombatAnimator.CombatAnimation.NormalAttack).GetFrameBeats(0), .75f), "Edited duration not saved/reloaded");
                Check(animator.GetComponentInParent<FightWeaponAttachment>() == null, "Character editor added weapon binding");
                Check(Convert.ToBase64String(originalBytes) == Convert.ToBase64String(File.ReadAllBytes(original)), "Original prefab changed");
                File.WriteAllText("Temp/CharacterAnimationPreviewValidation.result", "PASS: existing Inspector reused; edited 0.75-beat duration saved/reloaded; cursor clamps to edited frame count; preview emits no gameplay events; no weapon binding added; original prefab unchanged.");
            }
            finally
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
                AssetDatabase.DeleteAsset(copy);
            }
            if (File.Exists("Temp/OpenCharacterAnimationPreview.request"))
            {
                File.Delete("Temp/OpenCharacterAnimationPreview.request");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(original);
                WeaponAttachmentEditorWindow.OpenCharacter(prefab.GetComponentInChildren<FightCharacterCombatAnimator>(true));
            }
        }
    }
}
