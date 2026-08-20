using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmHunter.OtterAquariumPrototype
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(OtterSurfaceSensor))]
    public sealed class OtterMovementController : MonoBehaviour
    {
        [Header("Water Movement")]
        [SerializeField, Min(0.1f)] private float waterSpeed = 6.8f;
        [SerializeField, Min(0.1f)] private float waterAcceleration = 18f;
        [SerializeField, Min(0.1f)] private float waterDeceleration = 8f;

        [Header("Land Movement")]
        [SerializeField, Min(0.1f)] private float landSpeed = 4.1f;
        [SerializeField, Min(0.1f)] private float landAcceleration = 22f;
        [SerializeField, Min(0.1f)] private float landDeceleration = 18f;

        [Header("Slide")]
        [SerializeField, Min(0.1f)] private float drySlideSpeed = 7.2f;
        [SerializeField, Min(0.1f)] private float wetSlideSpeed = 9.4f;
        [SerializeField, Min(0.05f)] private float slideDuration = 0.7f;
        [SerializeField, Range(0f, 1f)] private float slideSteering = 0.28f;
        [SerializeField, Min(0f)] private float slideCooldown = 0.45f;
        [SerializeField, Min(0f)] private float wetDuration = 7f;

        private Rigidbody2D body;
        private OtterSurfaceSensor surfaceSensor;
        private Vector2 moveInput;
        private Vector2 lastMoveDirection = Vector2.down;
        private Vector2 slideDirection;
        private float slideTimer;
        private float slideCooldownTimer;
        private float wetTimer;

        public Vector2 MoveInput => moveInput;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public float Speed => Velocity.magnitude;
        public Vector2 FacingDirection => lastMoveDirection;
        public bool IsSliding { get; private set; }
        public bool IsWet => wetTimer > 0f;
        public float Wetness01 => wetDuration <= 0f ? 0f : Mathf.Clamp01(wetTimer / wetDuration);
        public float SlideCooldown01 => slideCooldown <= 0f ? 0f : Mathf.Clamp01(slideCooldownTimer / slideCooldown);
        public AquariumSurfaceType CurrentSurface => surfaceSensor != null ? surfaceSensor.CurrentSurface : AquariumSurfaceType.Land;

        public void Configure(
            float configuredWaterSpeed,
            float configuredLandSpeed,
            float configuredWetSlideSpeed,
            float configuredDrySlideSpeed)
        {
            waterSpeed = configuredWaterSpeed;
            landSpeed = configuredLandSpeed;
            wetSlideSpeed = configuredWetSlideSpeed;
            drySlideSpeed = configuredDrySlideSpeed;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            surfaceSensor = GetComponent<OtterSurfaceSensor>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void OnEnable()
        {
            if (surfaceSensor == null)
                surfaceSensor = GetComponent<OtterSurfaceSensor>();
            surfaceSensor.SurfaceChanged += HandleSurfaceChanged;
        }

        private void OnDisable()
        {
            if (surfaceSensor != null)
                surfaceSensor.SurfaceChanged -= HandleSurfaceChanged;
        }

        private void Update()
        {
            moveInput = ReadMoveInput();
            if (moveInput.sqrMagnitude > 1f)
                moveInput.Normalize();
            if (moveInput.sqrMagnitude > 0.02f)
                lastMoveDirection = moveInput.normalized;

            slideCooldownTimer = Mathf.Max(0f, slideCooldownTimer - Time.deltaTime);
            if (surfaceSensor.IsInWater || surfaceSensor.IsInShallowWater)
                wetTimer = wetDuration;
            else
                wetTimer = Mathf.Max(0f, wetTimer - Time.deltaTime);

            if (SlidePressedThisFrame())
            {
                Vector2 direction = moveInput.sqrMagnitude > 0.02f ? moveInput.normalized : lastMoveDirection;
                BeginSlide(direction);
            }
        }

        private void FixedUpdate()
        {
            if (IsSliding)
            {
                UpdateSlide();
                return;
            }

            bool waterMovement = surfaceSensor.IsInWater;
            float baseSpeed = waterMovement ? waterSpeed : landSpeed;
            float acceleration = waterMovement ? waterAcceleration : landAcceleration;
            float deceleration = waterMovement ? waterDeceleration : landDeceleration;
            float speed = baseSpeed * surfaceSensor.CurrentSpeedMultiplier;
            Vector2 targetVelocity = moveInput * speed;
            float rate = moveInput.sqrMagnitude > 0.001f ? acceleration : deceleration;
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, targetVelocity, rate * Time.fixedDeltaTime);
        }

        public bool BeginSlide(Vector2 direction)
        {
            if (IsSliding || slideCooldownTimer > 0f || surfaceSensor.IsInWater)
                return false;

            slideDirection = direction.sqrMagnitude > 0.02f ? direction.normalized : lastMoveDirection;
            if (slideDirection.sqrMagnitude < 0.1f)
                slideDirection = Vector2.down;

            IsSliding = true;
            slideTimer = slideDuration;
            float speed = IsWet ? wetSlideSpeed : drySlideSpeed;
            body.linearVelocity = slideDirection * speed;
            return true;
        }

        private void UpdateSlide()
        {
            slideTimer -= Time.fixedDeltaTime;
            if (moveInput.sqrMagnitude > 0.02f)
                slideDirection = Vector2.Lerp(slideDirection, moveInput.normalized, slideSteering * Time.fixedDeltaTime * 8f).normalized;

            float progress = slideDuration <= 0f ? 1f : 1f - Mathf.Clamp01(slideTimer / slideDuration);
            float startSpeed = IsWet ? wetSlideSpeed : drySlideSpeed;
            float currentSpeed = Mathf.Lerp(startSpeed, landSpeed * 0.8f, progress);
            body.linearVelocity = slideDirection * currentSpeed;

            if (slideTimer <= 0f || body.linearVelocity.sqrMagnitude < 1f)
                EndSlide();
        }

        private void EndSlide()
        {
            if (!IsSliding)
                return;

            IsSliding = false;
            slideCooldownTimer = slideCooldown;
        }

        private void HandleSurfaceChanged(AquariumSurfaceType previous, AquariumSurfaceType next)
        {
            if (next == AquariumSurfaceType.Water)
            {
                wetTimer = wetDuration;
                EndSlide();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsSliding && collision.contactCount > 0)
                EndSlide();
        }

        private static Vector2 ReadMoveInput()
        {
            Vector2 input = Vector2.zero;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                if (stick.sqrMagnitude > input.sqrMagnitude)
                    input = stick;
            }

            return input;
        }

        private static bool SlidePressedThisFrame()
        {
            return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        }
    }
}
