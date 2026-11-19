using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using RhythmHunter.FightDemo;
using RhythmHunter.FightDemo.Art2;
namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightForestEnvironmentValidation
    {
        static double until,deadline;static int stage;static bool errors;
        static Vector3 cloudPosition,rockPosition;
        static FightForestEnvironmentValidation(){EditorApplication.update+=Poll;Application.logMessageReceived+=(m,s,t)=>{if(File.Exists("Temp/ForestPlay.request")&&(t==LogType.Exception||t==LogType.Error))errors=true;};}
        static void Poll()
        {
            const string request="Temp/ForestPlay.request";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(!EditorApplication.isPlaying){if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.isPlaying=true;return;}
            try
            {
                Application.runInBackground=true;
                if(deadline==0)deadline=EditorApplication.timeSinceStartup+100;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Forest Play timeout");
                var fight=UnityEngine.Object.FindFirstObjectByType<FightCombatController>();if(!fight||fight.AwaitingBattleStart)return;
                if(until==0){until=EditorApplication.timeSinceStartup+5;return;}
                if(EditorApplication.timeSinceStartup<until)return;
                if(stage==1)
                {
                    var scenery=GameObject.Find("ForestEnvironment_2_5D").transform;
                    if(Vector3.Distance(cloudPosition,scenery.Find("Background_09").position)<.005f)throw new Exception("Clouds did not move");
                    if(Vector3.Distance(rockPosition,scenery.Find("Background_05").position)>.0001f)throw new Exception("Static scenery moved");
                    fight.GetComponent<FightPauseController>().RestartBattle();stage=2;until=0;return;
                }
                var plan=fight.GetComponent<FightLevelPlanner>();var selector=fight.GetComponent<FightEnvironmentSelector>();
                if(plan.ConfigurationError!=null||!plan.ActiveStage||!plan.ActiveStage.Tutorial||plan.StageIndex!=0||plan.Chapter.ChapterId!="Chapter01")throw new Exception("Wrong tutorial Stage");
                if(!selector.ActiveEnvironment||selector.ActiveEnvironment.EnvironmentId!="Forest")throw new Exception("Forest not applied");
                var forest=GameObject.Find("ForestEnvironment_2_5D");if(!forest)throw new Exception("Forest not active");
                var scrollers=forest.GetComponentsInChildren<LoopingBackgroundScroller>();
                if(scrollers.Length!=1||scrollers[0].name!="Background_09")throw new Exception("Only clouds may scroll");
                var caveLight=GameObject.Find("ShadowTestLight_MoveMe");
                if((caveLight&&caveLight.GetComponent<Light>().enabled)||GameObject.Find("Art2_Atmosphere"))throw new Exception("Cave lighting still active");
                if(!fight.FrontHero.UnitSlot.HasCharacter||!fight.SecondHero.UnitSlot.HasCharacter||!fight.ThirdHero.UnitSlot.HasCharacter)throw new Exception("Missing party");
                if(forest.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.sharedMaterial==null||!r.sharedMaterial.shader.isSupported))throw new Exception("Unsupported forest material");
                var floor=UnityEngine.Object.FindFirstObjectByType<Art2FloorOcclusion>();
                if(!floor||floor.floors.Length!=1||!floor.floors[0].transform.IsChildOf(forest.transform))throw new Exception("Incorrect ground binding");
                if(errors)throw new Exception("Console error during Forest validation; inspect Editor.log");
                ScreenCapture.CaptureScreenshot("Temp/ForestPlay"+stage+".png");
                if(stage==0){cloudPosition=forest.transform.Find("Background_09").position;rockPosition=forest.transform.Find("Background_05").position;stage=1;until=EditorApplication.timeSinceStartup+1;return;}
                Finish("PASS: Forest, party, materials, floor, Cave disabled, restart, moving clouds and stationary scenery; no logged errors.");
            }catch(Exception e){Finish("FAIL: "+e);}
        }
        static void Finish(string result)
        {
            File.WriteAllText("Temp/ForestPlay.result",result);File.Delete("Temp/ForestPlay.request");
            until=deadline=0;stage=0;errors=false;EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
        }
    }
}
