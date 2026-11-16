using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    // Presentation only; receives the existing entrance beat envelope, never owns a clock.
    [ExecuteAlways, DefaultExecutionOrder(300), DisallowMultipleComponent]
    public sealed class Art2CinematicFocus : MonoBehaviour
    {
        [SerializeField] private bool enableFocus = true;
        [SerializeField] private FightRosterManager roster;
        [SerializeField] private Art2FocusWall[] distantWalls;
        [SerializeField, Range(0,1)] private float distantBlurRetention;
        [SerializeField, Range(0,1)] private float distantFogRetention = 1;
        [SerializeField, Range(0,2)] private float foregroundBlurPixels = 1.2f;
        [SerializeField] private bool includeHeroes = true;
        [Tooltip("Optional scenery or edit-mode preview actors. Bosses and Canvas renderers are excluded.")]
        [SerializeField] private Transform[] additionalForegroundRoots;
        [SerializeField] private bool previewInEditMode;
        [SerializeField, Range(0,1)] private float previewWeight;
        private float runtimeWeight;
        public float AppliedWeight { get; private set; }
        private readonly List<SpriteRenderer> scratch = new();
        private readonly HashSet<SpriteRenderer> selected = new();
        private readonly Dictionary<SpriteRenderer,float> originals = new();
        private readonly List<SpriteRenderer> released = new();
        private MaterialPropertyBlock block;
        private static readonly int BlurId=Shader.PropertyToID("_CinematicBlur");
        public void SetEntranceWeight(float weight) => runtimeWeight=Mathf.Clamp01(weight);
        private void LateUpdate()
        {
            AppliedWeight=enableFocus ? Application.isPlaying ? runtimeWeight : previewInEditMode ? previewWeight : 0 : 0;
            if(distantWalls!=null)
                foreach(var wall in distantWalls)
                    if(wall)wall.SetCinematicMultipliers(Mathf.Lerp(1,distantBlurRetention,AppliedWeight),Mathf.Lerp(1,distantFogRetention,AppliedWeight));
            selected.Clear();
            if(AppliedWeight>0)
            {
                if(includeHeroes && roster)
                    foreach(var slot in roster.ActiveHeroes)
                        if(slot && slot.ActorInstance) Collect(slot.ActorInstance.transform);
                if(additionalForegroundRoots!=null)
                    foreach(var root in additionalForegroundRoots) if(root)Collect(root);
            }
            block ??= new MaterialPropertyBlock();
            released.Clear();
            foreach(var entry in originals)
                if(!entry.Key || !selected.Contains(entry.Key))
                { if(entry.Key)Write(entry.Key,entry.Value);released.Add(entry.Key); }
            foreach(var renderer in released)originals.Remove(renderer);
            foreach(var renderer in selected)
            {
                if(!originals.ContainsKey(renderer))
                { renderer.GetPropertyBlock(block);originals.Add(renderer,block.GetFloat(BlurId)); }
                Write(renderer,Mathf.Lerp(originals[renderer],foregroundBlurPixels,AppliedWeight));
            }
        }
        private void Collect(Transform root)
        {
            scratch.Clear();root.GetComponentsInChildren(false,scratch);
            foreach(var renderer in scratch)
            {
                if(!renderer || renderer.GetComponentInParent<Canvas>())continue;
                var slot=renderer.GetComponentInParent<FightUnitSlot>();
                if(slot && slot.Team==FightUnitSlot.UnitTeam.Enemy)continue;
                if(renderer.sharedMaterial && renderer.sharedMaterial.HasProperty(BlurId))selected.Add(renderer);
            }
        }
        private void Write(SpriteRenderer renderer,float value)
        {
            renderer.GetPropertyBlock(block);block.SetFloat(BlurId,value);renderer.SetPropertyBlock(block);
        }
        private void OnDisable()
        {
            runtimeWeight=AppliedWeight=0;
            if(distantWalls!=null)foreach(var wall in distantWalls)if(wall)wall.SetCinematicMultipliers(1,1);
            foreach(var entry in originals)if(entry.Key)Write(entry.Key,entry.Value);
            originals.Clear();
        }
    }
}
