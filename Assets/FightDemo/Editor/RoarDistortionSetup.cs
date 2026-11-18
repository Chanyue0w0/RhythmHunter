using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.FightDemo.Art2;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class RoarDistortionSetup
    {
        const string EffectPath = "Assets/FightDemo/Prefabs/VFX/RoarDistortion.prefab";
        static RoarDistortionSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/RoarDistortion.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Setup(); Validate(); ValidatePipeline(); File.WriteAllText("Temp/RoarDistortion.result", "PASS: Renderer feature appended without replacing floor recovery; Boss Landing frame 4 uses mouth anchor; GPU displaced checkerboard pixels, outside ring unchanged, zero strength identical; actual Forward+ camera RenderGraph distortion, center tracking and cleanup. Actual Play visual still requires review."); }
            catch (Exception e) { File.WriteAllText("Temp/RoarDistortion.result", "FAIL: " + e); Debug.LogException(e); }
        }
        static void Check(bool condition, string text) { if (!condition) throw new Exception(text); }
        static void Setup()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/FightDemo/Art2Prototype/RoarDistortion.shader");
            Check(shader && !ShaderUtil.ShaderHasError(shader), "Roar shader compilation");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/FightDemo/Art2Prototype/Art2ForwardPlusRenderer.asset");
            if (!renderer.rendererFeatures.OfType<RoarDistortionFeature>().Any())
            {
                var feature = ScriptableObject.CreateInstance<RoarDistortionFeature>();
                feature.name = "Roar Distortion (Art2 camera only)";
                var data = new SerializedObject(feature); data.FindProperty("shader").objectReferenceValue = shader; data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature); feature.Create(); renderer.SetDirty(); EditorUtility.SetDirty(renderer);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath);
            if (!prefab)
            {
                var root = new GameObject("RoarDistortion");
                try
                {
                    var controller = root.AddComponent<RoarDistortionController>();
                    var old = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/VFX/howl_VFX.prefab");
                    var optional = (GameObject)PrefabUtility.InstantiatePrefab(old);
                    optional.name = "OptionalSpriteHowl"; optional.transform.SetParent(root.transform, false); optional.SetActive(false);
                    var so = new SerializedObject(controller); so.FindProperty("optionalSpriteEffect").objectReferenceValue = optional; so.ApplyModifiedPropertiesWithoutUndo();
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, EffectPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            const string kingPath = "Assets/FightDemo/Prefabs/ArtBattle/Goblin_King.prefab";
            var king = PrefabUtility.LoadPrefabContents(kingPath);
            try
            {
                var mouth = king.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RoarOrigin");
                if (!mouth)
                {
                    var head = king.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "HeadAnchor");
                    mouth = new GameObject("RoarOrigin").transform; mouth.SetParent(head ? head : king.transform, false);
                    mouth.localPosition = head ? new Vector3(0, -.15f, 0) : new Vector3(0, 1.5f, 0);
                }
                var animator = king.GetComponentInChildren<FightCharacterCombatAnimator>(true);
                var so = new SerializedObject(animator); var sequences = so.FindProperty("sequences");
                bool found = false;
                for (int i = 0; i < sequences.arraySize; i++)
                {
                    var sequence = sequences.GetArrayElementAtIndex(i);
                    if (sequence.FindPropertyRelative("animationName").stringValue != "Landing") continue;
                    var frames = sequence.FindPropertyRelative("frameEffects"); Check(frames.arraySize > 3, "Landing frame 4 missing");
                    var effects = frames.GetArrayElementAtIndex(3).FindPropertyRelative("effects");
                    for (int j = 0; j < effects.arraySize; j++)
                    {
                        var entry = effects.GetArrayElementAtIndex(j);
                        var reference = entry.FindPropertyRelative("prefab");
                        if (!reference.objectReferenceValue || (reference.objectReferenceValue.name != "howl_VFX" && reference.objectReferenceValue != prefab)) continue;
                        reference.objectReferenceValue = prefab;
                        entry.FindPropertyRelative("selfAnchor").objectReferenceValue = mouth;
                        entry.FindPropertyRelative("followAnchor").boolValue = true;
                        entry.FindPropertyRelative("limitLifetime").boolValue = false;
                        found = true;
                    }
                }
                Check(found, "Could not find authored roar event"); so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(king, kingPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(king); }
            AssetDatabase.SaveAssets();
        }
        static void ValidatePipeline()
        {
            var root = new GameObject("Roar pipeline validation");
            var rt = new RenderTexture(128,128,0,RenderTextureFormat.ARGB32);
            var read = new Texture2D(128,128,TextureFormat.RGBA32,false);
            var texture = new Texture2D(128,128,TextureFormat.RGBA32,false) { filterMode=FilterMode.Point };
            var material = new Material(Shader.Find("RhythmHunter/Art2/SpriteCardUnlit"));
            var oldTarget=RenderTexture.active;
            try
            {
                var camera=root.AddComponent<Camera>(); camera.transform.position=new Vector3(0,0,-5);
                camera.orthographic=true; camera.orthographicSize=1; camera.nearClipPlane=.1f; camera.farClipPlane=10;
                camera.cullingMask=1<<31; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
                camera.allowHDR=false; camera.allowMSAA=false; camera.targetTexture=rt;
                camera.GetUniversalAdditionalCameraData().SetRenderer(2);
                var pixels=new Color[128*128];
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)pixels[y*128+x]=((x/4+y/4)%2)==0 ? Color.white : Color.black;
                texture.SetPixels(pixels); texture.Apply(); material.mainTexture=texture;
                var quad=GameObject.CreatePrimitive(PrimitiveType.Quad); quad.layer=31;
                quad.transform.SetParent(root.transform,true); quad.transform.position=Vector3.zero; quad.transform.localScale=Vector3.one*2;
                quad.GetComponent<Renderer>().sharedMaterial=material;
                var effectObject=new GameObject("Pipeline roar");effectObject.transform.SetParent(root.transform,true);effectObject.transform.position=new Vector3(-.5f,.5f,0);
                var effect=effectObject.AddComponent<RoarDistortionController>();
                var data=new SerializedObject(effect); data.FindProperty("targetCamera").objectReferenceValue=camera; data.FindProperty("maxRadius").floatValue=.12f; data.FindProperty("waveWidth").floatValue=.03f; data.ApplyModifiedPropertiesWithoutUndo();
                Color32[] Render()
                {
                    camera.Render(); RenderTexture.active=rt; read.ReadPixels(new Rect(0,0,128,128),0,0);read.Apply();return read.GetPixels32();
                }
                var before=Render();effect.Play();effect.Advance(.25f);var during=Render();
                File.WriteAllBytes("Temp/RoarDistortionPipeline.png",read.EncodeToPNG());
                var changed = Enumerable.Range(0,before.Length).Where(index=>!before[index].Equals(during[index])).ToArray();
                Check(changed.Length>50,"Renderer Feature did not distort actual camera output");
                float cx=changed.Average(index=>index%128+.5f), cy=changed.Average(index=>index/128+.5f);
                Check(Mathf.Abs(cx-32)<4 && Mathf.Abs(cy-96)<4,"World-to-screen center mismatch: "+cx+","+cy);
                effect.Advance(.3f);var after=Render();Check(before.SequenceEqual(after),"Pipeline output did not restore");
                effect.Stop(); camera.targetTexture=null;
            }
            finally { RenderTexture.active=oldTarget; UnityEngine.Object.DestroyImmediate(root); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(read); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(material); }
        }
        static void Validate()
        {
            var material = new Material(Shader.Find("Hidden/RhythmHunter/RoarDistortion"));
            var input = new Texture2D(128, 128, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var rt = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            var read = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var oldTarget = RenderTexture.active;
            try
            {
                var pixels = new Color[128*128];
                for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++) pixels[y*128+x] = ((x/4+y/4)%2)==0 ? Color.white : Color.black;
                input.SetPixels(pixels); input.Apply(); rt.Create();
                material.SetTexture("_BlitTexture", input); material.SetVector("_BlitScaleBias", new Vector4(1,1,0,0));
                material.SetVector("_RoarResolution", new Vector4(128,128,0,0));
                material.SetVector("_RoarCenterRadius", new Vector4(.5f,.5f,.25f,.08f));
                Color32[] Render(float strength)
                {
                    material.SetVector("_RoarWave", new Vector4(strength,1,0,0));
                    using var cmd = new CommandBuffer(); cmd.SetRenderTarget(rt); cmd.SetViewport(new Rect(0,0,128,128));
                    cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3); Graphics.ExecuteCommandBuffer(cmd);
                    RenderTexture.active = rt; read.ReadPixels(new Rect(0,0,128,128),0,0); read.Apply(); return read.GetPixels32();
                }
                var baseline = Render(0); var warped = Render(6); var restored = Render(0);
                int changed = 0;
                for (int y=0;y<128;y++) for (int x=0;x<128;x++)
                {
                    int index=y*128+x; Check(baseline[index].Equals(restored[index]), "Zero strength left residue");
                    if (!baseline[index].Equals(warped[index]))
                    {
                        changed++; float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(64,64));
                        Check(Mathf.Abs(d-32)<12, "Changed pixels outside ring");
                    }
                    Check(warped[index].r < 3 || warped[index].r > 252, "Bilinear blur created intermediate pixels");
                }
                Check(changed > 50, "No actual GPU refraction");
                var obj = new GameObject("Roar lifetime check");
                try
                {
                    var controller=obj.AddComponent<RoarDistortionController>(); controller.Play();
                    controller.Advance(.2f); Check(controller.IsPlaying, "Ended early"); controller.Advance(.31f); Check(!controller.IsPlaying, "Did not finish");
                    controller.Play(); obj.SetActive(false); typeof(RoarDistortionController).GetMethod("OnDisable", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller, null); Check(!controller.IsPlaying, "Disable cleanup");
                }
                finally { UnityEngine.Object.DestroyImmediate(obj); }
                File.WriteAllBytes("Temp/RoarDistortionGPU.png", read.EncodeToPNG());
            }
            finally { RenderTexture.active=oldTarget; UnityEngine.Object.DestroyImmediate(input); UnityEngine.Object.DestroyImmediate(read); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(material); }
        }
    }
}
