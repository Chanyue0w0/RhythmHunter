using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Animation = RhythmHunter.FightDemo.FightCharacterCombatAnimator.CombatAnimation;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightGoblinKingSetup
    {
        const string Art = "Assets/FightDemo/Arts/敵人/Goblin_King/";
        const string Prefabs = "Assets/FightDemo/Prefabs/ArtBattle/";
        const string King = Prefabs + "Goblin_King.prefab";
        const string Visual = Prefabs + "VisualPrefab/GoblinKing_Visual.prefab";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static FightGoblinKingSetup() => EditorApplication.update += Poll;
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
        static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Poll()
        {
            const string request = "Temp/GoblinKingSetup.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { File.Delete(request); } catch (IOException) { return; }
            try { Create(); Validate(); File.WriteAllText("Temp/GoblinKingSetup.result", "PASS: King independent Prefabs, Idle 1 beat, Strike 2 hits at beats 3/4, HeavyStrike 1 hit at beat 4; warnings, damage 1/2, random pool and cadence verified. No scene/Stage edits."); }
            catch (Exception e) { File.WriteAllText("Temp/GoblinKingSetup.result", "FAIL: " + e); Debug.LogException(e); }
        }
        static Sprite[] Sprites(string folder, string prefix, int count)
        {
            return Enumerable.Range(1, count).Select(i =>
            {
                string path = Art + folder + "/" + prefix + "_" + i + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Check(importer != null, "Missing sprite: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }).ToArray();
        }
        static FightCharacterCombatAnimator.Sequence Sequence(Animation type, Sprite[] frames, float[] beats, int[] warnings, int[] hits)
        {
            var result = new FightCharacterCombatAnimator.Sequence();
            result.Configure(type, frames, 16, -1, -1, -1);
            result.ConfigureFrameBeats(beats);
            Set(result, "frameEffects", Enumerable.Range(0, frames.Length).Select(i => new FightCharacterCombatAnimator.FrameEffects
            { playWarning = warnings.Contains(i), playAttackEffect = hits.Contains(i) }).ToList());
            if (warnings.Length > 0)
                Set(result, "warningEffect", new FightCharacterCombatAnimator.FrameEffect
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/VFX/WarningEffectPixel.prefab"),
                    soundEventPath = "event:/ZooGoblinFight/SoundEffects/Warning", followAnchor = true,
                    limitLifetime = true, lifetimeSeconds = .5f
                });
            return result;
        }
        [MenuItem("Rhythm Hunter/Characters/Create Goblin King Phase 1 Prefab")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(King) != null) return; // Never overwrite authored tuning.
            var idle = Sprites("idle", "idle", 3);
            var strike = Sprites("Strike", "Strike", 10);
            var heavy = Sprites("Heavy Strike", "Heavy Strike", 6);
            var hit = Sprites("attacked", "attacked", 2);
            Check(AssetDatabase.CopyAsset(Prefabs + "VisualPrefab/GoblinGiant_Visual.prefab", Visual), "Could not copy visual");
            var visual = PrefabUtility.LoadPrefabContents(Visual);
            float visibleHeight = 0;
            try
            {
                visual.name = "GoblinKing_Visual";
                var animator = visual.GetComponentInChildren<FightCharacterCombatAnimator>(true);
                var renderer = animator.TargetRenderer;
                animator.ConfigureIdle(null, renderer, idle);
                Set(animator, "useIdleFrameBeatDurations", true);
                Set(animator, "idleFrameBeatDurations", new List<float> { .33f, .33f, .34f });
                animator.Configure(renderer, new[]
                {
                    Sequence(Animation.NormalAttack, strike, new[] { 1f, .5f, .5f, .5f, .5f, .33f, .33f, .34f, .5f, .5f }, new[] { 3, 6 }, new[] { 5, 8 }),
                    Sequence(Animation.HeavyAttack, heavy, Enumerable.Repeat(1f, 6).ToArray(), new[] { 3 }, new[] { 4 }),
                    Sequence(Animation.Hit, hit, new[] { .5f, .5f }, Array.Empty<int>(), Array.Empty<int>())
                });
                animator.EnsureAttachmentFrameIds();
                renderer.sprite = idle[0];
                // Keep imported PPU/pivot and the Giant's scale; align visible feet without cropping art.
                var texture = new Texture2D(2, 2);
                try
                {
                    ImageConversion.LoadImage(texture, File.ReadAllBytes(AssetDatabase.GetAssetPath(idle[0])));
                    var pixels = texture.GetPixels32(); int bottom = texture.height, top = 0;
                    for (int y = 0; y < texture.height; y++)
                        for (int x = 0; x < texture.width; x++)
                            if (pixels[y * texture.width + x].a > 25) { bottom = Mathf.Min(bottom, y); top = y; break; }
                    visibleHeight = (top - bottom + 1) / idle[0].pixelsPerUnit * renderer.transform.localScale.y;
                    var p = renderer.transform.localPosition;
                    p.y = -(bottom - idle[0].pivot.y) / idle[0].pixelsPerUnit * renderer.transform.localScale.y;
                    renderer.transform.localPosition = p;
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                PrefabUtility.SaveAsPrefabAsset(visual, Visual);
            }
            finally { PrefabUtility.UnloadPrefabContents(visual); }
            Check(AssetDatabase.CopyAsset(Prefabs + "Goblin_Giant.prefab", King), "Could not copy King");
            var root = PrefabUtility.LoadPrefabContents(King);
            try
            {
                root.name = "Goblin_King";
                var oldAnimator = root.GetComponentInChildren<FightCharacterCombatAnimator>(true);
                var oldVisual = oldAnimator.transform;
                while (oldVisual.parent != root.transform) oldVisual = oldVisual.parent;
                Vector3 position = oldVisual.localPosition, scale = oldVisual.localScale;
                Quaternion rotation = oldVisual.localRotation;
                UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);
                var child = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Visual), root.transform);
                child.transform.localPosition = position; child.transform.localScale = scale; child.transform.localRotation = rotation;
                var definition = root.GetComponent<FightCharacterDefinition>();
                foreach (var anchor in new[] { definition.HeadAnchor, definition.AuthoredCastEffectAnchor, definition.AuthoredImpactEffectAnchor })
                {
                    if (anchor == null) continue;
                    var p = anchor.localPosition; p.x = 0;
                    p.y = position.y + visibleHeight * scale.y * (anchor == definition.HeadAnchor ? 1 : .55f);
                    anchor.localPosition = p;
                }
                Set(definition, "characterId", "Goblin_King"); Set(definition, "displayName", "GOBLIN KING");
                Set(definition, "ignoreFloorOcclusion", true);
                Set(definition, "basicAbilityName", "Strike"); Set(definition, "skillName", "HeavyStrike");
                Set(definition, "attackPower", 1f);
                Set(definition, "enemyAbilities", new List<FightCharacterDefinition.EnemyAbility>
                {
                    new() { name = "Strike", animation = Animation.NormalAttack, damagePerHit = 1 },
                    new() { name = "HeavyStrike", animation = Animation.HeavyAttack, damagePerHit = 2 }
                });
                PrefabUtility.SaveAsPrefabAsset(root, King);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        public static void Validate()
        {
            var definition = AssetDatabase.LoadAssetAtPath<FightCharacterDefinition>(King);
            Check(definition != null && definition.EnemyAbilities.Count == 2 && definition.MaxHp == 40, "King definition missing");
            var animator = definition.GetComponentInChildren<FightCharacterCombatAnimator>(true);
            foreach (var type in new[] { Animation.NormalAttack, Animation.HeavyAttack })
            {
                var sequence = animator.FindSequence(type);
                int expected = type == Animation.NormalAttack ? 2 : 1;
                Check(sequence != null && Enumerable.Range(0, sequence.Frames.Count).Count(sequence.PlaysAttackEffectAt) == expected, "Hit frames incorrect");
                Check(sequence.WarningEffect?.prefab != null && !string.IsNullOrEmpty(sequence.WarningEffect.soundEventPath), "Warning prefab/audio missing");
                Check(sequence.Frames.All(s => s != null && AssetDatabase.GetAssetPath(s).StartsWith(Art)), "Foreign/missing sprites");
            }
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            var random = UnityEngine.Random.state;
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FightCombatController>(true)).Single();
                fight.RosterManager.SpawnConfiguredRoster(); Call(fight, "RebuildRosterAndResetCombat");
                Set(fight, "beatClock", null); Set(fight, "waveEntrance", null);
                Check(fight.TryBeginBattle(), "Cannot start fixture");
                var enemy = fight.RosterManager.ActiveEnemies[0]; enemy.SpawnCharacter(definition, fight);
                UnityEngine.Random.InitState(1729);
                var seen = new HashSet<Animation>();
                for (int i = 0; i < 32; i++)
                {
                    Call(fight, "ClearEnemyScheduledActions"); enemy.CombatAnimator.InterruptPlayback();
                    Check((bool)Call(fight, "TryStartAuthoredEnemyAttack", enemy), "Random attack failed to start");
                    Animation selected = enemy.CombatAnimator.ActiveAnimation; seen.Add(selected);
                    var hits = (IList)fight.GetType().GetField("enemyHits", Flags).GetValue(fight);
                    bool strike = selected == Animation.NormalAttack;
                    Check(hits.Count == (strike ? 2 : 1), "Random action hit count wrong");
                    for (int h = 0; h < hits.Count; h++)
                    {
                        var item = hits[h];
                        Check((float)item.GetType().GetField("damage").GetValue(item) == (strike ? 1 : 2), "Per-ability damage wrong");
                        double due = (double)item.GetType().GetField("due").GetValue(item) - fight.NormalBattleBeatCount;
                        Check(Math.Abs(due - (strike ? 3 + h : 4)) < .001, "Damage not aligned to sprite hit frame");
                    }
                    var cadence = Call(fight, "CadenceFor", enemy);
                    double end = (double)cadence.GetType().GetField("actionEnd").GetValue(cadence);
                    Check(Math.Abs(end - fight.NormalBattleBeatCount - (strike ? 5 : 6)) < .001, "Pressure cooldown ignored selected animation length");
                }
                Check(seen.Count == 2, "Random pool never selected both abilities");
            }
            finally { UnityEngine.Random.state = random; EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
