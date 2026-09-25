using System;
using System.IO;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    /// <summary>Opt-in live flow check. Uses a play-mode start override; never saves the user's open scenes.</summary>
    [InitializeOnLoad]
    public static class FightSceneFlowValidation
    {
        const string Request="Temp/FightSceneFlowValidation.request", Result="Temp/FightSceneFlowValidation.result";
        const string Running="FightFlowValidation.Running", Previous="FightFlowValidation.PreviousStartScene";
        static int phase;
        static double deadline, checkpoint;
        static int pausedTimeline;
        static float pausedHp, pausedArmor;
        static long pausedBeat;
        static bool suppliedBackground;
        static FightSceneFlowValidation()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Running,false))
                {
                    RestoreStartScene();
                    if(File.Exists(Request)){File.Delete(Request);File.AppendAllText(Result,"\nFAIL: test was interrupted.");}
                }
            };
        }
        [MenuItem("Rhythm Hunter/Validate Calibration To Battle And Pause")]
        public static void RequestRun(){File.WriteAllText(Request,"run");}

        static void Tick()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling)return;
            if(!EditorApplication.isPlaying)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                SessionState.SetString(Previous,AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(FightCalibrationSceneBuilder.ScenePath);
                SessionState.SetBool(Running,true);
                File.WriteAllText(Result,"Starting live calibration -> battle -> pause/resume check.\n");
                EditorApplication.isPlaying=true;return;
            }
            try
            {
                if(deadline==0)deadline=EditorApplication.timeSinceStartup+60;
                Require(EditorApplication.timeSinceStartup<deadline,"Flow validation timed out.");
                var clock=UnityEngine.Object.FindFirstObjectByType<FmodBeatClock>();
                if(clock==null)return;
                if(!suppliedBackground)
                {
                    Application.runInBackground=true;
                    FMODUnity.RuntimeManager.CoreSystem.mixerResume();
                    suppliedBackground=true;
                }
                if(!clock.IsPlaying)clock.StartMusic();
                var judge=UnityEngine.Object.FindFirstObjectByType<FmodRhythmJudge>();
                if(phase==0)
                {
                    var scene=UnityEngine.Object.FindFirstObjectByType<FightCalibrationScene>();
                    var calibration=UnityEngine.Object.FindFirstObjectByType<FightTimingCalibration>();
                    if(scene==null||calibration==null||!calibration.IsOpen||clock.ReceivedBeatCount<3)return;
                    Require(UnityEngine.Object.FindFirstObjectByType<FightCombatController>()==null,"Calibration scene must not run combat.");
                    var saved=RhythmCalibrationStore.Read();
                    Require(saved.keyboardDelayMs==PlayerPrefs.GetFloat("FightTiming.v1.Keyboard",0) && saved.gamepadDelayMs==PlayerPrefs.GetFloat("FightTiming.v1.Gamepad",0),"Migration must retain both original values.");
                    var disk=JsonUtility.FromJson<RhythmCalibrationStore.Data>(File.ReadAllText(RhythmCalibrationStore.FilePath));
                    Require(disk.keyboardDelayMs==saved.keyboardDelayMs && disk.gamepadDelayMs==saved.gamepadDelayMs,"Shared JSON must match saved profiles.");
                    File.AppendAllText(Result,$"CALIBRATION event={clock.MusicEventPath} BPM={clock.LatestBeat.Tempo} keyboard={saved.keyboardDelayMs:R} gamepad={saved.gamepadDelayMs:R} file={RhythmCalibrationStore.FilePath}\n");
                    ScreenCapture.CaptureScreenshot("Temp/FightCalibrationScene-live.png");
                    phase=1;checkpoint=EditorApplication.timeSinceStartup+1;return;
                }
                if(phase==1)
                {
                    if(EditorApplication.timeSinceStartup<checkpoint)return;
                    UnityEngine.Object.FindFirstObjectByType<FightCalibrationScene>().EnterBattle();
                    phase=2;suppliedBackground=false;return;
                }
                if(phase==2)
                {
                    if(SceneManager.GetActiveScene().name!="FightScene3"||clock.ReceivedBeatCount<3)return;
                    Require(UnityEngine.Object.FindFirstObjectByType<FightTimingCalibration>()==null,"Battle must not create calibration UI.");
                    Require(UnityEngine.Object.FindObjectsByType<FmodBeatClock>(FindObjectsSortMode.None).Length==1,"Scene transition must not duplicate music.");
                    foreach(string profile in new[]{"Keyboard","Gamepad"})
                    {
                        judge.SetInputProfile(profile);
                        Require(judge.PersonalDelayMs==RhythmCalibrationStore.GetDelay(profile),"Battle must apply the shared saved profile.");
                    }
                    judge.SetInputProfile("Keyboard");
                    UnityEngine.Object.FindFirstObjectByType<FightPauseController>().TogglePause();
                    Require(clock.IsPaused && Time.timeScale==0,"Pause must stop FMOD and game time.");
                    phase=3;checkpoint=EditorApplication.timeSinceStartup+.5;return;
                }
                if(phase==3)
                {
                    if(EditorApplication.timeSinceStartup<checkpoint)return;
                    clock.TryGetTimelinePositionMs(out pausedTimeline);pausedBeat=clock.ReceivedBeatCount;
                    var fight=UnityEngine.Object.FindFirstObjectByType<FightCombatController>();pausedHp=fight.PartyHp;pausedArmor=fight.PartyArmor;
                    fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Back);
                    ScreenCapture.CaptureScreenshot("Temp/FightPause-live.png");
                    phase=4;checkpoint=EditorApplication.timeSinceStartup+1;return;
                }
                if(phase==4)
                {
                    if(EditorApplication.timeSinceStartup<checkpoint)return;
                    clock.TryGetTimelinePositionMs(out int now);
                    var fight=UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                    Require(Math.Abs(now-pausedTimeline)<=1 && clock.ReceivedBeatCount==pausedBeat,"Music timeline and beat count must remain frozen.");
                    Require(fight.PartyHp==pausedHp && fight.PartyArmor==pausedArmor,"Pause must not resolve damage or recovery.");
                    UnityEngine.Object.FindFirstObjectByType<FightPauseController>().TogglePause();
                    Require(!clock.IsPaused && Time.timeScale==1,"Second toggle must resume music and game time.");
                    phase=5;checkpoint=EditorApplication.timeSinceStartup+1;return;
                }
                if(phase==5)
                {
                    if(EditorApplication.timeSinceStartup<checkpoint)return;
                    clock.TryGetTimelinePositionMs(out int now);
                    Require(now>pausedTimeline+500 && clock.ReceivedBeatCount>pausedBeat,"Playback must continue from the paused position.");
                    File.AppendAllText(Result,$"PASS: new scene, profile migration, both profile reads in FightScene3, single music instance, pause freezes timeline at {pausedTimeline} ms, resume advances to {now} ms.\n");
                    EditorSceneManager.LoadSceneInPlayMode("Assets/FightDemo/Scenes/FightScene.unity",new LoadSceneParameters(LoadSceneMode.Single));
                    phase=6;suppliedBackground=false;return;
                }
                if(phase==6)
                {
                    if(SceneManager.GetActiveScene().name!="FightScene"||clock.ReceivedBeatCount<3)return;
                    var scene=SceneManager.GetActiveScene();
                    int animatorCount=0;
                    foreach(var root in scene.GetRootGameObjects())
                    {
                        foreach(var transform in root.GetComponentsInChildren<Transform>(true))
                            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject)==0,"Art scene must not reference removed scripts.");
                        foreach(var animator in root.GetComponentsInChildren<FightCharacterCombatAnimator>())
                        {
                            Require(animator.TargetRenderer!=null && animator.FrameCount>0,"Art character must retain its renderer and idle frames.");
                            Require(animator.TargetRenderer.sharedMaterial.shader.name=="Universal Render Pipeline/2D/Sprite-Lit-Default","Art characters must retain natural lighting.");
                            animatorCount++;
                        }
                    }
                    Require(animatorCount>=6,"Art scene must retain all six animated characters.");
                    Require(UnityEngine.Object.FindFirstObjectByType<FightSceneNaturalLighting>()!=null,"Art lighting must initialize.");
                    var fight=UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                    int pulseCount=fight.GetComponent<FightEnvironmentController>().PulseCount;
                    Require(pulseCount>0,"Art beat pulse targets must initialize.");
                    ScreenCapture.CaptureScreenshot("Temp/FightArtMerge-live.png");
                    File.AppendAllText(Result,$"PASS: art scene runs with {animatorCount} animated characters, natural lighting, {pulseCount} pulse targets and no missing scripts.\n");
                    phase=7;checkpoint=EditorApplication.timeSinceStartup+.5;return;
                }
                if(phase==7)
                {
                    if(EditorApplication.timeSinceStartup<checkpoint)return;
                    Finish();
                }
            }
            catch(Exception e){File.AppendAllText(Result,"FAIL: "+e+"\n");Debug.LogException(e);Finish();}
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static void Finish()
        {
            File.Delete(Request);RestoreStartScene();EditorApplication.isPlaying=false;
            phase=0;deadline=0;suppliedBackground=false;
        }
        static void RestoreStartScene()
        {
            string previous=SessionState.GetString(Previous,"");
            EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool(Running,false);
        }
    }
}
