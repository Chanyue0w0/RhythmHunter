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
                        sawDoor=true;
                        Check(!UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsSortMode.None).Any(i=>i.name.StartsWith("BeatPoint_")&&i.enabled),"Intro beat points visible");
                        foreach(var slot in roster.ActiveEnemies)Check(slot.CombatAnimator.ActiveAnimation==slot.CombatAnimator.FindSequence("OpenIdle").Animation,"Wrong opening animation");
                        var block=new MaterialPropertyBlock();
                        roster.ActiveEnemies[0].CombatAnimator.TargetRenderer.GetPropertyBlock(block);
                        Check(block.GetFloat("_EntranceSilhouette")==1,"Opening silhouette missing");
                        Check(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l=>l.name=="DoorSpotLight"&&l.intensity>0),"Door light missing");
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
                var cameraAfter=UnityEngine.Object.FindFirstObjectByType<RhythmHunter.FightDemo.Art2.Art2CameraController>();
                var offsetAfter=(Vector2)typeof(RhythmHunter.FightDemo.Art2.Art2CameraController).GetField("bossEntranceShake",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(cameraAfter);
                Check(offsetAfter==Vector2.zero,"Shake did not finish");
                Check(sawHidden&&sawDoor&&sawJump&&sawLand,$"Missing intro phases hidden={sawHidden} door={sawDoor} jump={sawJump} land={sawLand}");
                Check(pauseStep==3,"Pause was not tested");
                Check(roster.ActiveEnemies.Count>0,"Boss never spawned");
                foreach(var slot in roster.ActiveEnemies)
                    if(positions.TryGetValue(slot,out var expected))Check(Vector3.Distance(slot.ActorRoot.localPosition,expected)<.001f,"Landing changed slot position");
                File.WriteAllText(Result,"PASS: black silhouette and door light; takeoff restores ordinary Idle and color; landing shake starts and returns to zero; deferred spawn, pause/resume, beat UI, smoke, MP/input locks and battle unlock.");
                Finish();
            }
            catch(Exception e){File.WriteAllText(Result,"FAIL: "+e);Finish();}
        }
        static void Finish(){File.Delete(Request);EditorApplication.isPlaying=false;deadline=completedAt=0;positions.Clear();pauseStep=0;sawShake=sawHidden=sawDoor=sawJump=sawLand=false;}
    }
}
