using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightAngryEffectSetup
    {
        const string Folder = "Assets/FightDemo/Materials/AngryEffect";
        const string PrefabPath = "Assets/FightDemo/Prefabs/VFX/AngryEffect.prefab";
        static FightAngryEffectSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/AngryEffect.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Build(); }
            catch (Exception e) { File.WriteAllText("Temp/AngryEffect.result", "FAIL: " + e); Debug.LogException(e); }
        }

        [MenuItem("Rhythm Hunter/VFX/Build Angry Icon Effect")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play before saving the effect.");
            var scene = SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            if (!scene.isLoaded) throw new Exception("Open Art2_FightScene first.");
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var root = all.Single(t => t.name == "AngryEffect").gameObject;
            var icon = all.Single(t => t.name == "AngryIcon").GetComponent<SpriteRenderer>().sprite;
            var particles = root.GetComponentsInChildren<ParticleSystem>(true).Single(p => p.name == "Frowns");
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (icon == null || renderer.sharedMaterial == null) throw new Exception("Missing icon or particle material.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/FightDemo/Materials", "AngryEffect");
            string texturePath = Folder + "/AngryIcon.png";
            if (!File.Exists(texturePath))
            {
                // Copy pixels only. Never change the source pack's texture/import settings.
                File.Copy(AssetDatabase.GetAssetPath(icon.texture), texturePath);
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            string materialPath = Folder + "/AngryIconParticles.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(renderer.sharedMaterial) { name = "AngryIconParticles" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_MainTex", texture);
            var rect = icon.rect;
            material.SetTextureScale("_MainTex", new Vector2(rect.width / icon.texture.width, rect.height / icon.texture.height));
            material.SetTextureOffset("_MainTex", new Vector2(rect.x / icon.texture.width, rect.y / icon.texture.height));
            // Use the icon's RGB and alpha, not the original monochrome expression mask.
            if (material.HasProperty("_SingleChannel")) material.SetFloat("_SingleChannel", 0);
            if (material.HasProperty("_UseSP")) material.SetFloat("_UseSP", 0);
            material.DisableKeyword("_CFXR_SINGLE_CHANNEL");
            material.DisableKeyword("_SINGLECHANNEL_ON");
            material.DisableKeyword("_FADING_ON");
            EditorUtility.SetDirty(material);
            Undo.RecordObjects(new UnityEngine.Object[] { particles, renderer }, "Use AngryIcon particles");
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var sheet = particles.textureSheetAnimation;
            sheet.enabled = false;
            var main = particles.main;
            main.startColor = Color.white;
            var color = particles.colorOverLifetime;
            if (color.enabled)
            {
                var gradient = color.color.gradient;
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, gradient.alphaKeys);
                color.color = gradient;
            }
            // Keep emission, speed, shape, size and alpha fade authored in the scene.
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
            if (prefab == null) throw new Exception("Could not save AngryEffect prefab.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var saved = prefab.GetComponentsInChildren<ParticleSystem>(true).Single(p => p.name == "Frowns");
            if (saved.GetComponent<ParticleSystemRenderer>().sharedMaterial != material || saved.textureSheetAnimation.enabled || material.GetTexture("_MainTex") != texture)
                throw new Exception("Saved particle texture/material validation failed.");
            var clone = UnityEngine.Object.Instantiate(prefab);
            try
            {
                foreach (var p in clone.GetComponentsInChildren<ParticleSystem>(true)) p.Simulate(1.5f, false, true, true);
                if (clone.GetComponentsInChildren<ParticleSystem>(true).Single(p => p.name == "Frowns").particleCount == 0)
                    throw new Exception("Icon emitter did not emit particles during simulation.");
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
            Selection.activeGameObject = root;
            SceneView.RepaintAll();
            File.WriteAllText("Temp/AngryEffect.result", "PASS: scene AngryEffect saved and connected to standalone prefab; Frowns uses local Point/uncompressed AngryIcon texture and independent material; camera-facing billboard; original emission preserved; particle simulation emits icons. Source pack unchanged.");
        }
    }
}
