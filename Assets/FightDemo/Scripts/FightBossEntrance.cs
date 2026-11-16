using System;
using System.Collections.Generic;
using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    // Scene references live here; reusable timings/effects live in the selected Stage.
    // All movement offsets are applied after Art2 grounding, without moving formation slots.
    [DefaultExecutionOrder(250), DisallowMultipleComponent]
    public sealed partial class FightBossEntrance : MonoBehaviour
    {
        [SerializeField] private FightLevelPlanner planner;
        [SerializeField] private FightRosterManager roster;
        [SerializeField] private FightCombatController fight;
        [SerializeField] private FmodBeatClock beatClock;
        [SerializeField] private Camera view;
        [SerializeField] private FightBossDoorAnimation door;
        [SerializeField] private Transform doorSpawnAnchor;
        private FightStageDefinition Stage => planner != null ? planner.ActiveStage ?? planner.SelectedStage : null;
        private FightStageDefinition.BossEntranceSettings Settings => Stage != null ? Stage.BossEntrance : null;
        public bool BlocksBattle => isActiveAndEnabled && Stage != null && Stage.BossStage && Settings != null && Settings.enabled && !complete;
        public string Phase { get; private set; } = "Waiting for player entry";
        private bool started, complete, opened, spawned, landed, placed;
        private double startBeat; private double descentAt=double.PositiveInfinity; private bool tookOff;
        [Serializable]
        private sealed class Entry
        {
            public FightUnitSlot slot;
            public Transform root;
            public Vector3 restLocal, restScale, destination, origin, footOffset;
            public float topY; public bool jumpPlaying, landingStarted, hasLanding, exitedView;
        }
        private readonly List<Entry> entries = new();
        public bool TryBegin()
        {
            if (!BlocksBattle) return false;
            if (!started && TryBeat(out double beat)) { started=true; startBeat=Math.Ceiling(beat); }
            return true;
        }
        private bool TryBeat(out double beat)
        {
            beat=0;
            if (!beatClock || !beatClock.IsPlaying || !beatClock.HasTimingAnchor || !beatClock.TryGetTimelinePositionMs(out int ms)) return false;
            beat=beatClock.LatestBeat.GlobalBeat + Math.Max(0,ms-beatClock.LatestBeat.TimelinePositionMs)/Math.Max(1d,beatClock.MillisecondsPerBeat);
            return true;
        }
        private void LateUpdate()
        {
            if(!started) return;
            entries.RemoveAll(entry => entry == null || !entry.slot || !entry.root);
            bool paused=fight==null || fight.IsPaused || fight.TimingCalibrationActive;
            door?.SetPaused(paused);
            if(!complete)
                foreach(var entry in entries)
                    if(entry.slot && entry.slot.ActorInstance) entry.slot.CombatAnimator?.SetPlaybackPaused(paused);
            if(paused || !TryBeat(out double beat)) return;
            UpdatePresentation(beat);
            if(complete) return;
            var s=Settings;
            double time=Math.Max(0,beat-startBeat);
            double openAt=Math.Max(0,s.waitBeforeDoorBeats), spawnAt=openAt+Math.Max(.01f,s.doorOpenBeats);
            double jumpAt=spawnAt+DoorHoldBeats; double riseBeats=Math.Max(.05f,s.jumpBeats)*Mathf.Clamp(s.ascentFraction,.05f,.95f); double fallBeats=Math.Max(.05f,s.jumpBeats)-riseBeats;
            Phase="Waiting to open door";
            if(time<openAt) return;
            Phase="Opening door";
            if(!opened) { opened=true; door?.Open(fight,s.doorOpenBeats); }
            if(time<spawnAt) return;
            if(!spawned)
            {
                spawned=true; roster.SpawnBossOpeningEnemies();
                SpawnDoorExplosion(beat);
                foreach(var slot in roster.ActiveEnemies)
                {
                    if(slot==null || slot.ActorInstance==null || slot.ActorRoot==null) continue;
                    var animator=slot.CombatAnimator;
                    var sequence=animator?.FindSequence(s.openingAnimationName);
                    if(sequence!=null) animator.PlayVisualSequence(animator.TargetRenderer,sequence,true);
                    entries.Add(new Entry { slot=slot,root=slot.ActorRoot,restLocal=slot.ActorRoot.localPosition,restScale=slot.ActorRoot.localScale });
                    // Hide just this first frame until Art2 has grounded the new actor.
                    foreach(var renderer in slot.ActorInstance.GetComponentsInChildren<Renderer>()) renderer.forceRenderingOff=true;
                }
                return;
            }
            if(!placed)
            {
                placed=true;
                foreach(var entry in entries)
                {
                    entry.destination=entry.root.position;
                    var foot=entry.slot.ActorInstance.transform.Find("FootAnchor");
                    entry.footOffset=(foot ? foot.position : entry.slot.ActorInstance.transform.position)-entry.destination;
                    entry.origin=(doorSpawnAnchor ? doorSpawnAnchor.position : entry.destination+Vector3.forward*4)-entry.footOffset;
                    float top=Mathf.Max(entry.origin.y,entry.destination.y)+8;
                    if(view)
                    {
                        float depth=Mathf.Max(.1f,view.WorldToViewportPoint(entry.origin).z);
                        top=Mathf.Max(top,view.ViewportToWorldPoint(new Vector3(.5f,1.4f,depth)).y);
                        depth=Mathf.Max(.1f,view.WorldToViewportPoint(entry.destination).z);
                        top=Mathf.Max(top,view.ViewportToWorldPoint(new Vector3(.5f,1.4f,depth)).y);
                    }
                    entry.topY=top+Mathf.Max(0,s.extraJumpHeight);
                    foreach(var renderer in entry.slot.ActorInstance.GetComponentsInChildren<Renderer>()) renderer.forceRenderingOff=false;
                }
            }
            Phase="OpenIdle";
            if(time>=jumpAt && !tookOff)
            {
                tookOff=true; door?.Close(fight,s.doorCloseBeats);
                foreach(var entry in entries) entry.jumpPlaying=PlayEntrance(entry,s.jumpAnimationName,false);
            }
            if(tookOff && double.IsPositiveInfinity(descentAt) && time>=jumpAt+riseBeats)
            {
                bool ready=true;
                foreach(var entry in entries)
                    if(entry.jumpPlaying && !entry.slot.CombatAnimator.VisualSequenceComplete) ready=false;
                if(ready)
                {
                    foreach(var entry in entries) PlayEntrance(entry,s.flyingAnimationName,true);
                    descentAt=time+Math.Max(0,s.flyingHoldBeats);
                }
            }
            double landAt=descentAt+fallBeats;
            foreach(var entry in entries)
            {
                Vector3 position=entry.origin;
                float size=Mathf.Clamp(s.doorBossScale,.05f,1f);
                if(tookOff)
                {
                    Phase="Jump entrance";
                    if(time<jumpAt+riseBeats)
                        position.y=Mathf.Lerp(entry.origin.y,entry.topY,Mathf.SmoothStep(0,1,(float)((time-jumpAt)/riseBeats)));
                    else
                    {
                        size=1;position=entry.destination;
                        position.y=Mathf.Lerp(entry.topY,entry.destination.y,Mathf.SmoothStep(0,1,(float)((time-descentAt)/fallBeats)));
                        Phase=time<descentAt ? "Flying" : "Descending";
                    }
                }
                entry.root.localScale=entry.restScale*size;
                entry.root.position=position+entry.footOffset*(1-size);
                // Latch only after the entire visible sprite has left the game camera.
                // Returning to view during descent must not turn the silhouette back on.
                if(tookOff && !entry.exitedView && !InGameView(entry)) entry.exitedView=true;
                ApplySilhouette(entry,s.blackSilhouette && !entry.exitedView);
                if(time>=descentAt && !entry.landingStarted && (InGameView(entry) || time>=landAt))
                {
                    entry.landingStarted=true;
                    entry.hasLanding=PlayEntrance(entry,s.landingAnimationName,false);
                }
            }
            if(time<landAt) return;
            Phase="Landing recovery";
            if(!landed)
            {
                landed=true;
                shakeStartBeat=beat;
                foreach(var entry in entries)
                {
                    entry.root.localPosition=entry.restLocal;
                    SpawnLandingExplosion(entry);
                    if(s.landingEffect)
                    {
                        var effect=Instantiate(s.landingEffect,entry.destination+entry.footOffset+s.landingEffectOffset,s.landingEffect.transform.rotation);
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effect,gameObject.scene);
                        var owner=effect.GetComponent<FightBattleEffect>() ?? effect.AddComponent<FightBattleEffect>();
                        owner.Configure(fight,false,5);
                    }
                }
            }
            foreach(var entry in entries)
            {
                if(entry.hasLanding) { if(!entry.slot.CombatAnimator.VisualSequenceComplete) return; }
                else if(time<landAt+Math.Max(0,s.landingWaitBeats)) return;
            }
            foreach(var entry in entries) { entry.root.localPosition=entry.restLocal; entry.slot.CombatAnimator?.InterruptPlayback(); }
            complete=true;Phase="Complete";
        }
        private bool PlayEntrance(Entry entry,string name,bool loop)
        {
            var animator=entry.slot.CombatAnimator;
            var sequence=animator?.FindSequence(name);
            if(sequence==null || sequence.Frames.Count==0) return false;
            animator.PlayVisualSequence(animator.TargetRenderer,sequence,loop); return true;
        }
        private bool InGameView(Entry entry)
        {
            var renderer=entry.slot.CombatAnimator?.TargetRenderer;
            if(!view || !renderer) return true;
            var bounds=renderer.bounds;
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            bool inFront=false;
            for(int i=0;i<8;i++)
            {
                var point=view.WorldToViewportPoint(bounds.center+Vector3.Scale(bounds.extents,
                    new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                if(point.z<=0) continue;
                inFront=true;min=Vector2.Min(min,point);max=Vector2.Max(max,point);
            }
            return inFront && max.x>=0 && min.x<=1 && max.y>=0 && min.y<=1;
        }
        private void OnDisable()
        {
            ClearPresentation();
            foreach(var entry in entries)
            {
                if(entry == null) continue;
                if(entry.root) { entry.root.localPosition=entry.restLocal; entry.root.localScale=entry.restScale; }
                if(entry.slot && entry.slot.ActorInstance)
                    foreach(var renderer in entry.slot.ActorInstance.GetComponentsInChildren<Renderer>()) renderer.forceRenderingOff=false;
            }
            door?.ResetDoor();
        }
    }
}
