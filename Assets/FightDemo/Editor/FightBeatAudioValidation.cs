using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using FMOD;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightBeatAudioValidation
    {
        const string Request="Temp/BeatAudioValidation.request";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static FightBossRhythmChartWindow window;
        static FightBossRhythmChart chart, originalChart;
        static double deadline, pausedUntil, pausedPosition, firstEnergy=-1;
        static bool pauseDone;
        static float previousOffset, testOffset;
        static int step;
        static object Get(string n)=>typeof(FightBossRhythmChartWindow).GetField(n,Flags).GetValue(window);
        static void Set(string n,object v)=>typeof(FightBossRhythmChartWindow).GetField(n,Flags).SetValue(window,v);
        static void Call(string n,params object[] args)=>typeof(FightBossRhythmChartWindow).GetMethod(n,Flags).Invoke(window,args);
        static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
        static FightBeatAudioValidation()=>EditorApplication.update+=Tick;
        [MenuItem("Rhythm Hunter/Validation/Phase Two Beat Audio")]
        private static void RequestTest() { if (!EditorApplication.isPlayingOrWillChangePlaymode) File.WriteAllText(Request,"run"); }
        static void Tick()
        {
            if(File.Exists(Request))File.WriteAllText("Temp/BeatAudioValidation.state",$"play={EditorApplication.isPlayingOrWillChangePlaymode} compile={EditorApplication.isCompiling} update={EditorApplication.isUpdating} step={step}");
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            try
            {
                if(step==0)
                {
                    deadline=EditorApplication.timeSinceStartup+30;
                    chart=ScriptableObject.CreateInstance<FightBossRhythmChart>();
                    foreach(double t in new[]{10.2,10.5,10.8})chart.notes.Add(new FightBossRhythmChart.Note{useFreeTime=true,timeSeconds=t});
                    float.TryParse(File.ReadAllText(Request), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out testOffset);
                    window=EditorWindow.GetWindow<FightBossRhythmChartWindow>(); previousOffset=(float)Get("auditionSoundOffsetMs"); Set("auditionSoundOffsetMs",testOffset);originalChart=(FightBossRhythmChart)Get("chart");Set("chart",chart);Call("LoadSong");
                    Set("autoAudition",true);Set("auditionStartBeat",15f);Set("auditionEndBeat",23f);Set("repeatAudition",false);Set("cursor",7.5d);Call("Play");step=1;return;
                }
                Check(EditorApplication.timeSinceStartup<deadline,"Audio test timeout");
                Call("Tick");
                var scheduler=(FightBeatSoundScheduler)Get("auditionSounds");
                if(scheduler==null)return;
                double cursor=(double)Get("cursor");
                var music=(FMOD.Studio.EventInstance)Get("preview");music.getPaused(out bool musicPaused);music.getPlaybackState(out var musicState);music.getTimelinePosition(out int musicRaw);
                FMODUnity.EditorUtils.System.getCoreSystem(out var core);core.getOutput(out var output);core.getChannelsPlaying(out int total,out int real);core.getMasterChannelGroup(out var master);master.getDSPClock(out ulong dspClock,out _);
                File.WriteAllText("Temp/BeatAudioValidation.progress",$"step={step} cursor={cursor:F3} scheduled={scheduler.ScheduledCount} immediate={scheduler.ImmediateCount} ready={scheduler.Ready} {scheduler.LastResult} energy={firstEnergy} native={musicRaw}/{musicState}/paused={musicPaused} output={output} channels={total}/{real} dsp={dspClock}");
                var voices=(IEnumerable)typeof(FightBeatSoundScheduler).GetField("voices",Flags).GetValue(scheduler);
                foreach(var voice in voices)
                {
                    var group=(ChannelGroup)voice.GetType().GetField("group").GetValue(voice);
                    if(!group.hasHandle()||group.getDSP(CHANNELCONTROL_DSP_INDEX.TAIL,out var dsp)!=RESULT.OK)continue;
                    dsp.setMeteringEnabled(false,true);
                    if(dsp.getMeteringInfo(IntPtr.Zero,out var meter)==RESULT.OK && firstEnergy<0)
                        for(int channel=0;channel<meter.numchannels;channel++)if(meter.peaklevel[channel]>.001f){firstEnergy=cursor;break;}
                }
                if(!pauseDone && (pausedUntil>0 || (scheduler.ScheduledCount>0 && cursor<10.15)))
                {
                    if(pausedUntil==0){Call("Play");pausedUntil=EditorApplication.timeSinceStartup+.5;return;}
                    if(EditorApplication.timeSinceStartup<pausedUntil-.2){pausedPosition=cursor;return;}
                    if(EditorApplication.timeSinceStartup<pausedUntil)return;
                    Check(Math.Abs(cursor-pausedPosition)<.02,"Music advanced while paused");Call("Play");pauseDone=true;
                }
                if(cursor<11.3)return;
                Check(scheduler.ScheduledCount==3,"Not all three notes scheduled");
                Check(firstEnergy>=10.1 && firstEnergy<=10.5,"Audible DSP onset not aligned: "+firstEnergy);
                Check(pauseDone,"Pause case not exercised");
                scheduler.PlayAt(cursor*1000-40,cursor*1000,1,.3f,true,25);
                Check(scheduler.ImmediateCount==1,"Late fallback missing");
                File.WriteAllText("Temp/BeatAudioValidation.result",$"PASS: 3 music timeline schedules; offset {testOffset:F1}ms; first metered output at section {firstEnergy:F3}s for 10.200s target; pause/resume no duplicate; late fallback; isolated editor audition.");
                Finish();
            }
            catch(Exception e){File.WriteAllText("Temp/BeatAudioValidation.result","FAIL: "+e);Finish();}
        }
        static void Finish()
        {
            if(window){Call("Stop");Set("auditionSoundOffsetMs",previousOffset);Set("autoAudition",false);Set("chart",originalChart);}
            if(chart)UnityEngine.Object.DestroyImmediate(chart);
            step=0;pausedUntil=0;pauseDone=false;firstEnergy=-1;File.Delete(Request);
        }
    }
}







