using UnityEngine;
namespace RhythmHunter.FightDemo
{
    /// <summary>Multiplies every child particle lifetime alpha, including already living particles.</summary>
    [DisallowMultipleComponent]
    public sealed class FightVfxOpacity : MonoBehaviour
    {
        [SerializeField, Range(0,1)] private float opacity = 1;
        sealed class State
        {
            public ParticleSystem system; public bool enabled;
            public ParticleSystemGradientMode mode;
            public Color min,max;
            public GradientColorKey[] minColors,maxColors;
            public GradientAlphaKey[] minAlpha,maxAlpha;
            public GradientMode minMode,maxMode;
            public State(ParticleSystem p)
            {
                system=p;var m=p.colorOverLifetime;enabled=m.enabled;
                var c=enabled?m.color:new ParticleSystem.MinMaxGradient(Color.white);
                mode=c.mode;min=c.colorMin;max=c.colorMax;
                if(c.gradientMin!=null){minColors=c.gradientMin.colorKeys;minAlpha=c.gradientMin.alphaKeys;minMode=c.gradientMin.mode;}
                if(c.gradientMax!=null){maxColors=c.gradientMax.colorKeys;maxAlpha=c.gradientMax.alphaKeys;maxMode=c.gradientMax.mode;}
            }
            static Gradient Gradient(GradientColorKey[] colors,GradientAlphaKey[] alpha,GradientMode mode,float amount)
            {
                var result=new Gradient {mode=mode};
                if(colors==null||alpha==null)return result;
                var keys=(GradientAlphaKey[])alpha.Clone();for(int i=0;i<keys.Length;i++)keys[i].alpha*=amount;
                result.SetKeys(colors,keys);return result;
            }
            public ParticleSystem.MinMaxGradient ColorAt(float a)
            {
                var result=new ParticleSystem.MinMaxGradient();result.mode=mode;
                if(mode==ParticleSystemGradientMode.Color)result.color=Fade(max,a);
                else if(mode==ParticleSystemGradientMode.TwoColors){result.colorMin=Fade(min,a);result.colorMax=Fade(max,a);}
                else
                {
                    if(mode==ParticleSystemGradientMode.TwoGradients)result.gradientMin=Gradient(minColors,minAlpha,minMode,a);
                    result.gradientMax=Gradient(maxColors,maxAlpha,maxMode,a);
                }
                return result;
            }
        }
        State[] states;
        Light[] lights; float[] intensities; bool[] animatedLights;
        ParticleSystemRenderer[] renderers; Material[][] originals, instances;
        float applied = -1;
        void Awake()
        {
            var particles=GetComponentsInChildren<ParticleSystem>(true); states=new State[particles.Length];
            for(int i=0;i<particles.Length;i++)states[i]=new State(particles[i]);
            lights=GetComponentsInChildren<Light>(true); intensities=new float[lights.Length];
            animatedLights=new bool[lights.Length];
            var animators=GetComponentsInChildren<CartoonFX.CFXR_Effect>(true);
            for(int i=0;i<lights.Length;i++)
            {
                intensities[i]=lights[i].intensity;
                foreach(var animator in animators)if(animator.enabled&&animator.animatedLights!=null)
                    foreach(var item in animator.animatedLights)if(item.light==lights[i]&&item.animateIntensity)animatedLights[i]=true;
            }
            renderers=GetComponentsInChildren<ParticleSystemRenderer>(true);
            originals=new Material[renderers.Length][]; instances=new Material[renderers.Length][];
            for(int i=0;i<renderers.Length;i++)
            {
                originals[i]=renderers[i].sharedMaterials; instances[i]=new Material[originals[i].Length];
                for(int j=0;j<originals[i].Length;j++)
                {
                    if(!originals[i][j]) continue;
                    var mat=new Material(originals[i][j]); instances[i][j]=mat;
                    mat.DisableKeyword("_ALPHATEST_ON");
                    if(mat.HasProperty("_UseAlphaClip"))mat.SetFloat("_UseAlphaClip",0);
                    if(mat.HasProperty("_Cutoff"))mat.SetFloat("_Cutoff",0);
                }
                renderers[i].sharedMaterials=instances[i];
            }
            Apply();
        }
        public void SetOpacity(float value) { opacity=Mathf.Clamp01(value); Apply(); }
        void LateUpdate()
        {
            Apply();
            // CFXR writes animated light intensity in Update; attenuate that result once in LateUpdate.
            for(int i=0;i<lights.Length;i++)if(lights[i])lights[i].intensity=(animatedLights[i]?lights[i].intensity:intensities[i])*opacity;
        }
        static Color Fade(Color c,float a) { c.a*=a; return c; }
        void Apply()
        {
            if(states==null||Mathf.Abs(applied-opacity)<.002f)return; applied=opacity;
            foreach(var s in states)
            {
                if(!s.system)continue;
                var c=s.ColorAt(opacity);
                var m=s.system.colorOverLifetime; m.enabled=true;m.color=c;
            }
        }
        void OnDestroy()
        {
            if(states!=null)foreach(var s in states)if(s.system){var m=s.system.colorOverLifetime;m.enabled=s.enabled;m.color=s.ColorAt(1);}
            if(lights!=null)for(int i=0;i<lights.Length;i++)if(lights[i])lights[i].intensity=intensities[i];
            if(renderers==null)return;
            for(int i=0;i<renderers.Length;i++)
            {
                if(renderers[i])renderers[i].sharedMaterials=originals[i];
                foreach(var mat in instances[i])if(mat)Destroy(mat);
            }
        }
    }
}
