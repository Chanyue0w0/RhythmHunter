using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using RhythmHunter.FightDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightInitialSpriteValidation
    {
        static FightInitialSpriteValidation() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string path = "Temp/InitialSpriteValidation.request";
            if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(path); } catch (IOException) { return; }
            try { Run(); } catch (Exception e) { File.WriteAllText("Temp/InitialSpriteValidation.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Validate/Initial Actor Sprite Binding")]
        public static void Run()
        {
            var report = new System.Text.StringBuilder();
            foreach (var name in new[] { "LongswordWarrior", "Mage", "Skeleton", "Goblin_Normal", "Goblin_Killer", "Goblin_Giant" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/ArtBattle/" + name + ".prefab");
                var instance = UnityEngine.Object.Instantiate(prefab);
                instance.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    var animator = instance.GetComponentInChildren<FightCharacterCombatAnimator>(true);
                    var renderer = animator.TargetRenderer;
                    var material = renderer.sharedMaterial;
                    var scale = renderer.transform.lossyScale;
                    var properties = new MaterialPropertyBlock();
                    properties.SetFloat("_ValidationSentinel", .42f);
                    renderer.SetPropertyBlock(properties);
                    foreach (var callback in new[] { "Awake", "OnEnable", "LateUpdate" })
                    {
                        typeof(FightCharacterCombatAnimator).GetMethod(callback, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(animator, null);
                        renderer.GetPropertyBlock(properties);
                        if (!renderer.sprite || properties.GetTexture("_MainTex") != renderer.sprite.texture ||
                            Mathf.Abs(properties.GetFloat("_ValidationSentinel") - .42f) > .001f ||
                            renderer.sharedMaterial != material || renderer.transform.lossyScale != scale)
                            throw new Exception(name + " initial binding, material, or scale failed at " + callback);
                    }
                    report.AppendLine(name + ": " + AssetDatabase.GetAssetPath(renderer.sprite));
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            File.WriteAllText("Temp/InitialSpriteValidation.result", "PASS: six actors initialize their own sprite texture; shared materials, scale and existing properties preserved.\n" + report);
        }
    }
}
