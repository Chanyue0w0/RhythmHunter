using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    public sealed class OtterVfxController : MonoBehaviour
    {
        [SerializeField] private OtterMovementController movement;
        [SerializeField] private OtterSurfaceSensor surfaceSensor;
        [SerializeField] private ParticleSystem swimTrail;
        [SerializeField] private ParticleSystem entrySplash;
        [SerializeField] private ParticleSystem exitDrops;
        [SerializeField] private ParticleSystem slideSpray;
        [SerializeField] private ParticleSystem turnSplash;
        [SerializeField, Min(0f)] private float trailStartSpeed = 1.2f;
        [SerializeField, Min(0f)] private float turnSplashThreshold = 2.5f;

        private Vector2 previousVelocity;
        private float turnCooldown;
        private bool wasSliding;

        public void Configure(
            OtterMovementController movementController,
            OtterSurfaceSensor sensor,
            ParticleSystem configuredSwimTrail,
            ParticleSystem configuredEntrySplash,
            ParticleSystem configuredExitDrops,
            ParticleSystem configuredSlideSpray,
            ParticleSystem configuredTurnSplash)
        {
            movement = movementController;
            surfaceSensor = sensor;
            swimTrail = configuredSwimTrail;
            entrySplash = configuredEntrySplash;
            exitDrops = configuredExitDrops;
            slideSpray = configuredSlideSpray;
            turnSplash = configuredTurnSplash;
        }

        private void OnEnable()
        {
            if (surfaceSensor != null)
                surfaceSensor.SurfaceChanged += HandleSurfaceChanged;
        }

        private void Start()
        {
            if (surfaceSensor != null)
            {
                surfaceSensor.SurfaceChanged -= HandleSurfaceChanged;
                surfaceSensor.SurfaceChanged += HandleSurfaceChanged;
            }
        }

        private void OnDisable()
        {
            if (surfaceSensor != null)
                surfaceSensor.SurfaceChanged -= HandleSurfaceChanged;
        }

        private void Update()
        {
            if (movement == null || surfaceSensor == null)
                return;

            UpdateTrail();
            UpdateSlideSpray();
            UpdateTurnSplash();
            wasSliding = movement.IsSliding;
            previousVelocity = movement.Velocity;
            turnCooldown = Mathf.Max(0f, turnCooldown - Time.deltaTime);
        }

        private void UpdateTrail()
        {
            if (swimTrail == null)
                return;

            bool active = surfaceSensor.IsInWater && movement.Speed > trailStartSpeed;
            ParticleSystem.EmissionModule emission = swimTrail.emission;
            emission.rateOverTime = active ? Mathf.Lerp(5f, 24f, Mathf.InverseLerp(trailStartSpeed, 7f, movement.Speed)) : 0f;

            if (active && !swimTrail.isPlaying)
                swimTrail.Play();
            else if (!active && swimTrail.isPlaying)
                swimTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void UpdateSlideSpray()
        {
            if (slideSpray == null)
                return;

            ParticleSystem.EmissionModule emission = slideSpray.emission;
            emission.rateOverTime = movement.IsSliding ? (movement.IsWet ? 28f : 10f) : 0f;
            if (movement.IsSliding && !slideSpray.isPlaying)
                slideSpray.Play();
            else if (!movement.IsSliding && slideSpray.isPlaying)
                slideSpray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void UpdateTurnSplash()
        {
            if (turnSplash == null || !surfaceSensor.IsInWater || turnCooldown > 0f)
                return;

            Vector2 current = movement.Velocity;
            if (current.magnitude < turnSplashThreshold || previousVelocity.magnitude < turnSplashThreshold)
                return;

            float angle = Vector2.Angle(previousVelocity, current);
            if (angle > 28f)
            {
                turnSplash.Emit(Mathf.RoundToInt(Mathf.Lerp(3f, 8f, angle / 180f)));
                turnCooldown = 0.18f;
            }
        }

        private void HandleSurfaceChanged(AquariumSurfaceType previous, AquariumSurfaceType next)
        {
            if (next == AquariumSurfaceType.Water && previous != AquariumSurfaceType.Water && entrySplash != null)
            {
                int count = wasSliding || movement.Speed > 6.5f ? 22 : 12;
                entrySplash.Emit(count);
            }

            if (previous == AquariumSurfaceType.Water && next != AquariumSurfaceType.Water && exitDrops != null)
                exitDrops.Emit(10);
        }
    }
}
