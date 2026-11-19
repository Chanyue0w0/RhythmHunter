using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmHunter.FightDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightSoloTutorialSetup
    {
        const string Request="Temp/SoloTutorial.request";
        const string StepKey="SoloTutorial.ValidationStep";
        const string Levels="Assets/FightDemo/Levels/";
        static double deadline;
        static FightSoloTutorialSetup()=>EditorApplication.update+=Poll;
        static FightLevelPlanner Planner()=>UnityEngine.Object.FindFirstObjectByType<FightLevelPlanner>();
        static void Select(int index)
        {
            var plan=new SerializedObject(Planner());
            plan.FindProperty("chapter").objectReferenceValue=AssetDatabase.LoadAssetAtPath<FightChapterDefinition>(Levels+"Chapter01.asset");
            plan.FindProperty("stageIndex").intValue=index;plan.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(Planner().gameObject.scene);
            EditorSceneManager.SaveScene(Planner().gameObject.scene);
        }
        static void Setup()
        {
            string[] names={"LongSwordWarrior_Weapon","Mage","Skeleton"};
            var chapter=AssetDatabase.LoadAssetAtPath<FightChapterDefinition>(Levels+"Chapter01.asset");
            var data=new SerializedObject(chapter);var stages=data.FindProperty("stages");stages.arraySize=Math.Max(3,stages.arraySize);
            for(int i=0;i<3;i++)
            {
                string path=Levels+$"Chapter01_Stage0{i+1}_Tutorial.asset";
                if(i>0&&!AssetDatabase.LoadAssetAtPath<FightStageDefinition>(path))
                    AssetDatabase.CopyAsset(Levels+"Chapter01_Stage01_Tutorial.asset",path);
                var stage=AssetDatabase.LoadAssetAtPath<FightStageDefinition>(path);var fields=new SerializedObject(stage);
                fields.FindProperty("stageId").stringValue=$"Chapter01_Stage0{i+1}";
                fields.FindProperty("displayName").stringValue=$"Chapter 1 - Stage {i+1} - {names[i]} Tutorial";
                fields.FindProperty("tutorial").boolValue=true;
                fields.FindProperty("tutorialHero").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/FightDemo/Prefabs/ArtBattle/{names[i]}.prefab").GetComponent<FightCharacterDefinition>();
                fields.ApplyModifiedProperties();
                if(!stage.Validate(out var error))throw new Exception(error);
                stages.GetArrayElementAtIndex(i).objectReferenceValue=stage;
            }
            data.ApplyModifiedProperties();AssetDatabase.SaveAssets();Select(0);
            // Test stage switching without mutating the live scene or its default roster.
            var preview=EditorSceneManager.NewPreviewScene();
            var root=new GameObject("Tutorial roster validation");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,preview);
            try
            {
                var source=Planner().GetComponent<FightRosterManager>();var roster=root.AddComponent<FightRosterManager>();
                EditorUtility.CopySerialized(source,roster);var defaults=roster.HeroPrefabs.ToArray();
                foreach(var stage in chapter.Stages.Take(3))
                {
                    if(!roster.TryConfigureStage(stage,out var error))throw new Exception(error);
                    if(roster.HeroPrefabs[0]!=stage.TutorialHero||roster.HeroPrefabs[1]||roster.HeroPrefabs[2])throw new Exception("Solo snapshot failed");
                    if(roster.TrySwapHeroes(FightRosterManager.PartyPosition.Front,FightRosterManager.PartyPosition.Middle))throw new Exception("Tutorial formation may not move");
                }
                var boss=AssetDatabase.LoadAssetAtPath<FightStageDefinition>(Levels+"Chapter02_Stage03_Boss.asset");
                if(!roster.TryConfigureStage(boss,out var bossError)||!roster.HeroPrefabs.SequenceEqual(defaults))throw new Exception("Default party restoration failed: "+bossError);
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
        }
        static void Poll()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            try
            {
                int step=SessionState.GetInt(StepKey,-1);
                if(!EditorApplication.isPlaying)
                {
                    if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                    if(step<0){Setup();step=0;SessionState.SetInt(StepKey,0);}
                    if(step>=3){Select(0);Finish("PASS: all three solo tutorials start with only the correct Front hero; default three-hero party restored for Boss; scene returned to Chapter01 Stage01.");return;}
                    Select(step);deadline=0;EditorApplication.isPlaying=true;return;
                }
                if(step<0)throw new Exception("Stop Play before requesting tutorial setup");
                if(deadline==0)deadline=EditorApplication.timeSinceStartup+45;
                var plan=Planner();var fight=plan.GetComponent<FightCombatController>();var roster=plan.GetComponent<FightRosterManager>();
                if(!roster.BattleStarted)
                {
                    if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Solo tutorial could not start: "+plan.ConfigurationError);
                    return;
                }
                if(roster.ActiveHeroes.Count!=1||roster.GetHeroAtPosition(FightRosterManager.PartyPosition.Front)?.CharacterDefinition.CharacterId!=plan.ActiveStage.TutorialHero.CharacterId||fight.SecondHero.UnitSlot||fight.ThirdHero.UnitSlot)
                    throw new Exception("Wrong tutorial roster in Play");
                if(Mathf.Abs(fight.MaxPartyHp-fight.FrontHero.UnitSlot.MaxHp)>.001f)throw new Exception("Solo shared HP incorrect");
                SessionState.SetInt(StepKey,step+1);EditorApplication.isPlaying=false;
            }
            catch(Exception e){Finish("FAIL: "+e);if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;}
        }
        static void Finish(string result)
        {
            File.WriteAllText("Temp/SoloTutorial.result",result);File.Delete(Request);SessionState.EraseInt(StepKey);deadline=0;
        }
    }
}
