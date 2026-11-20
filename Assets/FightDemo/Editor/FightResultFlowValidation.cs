using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo.Editor
{
    [InitializeOnLoad]
    static class FightResultFlowValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static FightResultFlowValidation() { EditorApplication.update += Poll; }
        static void Set(object target,string field,object value) => target.GetType().GetField(field,Private).SetValue(target,value);
        static void Property(object target,string field,object value) => Set(target,"<"+field+">k__BackingField",value);
        static object Call(object target,string method,params object[] args) => target.GetType().GetMethod(method,Private).Invoke(target,args);
        static void Check(bool condition,string message) { if (!condition) throw new Exception(message); }
        static void Poll()
        {
            const string path = "Temp/ResultFlowValidation.request";
            if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(path);
            try { Run(); File.WriteAllText("Temp/ResultFlowValidation.result","PASS: rating thresholds, armor damage latch, healing cannot restore S, overtime latch/cap, tutorial no rank; next stage skips holes/opt-outs/empty chapters and crosses chapters, no wrap; results reveal in order and stay locked >=3s, wait for submit release; scoped navigation and disabled entries; title/value layout."); }
            catch(Exception e) { File.WriteAllText("Temp/ResultFlowValidation.result","FAIL: "+e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Validate Result Flow")]
        static void Run()
        {
            using var cleanup = new Cleanup();
            Check(FightStageResults.Evaluate(true,false,false,1)==FightStageResults.Grade.S,"S");
            Check(FightStageResults.Evaluate(true,true,false,.75f)==FightStageResults.Grade.A,"A threshold");
            Check(FightStageResults.Evaluate(true,true,false,.5f)==FightStageResults.Grade.B,"B threshold");
            Check(FightStageResults.Evaluate(true,true,false,.49f)==FightStageResults.Grade.C,"C");
            Check(FightStageResults.Evaluate(false,false,false,1)==FightStageResults.Grade.D,"D even full HP timeout");
            Check(FightStageResults.Evaluate(true,false,true,1)==FightStageResults.Grade.B,"Overtime S capped B");
            Check(FightStageResults.Evaluate(true,true,true,.25f)==FightStageResults.Grade.C,"Overtime C not promoted");
            Check(FightStageResults.Evaluate(true,true,false,1)==FightStageResults.Grade.A,"Healing cannot restore S");
            var source=AssetDatabase.LoadAssetAtPath<FightStageDefinition>("Assets/FightDemo/Levels/Chapter01_Stage03_Tutorial.asset");
            Check(source && source.Validate(out _),"Valid test stage source");
            var stage=cleanup.Asset(UnityEngine.Object.Instantiate(source));
            var reserved=cleanup.Asset(UnityEngine.Object.Instantiate(source)); Set(reserved,"includeInProgression",false);
            var a=cleanup.Asset(ScriptableObject.CreateInstance<FightChapterDefinition>());
            var empty=cleanup.Asset(ScriptableObject.CreateInstance<FightChapterDefinition>());
            var b=cleanup.Asset(ScriptableObject.CreateInstance<FightChapterDefinition>());
            Set(a,"stages",new List<FightStageDefinition>{stage,null,reserved});
            Set(b,"stages",new List<FightStageDefinition>{null,stage});
            var root=cleanup.Root("Result flow test"); root.SetActive(false);
            var planner=root.AddComponent<FightLevelPlanner>(); Set(planner,"chapter",a);Set(planner,"selectableChapters",new[]{a,empty,b});
            Check(planner.TryFindNextStage(out var chapter,out int index)&&chapter==b&&index==1,"Cross chapter / holes / reserved");
            Set(planner,"chapter",b);Set(planner,"stageIndex",1);
            Check(!planner.TryFindNextStage(out _,out _),"No wrapping at final stage");
            Set(planner,"chapter",a);Set(planner,"stageIndex",0);
            var chapterTwo = AssetDatabase.LoadAssetAtPath<FightChapterDefinition>("Assets/FightDemo/Levels/Chapter02.asset");
            Check(chapterTwo && chapterTwo.Stages.Count >= 2, "Chapter 2 stages exist");
            Set(planner,"chapter",chapterTwo); Set(planner,"stageIndex",0);
            Set(planner,"selectableChapters",new[]{chapterTwo});
            Check(planner.TryFindNextStage(out var nextChapter,out int nextIndex) &&
                nextChapter == chapterTwo && nextIndex == 1, "Chapter 2 Stage 1 advances to Stage 2");
            var results=root.AddComponent<FightStageResults>();
            Call(results,"OnDamageTaken",1f,0f);Check(results.TookDamage,"Armor damage disqualifies S");
            Call(results,"OnDamageTaken",0f,0f);Check(results.TookDamage,"Damage latch persists");
            Call(results,"OnOvertime");Check(results.UsedOvertime,"Overtime latch");
            Property(results,"IsTutorial",true);Check(results.RatingText=="訓練完成","Tutorial not ranked");
            Property(results,"IsTutorial",false);Property(results,"Won",true);Property(results,"Completed",true);
            Property(results,"ClearedWaves",2);Property(results,"TotalWaves",3);Property(results,"RemainingHp",6f);Property(results,"MaximumHp",8f);
            var panel=root.AddComponent<FightVictoryPanel>();Set(panel,"results",results);Call(panel,"Show");
            var nav=root.GetComponentInChildren<FightMenuNavigation>(true);
            Check(nav && nav.GetComponents<CanvasGroup>().Length == 1, "Result actions use one required CanvasGroup");
            var buttons=root.GetComponentsInChildren<Button>(true);
            Check(buttons.Length==2 && buttons[0].GetComponentInChildren<Text>().text=="下一場戰鬥","Next first, retry second");
            Check(!panel.ActionsReady&&!nav.Ready,"Initially locked");
            var values=(List<Text>)typeof(FightVictoryPanel).GetField("values",Private).GetValue(panel);
            Call(panel,"AdvanceReveal",.04f);
            Check(values[0].text.Length>0&&values[1].text.Length==0,"Sequential typewriter");
            Call(panel,"AdvanceReveal",2.9f);Check(!nav.Ready,"Minimum 3 seconds");
            Call(panel,"AdvanceReveal",10f);Check(nav.Ready&&!panel.ActionsReady,"Still requires release / arming");
            Check(values[4].text=="3 / 4" && values[5].text==results.RatingText,"Hearts and rank");
            Canvas.ForceUpdateCanvases();
            foreach(var value in values) Check(value.preferredHeight<=value.rectTransform.rect.height,"Visible value height");
            // A result locked by the presentation cannot be submitted even through a direct callback.
            Call(panel,"Restart");Check(!panel.ActionsReady,"Restart must not arm menu");
            Call(nav,"AdvanceConfirmation",true,100);
            Call(nav,"AdvanceConfirmation",true,200);Check(!nav.Armed,"Held A cannot arm");
            Call(nav,"AdvanceConfirmation",false,201);Check(!nav.Armed,"Release frame cannot submit");
            Call(nav,"AdvanceConfirmation",false,202);Check(nav.Armed,"Release arms on following frame");
            nav.Ready=false;Call(nav,"AdvanceConfirmation",false,203);Check(!nav.Armed,"Lock resets arming");
            // Independent scope proves nearest buttons, excluding disabled targets and external UI.
            var scope=cleanup.Root("Navigation scope");
            var menu=scope.AddComponent<FightMenuNavigation>();
            var left=MakeButton(scope.transform,"left",new Vector2(-200,0));
            var right=MakeButton(scope.transform,"right",new Vector2(200,0));
            var down=MakeButton(scope.transform,"down",new Vector2(-200,-100));
            Set(menu,"buttons",new[]{left,right,down});
            Check((Button)Call(menu,"Nearest",left,Vector2.right)==right,"Right navigation");
            Check((Button)Call(menu,"Nearest",left,Vector2.down)==down,"Down navigation");
            down.interactable=false;Check(Call(menu,"Nearest",left,Vector2.down)==null,"Disabled option skipped");
            // Defeat has only retry, even when a next stage exists.
            var lossRoot=cleanup.Root("Defeat flow test");lossRoot.SetActive(false);
            var loss=lossRoot.AddComponent<FightStageResults>();Property(loss,"Completed",true);Property(loss,"Rating",FightStageResults.Grade.D);
            var lossPanel=lossRoot.AddComponent<FightVictoryPanel>();Set(lossPanel,"results",loss);Call(lossPanel,"Show");
            Check(lossRoot.GetComponentsInChildren<Button>(true).Length==1,"Defeat has only retry");
            Check(!lossPanel.ActionsReady,"Defeat fade locked");Call(lossPanel,"AdvancePresentation",20f);
            Check(!lossPanel.ActionsReady,"Fade completion alone does not unlock");
        }
        static Button MakeButton(Transform parent,string name,Vector2 position)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button)); obj.transform.SetParent(parent,false);
            ((RectTransform)obj.transform).anchoredPosition=position;return obj.GetComponent<Button>();
        }
        sealed class Cleanup : IDisposable
        {
            readonly Scene scene=EditorSceneManager.NewPreviewScene();
            readonly List<UnityEngine.Object> assets=new();
            public T Asset<T>(T item) where T:UnityEngine.Object { assets.Add(item);return item; }
            public GameObject Root(string name) {var root=new GameObject(name);SceneManager.MoveGameObjectToScene(root,scene);return root;}
            public void Dispose() {EditorSceneManager.ClosePreviewScene(scene);foreach(var asset in assets)UnityEngine.Object.DestroyImmediate(asset);}
        }
    }
}
