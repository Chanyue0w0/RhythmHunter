using System;
using System.IO;
using System.Linq;
using RhythmHunter.FightDemo;
using RhythmHunter.FightDemo.Art2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class Art2FloorOcclusionSetup
    {
        static Art2FloorOcclusionSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string path = "Temp/Art2FloorOcclusion.request";
            if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string mode;
            try { mode = File.ReadAllText(path).Trim(); File.Delete(path); } catch (IOException) { return; }
            try { if (mode != "validate") Configure(); Validate(); }
            catch (Exception e) { File.WriteAllText("Temp/Art2FloorOcclusion.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Art2/Configure Floor Occlusion Experiment")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            if (!scene.isLoaded) throw new Exception("Open Art2_FightScene first");
            var renderers = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Renderer>(true)).ToArray();
            var camera = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Camera>(true)).First(c => c.enabled && c.orthographic == false);
            var settings = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Art2FloorOcclusion>(true)).FirstOrDefault();
            if (settings == null)
            {
                var root = new GameObject("WarriorFloorOcclusion_Test"); SceneManager.MoveGameObjectToScene(root, scene);
                settings = root.AddComponent<Art2FloorOcclusion>();
            }
            settings.targetCamera = camera;
            settings.floors = renderers.Where(r => r.sharedMaterial != null && r.sharedMaterial.shader.name == "RhythmHunter/Art2/CaveFloorLit").ToArray();
            if (settings.floors.Length == 0) throw new Exception("No cave floor renderer found");
            settings.sceneryOccluders = renderers.Where(r => r.sharedMaterial != null && !settings.floors.Contains(r) &&
                r.GetComponentInParent<FightUnitSlot>() == null && r.GetComponentInParent<FightCharacterDefinition>() == null &&
                (r is SpriteRenderer && r.sharedMaterial.shader.name.StartsWith("RhythmHunter/Art2/SpriteCard") || r is MeshRenderer && r.sharedMaterial.renderQueue < 2500)).ToArray();
            settings.battleSlots = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<FightUnitSlot>(true)).ToArray();
            settings.previewWarriors = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<FightCharacterDefinition>(true)).Where(d => d.EnableSwordCharge).ToArray();
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/FightDemo/Art2Prototype/Art2ForwardPlusRenderer.asset");
            if (!renderer.rendererFeatures.Any(f => f is Art2FloorOcclusionFeature))
            {
                var feature = ScriptableObject.CreateInstance<Art2FloorOcclusionFeature>(); feature.name = "Warrior Floor Occlusion (scene opt-in)";
                var so = new SerializedObject(feature);
                so.FindProperty("occluderShader").objectReferenceValue = Shader.Find("Hidden/RhythmHunter/Art2FloorOccluder");
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.AddObjectToAsset(feature, renderer); renderer.rendererFeatures.Add(feature);
                feature.Create(); renderer.SetDirty(); EditorUtility.SetDirty(renderer);
            }
            EditorUtility.SetDirty(settings); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        public static void Validate()
        {
            // Deliberate intersection fixture: red card behind blue floor, then a green solid occluder.
            // Uses the actual Art2 renderer and shader, with GPU readback; never moves the user's scene objects.
            var root = new GameObject("FloorOcclusionGpuFixture") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(128, 128, 24);
            Texture2D readback = null, texture = null; Sprite sprite = null; Material card = null, solid = null;
            var previous = RenderTexture.active;
            try
            {
                const int layer = 31;
                var cameraObject = new GameObject("Fixture camera"); cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = 2; camera.transform.position = new Vector3(0, 0, -10); camera.cullingMask = 1 << layer;
                camera.farClipPlane = 20;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.targetTexture = rt;
                var pipeline = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
                if (pipeline == null) pipeline = (UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                int index = -1;
                for (int i = 0; i < pipeline.rendererDataList.Length; i++)
                    if (pipeline.rendererDataList[i] != null && pipeline.rendererDataList[i].name == "Art2ForwardPlusRenderer") index = i;
                if (index < 0) throw new Exception("Art2 renderer missing");
                camera.GetUniversalAdditionalCameraData().SetRenderer(index);
                var settings = root.AddComponent<Art2FloorOcclusion>(); settings.targetCamera = camera; settings.previewInSceneView = false;
                texture = new Texture2D(4, 4); texture.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray()); texture.Apply();
                sprite = Sprite.Create(texture, new Rect(0,0,4,4), Vector2.one * .5f, 2);
                card = new Material(Shader.Find("RhythmHunter/Art2/SpriteCardLit")); card.SetFloat("_SurfaceDebugMode",1);
                var actor = new GameObject("Red warrior"); actor.layer = layer; actor.transform.SetParent(root.transform, false);
                var data = actor.AddComponent<FightCharacterDefinition>();
                var visual = actor.AddComponent<SpriteRenderer>(); visual.sprite = sprite; visual.sharedMaterial = card; visual.color = Color.red;
                settings.previewWarriors = new[] { data };
                solid = new Material(Shader.Find("Universal Render Pipeline/Unlit")); solid.SetColor("_BaseColor",Color.blue);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Quad); floor.transform.SetParent(root.transform,false);
                floor.layer = layer; floor.transform.localPosition = new Vector3(0,0,-1); floor.transform.localScale = Vector3.one * 4;
                floor.GetComponent<Renderer>().sharedMaterial = solid; settings.floors = new[] { floor.GetComponent<Renderer>() };
                Color off = Render(false, "floor_off"), on = Render(true, "floor_on");
                if (off.b < .5f || off.r > .2f || on.r < .5f || on.b > .2f)
                    throw new Exception($"Floor recovery failed: off={off} on={on}");
                var fence = new GameObject("Green fence"); fence.layer = layer; fence.transform.SetParent(root.transform,false);
                fence.transform.localPosition = new Vector3(0,0,-2);
                var fenceRenderer = fence.AddComponent<SpriteRenderer>(); fenceRenderer.sprite = sprite; fenceRenderer.sharedMaterial = card; fenceRenderer.color = Color.green;
                settings.sceneryOccluders = new Renderer[] { fenceRenderer };
                Color coveredOff = Render(false);
                Color covered = Render(true, "floor_foreground");
                if (covered.g < .5f || covered.r > .2f)
                    throw new Exception("Foreground occluder did not hide recovered pixels: " + covered + " off=" + coveredOff);
                // Transparent artwork must not act like a solid rectangle.
                texture.SetPixels(Enumerable.Repeat(Color.clear,16).ToArray()); texture.Apply();
                // Give the warrior its own opaque texture via an override so only the fence has the hole.
                var block = new MaterialPropertyBlock(); block.SetTexture("_MainTex", Texture2D.whiteTexture); visual.SetPropertyBlock(block);
                Color hole = Render(true);
                if (hole.r < .5f) throw new Exception("Transparent fence pixels incorrectly occlude: " + hole);
                var halfMask = new Color[16];
                for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) halfMask[y * 4 + x] = y < 2 ? Color.green : Color.clear;
                texture.SetPixels(halfMask); texture.Apply(); Render(true);
                if (readback.GetPixel(64,48).g < .5f || readback.GetPixel(64,80).r < .5f)
                    throw new Exception("Depth mask screen-space orientation or partial transparency is incorrect");
                texture.SetPixels(Enumerable.Repeat(Color.clear,16).ToArray()); texture.Apply();
                camera.orthographic = false; camera.fieldOfView = 40;
                Color perspective = Render(true);
                if (perspective.r < .5f) throw new Exception("Perspective recovery failed: " + perspective);
                texture.SetPixels(Enumerable.Repeat(Color.white,16).ToArray()); texture.Apply();
                if (Render(true).g < .5f) throw new Exception("Perspective foreground occlusion failed");
                fence.transform.localPosition = new Vector3(0,0,1);
                if (Render(true).r < .5f) throw new Exception("Scenery behind the actor incorrectly occludes it");
                File.WriteAllText("Temp/Art2FloorOcclusion.result", $"PASS: GPU floor recovery off={off} on={on}; foreground={covered}; transparent opening={hole}; perspective recovery and foreground; rear scenery does not occlude; no actor transform changes.\nScenery lighting/fog/shadow appearance still requires scene review.");
                Color Render(bool enabled, string snapshot = null)
                {
                    settings.enableCorrection = enabled; camera.Render();
                    RenderTexture.active = rt;
                    if (readback == null) readback = new Texture2D(128,128,TextureFormat.RGBA32,false);
                    readback.ReadPixels(new Rect(0,0,128,128),0,0); readback.Apply();
                    if (snapshot != null) File.WriteAllBytes("Temp/" + snapshot + ".png", readback.EncodeToPNG());
                    return readback.GetPixel(64,64);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(root); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                foreach (var o in new UnityEngine.Object[] { readback, sprite, texture, card, solid }) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
        }
    }
}
