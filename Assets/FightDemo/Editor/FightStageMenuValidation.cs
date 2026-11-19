using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using RhythmHunter.FightDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightStageMenuValidation
    {
        const string Request="Temp/StageMenu.request", Key="StageMenu.TestStep";
        static double readyAt,deadline;
        static int lastStep=-1;
        static FightStageMenuValidation()=>EditorApplication.update+=Poll;
        static FightChapterDefinition Chapter(int n)=>AssetDatabase.LoadAssetAtPath<FightChapterDefinition>($"Assets/FightDemo/Levels/Chapter0{n}.asset");
        static void Poll()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            try
            {
                var plan=UnityEngine.Object.FindFirstObjectByType<FightLevelPlanner>();if(!plan)return;
                int step=SessionState.GetInt(Key,0);
                if(!EditorApplication.isPlaying)
                {
                    if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                    var data=new SerializedObject(plan);var chapters=data.FindProperty("selectableChapters");chapters.arraySize=2;
                    chapters.GetArrayElementAtIndex(0).objectReferenceValue=Chapter(1);chapters.GetArrayElementAtIndex(1).objectReferenceValue=Chapter(2);
                    data.FindProperty("chapter").objectReferenceValue=Chapter(1);data.FindProperty("stageIndex").intValue=0;data.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(plan.gameObject.scene);EditorSceneManager.SaveScene(plan.gameObject.scene);
                    SessionState.SetInt(Key,0);lastStep=-1;EditorApplication.isPlaying=true;return;
                }
                if(lastStep!=step){lastStep=step;readyAt=EditorApplication.timeSinceStartup+3;deadline=readyAt+30;}
                Application.runInBackground=true;
                if(EditorApplication.timeSinceStartup<readyAt)return;
                var pause=plan.GetComponent<FightPauseController>();
                if(!pause||!pause.MenuReady){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Pause UI initialization timeout");return;}
                if(step==6){Finish("PASS: pause UI Stage arrows/load, Mage/Skeleton/Boss/Warrior reloads, invalid empty Stage rejected, restart retains Boss, environments and party sizes correct; time restored.");return;}
                if(!plan.ActiveStage){if(EditorApplication.timeSinceStartup>deadline)throw new Exception(plan.ConfigurationError??"Stage init timeout");return;}
                var roster=plan.GetComponent<FightRosterManager>();
                if(Time.timeScale!=1)throw new Exception("Reload left time paused");
                int chapter=step==3||step==4?2:1,index=step==1?1:step==2||step==3||step==4?2:0;
                if(plan.Chapter!=Chapter(chapter)||plan.StageIndex!=index)throw new Exception($"Step {step}: wrong Chapter/Stage");
                if(roster.ActiveHeroes.Count!=(chapter==1?1:3))throw new Exception($"Step {step}: wrong party after reload: {roster.ActiveHeroes.Count}, chapter {plan.Chapter.name}, tutorial {roster.IsTutorialStage}, prefabs {string.Join(",",roster.HeroPrefabs.Select(h=>h?h.name:"empty"))}");
                if(plan.GetComponent<FightEnvironmentSelector>().ActiveEnvironment.EnvironmentId!=(chapter==1?"Forest":"Cave"))throw new Exception("Wrong environment after reload");
                pause.SetPaused(true);
                if(!pause.IsPaused){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Pause unavailable");return;}
                var overlay=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).SelectMany(c=>c.GetComponentsInChildren<RectTransform>()).First(t=>t.name=="PauseOverlay");
                if(step==5){ScreenCapture.CaptureScreenshot("Temp/StageMenu.png");SessionState.SetInt(Key,6);return;}
                if(step==0)
                {
                    var next=overlay.GetComponentsInChildren<Button>().First(b=>b.name==">"&&Mathf.Approximately(((RectTransform)b.transform).anchoredPosition.y,-75));
                    next.onClick.Invoke();
                    SessionState.SetInt(Key,1);
                    overlay.GetComponentsInChildren<Button>().Single(b=>b.name=="LOAD SELECTED STAGE").onClick.Invoke();return;
                }
                SessionState.SetInt(Key,step+1);
                if(step==3){pause.RestartBattle();return;}
                if(step==2&&pause.TryLoadStage(Chapter(2),0,out _))throw new Exception("Unassigned stage accepted");
                if(!pause.TryLoadStage(Chapter(step==2?2:1),step==4?0:2,out var error))throw new Exception(error);
            }
            catch(Exception e){Finish("FAIL: "+e);}
        }
        static void Finish(string result)
        {
            File.WriteAllText("Temp/StageMenu.result",result);File.Delete(Request);SessionState.EraseInt(Key);lastStep=-1;
            EditorApplication.isPlaying=false;
        }
    }
}
