using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RhythmHunter.FightDemo;
namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightTrainingDummyValidation
    {
        const string Request="Temp/TrainingDummyPlay.request";
        static double deadline,finishAt;
        static long startBeat=-1;
        static float initialHp;
        static readonly Dictionary<Transform,Vector3> starts=new();
        static readonly HashSet<Transform> moved=new();
        static FightTrainingDummyValidation()=>EditorApplication.update+=Poll;
        static void Poll()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            try
            {
                if(!EditorApplication.isPlaying){if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.isPlaying=true;return;}
                Application.runInBackground=true;
                if(finishAt>0){if(EditorApplication.timeSinceStartup>finishAt)Finish("PASS: TrainingDummy takes damage, HP 99999, no attacks for 20 beats, dummy and four forest props move on shared clock; screenshot captured.");return;}
                if(deadline==0)deadline=EditorApplication.timeSinceStartup+60;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Dummy validation timeout");
                var fight=UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                if(!fight||fight.AwaitingBattleStart||fight.BattleEntranceRunning)return;
                var roster=fight.GetComponent<FightRosterManager>();var dummy=roster.ActiveEnemies.Single();
                if(startBeat<0)
                {
                    if(dummy.CharacterDefinition.CharacterId!="training_dummy"||dummy.CharacterDefinition.EnemyCanAttack||dummy.MaxHp!=99999)throw new Exception("Incorrect dummy configuration");
                    dummy.TakeDamage(2);if(dummy.CurrentHp!=99997)throw new Exception("Dummy not damageable");
                    initialHp=fight.PartyHp;startBeat=fight.NormalBattleBeatCount;
                    foreach(var manager in UnityEngine.Object.FindObjectsByType<SceneBeatMotionManager>(FindObjectsSortMode.None))
                    {
                        var data=new SerializedObject(manager);
                        if(data.FindProperty("beatClock").objectReferenceValue!=fight.BeatClock)throw new Exception("Wrong motion clock");
                        var targets=data.FindProperty("targets");
                        for(int i=0;i<targets.arraySize;i++)
                        {
                            var t=targets.GetArrayElementAtIndex(i).FindPropertyRelative("target").objectReferenceValue as Transform;
                            if(t)starts[t]=t.localPosition;
                        }
                    }
                    if(starts.Count!=5)throw new Exception("Expected dummy plus four forest motion targets");
                }
                foreach(var pair in starts)if(Vector3.Distance(pair.Key.localPosition,pair.Value)>.0001f)moved.Add(pair.Key);
                if(fight.PartyHp!=initialHp||fight.ScheduledEnemyHitCount>0)throw new Exception("Practice target attacked");
                if(fight.NormalBattleBeatCount-startBeat<20)return;
                if(moved.Count!=5)throw new Exception("Not all beat motion targets moved: "+moved.Count);
                ScreenCapture.CaptureScreenshot("Temp/TrainingDummy.png");finishAt=EditorApplication.timeSinceStartup+1;
            }
            catch(Exception e){Finish("FAIL: "+e);}
        }
        static void Finish(string message)
        {File.WriteAllText("Temp/TrainingDummyPlay.result",message);File.Delete(Request);startBeat=-1;deadline=finishAt=0;starts.Clear();moved.Clear();EditorApplication.isPlaying=false;}
    }
}
