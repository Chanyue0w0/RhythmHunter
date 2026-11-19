using UnityEngine;

namespace RhythmHunter.FightDemo
{
    // Both the original line art and authored particle VFX use the same note pool/timeline.
    public sealed class FightBossRhythmVisual : MonoBehaviour
    {
        [SerializeField] private Vector3 facingOffset;
        [SerializeField] private bool faceTravelDirection;
        [Tooltip("Local direction toward the target. Thruster exhaust points +Z, so its travel direction is -Z.")]
        [SerializeField] private Vector3 localTravelDirection=Vector3.back;
        [SerializeField] private bool localParticleSimulation;
        private Quaternion authoredRotation;
        private LineRenderer line;
        private float baseWidth;
        private Vector3 baseScale;
        private ParticleSystem[] particles;
        private bool paused, restartPending;
        private MaterialPropertyBlock properties;
        private void Awake()
        {
            line=GetComponent<LineRenderer>();if(line)baseWidth=line.widthMultiplier;
            baseScale=transform.localScale;authoredRotation=transform.localRotation;
            particles=GetComponentsInChildren<ParticleSystem>(true);
            foreach(var p in particles){var main=p.main;main.stopAction=ParticleSystemStopAction.None;main.playOnAwake=false;if(localParticleSimulation)main.simulationSpace=ParticleSystemSimulationSpace.Local;}
        }
        private void OnEnable()
        {
            paused=false;restartPending=true;
            foreach(var p in particles)if(p)p.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        private void OnDisable()
        {
            foreach(var p in particles)if(p)p.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            paused=false;
        }
        public void SetPaused(bool value)
        {
            if(paused==value)return;paused=value;
            foreach(var p in particles)if(p&&p.gameObject.activeInHierarchy){if(value)p.Pause(false);else p.Play(false);}
        }
        public void PresentFlight(Vector3 position, Vector3 direction, Quaternion cameraFacing, float scale, Color color)
        {
            var facing=cameraFacing;
            if(faceTravelDirection && direction.sqrMagnitude>.000001f)
                facing=Quaternion.LookRotation(direction.normalized,Vector3.up)*Quaternion.Inverse(Quaternion.LookRotation(localTravelDirection.sqrMagnitude>.000001f?localTravelDirection.normalized:Vector3.forward,Vector3.up))*authoredRotation;
            Present(position,facing,scale,color);
        }
        public void Present(Vector3 position, Quaternion facing, float scale, Color color)
        {
            transform.SetPositionAndRotation(position, facing*Quaternion.Euler(facingOffset));
            transform.localScale = baseScale * scale;
            // World-space particles must start at the new note origin, never at the old pooled position.
            if(restartPending)
            {
                restartPending=false;
                foreach(var p in particles)if(p&&p.gameObject.activeInHierarchy){p.Play(false);if(paused)p.Pause(false);}
            }
            // Particle colors stay authored; the legacy guard/line retains judgement tinting.
            if(!line)return;
            line.startColor = line.endColor = color;
            properties ??= new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color); line.SetPropertyBlock(properties);
            line.widthMultiplier = baseWidth;
        }
    }
}
