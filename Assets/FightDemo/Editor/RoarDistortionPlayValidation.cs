using System;
using System.IO;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.FightDemo.Art2;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class RoarDistortionPlayValidation
    {
        static double deadline, resumeAt;
        static RoarDistortionController effect;
        static FightCombatController fight;
        static bool observed, resumed;
        static float pausedElapsed;
        const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        static RoarDistortionPlayValidation()=>EditorApplication.update+=Tick;
        static void Tick()
        {
            const string request="Temp/RoarDistortionPlay.request";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(!EditorApplication.isPlaying) { if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.isPlaying=true; return; }
            try
            {
                Application.runInBackground=true;
                if(deadline==0)deadline=EditorApplication.timeSinceStartup+90;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timed out waiting for natural Landing roar");
                if(!fight)fight=UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                if(!observed)
                {
                    effect=UnityEngine.Object.FindFirstObjectByType<RoarDistortionController>();
                    if(!effect||!effect.IsPlaying)return;
                    var anchor=(Transform)typeof(RoarDistortionController).GetField("origin",Flags).GetValue(effect);
                    if(!anchor||anchor.name!="RoarOrigin")throw new Exception("Frame event did not bind mouth anchor");
                    if(Vector3.Distance(effect.WorldOrigin,anchor.position)>.001f)throw new Exception("World center mismatch");
                    var optional=effect.transform.Find("OptionalSpriteHowl");
                    if(optional&&optional.gameObject.activeSelf)throw new Exception("Old sprite should be disabled");
                    CaptureComparison(effect);
                    observed=true; pausedElapsed=(float)typeof(RoarDistortionController).GetField("elapsed",Flags).GetValue(effect);
                    if(!fight.BeatClock.SetPaused(true))throw new Exception("Could not pause beat clock");
                    resumeAt=EditorApplication.timeSinceStartup+.3;return;
                }
                if(!resumed)
                {
                    if(EditorApplication.timeSinceStartup<resumeAt)return;
                    if(!effect||!effect.IsPlaying)throw new Exception("Effect ended while paused");
                    float elapsed=(float)typeof(RoarDistortionController).GetField("elapsed",Flags).GetValue(effect);
                    if(Mathf.Abs(elapsed-pausedElapsed)>.001f)throw new Exception("Paused roar advanced");
                    fight.BeatClock.SetPaused(false);resumed=true;return;
                }
                if(effect)return;
                Finish("PASS: natural Boss Landing spawned distortion at RoarOrigin; old sprite disabled; battle pause froze lifetime; resumed effect destroyed cleanly.");
            }
            catch(Exception e){Finish("FAIL: "+e);}
        }
        static void CaptureComparison(RoarDistortionController controller)
        {
            var type=typeof(RoarDistortionController);
            var camera=(Camera)type.GetField("targetCamera",Flags).GetValue(controller);
            if(!camera)throw new Exception("No rendering camera");
            var previous=camera.targetTexture;var oldActive=RenderTexture.active;
            var rt=new RenderTexture(960,540,24,RenderTextureFormat.ARGB32);
            var read=new Texture2D(960,540,TextureFormat.RGBA32,false);
            float elapsed=(float)type.GetField("elapsed",Flags).GetValue(controller);
            float strength=(float)type.GetField("distortionStrength",Flags).GetValue(controller);
            try
            {
                camera.targetTexture=rt;type.GetField("elapsed",Flags).SetValue(controller,.2f);
                Color32[] Render(string path,float amount)
                {
                    type.GetField("distortionStrength",Flags).SetValue(controller,amount);
                    camera.Render();RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,960,540),0,0);read.Apply();
                    File.WriteAllBytes(path,read.EncodeToPNG());return read.GetPixels32();
                }
                var before=Render("Temp/RoarSceneBefore.png",0);
                var after=Render("Temp/RoarSceneAfter.png",strength);
                int changed=0;for(int i=0;i<before.Length;i++)if(!before[i].Equals(after[i]))changed++;
                Vector3 center=camera.WorldToViewportPoint(controller.WorldOrigin);
                File.WriteAllText("Temp/RoarSceneCapture.txt","center="+center+" strength="+strength+" changedPixels="+changed+" camera="+camera.name);
                if(changed<100)throw new Exception("No visible refraction in actual scene: "+changed+" pixels, center "+center);
            }
            finally
            {
                type.GetField("elapsed",Flags).SetValue(controller,elapsed);type.GetField("distortionStrength",Flags).SetValue(controller,strength);
                camera.targetTexture=previous;RenderTexture.active=oldActive;
                rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(read);
            }
        }
        static void Finish(string result)
        {
            if(fight&&fight.BeatClock)fight.BeatClock.SetPaused(false);
            File.Delete("Temp/RoarDistortionPlay.request");File.WriteAllText("Temp/RoarDistortionPlay.result",result);
            deadline=resumeAt=0;observed=resumed=false;effect=null;fight=null;
            EditorApplication.isPlaying=false;
        }
    }
}
