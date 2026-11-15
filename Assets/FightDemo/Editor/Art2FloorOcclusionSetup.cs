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
            if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.ReadAllText(path).Trim() == "live-vfx")
            {
                if (!EditorApplication.isPlaying) { if(!EditorApplication.isPlayingOrWillChangePlaymode) { SessionState.SetFloat("VfxDiagWait",0); EditorApplication.isPlaying=true; } return; }
                float deadline=SessionState.GetFloat("VfxDiagWait",0);
                if(deadline==0) { SessionState.SetFloat("VfxDiagWait",(float)EditorApplication.timeSinceStartup+4); return; }
                if(EditorApplication.timeSinceStartup<deadline) return;
                if(GameObject.Find("VfxLiveProbe") == null)
                {
                    var p=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/VFX/SwordSlash_Red.prefab");
                    var target=GameObject.Find("HeroSlot_Front").GetComponent<FightUnitSlot>().ImpactEffectAnchor;
                    var probe=UnityEngine.Object.Instantiate(p,target.position,p.transform.rotation); probe.name="VfxLiveProbe";
                    SessionState.SetFloat("VfxDiagWait",(float)EditorApplication.timeSinceStartup+.15f); return;
                }
                File.Delete(path);
                try { DiagnoseVfx(); } catch(Exception e) { File.WriteAllText("Temp/VfxScene.result",e.ToString()); }
                finally { EditorApplication.isPlaying=false; }
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string mode;
            try { mode = File.ReadAllText(path).Trim(); File.Delete(path); } catch (IOException) { return; }
            try { if (mode == "diagnose-vfx") { DiagnoseVfx(); return; } if (mode != "validate") Configure(); Validate(); }
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
            settings.previewWarriors = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<FightCharacterDefinition>(true)).Where(d => d.EnableSwordCharge || d.IgnoreFloorOcclusion).ToArray();
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

        static void DiagnoseVfx()
        {
            var settings = UnityEngine.Object.FindObjectsByType<Art2FloorOcclusion>(FindObjectsSortMode.None).First();
            var camera = settings.targetCamera;
            var source = UnityEngine.Object.FindObjectsByType<Art2EffectPresentation>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == "SwordSlash_Red");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/VFX/SwordSlash_Red.prefab");
            var liveProbe = Application.isPlaying ? GameObject.Find("VfxLiveProbe") : null;
            var clone = liveProbe != null ? liveProbe : UnityEngine.Object.Instantiate(prefab);
            if(liveProbe==null) clone.hideFlags = HideFlags.HideAndDontSave;
            if (source != null) { clone.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation); clone.transform.localScale = source.transform.lossyScale; source.gameObject.SetActive(false); }
            if (Application.isPlaying || source == null) { var front = GameObject.Find("HeroSlot_Front"); if(front!=null) clone.transform.position = front.GetComponent<FightUnitSlot>().ImpactEffectAnchor.position; }
            var rt = new RenderTexture(960,540,24); var read = new Texture2D(960,540,TextureFormat.RGB24,false);
            var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
            var occluders = settings.sceneryOccluders; var floor = settings.floors;
            var log = new System.Text.StringBuilder();
            try
            {
                camera.targetTexture = rt;
                if(liveProbe == null) foreach (var ps in clone.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(.15f,false,true,true);
                foreach (var r in clone.GetComponentsInChildren<Renderer>()) log.AppendLine(r.name + " bounds=" + r.bounds + " material=" + r.sharedMaterial.name + " queue=" + r.sharedMaterial.renderQueue);
                foreach (var ps in clone.GetComponentsInChildren<ParticleSystem>()) log.AppendLine(ps.name + " count="+ps.particleCount+" time="+ps.time+" playing="+ps.isPlaying+" paused="+ps.isPaused+" culling="+ps.main.cullingMode);
                var so = new SerializedObject(clone.GetComponent<Art2EffectPresentation>());
                for (int mode=0; mode<3; mode++)
                {
                    so.FindProperty("ignoreFloorOcclusion").boolValue = mode != 0; so.ApplyModifiedPropertiesWithoutUndo();
                    if (mode == 2) settings.sceneryOccluders = Array.Empty<Renderer>();
                    camera.Render(); RenderTexture.active=rt; read.ReadPixels(new Rect(0,0,960,540),0,0); read.Apply();
                    File.WriteAllBytes("Temp/vfx_scene_"+mode+".png",read.EncodeToPNG());
                }
                foreach (var r in occluders) if(r!=null) log.AppendLine("occluder " + r.name + " " + r.bounds);
                File.WriteAllText("Temp/VfxScene.result", log.ToString());
            }
            finally { settings.sceneryOccluders=occluders; settings.floors=floor; camera.targetTexture=oldTarget; RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(clone); if(source!=null) source.gameObject.SetActive(true); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(read); }
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
                // VFX use their original material exactly once, against depth without the floor.
                actor.SetActive(false); fence.SetActive(false);
                var effect = new GameObject("Opt-in VFX"); effect.layer = layer; effect.transform.SetParent(root.transform, false);
                var effectRenderer = effect.AddComponent<SpriteRenderer>(); effectRenderer.sprite = sprite; effectRenderer.sharedMaterial = card;
                effectRenderer.color = new Color(1, 0, 0, .5f);
                var presentation = effect.AddComponent<Art2EffectPresentation>();
                var so = new SerializedObject(presentation);
                if (presentation.IgnoreFloorOcclusion) throw new Exception("VFX ignore floor must default off");
                Color vfxOff = Render(true);
                if (vfxOff.b < .5f || vfxOff.r > .2f) throw new Exception("Default VFX bypassed floor");
                so.FindProperty("ignoreFloorOcclusion").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo();
                Vector3 authoredPosition = effect.transform.position;
                Quaternion authoredRotation = effect.transform.rotation;
                Color vfxOn = Render(true, "vfx_floor_on");
                if (vfxOn.r < .2f) throw new Exception("VFX recovery failed: " + vfxOn);
                if (effectRenderer.forceRenderingOff) throw new Exception("Renderer suppression leaked after camera");
                floor.SetActive(false);
                Color onceOn = Render(true);
                so.FindProperty("ignoreFloorOcclusion").boolValue = false; so.ApplyModifiedPropertiesWithoutUndo();
                Color onceOff = Render(true);
                if (Vector4.Distance(onceOn, onceOff) > .03f) throw new Exception($"Transparent VFX drawn twice or changed appearance: on={onceOn}, off={onceOff}");
                so.FindProperty("ignoreFloorOcclusion").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo();
                floor.SetActive(true); fence.SetActive(true); fence.transform.localPosition = new Vector3(0, 0, -2);
                if (Render(true).g < .5f) throw new Exception("Foreground did not occlude VFX");
                if (effect.transform.position != authoredPosition || effect.transform.rotation != authoredRotation) throw new Exception("VFX floor setting moved/rotated effect");
                fence.SetActive(false);
                presentation.enabled = false;
                if (Render(true).r > .2f || effectRenderer.forceRenderingOff) throw new Exception("Disabling VFX component did not restore ordinary rendering");
                effect.SetActive(false);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/VFX/SwordSlash_Red.prefab");
                var slash = UnityEngine.Object.Instantiate(prefab, root.transform);
                slash.transform.localPosition = Vector3.zero;
                foreach (var t in slash.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                var slashPresentation = slash.GetComponent<Art2EffectPresentation>();
                var initialSlashSo = new SerializedObject(slashPresentation);
                initialSlashSo.FindProperty("ignoreFloorOcclusion").boolValue = false; initialSlashSo.ApplyModifiedPropertiesWithoutUndo();
                foreach (var ps in slash.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(.15f, false, true, true);
                var particleRenderers = slash.GetComponentsInChildren<ParticleSystemRenderer>();
                var bounds = particleRenderers[0].bounds;
                foreach (var pr in particleRenderers) bounds.Encapsulate(pr.bounds);
                camera.orthographic = true; camera.orthographicSize = Mathf.Max(1, Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.2f);
                camera.transform.position = bounds.center + Vector3.back * 10;
                floor.transform.position = bounds.center + Vector3.back * 5;
                floor.transform.localScale = Vector3.one * camera.orthographicSize * 3;
                foreach (var pr in particleRenderers)
                {
                    foreach (var other in particleRenderers) other.enabled = other == pr;
                    floor.SetActive(false); Render(true, "vfx_isolated_" + pr.name);
                    var single = readback.GetPixels();
                    initialSlashSo.FindProperty("ignoreFloorOcclusion").boolValue = true; initialSlashSo.ApplyModifiedPropertiesWithoutUndo();
                    floor.SetActive(true); Render(true, "vfx_recovered_" + pr.name);
                    var singleOn = readback.GetPixels();
                    int visible = single.Count(c => c.r > .1f);
                    int restored = single.Zip(singleOn,(a,b)=>a.r > .1f && b.r > .1f ? 1 : 0).Sum();
                    File.AppendAllText("Temp/VfxIndividual.result", pr.name + " pixels=" + visible + " recovered=" + restored + " particles=" + pr.GetComponent<ParticleSystem>().particleCount + " bounds=" + pr.bounds + "\n");
                    if (visible < 5 || restored < visible * .9f) throw new Exception("Individual particle recovery failed: " + pr.name + " " + restored + "/" + visible);
                    initialSlashSo.FindProperty("ignoreFloorOcclusion").boolValue = false; initialSlashSo.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var pr in particleRenderers) pr.enabled = true;
                floor.SetActive(false); Render(true); var reference = readback.GetPixels();
                if (reference.Count(c => c.maxColorComponent > .1f) < 5) throw new Exception("SwordSlash fixture has no visible particles");
                var slashSo = new SerializedObject(slashPresentation);
                slashSo.FindProperty("ignoreFloorOcclusion").boolValue = true; slashSo.ApplyModifiedPropertiesWithoutUndo();
                floor.SetActive(true); Render(true, "vfx_sword_floor_on"); var recovered = readback.GetPixels();
                floor.SetActive(false); Render(true); var isolated = readback.GetPixels();
                float difference = reference.Zip(isolated, (a,b) => Vector4.Distance(a,b)).Average();
                if (difference > .02f) throw new Exception("SwordSlash changed appearance/double draw: " + difference);
                if (recovered.Zip(isolated, (a,b) => b.maxColorComponent > .2f && a.r > .05f ? 1 : 0).Sum() < 5)
                    throw new Exception("SwordSlash particles not recovered over floor");
                File.WriteAllText("Temp/Art2FloorOcclusion.result", $"PASS: GPU character floor recovery, foreground/transparent openings, perspective; VFX default off, recovery, no double blending, foreground occlusion, transform preservation and disable restoration; actual SwordSlash mesh/billboard particles (difference={difference}).\nScenery lighting/fog/shadow appearance still requires scene review.");
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
