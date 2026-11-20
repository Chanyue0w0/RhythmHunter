using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemo.Editor
{
    [InitializeOnLoad]
    public static class FightDeathCoinValidation
    {
        static FightDeathCoinValidation() => EditorApplication.update += Poll;
        static void Poll()
        {
            // A separate sync request can wait safely while Unity still runs an older assembly.
            string request = File.Exists("Temp/DeathCoinSync.request")
                ? "Temp/DeathCoinSync.request" : "Temp/DeathCoinValidation.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string mode = File.ReadAllText(request).Trim();
            if ((mode == "render" || mode == "save") && EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool repair = mode == "repair" || mode == "save";
            File.Delete(request);
            try
            {
                if (mode == "render") RenderDeath();
                else
                {
                    Run(repair);
                    if (mode == "save")
                        foreach (var fight in UnityEngine.Object.FindObjectsByType<FightCombatController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                            if (fight.gameObject.scene.path.EndsWith("Art2_FightScene.unity"))
                                EditorSceneManager.SaveScene(fight.gameObject.scene);
                }
            }
            catch (Exception e) { File.WriteAllText("Temp/DeathCoinValidation.result", "FAIL: " + e); }
        }
        [MenuItem("Rhythm Hunter/Validate Death Coin References")]
        public static void Inspect() => Run(false);
        [MenuItem("Rhythm Hunter/Repair Death Coin References")]
        public static void Repair() => Run(true);
        [MenuItem("Rhythm Hunter/Validate Death Coin Rendering")]
        public static void RenderDeath()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var preview = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            var rt = RenderTexture.GetTemporary(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                var all = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var fight = all.OfType<FightCombatController>().Single();
                var camera = (Camera)typeof(FightCombatController).GetField("damageNumberCamera", flags).GetValue(fight);
                camera.scene = preview;
                camera.targetTexture = rt;
                var slot = all.OfType<FightUnitSlot>().First(s => s.Team == FightUnitSlot.UnitTeam.Enemy);
                slot.SpawnCharacter(AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>("Assets/FightDemo/Prefabs/ArtBattle/Goblin_Normal.prefab"), fight);
                Color32[] Capture(string path)
                {
                    camera.Render(); RenderTexture.active = rt;
                    image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                    File.WriteAllBytes(path, image.EncodeToPNG()); return image.GetPixels32();
                }
                var before = Capture("Temp/DeathCoins-before.png");
                slot.TakeDamage(slot.MaxHp + 1);
                if (slot.CurrentHp > 0) throw new Exception("Test enemy did not die");
                var bursts = fight.GetComponentsInChildren<FightDeathCoinBurst>(true);
                if (bursts.Length != 1) throw new Exception("Death must spawn exactly one burst, got " + bursts.Length);
                var particles = bursts[0].GetComponent<ParticleSystem>();
                particles.Simulate(.2f, true, false);
                var after = Capture("Temp/DeathCoins-after.png");
                particles.GetComponent<ParticleSystemRenderer>().enabled = false;
                var withoutCoins = Capture("Temp/DeathCoins-without.png");
                int changed = 0;
                for (int i = 0; i < before.Length; i++)
                    if (Math.Abs(withoutCoins[i].r - after[i].r) + Math.Abs(withoutCoins[i].g - after[i].g) + Math.Abs(withoutCoins[i].b - after[i].b) > 30) changed++;
                if (changed < 100) throw new Exception("Death coins are not visibly rendered: " + changed + " changed pixels");
                var data = new ParticleSystem.Particle[20]; int count = particles.GetParticles(data);
                File.WriteAllText("Temp/DeathCoinValidation.result", $"Rendered {count} coins; changed pixels={changed}; slot={slot.ActorInstance.transform.position}; emitter={bursts[0].transform.position}; first particle={(count > 0 ? data[0].position.ToString() : "none")}; camera={camera.transform.position}; size={particles.main.startSize.constantMax}");
            }
            finally
            {
                RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(preview);
                RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(image);
            }
        }
        static void Run(bool repair)
        {
            var report = new System.Text.StringBuilder();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/Item/CoinItem.prefab");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/FightDemo/Prefabs/Item/CoinParticles.mat");
            foreach (var fight in UnityEngine.Object.FindObjectsByType<FightCombatController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = new SerializedObject(fight);
                if (repair && fight.gameObject.scene.path.EndsWith("Art2_FightScene.unity"))
                {
                    if (!data.FindProperty("deathCoinItem").objectReferenceValue)
                        data.FindProperty("deathCoinItem").objectReferenceValue = prefab;
                    if (!data.FindProperty("deathCoinMaterial").objectReferenceValue)
                        data.FindProperty("deathCoinMaterial").objectReferenceValue = material;
                    data.FindProperty("deathCoinLifetime").floatValue = 1f;
                    data.FindProperty("deathCoinSize").floatValue = .65f;
                    data.FindProperty("deathCoinOffset").vector3Value = new Vector3(0, .85f, 0);
                    if (data.ApplyModifiedProperties() && !Application.isPlaying)
                        EditorSceneManager.MarkSceneDirty(fight.gameObject.scene);
                }
                report.AppendLine($"{fight.gameObject.scene.path}: {fight.name}, playing={Application.isPlaying}");
                foreach (var name in new[] { "deathCoinItem", "deathCoinMaterial" })
                    report.AppendLine(name + "=" + data.FindProperty(name).objectReferenceValue);
            }
            var sprite = prefab.GetComponentInChildren<SpriteRenderer>(true).sprite;
            report.AppendLine($"Coin sprite={sprite}, texture={sprite?.texture}, size={sprite?.rect}");
            if (!Application.isPlaying)
            {
                var preview = EditorSceneManager.NewPreviewScene();
                var root = new GameObject("Coin validation");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
                root.SetActive(false);
                try
                {
                    var fight = root.AddComponent<FightCombatController>();
                    fight.enabled = false;
                    root.SetActive(true);
                    var burst = root.AddComponent<FightDeathCoinBurst>();
                    typeof(FightDeathCoinBurst).GetMethod("Initialize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(burst, new object[] { fight, sprite, material, 7, 1f, .65f });
                    var particles = root.GetComponent<ParticleSystem>();
                    if (particles.particleCount != 7) throw new Exception("Expected 7 death coin particles, got " + particles.particleCount);
                    var coins = new ParticleSystem.Particle[7];
                    particles.GetParticles(coins);
                    foreach (var coin in coins)
                        if (coin.velocity.y <= 0 || coin.startLifetime > 1.001f) throw new Exception("Invalid coin trajectory/lifetime");
                    particles.Simulate(1.01f, true, false);
                    if (particles.particleCount != 0) throw new Exception("Coins did not expire");
                    report.AppendLine("PASS: 7 upward particles, lifetime <= 1 second, all expire by 1.01 seconds.");
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            File.WriteAllText("Temp/DeathCoinValidation.result", report.ToString());
        }
    }
}
