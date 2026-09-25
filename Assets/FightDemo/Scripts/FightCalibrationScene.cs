using RhythmHunter.RhythmDemo;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    public sealed class FightCalibrationScene : MonoBehaviour
    {
        [SerializeField] FmodBeatClock beatClock;
        [SerializeField] FmodRhythmJudge rhythmJudge;
        [SerializeField] FightInputRouter inputRouter;
        [SerializeField] Canvas canvas;
        [SerializeField] Font font;
        [SerializeField] string battleSceneName = "FightScene3";
        FightTimingCalibration calibration;
        RectTransform cueRoot;
        Image center;
        Image[] points;
        bool enteringBattle;

        public void Configure(FmodBeatClock clock,FmodRhythmJudge judge,FightInputRouter router,Canvas ui,Font uiFont)
        { beatClock=clock;rhythmJudge=judge;inputRouter=router;canvas=ui;font=uiFont; }

        void Start()
        {
            rhythmJudge.EnablePersonalCalibration();
            calibration=gameObject.AddComponent<FightTimingCalibration>();
            calibration.Configure(null,beatClock,rhythmJudge,inputRouter,canvas,font,EnterBattle);
            var title=FightMenuUi.Label(canvas.transform,"RHYTHM CALIBRATION",new Vector2(0,-80),new Vector2(1000,64),font,36);
            title.rectTransform.anchorMin=title.rectTransform.anchorMax=title.rectTransform.pivot=new Vector2(.5f,1);
            var replay=FightMenuUi.Button(canvas.transform,"REPLAY MUSIC",new Vector2(-30,-30),new Vector2(220,42),font,ReplayMusic);
            var replayRect=(RectTransform)replay.transform;
            replayRect.anchorMin=replayRect.anchorMax=replayRect.pivot=Vector2.one;
            cueRoot=FightMenuUi.Rect("BeatCues",canvas.transform,new Vector2(0,120),new Vector2(840,50));
            cueRoot.anchorMin=cueRoot.anchorMax=new Vector2(.5f,0);cueRoot.pivot=new Vector2(.5f,.5f);
            center=Cue("Center",new Vector2(24,36));
            points=new Image[6];
            for(int i=0;i<points.Length;i++)points[i]=Cue("Beat"+i,new Vector2(14,14));
            calibration.Open();
        }

        Image Cue(string name,Vector2 size)
        {
            var rect=FightMenuUi.Rect(name,cueRoot,Vector2.zero,size);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            var image=rect.gameObject.AddComponent<Image>();image.color=Color.cyan;image.raycastTarget=false;return image;
        }

        void Update()
        {
            if(cueRoot==null)return;
            bool show=calibration!=null && !calibration.HideRhythmCues && beatClock.HasTimingAnchor;
            cueRoot.gameObject.SetActive(show);
            if(!show || !beatClock.TryGetTimelinePositionMs(out int ms))return;
            double beatPosition=(ms+rhythmJudge.VisualOffsetMs-beatClock.LatestBeat.TimelinePositionMs)/beatClock.MillisecondsPerBeat;
            float phase=(float)(beatPosition-System.Math.Floor(beatPosition));
            for(int i=0;i<3;i++)
            {
                float distance=(i+1-phase)*140;
                points[i*2].rectTransform.anchoredPosition=new Vector2(-distance,0);
                points[i*2+1].rectTransform.anchoredPosition=new Vector2(distance,0);
            }
            center.color=Color.Lerp(new Color(.05f,.15f,.2f),Color.cyan,Mathf.Clamp01(1-phase*5));
        }

        public void ReplayMusic()
        {
            calibration.Begin();rhythmJudge.ResetDuplicateTracking();beatClock.RestartMusic();
        }

        public void EnterBattle()
        {
            if(enteringBattle)return;
            if(!Application.CanStreamedLevelBeLoaded(battleSceneName))
            { Debug.LogError("FightScene3 must be enabled in Build Settings.");return; }
            enteringBattle=true;
            calibration.Close(); // Discard an unsaved preview; saved profiles stay on disk.
            beatClock.StopMusic();
            SceneManager.LoadScene(battleSceneName);
        }
    }
}
