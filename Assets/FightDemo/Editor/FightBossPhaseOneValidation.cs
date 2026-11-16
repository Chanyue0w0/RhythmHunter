using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmHunter.FightDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightBossPhaseOneValidation
    {
        const string Request="Temp/BossPhaseOneValidation.request",Result="Temp/BossPhaseOneValidation.result";
        static double deadline,waitUntil;
        static int phase,deaths,completions,extraEvents;
        static FightUnitSlot boss;
        const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        static FightBossPhaseOneValidation()=>EditorApplication.update+=Tick;
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static void Tick()
        {
            const string setup="Temp/BossPhaseOneSetup.request";
            if(File.Exists(setup) && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                File.Delete(setup);
                try
                {
                    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
                    var fight=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<FightCombatController>(true)).Single();
                    var debug=fight.GetComponent<FightBossStageDebug>()??Undo.AddComponent<FightBossStageDebug>(fight.gameObject);
                    var so=new SerializedObject(debug);so.FindProperty("fight").objectReferenceValue=fight;so.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                    File.WriteAllText("Temp/BossPhaseOneSetup.result","PASS: FightDemoController / FightBossStageDebug installed; debug flags default off.");
                }
                catch(Exception e){File.WriteAllText("Temp/BossPhaseOneSetup.result","FAIL: "+e);}
            }
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(!EditorApplication.isPlaying)
            {if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.isPlaying=true;return;}
            try
            {
                Application.runInBackground=true;
                if(deadline==0)deadline=EditorApplication.timeSinceStartup+100;
                Check(EditorApplication.timeSinceStartup<deadline,"Phase one test timeout");
                var fight=UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                if(!fight || fight.AwaitingBattleStart)return;
                if(phase==0)
                {
                    boss=fight.RosterManager.ActiveEnemies.FirstOrDefault(x=>x.BossPhaseOneProtected);
                    Check(boss,"Protected boss missing");
                    boss.EnemyDefeated+=(slot,position)=>deaths++;
                    boss.BossPhaseOneCompleted+=slot=>completions++;
                    boss.BossPhaseOneExtraDamageReceived+=(slot,amount,kind)=>extraEvents++;
                    boss.RestoreFullHealth();
                    foreach(var ability in boss.CharacterDefinition.EnemyAbilities)ability.selectionWeight=ability.name=="Strike"?1:0;
                    var start=typeof(FightCombatController).GetMethod("TryStartAuthoredEnemyAttack",Flags);
                    Check((bool)start.Invoke(fight,new object[]{boss}),"Attack could not start");
                    Check(fight.ScheduledEnemyHitCount>0,"Attack had no queued hits");
                    float initial=boss.CurrentHp;
                    Check(Math.Abs(boss.TakeDamage(10000)- (initial-1))<.001f,"HP-loss return should stop at one");
                    Check(boss.CurrentHp==1 && boss.BossPhaseOneTired && deaths==0 && completions==1,"Lethal clamp/success failed");
                    Check(fight.ScheduledEnemyHitCount==0,"Unlaunched hits survived success");
                    Check(boss.CombatAnimator.ActiveAnimation==FightCharacterCombatAnimator.CombatAnimation.Hit,"Completion hit reaction missing");
                    Check(boss.Heal(30)==0 && boss.CurrentHp==1,"Tired Boss healed");
                    Check(!(bool)start.Invoke(fight,new object[]{boss}),"Tired Boss selected another action");
                    Check(!fight.ScheduleEnemyHits(boss,new[]{1},2),"Tired Boss accepted scheduled attack");
                    foreach(FightUnitSlot.DamageKind kind in Enum.GetValues(typeof(FightUnitSlot.DamageKind)))
                    {Check(boss.TakeDamage(2,kind)==0,"Extra damage reduced HP");Check(boss.CurrentHp==1,"Floor failed");}
                    Check(extraEvents==3 && boss.BossPhaseOneExtraDamage==6 && completions==1,"Reward hook or success repeated incorrectly");
                    boss.ApplyFragile();boss.TakeDamage(1);
                    Check(!boss.IsFragile && boss.BossPhaseOneExtraDamage==8,"Fragile at HP floor failed");
                    // Use existing sprite only in the runtime test to verify future named idle wiring.
                    var tired=boss.CombatAnimator.FindSequence("Idle_Tired")??boss.CombatAnimator.AddCustomSequence("Idle_Tired");
                    if(tired.Frames.Count==0)
                    {tired.Configure(FightCharacterCombatAnimator.CombatAnimation.NormalAttack,new[]{boss.CombatAnimator.TargetRenderer.sprite},16,-1,-1,-1);tired.ConfigureFrameBeats(new[]{1f});}
                    waitUntil=EditorApplication.timeSinceStartup+2;phase=1;
                }
                if(phase==1 && EditorApplication.timeSinceStartup>=waitUntil)
                {
                    Check(boss && boss.ActorInstance && boss.CurrentHp==1 && !boss.DeathPresentationPending && deaths==0,"Boss removed or killed");
                    Check(!boss.CombatAnimator.IsPlaying && boss.CombatAnimator.DisplayedFrame.Source==(int)boss.CombatAnimator.FindSequence("Idle_Tired").Animation,"Hit did not return to tired idle");
                    Check(fight.ScheduledEnemyHitCount==0,"New actions while tired");
                    boss.RestoreFullHealth();Check(!boss.BossPhaseOneTired && boss.BossPhaseOneExtraDamage==0 && boss.CurrentHp==boss.MaxHp,"Reset did not clear success");
                    var debug=fight.GetComponent<FightBossStageDebug>();
                    debug.RequestPhaseTwoSkip();Check(boss.CurrentHp==1 && debug.PhaseTwoSkipRequested,"Debug request failed");
                    Check(debug.ConsumePhaseTwoSkipRequest() && !debug.PhaseTwoSkipRequested,"Debug request not consumable");
                    boss.ReleaseBossPhaseOneProtection();Check(!boss.BossPhaseOneTired && !boss.BossPhaseOneProtected,"Phase-two release failed");
                    File.WriteAllText(Result,"PASS: one-HP floor, one success/no deaths, attack cancellation, no new attacks/heal, direct/DoT/secondary damage floor, extra-damage event, fragile, Hit->Idle_Tired, full reset and debug skip request. Runtime placeholder only; prefab artwork unchanged.");
                    Finish();
                }
            }
            catch(Exception e){File.WriteAllText(Result,"FAIL: "+e);Finish();}
        }
        static void Finish(){File.Delete(Request);EditorApplication.isPlaying=false;deadline=waitUntil=0;phase=0;}
    }
}
