using System;
using System.IO;
using System.Reflection;
using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemoEditor
{
    internal static class FightTimingCalibrationValidation
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,Private).GetValue(o);
        static void Set(object o,string field,object v)=>o.GetType().GetField(field,Private).SetValue(o,v);
        static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Private).Invoke(o,args);
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}

        internal static void Run(FightCombatController fight,FightScenePresenter presenter,Action<Canvas> capture)
        {
            var samples=new RhythmTimingSamples();
            for(int i=0;i<4;i++)samples.Add(i,80,495);
            Check(samples.Count==0 && samples.WarmupRemaining==0,"Warmup must not bias the measured habit.");
            for(int i=4;i<36;i++)samples.Add(i,i==14?180:60+(i%3-1)*4,495);
            Check(samples.Reliable && Math.Abs(samples.MedianMs-60)<5 && samples.InlierCount==31,"Stable late taps and one outlier should recommend about +60 ms.");
            samples.Clear();
            for(int i=0;i<4;i++)samples.Add(i,0,495);
            for(int i=4;i<36;i++)samples.Add(i,-55+(i%3-1)*3,495);
            Check(samples.Reliable && Math.Abs(samples.MedianMs+55)<4,"Early taps require negative personal delay.");
            samples.Clear();
            Check(samples.Add(0,0,495) && !samples.Add(0,0,495),"Repeated same-beat taps must not fill calibration.");
            Check(!samples.Add(1,double.NaN,495) && !samples.Add(1,240,495),"Invalid and ambiguous half-beat taps must be rejected.");
            samples.Clear();
            for(int i=0;i<36;i++)samples.Add(i,i*3,495);
            Check(samples.Complete && !samples.Reliable,"A drifting sample set must not be saved as personal latency.");

            var judge=Get<FmodRhythmJudge>(fight,"rhythmJudge");
            var clock=Get<FmodBeatClock>(fight,"beatClock");
            var anchor=clock.LatestBeat;bool anchored=clock.HasTimingAnchor;
            float baseOffset=judge.VisualOffsetMs;
            var health=Get<Text>(presenter,"healthText");
            FightTimingCalibration ui=null;
            try
            {
                Set(clock,"hasAnchor",true);
                Set(clock,"latestBeat",new FmodBeatClock.BeatSnapshot(0,1,1,1000,121.15f,4,4));
                judge.EnablePersonalCalibration();judge.SetPersonalDelay(60,false);judge.ResetDuplicateTracking();
                var r=(FmodRhythmJudge.Result)Call(judge,"JudgeTimelinePosition",1060-Mathf.RoundToInt(baseOffset));
                Check(r.Judgement==FmodRhythmJudge.Grade.Perfect && Math.Abs(r.DeltaMs)<.001,"Late +60 ms habit must be corrected to zero, not doubled.");
                Check(judge.VisualOffsetMs==baseOffset,"Input compensation must leave musical visuals unchanged.");
                judge.SetPersonalDelay(-55,false);judge.ResetDuplicateTracking();
                r=(FmodRhythmJudge.Result)Call(judge,"JudgeTimelinePosition",945-Mathf.RoundToInt(baseOffset));
                Check(Math.Abs(r.DeltaMs)<.001,"Early habit compensation has the correct sign.");
                judge.SetPersonalDelay(0,false);judge.ResetDuplicateTracking();
                Call(judge,"JudgeTimelinePosition",1000-Mathf.RoundToInt(baseOffset));
                Call(judge,"JudgeTimelinePosition",1495-Mathf.RoundToInt(baseOffset));
                r=(FmodRhythmJudge.Result)Call(judge,"JudgeTimelinePosition",1000-Mathf.RoundToInt(baseOffset));
                Check(r.DuplicateBeat,"Revisiting an older accepted beat must not grant a second Basic.");
                judge.SetPersonalDelay(60,false);
                judge.SetInputProfile(judge.InputProfile);
                Check(judge.PersonalDelayMs==60,"Repeated same-device input must preserve an unsaved preview.");

                ui=fight.gameObject.AddComponent<FightTimingCalibration>();
                ui.Configure(fight,clock,judge,Get<FightInputRouter>(fight,"inputRouter"),health.canvas,health.font);
                Call(fight,"QueueEnemyAttack",new FmodBeatClock.BeatSnapshot(3,1,4,2486,121.15f,4,4));
                float hp=fight.PartyHp,armor=fight.PartyArmor;
                ui.Open();
                Check(fight.TimingCalibrationActive && !fight.HasPendingEnemyAttack,"Calibration must cancel pending damage and freeze combat.");
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Back);
                fight.AdvanceArmorRecovery(100);
                Check(fight.PartyHp==hp && fight.PartyArmor==armor,"Calibration taps cannot change party resources.");
                var collected=Get<RhythmTimingSamples>(ui,"samples");
                for(int i=0;i<36;i++)collected.Add(i,40+(i%3-1)*3,495);
                ui.Preview();Check(Math.Abs(judge.PersonalDelayMs-40)<4,"Preview should use measured habit.");
                Call(ui,"Refresh");capture(health.canvas);
                ui.Close();
                Check(!fight.TimingCalibrationActive && judge.PersonalDelayMs==60,"Closing an unsaved preview restores the previous offset and resumes combat.");
                File.WriteAllText("Temp/FightTimingCalibrationValidation.result","PASS: warmup, robust median/outlier rejection, early/late signs, duplicate taps, drift rejection, unchanged visual offset, out-of-order duplicate protection, preview preservation/cancel, combat suspension, calibration UI render.\n");
            }
            finally
            {
                if(ui!=null){ui.Close();UnityEngine.Object.DestroyImmediate(ui);}
                Set(clock,"hasAnchor",anchored);Set(clock,"latestBeat",anchor);
                Set(judge,"personalCalibrationEnabled",false);Set(judge,"profileLoaded",false);Set(judge,"personalDelayMs",0f);
                judge.ResetDuplicateTracking();
            }
        }
    }
}
