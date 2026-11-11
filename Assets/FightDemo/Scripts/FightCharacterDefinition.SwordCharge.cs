using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCharacterDefinition
    {
        [Header("Resonance Sword / Consecutive Normal Hits")]
        [SerializeField] private bool enableSwordCharge;
        [SerializeField, Min(1)] private int swordHitsPerStage = 1;
        [SerializeField, Min(1)] private int swordFullChargeBeats = 5;
        [SerializeField, Min(.5f)] private float swordChargedDamage = 2;
        [SerializeField, Min(.5f)] private float swordDelayedDamage = 1;
        [SerializeField, Min(.01f)] private float swordDelayedHitBeats = .5f;
        [Tooltip("Optional enhanced slash VFX. Empty uses the ordinary slash until artwork is ready.")]
        [SerializeField] private GameObject swordChargedSlashEffect;
        [Tooltip("Optional enhanced impact VFX for the first hit. Empty uses the ordinary OnHit.")]
        [SerializeField] private GameObject swordChargedOnHitEffect;
        [InspectorName("Sword Immediate Explosion Effect")]
        [Tooltip("Extra VFX spawned on the initial charged hit. The secondary damage still waits Sword Delayed Hit Beats. Serialized name retained for existing prefab references.")]
        [SerializeField] private GameObject swordDelayedOnHitEffect;
        [Tooltip("Existing child aura. Enabled only at full charge; stops emitting and naturally fades before being disabled.")]
        [SerializeField] private GameObject swordChargeAura;
        private ParticleSystem[] swordAuraParticles;
        private float[] swordAuraSpeeds;
        private bool[] swordAuraLoops;
        private FightCombatController swordAuraFight;
        private bool swordAuraCharged, swordAuraDraining, swordAuraPaused;

        private void Awake()
        {
            if (swordChargeAura != null) ResetSwordAura();
        }

        public void SetSwordChargeAura(bool charged, FightCombatController fight)
        {
            if (swordChargeAura == null) return;
            swordAuraFight = fight;
            CacheSwordAura();
            if (charged == swordAuraCharged) return;
            swordAuraCharged = charged;
            swordAuraDraining = !charged;
            if (charged)
            {
                swordChargeAura.SetActive(true);
                for (int i = 0; i < swordAuraParticles.Length; i++)
                    if (swordAuraParticles[i] != null && swordAuraParticles[i].gameObject.activeInHierarchy)
                    {
                        var main = swordAuraParticles[i].main;
                        main.loop = swordAuraLoops[i];
                        swordAuraParticles[i].Play(false);
                    }
            }
            else
                foreach (var particles in swordAuraParticles)
                    if (particles != null)
                    {
                        var main = particles.main;
                        main.loop = false;
                        particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                    }
        }

        private void CacheSwordAura()
        {
            if (swordAuraParticles != null) return;
            swordAuraParticles = swordChargeAura.GetComponentsInChildren<ParticleSystem>(true);
            swordAuraSpeeds = new float[swordAuraParticles.Length];
            swordAuraLoops = new bool[swordAuraParticles.Length];
            for (int i = 0; i < swordAuraParticles.Length; i++)
            {
                var main = swordAuraParticles[i].main;
                swordAuraSpeeds[i] = main.simulationSpeed;
                swordAuraLoops[i] = main.loop;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            }
        }

        private void Update()
        {
            if (swordChargeAura == null || (!swordAuraCharged && !swordAuraDraining)) return;
            bool paused = swordAuraFight != null && (swordAuraFight.IsPaused || swordAuraFight.TimingCalibrationActive);
            if (paused != swordAuraPaused)
            {
                swordAuraPaused = paused;
                for (int i = 0; i < swordAuraParticles.Length; i++)
                    if (swordAuraParticles[i] != null)
                    {
                        var main = swordAuraParticles[i].main;
                        main.simulationSpeed = paused ? 0 : swordAuraSpeeds[i];
                    }
            }
            if (paused || !swordAuraDraining) return;
            foreach (var particles in swordAuraParticles)
                // Emission is already stopped. IsAlive can remain true for a paused
                // emitter with no particles; wait for the actual visible particles.
                if (particles != null && particles.gameObject.activeInHierarchy && particles.particleCount > 0) return;
            swordAuraDraining = false;
            swordChargeAura.SetActive(false);
        }

        private void OnDisable() => ResetSwordAura();

        private void ResetSwordAura()
        {
            if (swordChargeAura == null) return;
            CacheSwordAura();
            for (int i = 0; i < swordAuraParticles.Length; i++)
                if (swordAuraParticles[i] != null)
                {
                    swordAuraParticles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = swordAuraParticles[i].main;
                    main.simulationSpeed = swordAuraSpeeds[i];
                    main.loop = swordAuraLoops[i];
                }
            swordAuraCharged = swordAuraDraining = swordAuraPaused = false;
            swordChargeAura.SetActive(false);
        }
        public bool EnableSwordCharge => enableSwordCharge;
        public int SwordHitsPerStage => Mathf.Max(1, swordHitsPerStage);
        public int SwordFullChargeBeats => Mathf.Max(1, swordFullChargeBeats);
        public float SwordChargedDamage => Mathf.Max(.5f, swordChargedDamage);
        public float SwordDelayedDamage => Mathf.Max(.5f, swordDelayedDamage);
        public float SwordDelayedHitBeats => Mathf.Max(.01f, swordDelayedHitBeats);
        public GameObject SwordChargedSlashEffect => swordChargedSlashEffect;
        public GameObject SwordChargedOnHitEffect => swordChargedOnHitEffect;
        public GameObject SwordImmediateExplosionEffect => swordDelayedOnHitEffect;
    }
}
