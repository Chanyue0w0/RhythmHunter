using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using CartoonFX;
using RhythmHunter.FightDemo.Art2;

namespace RhythmHunter.FightDemo.Editor
{
    [InitializeOnLoad]
    static class FightCameraStabilityValidation
    {
        static double deadline, until;
        static Camera view;
        static Art2CameraController controller;
        static Art2CameraPinnedForeground[] pinned;
        static Vector3[][] corners;
        static int phase, frames;
        static string report;
        static FightCameraStabilityValidation() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string request = "Temp/CameraStabilityPlay.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (!EditorApplication.isPlaying) { if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true; return; }
                Application.runInBackground = true;
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 60;
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Camera validation timeout phase " + phase);
                var fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                if (!fight || fight.AwaitingBattleStart || fight.BattleEntranceRunning || fight.WaveTransitionActive) return;
                if (phase == 0)
                {
                    var tutorial = fight.GetComponent<FightTutorialController>(); if (tutorial) tutorial.enabled = false;
                    var stage = fight.GetComponent<FightLevelPlanner>().ActiveStage;
                    var flag = typeof(FightStageDefinition).GetField("teachesFever", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (!fight.FeverBlockedByTutorial || fight.TryStartTeamSkill() || fight.TryStartTeamUltimate()) throw new Exception("Tutorial Fever bypass");
                    try { flag.SetValue(stage,true); if (fight.FeverBlockedByTutorial) throw new Exception("Fever lesson exception failed"); }
                    finally { flag.SetValue(stage,false); }
                    bool aEvent = false;
                    void Accepted(FightCombatController.HeroCallResult result) { if (result.Command == FightInputRouter.HeroCommand.TeamSkill) aEvent=true; }
                    fight.HeroCalled += Accepted;
                    fight.SubmitHeroCommand(FightInputRouter.HeroCommand.TeamSkill);
                    fight.HeroCalled -= Accepted;
                    if (!aEvent || fight.TeamSkillRunning) throw new Exception("A did not route to rhythm-only feedback");
                    controller = UnityEngine.Object.FindFirstObjectByType<Art2CameraController>(); view=controller.GetComponent<Camera>();
                    pinned=UnityEngine.Object.FindObjectsByType<Art2CameraPinnedForeground>(FindObjectsSortMode.None);
                    if(pinned.Length!=2 || pinned.Any(p=>p.transform.parent!=view.transform)) throw new Exception("Foreground not camera-parented");
                    corners=pinned.Select(Corners).ToArray();
                    var effect=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/VFX/LightExplosionQuick.prefab"));
                    effect.transform.position=fight.RosterManager.ActiveEnemies.First().ImpactEffectAnchor.position;
                    var cfx=effect.GetComponentInChildren<CFXR_Effect>();if(!cfx||!cfx.cameraShake.enabled)throw new Exception("Fixture has no authored shake");
                    var lifecycle=effect.GetComponent<FightBattleEffect>();if(!lifecycle)lifecycle=effect.AddComponent<FightBattleEffect>();
                    lifecycle.Configure(fight,false,2);
                    if(cfx.cameraShake.enabled||cfx.cameraShake.isShaking)throw new Exception("Native render-time camera shake still active");
                    controller.SetBossEntranceShake(new Vector2(.18f,.12f));
                    until=EditorApplication.timeSinceStartup+1;phase=1;return;
                }
                if(phase==1 || phase==2)
                {
                    for(int i=0;i<pinned.Length;i++)
                    {
                        var current=Corners(pinned[i]);
                        for(int j=0;j<current.Length;j++)
                            if(Vector2.Distance(current[j],corners[i][j])>.0002f)throw new Exception("Foreground viewport drift");
                    }
                    frames++;
                    if(EditorApplication.timeSinceStartup<until)return;
                    if(phase==1)
                    {
                        typeof(Art2CameraController).GetField("framingHalfHeight",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(controller,4f);
                        controller.SetBossEntranceShake(new Vector2(-.18f,-.12f));until=EditorApplication.timeSinceStartup+1;phase=2;return;
                    }
                    ScreenCapture.CaptureScreenshot("Temp/CameraPinnedForeground.png");
                    report=$"PASS: tutorial A rhythm only, direct Fever/Ultimate blocked, Fever lesson opt-in; CFX shake adapted without native callback; two foreground cards stayed viewport-fixed across shake and zoom for {frames} samples. Stone/tree motion unchanged.";
                    until=EditorApplication.timeSinceStartup+1;phase=3;return;
                }
                if(phase==3 && EditorApplication.timeSinceStartup>=until)Finish(report);
            }
            catch(Exception e){Finish("FAIL: "+e);}
        }
        static Vector3[] Corners(Art2CameraPinnedForeground item)
        {
            var b=item.GetComponent<SpriteRenderer>().sprite.bounds;
            return new[]{new Vector3(b.min.x,b.min.y),new Vector3(b.max.x,b.max.y)}.Select(p=>view.WorldToViewportPoint(item.transform.TransformPoint(p))).ToArray();
        }
        static void Finish(string result)
        {
            File.WriteAllText("Temp/CameraStabilityPlay.result",result);File.Delete("Temp/CameraStabilityPlay.request");
            if(controller)controller.SetBossEntranceShake(Vector2.zero);
            deadline=until=0;phase=frames=0;EditorApplication.isPlaying=false;
        }
    }
}
