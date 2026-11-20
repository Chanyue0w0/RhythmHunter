using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        [Header("Enemy Death Coins")]
        [SerializeField] private GameObject deathCoinItem;
        [SerializeField] private Material deathCoinMaterial;
        [SerializeField, Range(1, 20)] private int deathCoinCount = 7;
        [SerializeField, Min(.1f)] private float deathCoinLifetime = 1f;
        [SerializeField, Min(.01f)] private float deathCoinSize = .65f;
        [SerializeField] private Vector3 deathCoinOffset = new Vector3(0, .85f, 0);

        internal void PlayDeathCoins(Vector3 position)
        {
            if (deathCoinItem == null || deathCoinMaterial == null) return;
            var source = deathCoinItem.GetComponentInChildren<SpriteRenderer>(true);
            if (source == null || source.sprite == null) return;
            var root = new GameObject("Enemy Death Coins");
            root.transform.SetParent(transform, false);
            root.transform.position = position + deathCoinOffset;
            root.AddComponent<FightDeathCoinBurst>().Initialize(this, source.sprite,
                deathCoinMaterial, deathCoinCount, deathCoinLifetime, deathCoinSize);
        }
    }

    // Independent of the dying actor and enemy-action pauses: the final reaction
    // and its coins may finish during a Team Skill or wave transition.
    public sealed class FightDeathCoinBurst : MonoBehaviour
    {
        private FightCombatController fight;
        private ParticleSystem particles;
        private Material instanceMaterial;
        private bool paused;

        internal void Initialize(FightCombatController controller, Sprite sprite,
            Material material, int count, float lifetime, float size)
        {
            fight = controller;
            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .75f, lifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .8f, size * 1.2f);
            main.startSpeed = 0;
            main.startRotation = new ParticleSystem.MinMaxCurve(-.5f, .5f);
            main.gravityModifier = .7f;
            main.maxParticles = Mathf.Clamp(count, 1, 20);
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var texture = particles.textureSheetAnimation;
            texture.enabled = true;
            texture.mode = ParticleSystemAnimationMode.Sprites;
            texture.AddSprite(sprite);
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .45f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            var scale = particles.sizeOverLifetime;
            scale.enabled = true;
            scale.size = new ParticleSystem.MinMaxCurve(1,
                new AnimationCurve(new Keyframe(0, 1), new Keyframe(.55f, 1), new Keyframe(1, .35f)));

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            instanceMaterial = new Material(material);
            instanceMaterial.SetTexture("_BaseMap", sprite.texture);
            renderer.sharedMaterial = instanceMaterial;
            renderer.sortingOrder = 101;
            renderer.maxParticleSize = 1;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
            for (int i = 0; i < main.maxParticles; i++)
            {
                var coin = new ParticleSystem.EmitParams
                {
                    position = transform.position + new Vector3(Random.Range(-.16f, .16f), 0, 0),
                    velocity = new Vector3(Mathf.Lerp(-3f, 3f, (i + .5f) / main.maxParticles),
                        Random.Range(4.2f, 5.8f), Random.Range(-.15f, .15f))
                };
                particles.Emit(coin, 1);
            }
        }

        private void Update()
        {
            if (fight == null || !fight.isActiveAndEnabled) { Destroy(gameObject); return; }
            bool freeze = fight.IsPaused || fight.TimingCalibrationActive;
            if (freeze != paused)
            {
                paused = freeze;
                if (paused) particles.Pause(); else particles.Play();
            }
            if (!paused && !particles.IsAlive()) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (instanceMaterial != null)
            {
                if (Application.isPlaying) Destroy(instanceMaterial); else DestroyImmediate(instanceMaterial);
            }
        }
    }
}
