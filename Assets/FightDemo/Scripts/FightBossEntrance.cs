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
        private double startBeat;
        [Serializable]
        private sealed class Entry
        {
            public FightUnitSlot slot;
            public Transform root;
            public Vector3 restLocal, restScale, destination, origin, footOffset;
            public float topY;
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
            double jumpAt=spawnAt+DoorHoldBeats, landAt=jumpAt+Math.Max(.05f,s.jumpBeats);
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
            foreach(var entry in entries)
            {
                if(!entry.root) continue;
                Vector3 position=entry.origin;
                float size=Mathf.Clamp(s.doorBossScale,.05f,1f);
                if(time>=jumpAt)
                {
                    Phase="Jump entrance";
                    float t=Mathf.Clamp01((float)((time-jumpAt)/Math.Max(.05f,s.jumpBeats)));
                    float split=Mathf.Clamp(s.ascentFraction,.05f,.95f);
                    // Rise vertically at the door, relocate above the destination while offscreen, then descend vertically.
                    if(t<split) { position=entry.origin;position.y=Mathf.Lerp(entry.origin.y,entry.topY,Mathf.SmoothStep(0,1,t/split)); }
                    else { size=1;position=entry.destination;position.y=Mathf.Lerp(entry.topY,entry.destination.y,Mathf.SmoothStep(0,1,(t-split)/(1-split))); }
                }
                entry.root.localScale=entry.restScale*size;
                // Scale about the feet, preserving the authored door anchor and grounding.
                entry.root.position=position+entry.footOffset*(1-size);
                ApplySilhouette(entry, s.blackSilhouette && time < jumpAt);
                if(time>=jumpAt && !entryIdleRestored.Contains(entry))
                {
                    entryIdleRestored.Add(entry);
                    entry.slot.CombatAnimator?.InterruptPlayback();
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
            if(time<landAt+Math.Max(0,s.landingWaitBeats)) return;
            foreach(var entry in entries) { entry.root.localPosition=entry.restLocal; entry.slot.CombatAnimator?.InterruptPlayback(); }
            complete=true;Phase="Complete";
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
