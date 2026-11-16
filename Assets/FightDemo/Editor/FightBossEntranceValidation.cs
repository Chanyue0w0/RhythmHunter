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
    internal static class FightBossEntranceValidation
    {
        const string Request="Temp/FightBossEntranceValidation.request",Result="Temp/FightBossEntranceValidation.result";
        static double deadline;
        static bool sawHidden,sawDoor,sawJump,sawLand;
        static int pauseStep;
        static double pauseUntil;
        static Vector3 pausedPosition;
        static double completedAt;
        static bool sawShake;
        static bool sawSmoke, sawReveal, sawFocus, sawSmokeParticles;
        static bool sawPartialReveal, sawLandingExplosion;
        static readonly Dictionary<FightUnitSlot,Vector3> positions=new();
        static FightBossEntranceValidation()=>EditorApplication.update+=Tick;
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static void Tick()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(!EditorApplication.isPlaying)
            {
                if(!EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=true;}
                return;
            }
            try
            {
                if(deadline==0)deadline=EditorApplication.timeSinceStartup+45;
                Check(EditorApplication.timeSinceStartup<deadline,"Intro timeout");
                Application.runInBackground=true;
                var intro=UnityEngine.Object.FindFirstObjectByType<FightBossEntrance>();
                if(intro==null)return;
                var fight=intro.GetComponent<FightCombatController>();var roster=intro.GetComponent<FightRosterManager>();
                if(positions.Count==0)
                    foreach(var slot in UnityEngine.Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None))
                        if(slot.Team==FightUnitSlot.UnitTeam.Enemy && slot.ActorRoot) positions[slot]=slot.ActorRoot.localPosition;
                if(intro.BlocksBattle)
                {
                    float mana=fight.TeamSkillGaugeExact;
                    foreach(var command in new[]{FightInputRouter.HeroCommand.Front,FightInputRouter.HeroCommand.Middle,FightInputRouter.HeroCommand.Back,FightInputRouter.HeroCommand.TeamSkill})fight.SubmitHeroCommand(command);
                    Check(fight.TeamSkillGaugeExact==mana,"Intro input gained MP");
                    Check(fight.ScheduledEnemyHitCount==0,"Enemy attacks during intro");
                    if(roster.ActiveEnemies.Count==0)sawHidden=true;
                    if(intro.Phase=="OpenIdle")
                    {
                        if(!sawDoor) ScreenCapture.CaptureScreenshot("Temp/BossDoorLightingPreview.png");
                        sawDoor=true;
                        Check(!UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsSortMode.None).Any(i=>i.name.StartsWith("BeatPoint_")&&i.enabled),"Intro beat points visible");
                        foreach(var slot in roster.ActiveEnemies)Check(slot.CombatAnimator.ActiveAnimation==slot.CombatAnimator.FindSequence("OpenIdle").Animation,"Wrong opening animation");
                        var block=new MaterialPropertyBlock();
                        roster.ActiveEnemies[0].CombatAnimator.TargetRenderer.GetPropertyBlock(block);
                        float reveal=block.GetFloat("_EntranceReveal");
                        if(reveal>.1f && reveal<.9f)sawPartialReveal=true;
                        const System.Reflection.BindingFlags instanceFlags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                        {
                            double revealStart=(double)typeof(FightBossEntrance).GetField("smokeStartBeat",instanceFlags).GetValue(intro);
                            double renderedBeat=(double)typeof(FightBossEntrance).GetField("lastPresentationBeat",instanceFlags).GetValue(intro);
                            float expectedReveal=Mathf.Clamp01((float)(renderedBeat-revealStart));
                            Check(Mathf.Abs(reveal-expectedReveal)<.001f,$"Reveal does not follow one beat fade: actual={reveal}, expected={expectedReveal}");
                        }
                        Check(block.GetFloat("_EntranceSilhouette")==1,"Opening silhouette missing");
                        var smoke=GameObject.Find("BossDoorSmoke_Runtime");
                        if(smoke && !sawSmokeParticles && smoke.GetComponentsInChildren<ParticleSystem>().Any(p=>p.particleCount>0 && p.time>.3f))
                        { sawSmokeParticles=true;ScreenCapture.CaptureScreenshot("Temp/BossDoorSmokePreview.png"); }
                        if(!sawDoor || !sawSmoke)
                        {
                            Check(smoke,"Boss appeared without simultaneous smoke");
                            Check(block.GetFloat("_EntranceReveal")<1,"Boss flashed before smoke reveal");
                            sawSmoke=true;
                        }
                        if(block.GetFloat("_EntranceReveal")>=.99f && !sawReveal)
                        { sawReveal=true; ScreenCapture.CaptureScreenshot("Temp/BossDoorRevealedPreview.png"); }
                        var focusCamera=UnityEngine.Object.FindFirstObjectByType<RhythmHunter.FightDemo.Art2.Art2CameraController>();
                        float focusWeight=(float)typeof(RhythmHunter.FightDemo.Art2.Art2CameraController).GetField("bossFocusWeight",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(focusCamera);
                        if(focusWeight>0)sawFocus=true;
                        Check(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l=>l.name=="DoorSpotLight"&&l.intensity>0),"Door light missing");
                        var doorLight=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l=>l.name=="DoorSpotLight");
                        Check(doorLight.shadows!=LightShadows.None,"Door shadows disabled");
                        foreach(var hero in roster.ActiveHeroes)
                            foreach(var renderer in hero.GetComponentsInChildren<Renderer>(true))
                            {
                                Check((renderer.renderingLayerMask & (1u<<7))==0,"Hero included in door light");
                                Check((renderer.renderingLayerMask & 1u)!=0,"Hero lost ordinary scene lighting");
                            }
                        Check((roster.ActiveEnemies[0].CombatAnimator.TargetRenderer.renderingLayerMask&(1u<<7))!=0,"Boss excluded from door shadows");
                        var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                        Check(pipeline && pipeline.supportsAdditionalLightShadows,"Pipeline additional shadows disabled");
                    }
                    if(intro.Phase=="Jump entrance")
                    {
                        sawJump=true;
                        Check(roster.ActiveEnemies[0].CombatAnimator.ActiveAnimation!=roster.ActiveEnemies[0].CombatAnimator.FindSequence("OpenIdle").Animation,"OpenIdle continued after takeoff");
                        var block=new MaterialPropertyBlock();roster.ActiveEnemies[0].CombatAnimator.TargetRenderer.GetPropertyBlock(block);
                        Check(block.GetFloat("_EntranceSilhouette")==0,"Silhouette not restored");
                        var pause=intro.GetComponent<FightPauseController>();
                        if(pauseStep==0) { pause.SetPaused(true); pauseStep=1; pauseUntil=EditorApplication.timeSinceStartup+.15; }
                        else if(pauseStep==1 && EditorApplication.timeSinceStartup>=pauseUntil) { pausedPosition=roster.ActiveEnemies[0].ActorRoot.position; pauseStep=2; pauseUntil=EditorApplication.timeSinceStartup+.35; }
                        else if(pauseStep==2 && EditorApplication.timeSinceStartup>=pauseUntil)
                        { Check(Vector3.Distance(pausedPosition,roster.ActiveEnemies[0].ActorRoot.position)<.001f,"Boss moved during pause"); pause.SetPaused(false);pauseStep=3; }
                    }
                    if(intro.Phase=="Landing recovery")
                    {
                        if(!sawLand)
                        {
                            Check(GameObject.Find("BossLandingSmoke_Runtime"),"Landing explosion missing");
                            sawLandingExplosion=true;
                        }
                        if(!sawLand)Check(UnityEngine.Object.FindObjectsByType<FightBattleEffect>(FindObjectsSortMode.None).Any(e=>e.name.StartsWith("GroundSmoke")),"Landing smoke missing");
                        sawLand=true;
                        var camera=UnityEngine.Object.FindFirstObjectByType<RhythmHunter.FightDemo.Art2.Art2CameraController>();
                        var offset=(Vector2)typeof(RhythmHunter.FightDemo.Art2.Art2CameraController).GetField("bossEntranceShake",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(camera);
                        if(offset.sqrMagnitude>0.000001f)sawShake=true;
                    }
                    foreach(var slot in roster.ActiveEnemies)
                    {
                        var jump=slot.CombatAnimator.FindSequence("Jump");
                        Check(jump==null||slot.CombatAnimator.ActiveAnimation!=jump.Animation,"Jump animation must not play");
                    }
                    return;
                }
                if(intro.Phase!="Complete"||fight.AwaitingBattleStart)return;
                if(completedAt==0)completedAt=EditorApplication.timeSinceStartup;
                if(EditorApplication.timeSinceStartup-completedAt<3)return;
                Check(sawShake,"Landing shake missing");
                Check(sawSmoke && sawReveal && sawFocus && sawSmokeParticles,"Smoke particles, reveal or focus missing");
                Check(sawPartialReveal && sawLandingExplosion,"One-beat fade or landing explosion missing");
                foreach(string shaderName in new[]{"RhythmHunter/Art2/BossDoorFan","RhythmHunter/Art2/SpriteCardLit"})
                    Check(!ShaderUtil.ShaderHasError(Shader.Find(shaderName)),"Shader compile error: "+shaderName);
                var cameraAfter=UnityEngine.Object.FindFirstObjectByType<RhythmHunter.FightDemo.Art2.Art2CameraController>();
                var offsetAfter=(Vector2)typeof(RhythmHunter.FightDemo.Art2.Art2CameraController).GetField("bossEntranceShake",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(cameraAfter);
                Check(offsetAfter==Vector2.zero,"Shake did not finish");
                Check((float)typeof(RhythmHunter.FightDemo.Art2.Art2CameraController).GetField("bossFocusWeight",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(cameraAfter)==0,"Camera focus not restored");
                Check(!GameObject.Find("BossDoorSmoke_Runtime"),"Door smoke not cleaned up");
                Check(sawHidden&&sawDoor&&sawJump&&sawLand,$"Missing intro phases hidden={sawHidden} door={sawDoor} jump={sawJump} land={sawLand}");
                Check(pauseStep==3,"Pause was not tested");
                Check(roster.ActiveEnemies.Count>0,"Boss never spawned");
                foreach(var slot in roster.ActiveEnemies)
                    if(positions.TryGetValue(slot,out var expected))Check(Vector3.Distance(slot.ActorRoot.localPosition,expected)<.001f,"Landing changed slot position");
                File.WriteAllText(Result,"PASS: simultaneous Boss/smoke, immediate linear one-beat opacity fade, second smoke on landing; camera restore, smoke cleanup, silhouette/door shadows/hero exclusion, takeoff Idle, landing shake and input locks.");
                Finish();
            }
            catch(Exception e){File.WriteAllText(Result,"FAIL: "+e);Finish();}
        }
        static void Finish(){File.Delete(Request);EditorApplication.isPlaying=false;deadline=completedAt=0;positions.Clear();pauseStep=0;sawPartialReveal=sawLandingExplosion=sawSmokeParticles=sawSmoke=sawReveal=sawFocus=sawShake=sawHidden=sawDoor=sawJump=sawLand=false;}
    }
}
