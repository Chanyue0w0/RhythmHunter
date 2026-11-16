using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightBossFlightValidation
    {
        const string Request="Temp/FightBossFlightValidation.request", Result="Temp/FightBossFlightValidation.result";
        static double deadline, completedAt;
        static bool jump, flying, landing, howl, smoke, doorClosed, pausedChecked, sawFocus, sawFocusRestore;
        static readonly HashSet<int> howls=new();
        static readonly HashSet<int> howlFrames=new();
        static readonly System.Text.StringBuilder trace=new();
        static int lastSource=-99,pauseStep;
        static double pauseUntil;
        static Vector3 pausedPosition;
        static FightCharacterCombatAnimator bossAnimator;
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static FightBossFlightValidation()=>EditorApplication.update+=Tick;
        static void Tick()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(!EditorApplication.isPlaying)
            { if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.isPlaying=true;return; }
            try
            {
                Application.runInBackground=true;
                if(deadline==0)deadline=EditorApplication.timeSinceStartup+90;
                Check(EditorApplication.timeSinceStartup<deadline,"Intro timeout");
                var intro=UnityEngine.Object.FindFirstObjectByType<FightBossEntrance>();if(!intro)return;
                var fight=intro.GetComponent<FightCombatController>();var roster=intro.GetComponent<FightRosterManager>();
                var cinematic=UnityEngine.Object.FindFirstObjectByType<RhythmHunter.FightDemo.Art2.Art2CinematicFocus>();
                Check(cinematic,"Cinematic focus component missing");
                if(intro.Phase=="OpenIdle" && cinematic.AppliedWeight>.99f)
                {
                    foreach(var hero in roster.ActiveHeroes)
                    {
                        var props=new MaterialPropertyBlock();hero.CombatAnimator.TargetRenderer.GetPropertyBlock(props);
                        Check(props.GetFloat("_CinematicBlur")>1,"Hero not blurred during door focus");
                        foreach(var renderer in hero.ActorInstance.GetComponentsInChildren<SpriteRenderer>())
                            if(renderer.sharedMaterial && renderer.sharedMaterial.HasProperty("_CinematicBlur"))
                            {renderer.GetPropertyBlock(props);Check(props.GetFloat("_CinematicBlur")>1,"Hero/weapon blur mismatch");}
                    }
                    foreach(var enemy in roster.ActiveEnemies)
                    {var props=new MaterialPropertyBlock();enemy.CombatAnimator.TargetRenderer.GetPropertyBlock(props);Check(props.GetFloat("_CinematicBlur")==0,"Boss blurred during focus");}
                    foreach(var wall in UnityEngine.Object.FindObjectsByType<RhythmHunter.FightDemo.Art2.Art2FocusWall>(FindObjectsSortMode.None))
                    {
                        var so=new SerializedObject(wall);var targets=so.FindProperty("targets");
                        for(int i=0;i<targets.arraySize;i++)
                        {
                            var renderer=targets.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;if(!renderer)continue;
                            var props=new MaterialPropertyBlock();renderer.GetPropertyBlock(props);var value=props.GetVector("_FocusWallSettings");
                            Check(value.z<.001f,"Distant blur did not clear");Check(Mathf.Abs(value.w-so.FindProperty("fogDensity").floatValue)<.001f,"Fog changed unexpectedly");
                        }
                    }
                    if(!sawFocus) { CompareFocus(cinematic,roster); ScreenCapture.CaptureScreenshot("Temp/BossCinematicFocus.png"); } sawFocus=true;
                }
                if(intro.Phase=="Landing recovery" && cinematic.AppliedWeight<.001f)
                {
                    foreach(var hero in roster.ActiveHeroes)
                    {var props=new MaterialPropertyBlock();hero.CombatAnimator.TargetRenderer.GetPropertyBlock(props);Check(props.GetFloat("_CinematicBlur")==0,"Hero blur remained at landing");}
                    sawFocusRestore=true;
                }
                Check(!GameObject.Find("BossDoorSmoke_Runtime"),"Door smoke should be disabled");
                foreach(var effect in UnityEngine.Object.FindObjectsByType<FightBeatSpriteEffect>(FindObjectsSortMode.None))
                {
                    howls.Add(effect.GetInstanceID());howl=true;
                    var effectAnimator=effect.GetComponent<FightCharacterCombatAnimator>();
                    howlFrames.Add(effectAnimator.DisplayedFrame.Index);
                    if(bossAnimator && effectAnimator.DisplayedFrame.Index>0)
                    {
                        var foreground=effectAnimator.TargetRenderer;
                        Check(foreground.sortingLayerID==bossAnimator.TargetRenderer.sortingLayerID && foreground.sortingOrder>bossAnimator.TargetRenderer.sortingOrder,"Howl sorting not above Boss");
                        Check(effect.GetComponent<RhythmHunter.FightDemo.Art2.Art2EffectPresentation>().IgnoreFloorOcclusion,"Howl absent from post-character effect pass");
                        var camera=UnityEngine.Object.FindFirstObjectByType<RhythmHunter.FightDemo.Art2.Art2CameraController>().GetComponent<Camera>();
                        var f=camera.transform.forward;var abs=new Vector3(Mathf.Abs(f.x),Mathf.Abs(f.y),Mathf.Abs(f.z));
                        float far=Vector3.Dot(foreground.bounds.center,f)+Vector3.Dot(foreground.bounds.extents,abs);
                        var body=bossAnimator.TargetRenderer.bounds;float near=Vector3.Dot(body.center,f)-Vector3.Dot(body.extents,abs);
                        Check(far<near,"Howl overlaps Boss depth mask");
                    }
                    if(howls.Count==1 && howlFrames.Count==1) ScreenCapture.CaptureScreenshot("Temp/BossHowlPreview.png");
                }
                if(GameObject.Find("BossLandingSmoke_Runtime"))smoke=true;
                if(intro.BlocksBattle)
                {
                    float mana=fight.TeamSkillGaugeExact;
                    foreach(var command in new[]{FightInputRouter.HeroCommand.Front,FightInputRouter.HeroCommand.Middle,FightInputRouter.HeroCommand.Back,FightInputRouter.HeroCommand.TeamSkill})fight.SubmitHeroCommand(command);
                    Check(fight.TeamSkillGaugeExact==mana,"Intro input gained MP");
                    Check(fight.ScheduledEnemyHitCount==0,"Enemy attacks during intro");
                    if(roster.ActiveEnemies.Count==0)return;
                    var slot=roster.ActiveEnemies[0];bossAnimator=slot.CombatAnimator;
                    var rig=UnityEngine.Object.FindFirstObjectByType<RhythmHunter.FightDemo.Art2.Art2CameraController>();
                    var camera=rig.GetComponent<Camera>();
                    var block=new MaterialPropertyBlock();bossAnimator.TargetRenderer.GetPropertyBlock(block);
                    var center=camera.WorldToViewportPoint(bossAnimator.TargetRenderer.bounds.center);
                    if(bossAnimator.FindSequence(bossAnimator.ActiveAnimation)?.Name=="Jump" && center.z>0 && center.x>0 && center.x<1 && center.y>0 && center.y<1)
                        Check(block.GetFloat("_EntranceSilhouette")==1,"Visible ascending Boss lost black silhouette");
                    if(bossAnimator.FindSequence(bossAnimator.ActiveAnimation)?.Name=="Flying")
                        Check(block.GetFloat("_EntranceSilhouette")==0,"Offscreen Boss did not restore color");
                    int source=(int)bossAnimator.ActiveAnimation;
                    if(source!=lastSource){trace.AppendLine(intro.Phase+": "+bossAnimator.FindSequence(bossAnimator.ActiveAnimation)?.Name);lastSource=source;}
                    var name=bossAnimator.FindSequence(bossAnimator.ActiveAnimation)?.Name;
                    if(name=="Jump")
                    {
                        jump=true;
                        var pause=UnityEngine.Object.FindFirstObjectByType<FightPauseController>();
                        if(pauseStep==0){pause.SetPaused(true);pauseStep=1;pauseUntil=EditorApplication.timeSinceStartup+.2;}
                        else if(pauseStep==1 && EditorApplication.timeSinceStartup>=pauseUntil){pausedPosition=slot.ActorRoot.position;pauseStep=2;pauseUntil=EditorApplication.timeSinceStartup+.3;}
                        else if(pauseStep==2 && EditorApplication.timeSinceStartup>=pauseUntil){Check(Vector3.Distance(pausedPosition,slot.ActorRoot.position)<.001f,"Movement continued while paused");pause.SetPaused(false);pauseStep=3;pausedChecked=true;}
                    }
                    if(name=="Flying"){Check(jump,"Flying before Jump");flying=true;}
                    if(name=="Landing")
                    {
                        Check(flying,"Landing before Flying");landing=true;
                        if(bossAnimator.DisplayedFrame.Index<3)Check(!howl,"Howl before Landing frame four");
                        var door=UnityEngine.Object.FindFirstObjectByType<FightBossDoorAnimation>();
                        var so=new SerializedObject(door);
                        var renderer=(SpriteRenderer)so.FindProperty("doorRenderer").objectReferenceValue;
                        var closed=(Sprite)typeof(FightBossDoorAnimation).GetField("closedSprite",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(door);
                        Check(renderer.sprite==closed,"Door did not return to closed sprite");doorClosed=true;
                    }
                    return;
                }
                if(intro.Phase!="Complete")return;
                if(completedAt==0)
                {
                    Check(sawFocus && sawFocusRestore,"Cinematic focus or restoration not observed");
                    Check(jump&&flying&&landing&&howl&&smoke&&doorClosed&&pausedChecked,"Missing phase/effect/pause: "+trace);
                    Check(howls.Count==1,"Howl spawned more than once");
                    Check(howlFrames.SetEquals(new[]{0,1,2}),"Howl did not display all three frames");
                    Check(bossAnimator.DisplayedFrame.Source!= (int)bossAnimator.FindSequence("Landing").Animation,"Landing not released to Idle");
                    completedAt=EditorApplication.timeSinceStartup;
                }
                if(EditorApplication.timeSinceStartup-completedAt<1)return;
                Check(UnityEngine.Object.FindObjectsByType<FightBeatSpriteEffect>(FindObjectsSortMode.None).Length==0,"Howl did not clean up");
                File.WriteAllText(Result,"PASS: cinematic hero/weapon blur, sharp Boss/background, fog unchanged, restoration at landing; Jump -> Flying -> Landing -> Idle; no attacks/MP during intro; door closed; no door smoke; landing smoke retained; one howl at frame four, all three sprites and cleanup; pause holds movement.\n"+trace);
                Finish();
            }
            catch(Exception e){File.WriteAllText(Result,"FAIL: "+e+"\n"+trace);Finish();}
        }
        static void CompareFocus(RhythmHunter.FightDemo.Art2.Art2CinematicFocus focus,FightRosterManager roster)
        {
            var camera=focus.GetComponent<Camera>();var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32);
            var image=new Texture2D(960,540,TextureFormat.RGBA32,false);
            var update=typeof(RhythmHunter.FightDemo.Art2.Art2CinematicFocus).GetMethod("LateUpdate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            try
            {
                camera.targetTexture=rt;
                focus.SetEntranceWeight(0);update.Invoke(focus,null);camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();var sharp=image.GetPixels32();File.WriteAllBytes("Temp/BossFocusSharp.png",image.EncodeToPNG());
                focus.SetEntranceWeight(1);update.Invoke(focus,null);camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();var soft=image.GetPixels32();File.WriteAllBytes("Temp/BossFocusSoft.png",image.EncodeToPNG());
                int changed=0, samples=0;
                foreach(var slot in roster.ActiveHeroes)
                {
                    var b=slot.CombatAnimator.TargetRenderer.bounds;var lo=new Vector2(1,1);var hi=Vector2.zero;
                    for(int i=0;i<8;i++)
                    { var v=camera.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));lo=Vector2.Min(lo,v);hi=Vector2.Max(hi,v); }
                    for(int y=Mathf.Clamp((int)(lo.y*540),0,539);y<Mathf.Clamp((int)(hi.y*540),0,540);y++)
                    for(int x=Mathf.Clamp((int)(lo.x*960),0,959);x<Mathf.Clamp((int)(hi.x*960),0,960);x++)
                    {int i=y*960+x;samples++;if(Math.Abs(sharp[i].r-soft[i].r)+Math.Abs(sharp[i].g-soft[i].g)+Math.Abs(sharp[i].b-soft[i].b)>9)changed++;}
                }
                File.WriteAllText("Temp/BossFocusGPU.result",$"Hero regions changed pixels: {changed}/{samples}");
                Check(samples>0 && changed>samples*.01,"GPU comparison did not show foreground blur");
                // Disabled components must release only their own properties, preserving other effects.
                focus.enabled=false;
                foreach(var hero in roster.ActiveHeroes)
                {var props=new MaterialPropertyBlock();hero.CombatAnimator.TargetRenderer.GetPropertyBlock(props);Check(props.GetFloat("_CinematicBlur")==0,"Disable did not restore hero");}
                focus.enabled=true;focus.SetEntranceWeight(1);update.Invoke(focus,null);
            }
            finally
            {
                camera.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);
                focus.enabled=true;focus.SetEntranceWeight(1);update.Invoke(focus,null);
            }
        }        static void Finish(){File.Delete(Request);EditorApplication.isPlaying=false;deadline=completedAt=0;}
    }
}
