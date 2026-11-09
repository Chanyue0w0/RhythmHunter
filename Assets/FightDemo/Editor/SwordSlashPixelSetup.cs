using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using RhythmHunter.FightDemo;
using RhythmHunter.FightDemo.Art2;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class SwordSlashPixelSetup
    {
        static SwordSlashPixelSetup() => EditorApplication.update += Requested;
        static void Requested()
        {
            const string request = "Temp/SwordSlashPixelSetup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(request); } catch (IOException) { return; }
            try { Run(); } catch (Exception e) { File.WriteAllText("Temp/SwordSlashPixelSetup.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Art2/Configure Sword Slash Pixel")]
        public static void Run()
        {
            const string path = "Assets/FightDemo/Prefabs/VFX/SwordSlash_Pixel.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var adapter = root.GetComponent<Art2EffectPresentation>() ?? root.AddComponent<Art2EffectPresentation>();
                var data = new SerializedObject(adapter);
                data.FindProperty("keepAboveGround").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.loop = false;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    main.stopAction = ParticleSystemStopAction.None;
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    if (renderer && renderer.renderMode == ParticleSystemRenderMode.Billboard)
                        renderer.alignment = ParticleSystemRenderSpace.View;
                }
                var settings = root.GetComponent<FightEffectSettings>() ?? root.AddComponent<FightEffectSettings>();
                settings.destroyAfterParticles = true;
                settings.onHitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/VFX/SwordHit.prefab");
                if (!settings.onHitPrefab) throw new Exception("Missing SwordHit prefab");
                settings.onHitLifetime = 1.5f;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                if (root.GetComponentsInChildren<ParticleSystem>(true).Any(p => p.main.loop)) throw new Exception("Loop remains");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Validate(path);
            File.WriteAllText("Temp/SwordSlashPixelSetup.result", "PASS: SwordSlash_Pixel view alignment, ground clearance, one-shot particles, battle cleanup and SwordHit OnHit configured; simulated particles finish and shared cleanup removes root.");
        }
        static void Validate(string path)
        {
            var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                instance.transform.SetPositionAndRotation(new Vector3(0, .4f, 0), Quaternion.Euler(45, 0, 0));
                var adapter = instance.GetComponent<Art2EffectPresentation>();
                var update = typeof(Art2EffectPresentation).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                update.Invoke(adapter, null);
                var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                foreach (float time in new[] { .1f, .3f, .6f })
                {
                    foreach (var ps in particles) ps.Simulate(time, false, true, true);
                    update.Invoke(adapter, null);
                    foreach (var ps in particles.Where(p => p.particleCount > 0))
                    {
                        var renderer = ps.GetComponent<ParticleSystemRenderer>();
                        if (renderer.bounds.min.y < .029f) throw new Exception("Slash intersects ground");
                        if (renderer.renderMode == ParticleSystemRenderMode.Billboard && renderer.alignment != ParticleSystemRenderSpace.View)
                            throw new Exception("Slash is not camera-facing");
                    }
                }
                foreach (var ps in particles) ps.Simulate(0, false, true, true);
                for (int step = 0; step < 100; step++)
                    foreach (var ps in particles) ps.Simulate(.1f, false, false, true);
                // Simulate leaves the emitter paused even at duration; mirror the
                // non-looping emitter's runtime stop without clearing live particles.
                foreach (var ps in particles)
                {
                    if (ps.time < ps.main.duration || ps.particleCount != 0)
                        throw new Exception("Particles have not completed their authored duration");
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }
                if (particles.Any(p => p.IsAlive(false))) throw new Exception("Slash particles failed to finish: " +
                    string.Join(", ", particles.Select(p => p.name + " time=" + p.time + " count=" + p.particleCount + " alive=" + p.IsAlive(false))));
                var owner = instance.AddComponent<FightBattleEffect>();
                owner.Configure(null, false, 1.5f);
                typeof(FightBattleEffect).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
                if (instance) throw new Exception("Completed effect root was not removed");
            }
            finally { if (instance) UnityEngine.Object.DestroyImmediate(instance); }
        }
    }
}
