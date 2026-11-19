using UnityEngine;
using UnityEngine.Rendering;

namespace RhythmHunter.FightDemo
{
    /// <summary>Read-only presentation subscriber. Never changes chart, input, camera or music.</summary>
    [DisallowMultipleComponent]
    public sealed class FightPhaseTwoSpeedLines : MonoBehaviour
    {
        [SerializeField] private FightCombatController fight;
        [SerializeField] private FightBossRhythmBattle rhythm;
        [Header("3D tunnel airflow: door toward camera")]
        [SerializeField] private bool speedLinesEnabled = true;
        [SerializeField] private Transform tunnelOrigin;
        [SerializeField] private Camera tunnelCamera;
        [SerializeField] private Shader tunnelShader;
        [SerializeField] private Vector3 originOffset;
        [SerializeField] private Color lineColor = new(.5f, .75f, 1f, .55f);
        [SerializeField, Min(0)] private float sourceRadius = .35f;
        [SerializeField, Min(.1f)] private float nearRadius = 4f;
        [SerializeField, Range(0, 128)] private int lowIntensityCount = 28;
        [SerializeField, Range(0, 128)] private int highIntensityCount = 96;
        [Tooltip("Fraction of door-to-camera travel per song beat, low/high intensity.")]
        [SerializeField] private Vector2 speedPerBeat = new(.12f, .5f);
        [Tooltip("World-space streak length at low/high intensity.")]
        [SerializeField] private Vector2 streakLength = new(.5f, 2.2f);
        [SerializeField] private Vector2 streakWidth = new(.018f, .045f);
        [SerializeField] private bool pixelSnap = false;
        [SerializeField] private bool respectSceneDepth = true;
        [SerializeField, Min(.01f)] private float fadeInSeconds = .5f;
        [SerializeField, Min(.01f)] private float fadeOutSeconds = .25f;
        [Header("Central protection (all three lanes)")]
        [SerializeField] private bool centralProtectionEnabled = true;
        [SerializeField, Range(0, 1)] private float protectedCenterX = .5f;
        [SerializeField, Range(0, 1)] private float protectedWidth = .68f;
        [Tooltip("Protected width at the distant door; widens toward the judgement area.")]
        [SerializeField, Range(0,1)] private float protectedDoorWidth = .1f;
        [SerializeField, Range(0, .25f)] private float protectionFeather = .04f;
        [Header("Chart density growth")]
        [SerializeField] private bool increaseCountWithChart = true;
        [Tooltip("Multiplier of launched-note progress. 1.6 reaches maximum density after 62.5% of notes.")]
        [SerializeField,Min(.01f)] private float countGrowthMultiplier=1;
        [SerializeField, Min(.01f)] private float countFadeBeats = .5f;
        [Header("Song section intensity")]
        [SerializeField] private bool sectionIntensityEnabled = true;
        [Tooltip("X = phase-two song progress 0..1, Y = intensity 0..1. Does not edit chart data.")]
        [SerializeField] private AnimationCurve sectionIntensity = new(new Keyframe(0,.15f), new Keyframe(.25f,.3f), new Keyframe(.55f,.55f), new Keyframe(.8f,.8f), new Keyframe(1,1));
        [SerializeField, Range(0,1)] private float constantIntensity = .35f;
        [Header("Successful guard acceleration")]
        [SerializeField] private bool guardBoostEnabled = true;
        [SerializeField, Range(0, 2)] private float guardBoostAmount = .35f;
        [SerializeField, Min(.01f)] private float guardBoostDurationBeats = .4f;
        [SerializeField, Range(1, 4)] private float maximumSpeedMultiplier = 1.6f;
        [Header("Streak exit")]
        [SerializeField,Min(0)] private float exitViewportMargin=.08f;
        [SerializeField,Min(1)] private float maximumTravel=1.4f;
        private FightTunnelAirflow airflow;
        private float visibility, boostRemaining;
        private double previousBeat;
        private bool wasRunning, haveBeat;
        private FightBossRhythmBattle subscribedRhythm;
        public float CurrentIntensity { get; private set; }
        public float GuardBoostRemaining => boostRemaining;
        private void OnEnable()
        {
            if (!fight) fight=GetComponent<FightCombatController>();
            if (!rhythm) rhythm=GetComponent<FightBossRhythmBattle>();
            subscribedRhythm=rhythm;
            if (subscribedRhythm) subscribedRhythm.NoteResolved+=OnNoteResolved;
        }
        private void OnDisable()
        {
            if(subscribedRhythm)subscribedRhythm.NoteResolved-=OnNoteResolved;
            subscribedRhythm=null;boostRemaining=visibility=0;haveBeat=wasRunning=false;
            if(airflow)airflow.gameObject.SetActive(false);
        }
        private void OnDestroy() { if(airflow)Destroy(airflow.gameObject); }
        private void OnNoteResolved(int lane,bool blocked,double beat)
        {
            if(speedLinesEnabled && guardBoostEnabled && blocked && rhythm && rhythm.Running)
                boostRemaining=Mathf.Max(.01f,guardBoostDurationBeats);
        }
        public static float ProtectionAlpha(float x,bool enabled,float center,float width,float feather)
        {
            if(!enabled)return 1;
            float distance=Mathf.Abs(x-center)-Mathf.Clamp01(width)*.5f;
            return feather<=0 ? (distance>0 ? 1 : 0) : Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/feather));
        }
        private void EnsureAirflow()
        {
            if(airflow || !tunnelCamera || !tunnelOrigin)return;
            var root=new GameObject("PhaseTwoTunnelAirflow");root.transform.SetParent(transform,false);
            airflow=root.AddComponent<FightTunnelAirflow>();airflow.Initialize(tunnelShader);
        }
        private void Update()
        {
            if(!fight||!rhythm)return;
            if(fight.IsPaused||fight.TimingCalibrationActive)return;
            bool running=speedLinesEnabled && rhythm.Running && !rhythm.FinaleActive && rhythm.LaunchedNotes > 0;
            if(running && !wasRunning) {boostRemaining=0;haveBeat=false;EnsureAirflow();if(airflow)airflow.ResetLines();}
            if(!running){boostRemaining=0;}
            wasRunning=running;
            visibility=Mathf.MoveTowards(visibility,running?1:0,Time.unscaledDeltaTime/Mathf.Max(.01f,running?fadeInSeconds:fadeOutSeconds));
            if(visibility<=0){if(airflow)airflow.gameObject.SetActive(false);return;}
            EnsureAirflow();if(!airflow)return;airflow.gameObject.SetActive(true);
            float deltaBeat= !running && fight.BeatClock ? Time.unscaledDeltaTime*1000f/Mathf.Max(1,(float)fight.BeatClock.MillisecondsPerBeat):0;
            if(running)
            {
                double beat=rhythm.CurrentBeat;
                if(haveBeat && beat>=previousBeat && beat-previousBeat<8)deltaBeat=(float)(beat-previousBeat);
                else if(haveBeat)airflow.ResetLines();
                previousBeat=beat;haveBeat=true;
                if(!guardBoostEnabled)boostRemaining=0;
                else boostRemaining=Mathf.Max(0,boostRemaining-deltaBeat);
                float progress=1-fight.BossPhaseTwoSecondsRemaining/Mathf.Max(.01f,fight.BossPhaseTwoDurationSeconds);
                CurrentIntensity=Mathf.Clamp01(sectionIntensityEnabled && sectionIntensity!=null ? sectionIntensity.Evaluate(Mathf.Clamp01(progress)) : constantIntensity);
            }
            float boost=guardBoostEnabled ? guardBoostAmount*Mathf.Clamp01(boostRemaining/Mathf.Max(.01f,guardBoostDurationBeats)) : 0;
            float speed=Mathf.Lerp(Mathf.Max(0,speedPerBeat.x),Mathf.Max(0,speedPerBeat.y),CurrentIntensity)*Mathf.Min(maximumSpeedMultiplier,1+boost);
            float populationProgress=Mathf.Clamp01((increaseCountWithChart?rhythm.LaunchProgress:CurrentIntensity)*countGrowthMultiplier);
            int count=Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(lowIntensityCount,Mathf.Max(lowIntensityCount,highIntensityCount),populationProgress)),0,128);
            Color tint=lineColor;tint.a*=visibility;
            airflow.Present(tunnelCamera,tunnelOrigin.TransformPoint(originOffset),deltaBeat*speed,count,sourceRadius,nearRadius,
                Mathf.Lerp(streakLength.x,streakLength.y,CurrentIntensity)*(1+boost*.25f),streakWidth,tint,
                centralProtectionEnabled,protectedCenterX,protectedWidth,protectedDoorWidth,protectionFeather,pixelSnap,respectSceneDepth,deltaBeat/Mathf.Max(.01f,countFadeBeats),running,exitViewportMargin,maximumTravel);

        }
    }
}
