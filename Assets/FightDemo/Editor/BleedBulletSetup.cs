using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using RhythmHunter.FightDemo;
using RhythmHunter.FightDemo.Art2;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class BleedBulletSetup
    {
        static BleedBulletSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/BleedBulletSetup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(request); } catch (IOException) { return; }
            try { Run(); } catch (Exception e) { File.WriteAllText("Temp/BleedBulletSetup.result", e.ToString()); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Art2/Configure Bleed Bullet")]
        public static void Run()
        {
            const string folder = "Assets/FightDemo/Prefabs/VFX/";
            var impact = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "OnHitPixelVFX(Red).prefab");
            if (!impact) throw new Exception("Missing red impact prefab");
            foreach (var name in new[] { "BleedBullet", "OnHitPixelVFX(Red)" })
            {
                var path = folder + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (!root.GetComponent<Art2EffectPresentation>()) root.AddComponent<Art2EffectPresentation>();
                    var settings = root.GetComponent<FightEffectSettings>() ?? root.AddComponent<FightEffectSettings>();
                    settings.flyToTarget = name == "BleedBullet";
                    settings.destroyAfterParticles = !settings.flyToTarget;
                    if (settings.flyToTarget)
                    {
                        settings.flightBeats = .6f; settings.forwardSpawnDistance = .25f;
                        settings.onHitPrefab = impact;
                    }
                    else
                    {
                        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                        { var main = ps.main; main.loop = false; main.stopAction = ParticleSystemStopAction.None; }
                        foreach (var fx in root.GetComponentsInChildren<CartoonFX.CFXR_Effect>(true))
                            fx.clearBehavior = CartoonFX.CFXR_Effect.ClearBehavior.None;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            const string actorPath = "Assets/FightDemo/Prefabs/ArtBattle/Skeleton.prefab";
            var actor = PrefabUtility.LoadPrefabContents(actorPath);
            try
            {
                var data = new SerializedObject(actor.GetComponent<FightCharacterDefinition>());
                data.FindProperty("normalAbilityEffectPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "BleedBullet.prefab");
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(actor, actorPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(actor); }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/BleedBulletSetup.result", "PASS: Skeleton normal projectile BleedBullet; 0.6 beat, forward 0.25; red OnHit with shared particle completion cleanup and Art2 alignment.");
        }
    }
}
