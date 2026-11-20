using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightPauseController
    {
        readonly List<FightChapterDefinition> chapters=new();
        readonly List<int> stageIndices=new();
        int chapterChoice,stageChoice;
        Text chapterLabel,stageLabel,selectionHint;
        Button loadStage;
        void BuildStageSelector(Transform parent,Font font)
        {
            var planner=GetComponent<FightLevelPlanner>();
            if(!planner)return;
            foreach(var item in planner.SelectableChapters)if(item&&!chapters.Contains(item))chapters.Add(item);
            if(planner.Chapter&&!chapters.Contains(planner.Chapter))chapters.Add(planner.Chapter);
            chapterChoice=Mathf.Max(0,chapters.IndexOf(planner.Chapter));
            chapterLabel=FightMenuUi.Label(parent,"",new Vector2(0,-10),new Vector2(620,42),font,24);Center(chapterLabel.rectTransform);
            stageLabel=FightMenuUi.Label(parent,"",new Vector2(0,-75),new Vector2(620,52),font,20);Center(stageLabel.rectTransform);
            void Arrow(string label,int x,int y,UnityEngine.Events.UnityAction action)
            {var b=FightMenuUi.Button(parent,label,new Vector2(x,y),new Vector2(60,44),font,action);Center((RectTransform)b.transform);}
            Arrow("<",-355,-10,()=>ChangeChapter(-1));Arrow(">",355,-10,()=>ChangeChapter(1));
            Arrow("<",-355,-75,()=>ChangeStage(-1));Arrow(">",355,-75,()=>ChangeStage(1));
            loadStage=FightMenuUi.Button(parent,"進入所選關卡",new Vector2(0,-155),new Vector2(370,46),font,LoadSelection);Center((RectTransform)loadStage.transform);
            selectionHint=FightMenuUi.Label(parent,"",new Vector2(0,-199),new Vector2(1040,54),font,17);Center(selectionHint.rectTransform);
            PopulateStages();stageChoice=Mathf.Max(0,stageIndices.IndexOf(planner.StageIndex));RefreshStageLabels();
        }
        void PopulateStages()
        {
            stageIndices.Clear();stageChoice=0;
            if(chapters.Count==0)return;
            var chapter=chapters[chapterChoice];
            for(int i=0;i<chapter.Stages.Count;i++)if(chapter.Stages[i])stageIndices.Add(i);
        }
        void ChangeChapter(int delta)
        {if(chapters.Count==0)return;chapterChoice=(chapterChoice+delta+chapters.Count)%chapters.Count;PopulateStages();RefreshStageLabels();}
        void ChangeStage(int delta)
        {if(stageIndices.Count==0)return;stageChoice=(stageChoice+delta+stageIndices.Count)%stageIndices.Count;RefreshStageLabels();}
        void RefreshStageLabels()
        {
            chapterLabel.text=chapters.Count>0?chapters[chapterChoice].DisplayName:"NO CHAPTERS";
            loadStage.interactable=stageIndices.Count>0;
            if(stageIndices.Count==0){stageLabel.text="NO CONFIGURED STAGES";selectionHint.text="Assign Chapters on Fight Level Planner.";return;}
            var chapter=chapters[chapterChoice];var stage=chapter.Stages[stageIndices[stageChoice]];
            stageLabel.text=$"Stage {stageIndices[stageChoice]+1}  |  {stage.DisplayName}";
            var environment=stage.Environment?stage.Environment:chapter.DefaultEnvironment;
            selectionHint.text=$"{(stage.Tutorial?"教學":stage.BossStage?"首領戰":"戰鬥")}  /  {(environment?environment.DisplayName:"洞窟")}\n進入所選關卡將重新開始該關卡。";
        }
        void LoadSelection()
        {
            if(stageIndices.Count==0)return;
            if(!TryLoadStage(chapters[chapterChoice],stageIndices[stageChoice],out string error))selectionHint.text=error;
        }
        public bool TryLoadStage(FightChapterDefinition chapter,int index,out string error)
        {
            if(!Application.isPlaying||restarting||string.IsNullOrEmpty(gameObject.scene.path))
            {error="Stage loading requires a saved, running battle scene.";return false;}
#if !UNITY_EDITOR
            if(!Application.CanStreamedLevelBeLoaded(gameObject.scene.path))
            {error="Battle scene is missing from the build scene list.";return false;}
#endif
            var planner=GetComponent<FightLevelPlanner>();
            if(!planner){error="No Level Planner in this scene.";return false;}
            if(!planner.TrySelectForReload(chapter,index,out error))return false;
            RestartBattle();return true;
        }
    }
}
