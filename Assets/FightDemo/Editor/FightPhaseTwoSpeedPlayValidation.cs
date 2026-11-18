using System;
using System.IO;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;
namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightPhaseTwoSpeedPlayValidation
    {
        const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        static double deadline,waitUntil;
        static int step;
        static float pulse;
        static FightCombatController fight;
        static FightPhaseTwoSpeedLines speed;
        static FightPhaseTwoSpeedPlayValidation()=>EditorApplication.update+=Tick;
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static object Get(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
        static void Tick()
        {
            if(!File.Exists("Temp/PhaseTwoSpeedPlay.request")||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(!EditorApplication.isPlaying){if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.isPlaying=true;return;}
            try
            {
                Application.runInBackground=true;
                if(deadline==0)deadline=EditorApplication.timeSinceStartup+90;
                Check(EditorApplication.timeSinceStartup<deadline,"Timeout step "+step);
                if(!fight)fight=UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                if(!fight||fight.AwaitingBattleStart)return;
                if(!speed)speed=fight.GetComponent<FightPhaseTwoSpeedLines>();
                var rhythm=fight.GetComponent<FightBossRhythmBattle>();
                Check(speed,"Speed component missing");
                if(step==0)
                {
                    Check(!speed.GetComponentInChildren<FightTunnelAirflow>(true),"First phase generated speed lines");
                    fight.GetComponent<FightBossStageDebug>().RequestPhaseTwoSkip();step=1;return;
                }
                if(step==1)
                {
                    if(rhythm.LaunchedNotes==0) { Check((float)Get(speed,"visibility")==0,"Airflow appeared before first note launch"); return; }
                    if(!rhythm.Running || (float)Get(speed,"visibility")<.9f || !fight.GetComponent<FightBossPhaseTwoTransition>().Ready)return;
                    var airflow=speed.GetComponentInChildren<FightTunnelAirflow>(true);Check(airflow&&airflow.GetComponent<MeshRenderer>().sharedMaterial,"World mesh");
                    Check(!speed.GetComponentInChildren<Canvas>(true),"Old overlay still exists");
                    var callback=(Action<int,bool,double>)Get(rhythm,"NoteResolved");Check(callback!=null,"Not subscribed");
                    int hits=rhythm.Hits,miss= rhythm.MissedNotes;
                    callback.Invoke(0,false,rhythm.CurrentBeat);Check(speed.GuardBoostRemaining==0,"Miss boosted");
                    callback.Invoke(0,true,rhythm.CurrentBeat);Check(speed.GuardBoostRemaining>0,"Success did not boost");
                    callback.Invoke(0,true,rhythm.CurrentBeat);Check(speed.GuardBoostRemaining<=.4001f,"Unbounded stacking");
                    Check(rhythm.Hits==hits&&rhythm.MissedNotes==miss,"Visual handler changed judgement");
                    pulse=speed.GuardBoostRemaining;fight.BeatClock.SetPaused(true);waitUntil=EditorApplication.timeSinceStartup+.3;step=2;return;
                }
                if(step==2)
                {
                    if(EditorApplication.timeSinceStartup<waitUntil)return;
                    Check(Mathf.Abs(speed.GuardBoostRemaining-pulse)<.001f,"Pause advanced pulse");
                    fight.BeatClock.SetPaused(false);Set(speed,"guardBoostEnabled",false);
                    Set(speed,"sectionIntensityEnabled",false);Set(speed,"constantIntensity",.7f);
                    waitUntil=EditorApplication.timeSinceStartup+.2;step=3;return;
                }
                if(step==3)
                {
                    if(EditorApplication.timeSinceStartup<waitUntil)return;
                    Check(speed.GuardBoostRemaining==0&&Mathf.Abs(speed.CurrentIntensity-.7f)<.001f,"Inspector toggles");
                    ScreenCapture.CaptureScreenshot("Temp/PhaseTwoTunnelPreview.png");
                    Set(speed,"speedLinesEnabled",false);waitUntil=EditorApplication.timeSinceStartup+.5;step=4;return;
                }
                if(step==4)
                {
                    if(EditorApplication.timeSinceStartup<waitUntil)return;
                    var airflow=speed.GetComponentInChildren<FightTunnelAirflow>(true);Check(airflow&&!airflow.gameObject.activeSelf,"Disable fade cleanup");
                    speed.enabled=false;
                    var callback=(Action<int,bool,double>)Get(rhythm,"NoteResolved");
                    if(callback!=null)foreach(var item in callback.GetInvocationList())Check(!ReferenceEquals(item.Target,speed),"Subscription leaked");
                    Finish("PASS: no phase-one effect; phase-two world-space tunnel mesh; no overlay canvas; success-only bounded pulse; pause freeze; constant intensity/boost/master switches; fade cleanup and unsubscribe; judgement counts unchanged.");
                }
            }
            catch(Exception e){Finish("FAIL: "+e);}
        }
        static void Finish(string text)
        {
            if(fight&&fight.BeatClock)fight.BeatClock.SetPaused(false);
            File.Delete("Temp/PhaseTwoSpeedPlay.request");File.WriteAllText("Temp/PhaseTwoSpeedPlay.result",text);
            deadline=waitUntil=0;step=0;fight=null;speed=null;EditorApplication.isPlaying=false;
        }
    }
}
