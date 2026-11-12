using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.FightDemo.Art2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class Art2LightingPolishSetup
    {
        static Art2LightingPolishSetup() => EditorApplication.update += Poll;
        private static void Poll()
        {
            const string request = "Temp/Art2LightingPolish.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Configure(); }
            catch (Exception e) { File.WriteAllText("Temp/Art2LightingPolish.result", "FAIL " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Art2/Configure Lighting Polish")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            if (!scene.isLoaded) throw new Exception("Open Art2_FightScene first.");
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var host = all.Single(t => t.name == "Art2_2_5D_Prototype").gameObject;
            var fill = host.GetComponent<Art2CharacterFillLighting>();
            if (fill == null) throw new Exception("Character fill missing");
            // Reconfiguration must preserve the user's rim switch, direction and light selection.
            var tuning = host.GetComponent<Art2SceneLightingTuning>();
            if (tuning == null) tuning = Undo.AddComponent<Art2SceneLightingTuning>(host);
            Undo.RecordObject(tuning, "Configure environment lighting");
            var targets = new List<Art2SceneLightingTuning.Target>();
            var surfaces = new Dictionary<string, string> {
                { "Rock_Left", "CaveRockLeftNormal" }, { "Rock_Right", "CaveRockRightNormal" },
                { "WoodenBarricade_Left", "CaveWoodLeftNormal" }, { "WoodenBarricade_Right", "CaveWoodRightNormal" } };
            string[] backgrounds = { "RearWall_GroundAnchor", "SideWalls_GroundAnchor", "ClosedDoor_GroundAnchor", "OverheadMoss_GroundAnchor" };
            string[] foregrounds = { "FrontRockEdges_GroundAnchor", "ForegroundSilhouette_GroundAnchor" };
            int surfaceCount = 0;
            foreach (var t in all)
            {
                var renderer = t.GetComponent<SpriteRenderer>();
                if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.shader.name != "RhythmHunter/Art2/SpriteCardLit") continue;
                if (surfaces.TryGetValue(t.name, out string map))
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FightDemo/Art2Prototype/" + map + ".png");
                    if (texture == null) throw new Exception("Missing map " + map);
                    targets.Add(new Art2SceneLightingTuning.Target { renderer = renderer, normalMap = texture }); surfaceCount++;
                    continue;
                }
                for (var parent = t; parent != null; parent = parent.parent)
                {
                    bool foreground = foregrounds.Contains(parent.name);
                    if (!foreground && !backgrounds.Contains(parent.name)) continue;
                    targets.Add(new Art2SceneLightingTuning.Target { renderer = renderer, foreground = foreground }); break;
                }
            }
            if (surfaceCount != 4) throw new Exception("Expected four surfaces, got " + surfaceCount);
            tuning.targets = targets.ToArray();
            var flashes = host.GetComponent<Art2SkillFlashLighting>();
            if (flashes == null) flashes = Undo.AddComponent<Art2SkillFlashLighting>(host);
            Undo.RecordObject(flashes, "Configure skill light pool");
            flashes.combat = all.Select(t => t.GetComponent<FightCombatController>()).First(c => c != null);
            if (flashes.rules.Length == 0) flashes.rules = new[] {
                new Art2SkillFlashLighting.Rule { effectName = "LightExplosionQuick", intensity = 1.5f, durationBeats = .3f },
                new Art2SkillFlashLighting.Rule { effectName = "SwordSlash_Pixel_Yellow", intensity = .8f, durationBeats = .2f, range = 2.5f },
                new Art2SkillFlashLighting.Rule { effectName = "FireExplosion", color = new Color(1, .3f, .08f), intensity = 1.5f, durationBeats = .35f }
            };
            EditorUtility.SetDirty(fill); EditorUtility.SetDirty(tuning); EditorUtility.SetDirty(flashes);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = host;
            EditorGUIUtility.PingObject(host);
            ValidateControls(flashes);
            File.WriteAllText("Temp/Art2LightingPolish.result", $"PASS setup: {targets.Count} scene targets, {surfaceCount} surface maps, rim main-light reference, {flashes.rules.Length} flash rules. Material toggles/restoration and bounded light pool passed. Main light transform/settings unchanged.");
        }

        private static void ValidateControls(Art2SkillFlashLighting sceneFlashes)
        {
            var root = new GameObject("Lighting control validation") { hideFlags = HideFlags.HideAndDontSave };
            var material = new Material(Shader.Find("RhythmHunter/Art2/SpriteCardLit"));
            Art2SceneLightingTuning tuning = null;
            try
            {
                material.SetFloat("_AmbientStrength", 1);
                var renderer = root.AddComponent<SpriteRenderer>(); renderer.sharedMaterial = material;
                tuning = root.AddComponent<Art2SceneLightingTuning>();
                tuning.targets = new[] { new Art2SceneLightingTuning.Target { renderer = renderer } };
                Call(tuning, "Apply");
                if (!Mathf.Approximately(renderer.sharedMaterial.GetFloat("_AmbientStrength"), .8f)) throw new Exception("Background multiplier failed");
                tuning.enableSceneSeparation = false; Call(tuning, "Apply");
                if (!Mathf.Approximately(renderer.sharedMaterial.GetFloat("_AmbientStrength"), 1)) throw new Exception("Background toggle failed");
                tuning.targets[0].normalMap = Texture2D.normalTexture; Call(tuning, "Apply");
                if (renderer.sharedMaterial.GetFloat("_UseNormalMap") != 1) throw new Exception("Surface enable failed");
                tuning.enableSurfaceLighting = false; Call(tuning, "Apply");
                if (renderer.sharedMaterial.GetFloat("_UseNormalMap") != material.GetFloat("_UseNormalMap")) throw new Exception("Surface disable failed");
                Call(tuning, "OnDisable");
                if (renderer.sharedMaterial != material) throw new Exception("Original material not restored");

                var flashes = root.AddComponent<Art2SkillFlashLighting>();
                flashes.combat = sceneFlashes.combat; flashes.rules = sceneFlashes.rules;
                var effect = new GameObject(flashes.rules[0].effectName + "(Clone)").AddComponent<FightBattleEffect>();
                effect.transform.SetParent(root.transform);
                for (int i = 0; i < 12; i++) Call(flashes, "OnEffect", effect);
                var lights = root.GetComponentsInChildren<Light>(true);
                if (lights.Length != 2 || lights.Any(l => !l.enabled || l.shadows != LightShadows.None)) throw new Exception("Flash pool limit/shadows failed");
                flashes.enableSkillFlashes = false; Call(flashes, "LateUpdate");
                if (lights.Any(l => l.enabled)) throw new Exception("Flash disable failed");
                Call(flashes, "OnDisable");
            }
            finally
            {
                if (tuning != null) Call(tuning, "OnDisable");
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(material);
            }
            static void Call(object target, string method, params object[] args) =>
                target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        }
    }
}
