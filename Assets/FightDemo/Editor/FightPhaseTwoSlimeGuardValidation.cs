using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using RhythmHunter.FightDemo;
namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightPhaseTwoSlimeGuardValidation
    {
        static bool requested;static int lane,stage,created;static double next,deadline;
        static FightPhaseTwoSlimeGuardValidation()=>EditorApplication.update+=Poll;
        static void Poll()
        {
            const string request="Temp/PhaseTwoSlimeGuardPlay.request";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(!EditorApplication.isPlaying){if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.isPlaying=true;return;}
            try
            {
                Application.runInBackground=true;
                if(deadline==0)deadline=EditorApplication.timeSinceStartup+90;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Guard validation timeout");
                var fight=UnityEngine.Object.FindFirstObjectByType<FightCombatController>();if(!fight||fight.AwaitingBattleStart)return;
                var battle=fight.GetComponent<FightBossRhythmBattle>();
                if(!requested){fight.GetComponent<FightBossStageDebug>().RequestPhaseTwoSkip();requested=true;return;}
                if(!fight.GetComponent<FightBossPhaseTwoTransition>().Ready||EditorApplication.timeSinceStartup<next)return;
                if(stage==0)
                {
                    if(lane==0)created=battle.CreatedVisuals;
                    fight.SubmitHeroCommand((FightInputRouter.HeroCommand)lane);stage=1;next=EditorApplication.timeSinceStartup+.1;return;
                }
                var visual=battle.GetComponentsInChildren<FightBossRhythmVisual>().FirstOrDefault(v=>v.name.StartsWith("BossPhaseTwo_SlimeGuard")&&v.gameObject.activeInHierarchy);
                if(!visual||visual.GetComponentsInChildren<ParticleSystem>().Length==0)throw new Exception("Missing shield lane "+lane);
                var slot=new[]{fight.FrontHero.UnitSlot,fight.SecondHero.UnitSlot,fight.ThirdHero.UnitSlot}[lane];
                var emitter=visual.GetComponentsInChildren<ParticleSystem>().First(p=>p.name=="VFX_Shield_01_Color_Loop_Static");
                Vector3 body=slot.CombatAnimator.TargetRenderer.bounds.center;
                var foot=slot.ActorInstance.transform.Find("FootAnchor");
                var settings=new SerializedObject(battle);
                var camera=settings.FindProperty("view").objectReferenceValue as Camera;
                if(foot&&camera)body+=camera.transform.right*Vector3.Dot(foot.position-body,camera.transform.right);
                // Honour the user's authored per-lane adjustments rather than assuming
                // the initial zero-offset setup is still in use.
                var offset=settings.FindProperty("guardCameraOffset").vector3Value;
                var laneOffsets=settings.FindProperty("guardLaneOffsets");
                if(lane<laneOffsets.arraySize)offset+=laneOffsets.GetArrayElementAtIndex(lane).vector3Value;
                body+=settings.FindProperty("guardEffectOffset").vector3Value+(camera?camera.transform.TransformDirection(offset):offset);
                var environment=fight.GetComponent<FightEnvironmentSelector>();
                if(environment&&environment.ActiveEnvironment!=fight.GetComponent<FightLevelPlanner>().SelectedEnvironment)
                    throw new Exception("Stage environment did not apply before combat");
                if(Vector3.Distance(emitter.transform.position,body)>.3f)throw new Exception($"Shield emitter offset from body {lane}: emitter={emitter.transform.position}, body={body}");
                if(battle.CreatedVisuals!=created)throw new Exception("Guard did not reuse prewarmed pool");
                ScreenCapture.CaptureScreenshot("Temp/PhaseTwoSlimeGuardLane"+lane+".png");
                lane++;stage=0;next=EditorApplication.timeSinceStartup+1;
                if(lane==3)Finish("PASS: all three shield emitter centres track hero body centres; prewarmed pool reused.");
            }
            catch(Exception e){Finish("FAIL: "+e);}
        }
        static void Finish(string result)
        {
            File.WriteAllText("Temp/PhaseTwoSlimeGuardPlay.result",result);File.Delete("Temp/PhaseTwoSlimeGuardPlay.request");
            requested=false;lane=stage=created=0;next=deadline=0;EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
        }
    }
}
