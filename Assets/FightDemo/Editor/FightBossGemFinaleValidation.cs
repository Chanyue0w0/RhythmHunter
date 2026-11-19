using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using RhythmHunter.FightDemo;
namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightBossGemFinaleValidation
    {
        const string Request="Temp/GemFinalePlay.request";
        static double deadline,nextPress,deathAt;static bool requested,chargeShot,beamShot,death,weak;static int missed,bad;
        static double pauseUntil;static bool pauseChecked;
        static Vector3 leftBefore;static bool savedLeft;
        static bool sawImpactFlash,sawImpactShake,haveBossPosition;static Vector3 previousBossPosition;
        static float[] initialFootY;static string footReport;
        static FightBossGemFinaleValidation()=>EditorApplication.update+=Poll;
        static void Poll()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(!EditorApplication.isPlaying){if(!EditorApplication.isPlayingOrWillChangePlaymode){weak=File.ReadAllText(Request).Contains("weak");EditorApplication.isPlaying=true;}return;}
            try
            {
                weak=File.ReadAllText(Request).Contains("weak");
                Application.runInBackground=true;
                if(deadline==0)deadline=EditorApplication.timeSinceStartup+130;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Finale play validation timeout");
                var fight=UnityEngine.Object.FindFirstObjectByType<FightCombatController>();if(!fight||fight.AwaitingBattleStart)return;
                bool quick=File.ReadAllText(Request).Contains("quick");
                var battle=fight.GetComponent<FightBossRhythmBattle>();var finale=fight.GetComponent<FightBossGemFinale>();
                if(!requested)
                {
                    var so=new SerializedObject(battle);so.FindProperty("preventPlayerDeathForTesting").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
                    if(quick){var test=new SerializedObject(finale);test.FindProperty("startBeat").floatValue=20;test.FindProperty("fireBeat").floatValue=22;test.ApplyModifiedPropertiesWithoutUndo();}
                    fight.GetComponent<FightBossStageDebug>().RequestPhaseTwoSkip();requested=true;return;
                }
                if(!fight.GetComponent<FightBossPhaseTwoTransition>().Ready)return;
                if(quick)
                {
                    var slots=new[]{fight.FrontHero.UnitSlot,fight.SecondHero.UnitSlot,fight.ThirdHero.UnitSlot};
                    if(!finale.Active&&initialFootY==null)
                    {
                        initialFootY=new float[3];footReport="";
                        for(int i=0;i<3;i++)
                        {
                            var foot=slots[i].ActorInstance.transform.Find("FootAnchor");initialFootY[i]=foot.position.y;
                            footReport+=$"hero {i}: rootY={slots[i].ActorRoot.position.y:F4}, footY={foot.position.y:F4}, sprite={slots[i].CombatAnimator.TargetRenderer.sprite.name}\n";
                        }
                    }
                    if(finale.Fired&&battle.Boss)
                    {
                        var flash=battle.Boss.GetComponentInChildren<FightHitFlashController>();
                        sawImpactFlash|=flash&&flash.IsFlashing;
                        var position=battle.Boss.ActorRoot.position;
                        if(haveBossPosition)sawImpactShake|=Vector3.Distance(position,previousBossPosition)>.001f;
                        previousBossPosition=position;haveBossPosition=true;
                    }
                    if(finale.Accepting&&!weak&&finale.PressCount<32)fight.SubmitHeroCommand(FightInputRouter.HeroCommand.TeamSkill);
                    if(finale.Active&&!finale.Fired&&!chargeShot&&battle.CurrentBeat>21){ScreenCapture.CaptureScreenshot("Temp/GemFinaleNewCharge.png");chargeShot=true;}
                    if(finale.Fired&&battle.CurrentBeat>24){
                        var beam=GameObject.Find("FinaleGemUltRay_Runtime");
                        if(!beam||!beam.GetComponent<FightGemBeamVolume>()||!beam.transform.Find("BeamVolume3D"))throw new Exception("Missing beam volume");
                        var overlay=beam.GetComponent<RhythmHunter.FightDemo.Art2.Art2EffectPresentation>();
                        if(!overlay||!overlay.DrawAboveScene)throw new Exception("Missing late overlay registration");
                        if(!UnityEngine.Object.FindFirstObjectByType<FightGemFinaleGlare>())throw new Exception("Missing camera glare");
                        if(!sawImpactFlash||!sawImpactShake)throw new Exception("Missing Boss beat impact flash/shake");
                        if(finale.BeamDamagePulses<2||battle.Boss.CurrentHp<=0)throw new Exception("Beam did not repeat damage feedback or killed Boss early");
                        for(int i=0;i<3;i++)
                        {
                            float footY=slots[i].ActorInstance.transform.Find("FootAnchor").position.y;
                            if(initialFootY==null||Mathf.Abs(footY-initialFootY[i])>.01f)throw new Exception("Gather changed foot height: "+i);
                            footReport+=$"hero {i} gathered footY={footY:F4}\n";
                        }
                        File.WriteAllText("Temp/GemFinaleFeet.result",footReport);
                        var front=GameObject.Find("GemAboveGlare");var prompt=GameObject.Find("PromptAboveGlare");
                        if(!front||!prompt||front.transform.GetSiblingIndex()<=0)throw new Exception("Missing foreground gem/prompt");
                        foreach(var particle in beam.GetComponentsInChildren<ParticleSystem>(false))
                            if(particle.name=="Sparks around")throw new Exception("Long tearing sparks still active");
                        ScreenCapture.CaptureScreenshot("Temp/GemFinaleNewBeam.png");Finish("PASS: weak beam visual geometry preview (runtime accelerated beats only).");}
                    return;
                }
                if(!savedLeft){leftBefore=fight.FrontHero.UnitSlot.ActorRoot.position;savedLeft=true;}
                if(battle.CurrentBeat<95 && finale.Active)throw new Exception("Finale started before beat 95");
                if(finale.Active && !chargeShot)
                {
                    missed=battle.MissedNotes;bad=battle.BadInputs;
                    fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Front);
                    if(battle.BadInputs!=bad)throw new Exception("Lane input still judged in finale");
                    chargeShot=true;
                }
                if(finale.Fired&&fight.CurrentBossMusicPhase!=FightCombatController.BossMusicPhase.PhaseTwoMusicEnded&&battle.Boss&&battle.Boss.CurrentHp<=0)
                    throw new Exception("Boss died before music end");
                if(finale.Active&&!pauseChecked)
                {
                    var pause=fight.GetComponent<FightPauseController>();
                    if(pauseUntil==0)
                    {
                        int before=finale.PressCount;pause.SetPaused(true);fight.SubmitHeroCommand(FightInputRouter.HeroCommand.TeamSkill);
                        if(finale.PressCount!=before)throw new Exception("Pause accepted input");
                        pauseUntil=EditorApplication.timeSinceStartup+.4;return;
                    }
                    if(EditorApplication.timeSinceStartup<pauseUntil)return;
                    pause.SetPaused(false);pauseChecked=true;
                }
                if(finale.Accepting&&!weak&&finale.PressCount<32&&EditorApplication.timeSinceStartup>=nextPress)
                {
                    int previous=finale.PressCount;fight.SubmitHeroCommand(FightInputRouter.HeroCommand.TeamSkill);
                    if(finale.PressCount!=previous+1)throw new Exception("Free A did not count");
                    nextPress=EditorApplication.timeSinceStartup+.05;
                }
                if(finale.Active&&battle.CurrentBeat>99&&!beamShot&&!finale.Fired)
                {
                    if(battle.MissedNotes!=missed)throw new Exception("Notes still deal damage during finale");
                    if(Vector3.Distance(leftBefore,fight.FrontHero.UnitSlot.ActorRoot.position)<.1f)throw new Exception("Heroes did not gather");
                    ScreenCapture.CaptureScreenshot("Temp/GemFinaleCharge"+(weak?"Weak":"Strong")+".png");
                    beamShot=true;
                }
                if(finale.Fired && !death && battle.CurrentBeat>105)
                {
                    var beam=GameObject.Find("FinaleGemUltRay_Runtime");if(!beam)throw new Exception("No beam at beat 104");
                    if(weak&&finale.PressCount!=0)throw new Exception("Zero-input test received input");
                    if(!weak&&finale.Charge<.99f)throw new Exception("Charge did not reach full");
                    ScreenCapture.CaptureScreenshot("Temp/GemFinaleBeam"+(weak?"Weak":"Strong")+".png");
                    death=true;
                }
                if(fight.CurrentBossMusicPhase==FightCombatController.BossMusicPhase.PhaseTwoMusicEnded)
                {
                    if(deathAt==0)deathAt=EditorApplication.timeSinceStartup;
                    if(EditorApplication.timeSinceStartup-deathAt<.65)return;
                    if(GameObject.Find("FinaleGemUltRay_Runtime"))throw new Exception("Beam persists beyond one beat after Boss death");
                    if(battle.Boss&&battle.Boss.CurrentHp>0)throw new Exception("Boss not executed");
                    if(!chargeShot||!beamShot||!death)throw new Exception("Missing presentation stages");
                    Finish("PASS "+(weak?"zero-input weak":"32-press full")+": no lane judgement/damage after 95; heroes gathered; beam after 104; song-end execution; beam removed within one beat. CompletedPressCount="+finale.CompletedPressCount);
                }
            }catch(Exception e){Finish("FAIL: "+e);}
        }
        static void Finish(string text)
        {
            File.WriteAllText("Temp/GemFinalePlay"+(weak?"Weak":"Strong")+".result",text);File.Delete(Request);
            deadline=nextPress=deathAt=pauseUntil=0;pauseChecked=false;requested=chargeShot=beamShot=death=savedLeft=false;
            sawImpactFlash=sawImpactShake=haveBossPosition=false;
            initialFootY=null;footReport=null;
            EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
        }
    }
}
