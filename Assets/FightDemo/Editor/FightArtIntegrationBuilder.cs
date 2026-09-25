using System;
using System.IO;
using System.Linq;
using System.Text;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightArtIntegrationBuilder
    {
        const string Request = "Temp/FightArtIntegration.request";
        const string Result = "Temp/FightArtIntegration.result";
        const string ArtPath = "Assets/FightDemo/Scenes/FightScene.unity";
        const string BattlePath = "Assets/FightDemo/Scenes/FightScene3.unity";
        const string PrefabFolder = "Assets/FightDemo/Prefabs/ArtBattle";
        static FightArtIntegrationBuilder() => EditorApplication.update += Tick;
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
            try { File.WriteAllText(Result, command == "build" ? Build() : Inspect()); }
            catch (Exception e) { File.WriteAllText(Result, "FAIL: " + e); Debug.LogException(e); }
        }
        static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
        static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static GameObject Clone(GameObject source, Transform parent)
        {
            var copy = UnityEngine.Object.Instantiate(source, parent);
            copy.name = source.name;
            copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            copy.transform.localScale = source.transform.lossyScale;
            return copy;
        }
        static GameObject Save(GameObject root, string name)
        {
            var asset = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            if (asset == null) throw new IOException("Could not save prefab " + name);
            return asset;
        }
        static Material CharacterMaterial()
        {
            string path = PrefabFolder + "/BattleCharacterLit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }
        static GameObject CreateVisual(FightCharacterCombatAnimator original, string name, Material material)
        {
            var root = new GameObject(name);
            var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = original.transform.lossyScale;
            visual.transform.localPosition = new Vector3(0, -original.TargetRenderer.sprite.bounds.min.y * original.transform.lossyScale.y, -.2f);
            var renderer = visual.AddComponent<SpriteRenderer>(); EditorUtility.CopySerialized(original.TargetRenderer, renderer);
            renderer.sharedMaterial = material;
            var animator = visual.AddComponent<FightCharacterCombatAnimator>();
            animator.ConfigureIdle(null, renderer, original.Frames, original.CyclesPerBeat, original.PingPong);
            // The artist supplied idle poses only. Keep all actions in the same art set;
            // these independently editable sequences can receive dedicated action art later.
            var sequences = Enum.GetValues(typeof(FightCharacterCombatAnimator.CombatAnimation))
                .Cast<FightCharacterCombatAnimator.CombatAnimation>().Select(action =>
                {
                    var sequence = new FightCharacterCombatAnimator.Sequence();
                    sequence.Configure(action, original.Frames, 10, -1, 0, 1); return sequence;
                }).ToArray();
            animator.Configure(renderer, sequences);
            return Save(root, name);
        }
        static FightCharacterDefinition CreateCharacter(FightCharacterDefinition data, GameObject visual, string name, bool enemy)
        {
            var root = new GameObject(name);
            var definition = root.AddComponent<FightCharacterDefinition>(); EditorUtility.CopySerialized(data, definition);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(visual, root.transform);
            instance.transform.localPosition = Vector3.zero;
            foreach (string anchorName in new[] { "VFX_CastAnchor", "VFX_ImpactAnchor" })
            {
                var anchor = new GameObject(anchorName); anchor.transform.SetParent(root.transform, false);
                anchor.transform.localPosition = new Vector3(0, 1, -.3f);
                Set(definition, anchorName == "VFX_CastAnchor" ? "castEffectAnchor" : "impactEffectAnchor", anchor.transform);
            }
            if (enemy)
            {
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("characterId").stringValue = name;
                serialized.FindProperty("displayName").stringValue = name == "Goblin_Giant" ? "GIANT GOBLIN" : "KILLER GOBLIN";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return Save(root, name).GetComponent<FightCharacterDefinition>();
        }
        static string Build()
        {
            // Never overwrite an open scene with unsaved user changes.
            Scene openBattle = SceneManager.GetSceneByPath(BattlePath);
            if (openBattle.IsValid() && openBattle.isDirty) throw new InvalidOperationException("FightScene3 has unsaved edits. Save it before integrating art.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/ForestEnvironment.prefab") != null)
                throw new InvalidOperationException("Art integration already exists. Edit its prefabs directly; do not regenerate user authoring.");
            Directory.CreateDirectory(PrefabFolder); AssetDatabase.Refresh();
            Scene previous = SceneManager.GetActiveScene();
            Scene source = EditorSceneManager.OpenPreviewScene(ArtPath);
            Scene battle = openBattle.IsValid() ? openBattle : EditorSceneManager.OpenScene(BattlePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(battle);
            try
            {
                var sourceTransforms = All<Transform>(source);
                var sourceActors = All<FightCharacterCombatAnimator>(source);
                var material = CharacterMaterial();
                var heroVisual = CreateVisual(sourceActors.First(a => a.GetComponentInParent<FightUnitSlot>().name == "HeroSlot_Paladin"), "HeroSwordsman_Visual", material);
                var giantVisual = CreateVisual(sourceActors.First(a => AssetDatabase.GetAssetPath(a.TargetRenderer.sprite).Contains("Goblin_Giant")), "GoblinGiant_Visual", material);
                var killerVisual = CreateVisual(sourceActors.First(a => AssetDatabase.GetAssetPath(a.TargetRenderer.sprite).Contains("Goblin_Killer")), "GoblinKiller_Visual", material);
                var roster = All<FightRosterManager>(battle).Single();
                var heroes = roster.HeroPrefabs.Select((data, i) => CreateCharacter(data, heroVisual, new[] { "Swordsman_Paladin", "Swordsman_Bard", "Swordsman_Mage" }[i], false)).ToArray();
                var enemyData = roster.EnemyPrefabs.First(p => p != null);
                var giant = CreateCharacter(enemyData, giantVisual, "Goblin_Giant", true);
                var killer = CreateCharacter(enemyData, killerVisual, "Goblin_Killer", true);
                // Keep the existing sparse lineup; both official enemy prefabs are ready
                // for any slot, without silently increasing encounter difficulty.
                var enemies = roster.EnemyPrefabs.Select(p => p == null ? null : killer).ToArray();
                roster.SetRoster(heroes, enemies, false);

                var environment = new GameObject("ForestEnvironment");
                foreach (string name in new[] { "WorldBackground", "BattleBackground (11 Layers)", "Warm Sun Light 2D", "Natural Light Post Processing" })
                    Clone(sourceTransforms.Single(t => t.name == name).gameObject, environment.transform);
                foreach (var old in environment.GetComponentsInChildren<BeatBounce>(true)) UnityEngine.Object.DestroyImmediate(old);
                foreach (var scroller in environment.GetComponentsInChildren<LoopingBackgroundScroller>(true)) Set(scroller, "beatSource", null);
                var sunObject = new GameObject("Warm Window Light"); sunObject.SetActive(false); sunObject.transform.SetParent(environment.transform, false);
                sunObject.transform.localPosition = new Vector3(-3.6f, 3.2f, -1);
                var sun = sunObject.AddComponent<Light2D>(); sun.lightType = Light2D.LightType.Point; sun.blendStyleIndex = 1;
                sun.color = new Color(1, .78f, .52f); sun.intensity = 1.1f; sun.pointLightInnerRadius = 1.5f; sun.pointLightOuterRadius = 12; sun.falloffIntensity = .45f;
                sunObject.SetActive(true);
                var motion = environment.AddComponent<FightEnvironmentController>();
                motion.Configure(null,
                    environment.GetComponentsInChildren<LoopingBackgroundScroller>(true).Select(s => new FightEnvironmentController.Layer { scroller = s }).ToArray(),
                    environment.GetComponentsInChildren<Transform>(true).Select(FightEnvironmentController.CreateAuthoredPulse).Where(p => p != null).ToArray());
                var envPrefab = Save(environment, "ForestEnvironment");
                var world = All<Transform>(battle).Single(t => t.name == "BattlefieldWorld");
                foreach (string name in new[] { "WorldBackground", "BattleBackground (Assign Sprite In Inspector)" })
                {
                    var old = world.Find(name); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
                var envInstance = (GameObject)PrefabUtility.InstantiatePrefab(envPrefab, world);
                var fight = All<FightCombatController>(battle).Single();
                Set(envInstance.GetComponent<FightEnvironmentController>(), "beatSource", fight);
                PrefabUtility.RecordPrefabInstancePropertyModifications(envInstance.GetComponent<FightEnvironmentController>());

                var rosterSerialized = new SerializedObject(roster);
                foreach (var slot in All<FightUnitSlot>(battle))
                {
                    var artActor = sourceActors.Single(a => a.GetComponentInParent<FightUnitSlot>().name == slot.name);
                    var sourceSlot = artActor.GetComponentInParent<FightUnitSlot>();
                    // Put the slot itself at the character's feet: scene handles now
                    // control character, shadow, health UI and VFX together.
                    slot.transform.position = new Vector3(artActor.transform.position.x, artActor.TargetRenderer.bounds.min.y, 0);
                    slot.ActorRoot.localPosition = Vector3.zero; slot.ActorRoot.localScale = Vector3.one;
                    FightCharacterDefinition assigned = null;
                    foreach (string side in new[] { "hero", "enemy" })
                    {
                        var slots = rosterSerialized.FindProperty(side + "SpawnSlots"); var prefabs = rosterSerialized.FindProperty(side + "Prefabs");
                        for (int i = 0; i < slots.arraySize; i++)
                            if (slots.GetArrayElementAtIndex(i).objectReferenceValue == slot) assigned = (FightCharacterDefinition)prefabs.GetArrayElementAtIndex(i).objectReferenceValue;
                    }
                    Set(slot, "actorPrefab", assigned == null ? null : assigned.gameObject);
                    var serialized = new SerializedObject(slot); serialized.FindProperty("actorLocalOffset").vector3Value = Vector3.zero; serialized.ApplyModifiedPropertiesWithoutUndo();
                    var shadow = slot.transform.Find("SlotGround");
                    var originalShadow = sourceSlot.transform.Find("SlotGround");
                    if (shadow != null && originalShadow != null)
                    {
                        EditorUtility.CopySerialized(originalShadow.GetComponent<SpriteRenderer>(), shadow.GetComponent<SpriteRenderer>());
                        shadow.localPosition = new Vector3(.12f, -.03f, .1f); shadow.localScale = originalShadow.localScale;
                        shadow.GetComponent<SpriteRenderer>().color = new Color(.035f, .055f, .1f, .34f);
                    }
                    foreach (var child in slot.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name == "HealthBackground" || child.name == "HealthFill" || child.name == "HealthLabel")
                        { var p = child.localPosition; p.y += .85f; child.localPosition = p; }
                    }
                }
                var camera = All<Camera>(battle).Single();
                var artCamera = All<Camera>(source).Single();
                camera.orthographicSize = artCamera.orthographicSize;
                camera.transform.SetPositionAndRotation(artCamera.transform.position, artCamera.transform.rotation);
                camera.allowHDR = true; camera.GetUniversalAdditionalCameraData().SetRenderer(1); camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                PolishPresentation(battle);
                EditorSceneManager.MarkSceneDirty(battle);
                if (!EditorSceneManager.SaveScene(battle)) throw new IOException("Could not save FightScene3.");
                AssetDatabase.SaveAssets();
                return "PASS: FightScene3 art environment, lit character prefabs, existing roster/data/music preserved. Giant and Killer available as enemy roster prefabs.\n";
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (!openBattle.IsValid()) EditorSceneManager.CloseScene(battle, true);
                EditorSceneManager.ClosePreviewScene(source);
            }
        }
        static void PolishPresentation(Scene scene)
        {
            foreach (var t in All<Transform>(scene).Where(t => t.name == "HeroField" || t.name == "EnemyField" || t.name == "HeroHeader" || t.name == "EnemyHeader"))
                t.gameObject.SetActive(false);
            foreach (var slot in All<FightUnitSlot>(scene))
            {
                foreach (var label in slot.GetComponentsInChildren<TextMesh>(true))
                {
                    if (label.name == "UnitName" || label.name == "RoleAndInput")
                    {
                        label.transform.localPosition = new Vector3(0, label.name == "UnitName" ? -.27f : -.53f, -.4f);
                        label.color = new Color(.08f, .12f, .11f, 1);
                    }
                    if (label.name == "Stats") label.transform.localPosition = new Vector3(0, 2.4f, -.4f);
                }
            }
            var environment = All<FightEnvironmentController>(scene).Single();
            var serialized = new SerializedObject(environment);
            PrefabUtility.RevertPropertyOverride(serialized.FindProperty("pulses"), InteractionMode.AutomatedAction);
            // Keep prefab pulse order, so art tuning in the environment prefab is
            // inherited; append only the scene-owned character shadows.
            environment.Configure(All<FightCombatController>(scene).Single(),
                environment.GetComponentsInChildren<LoopingBackgroundScroller>(true).Select(s => new FightEnvironmentController.Layer { scroller = s }).ToArray(),
                environment.GetComponentsInChildren<Transform>(true).Concat(All<Transform>(scene).Where(t => t.name == "SlotGround"))
                    .Select(FightEnvironmentController.CreateAuthoredPulse).Where(p => p != null).ToArray());
            PrefabUtility.RecordPrefabInstancePropertyModifications(environment);
        }
        static string Inspect()
        {
            var output = new StringBuilder();
            foreach (string path in new[] { ArtPath, BattlePath })
            {
                Scene scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    output.AppendLine(path);
                    foreach (var slot in All<FightUnitSlot>(scene))
                        output.AppendLine($"SLOT {PathOf(slot.transform)} pos={slot.transform.position} scale={slot.transform.lossyScale} actor={slot.ActorRoot?.localPosition} actorScale={slot.ActorRoot?.lossyScale}");
                    foreach (var a in All<FightCharacterCombatAnimator>(scene))
                        output.AppendLine($"ACTOR {PathOf(a.transform)} pos={a.transform.position} scale={a.transform.lossyScale} sprite={AssetDatabase.GetAssetPath(a.TargetRenderer.sprite)} size={a.TargetRenderer.bounds.size} flip={a.TargetRenderer.flipX} frames={a.FrameCount}");
                    foreach (var r in All<Transform>(scene).Where(t => t.name.Contains("Background") || t.name.Contains("Light")))
                        output.AppendLine($"ENV {PathOf(r)} pos={r.localPosition} scale={r.localScale}");
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            return output.ToString();
        }
    }
}
