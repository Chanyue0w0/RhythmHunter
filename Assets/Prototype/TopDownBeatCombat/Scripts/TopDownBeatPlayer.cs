using System;
using System.Collections;
using RhythmHunter.RhythmArena;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmHunter.TopDownBeatCombat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class TopDownBeatPlayer : MonoBehaviour
    {
        public readonly struct AttackResult
        {
            public AttackResult(RhythmClock.TimingGrade grade, int damage, bool hit)
            {
                Grade = grade;
                Damage = damage;
                Hit = hit;
            }

            public RhythmClock.TimingGrade Grade { get; }
            public int Damage { get; }
            public bool Hit { get; }
        }

        [Header("Dependencies")]
        [SerializeField] private RhythmClock rhythmClock;
        [SerializeField] private PixelFourDirectionPresenter presenter;
        [SerializeField] private TopDownBeatCamera cameraRig;
        [SerializeField] private Transform attackFlash;

        [Header("Four-direction Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 4.2f;
        [SerializeField] private Vector2 movementBounds = new(10f, 7f);

        [Header("Beat Attack")]
        [SerializeField, Min(0)] private int baseDamage = 10;
        [SerializeField, Min(1f)] private float goodDamageMultiplier = 1.5f;
        [SerializeField, Min(1f)] private float perfectDamageMultiplier = 2f;
        [SerializeField, Min(0.1f)] private float attackDistance = 0.85f;
        [SerializeField, Min(0.1f)] private float attackRadius = 0.7f;
        [SerializeField, Min(0.01f)] private float attackCooldown = 0.22f;

        [Header("Quick Dodge")]
        [SerializeField, Min(0.1f)] private float dodgeSpeed = 11f;
        [SerializeField, Min(0.05f)] private float dodgeDuration = 0.16f;
        [SerializeField, Min(0.05f)] private float dodgeCooldown = 0.42f;

        private Rigidbody2D body;
        private InputAction moveAction;
        private InputAction attackAction;
        private InputAction dodgeAction;
        private Vector2 moveInput;
        private Vector2 testMoveInput;
        private Vector2 dodgeDirection;
        private float dodgeEndsAt;
        private float nextDodgeAt;
        private float nextAttackAt;

        public event Action<AttackResult> AttackPerformed;

        public Vector2 Facing { get; private set; } = Vector2.right;
        public bool IsDodging => Time.time < dodgeEndsAt;
        public Vector2 MoveInput => moveInput.sqrMagnitude > 0f ? moveInput : testMoveInput;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            CreateInputActions();
        }

        private void OnEnable()
        {
            moveAction.Enable();
            attackAction.performed += OnAttack;
            dodgeAction.performed += OnDodge;
            attackAction.Enable();
            dodgeAction.Enable();
        }

        private void OnDisable()
        {
            attackAction.performed -= OnAttack;
            dodgeAction.performed -= OnDodge;
            moveAction.Disable();
            attackAction.Disable();
            dodgeAction.Disable();
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
            attackAction?.Dispose();
            dodgeAction?.Dispose();
        }

        private void Update()
        {
            moveInput = moveAction.ReadValue<Vector2>();
            Vector2 desired = MoveInput;
            if (desired.sqrMagnitude > 1f)
                desired.Normalize();

            if (!IsDodging && desired.sqrMagnitude > 0.01f)
            {
                Facing = PixelFourDirectionPresenter.Cardinalize(desired);
                presenter?.SetFacing(Facing);
            }
        }

        private void FixedUpdate()
        {
            Vector2 velocity;
            if (IsDodging)
            {
                velocity = dodgeDirection * dodgeSpeed;
            }
            else
            {
                Vector2 desired = MoveInput;
                velocity = Vector2.ClampMagnitude(desired, 1f) * moveSpeed;
            }

            Vector2 next = body.position + velocity * Time.fixedDeltaTime;
            next.x = Mathf.Clamp(next.x, -movementBounds.x, movementBounds.x);
            next.y = Mathf.Clamp(next.y, -movementBounds.y, movementBounds.y);
            body.MovePosition(next);
        }

        public void Configure(
            RhythmClock clock,
            PixelFourDirectionPresenter visualPresenter,
            TopDownBeatCamera followCamera,
            Transform attackFeedback)
        {
            rhythmClock = clock;
            presenter = visualPresenter;
            cameraRig = followCamera;
            attackFlash = attackFeedback;
        }

        public void SetTestMove(Vector2 input)
        {
            testMoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public bool TryAttack()
        {
            if (rhythmClock == null || !rhythmClock.IsReady || IsDodging || Time.time < nextAttackAt)
                return false;

            nextAttackAt = Time.time + attackCooldown;
            RhythmClock.TimingGrade grade = rhythmClock.JudgeNow();
            float multiplier = grade switch
            {
                RhythmClock.TimingGrade.Perfect => perfectDamageMultiplier,
                RhythmClock.TimingGrade.Good => goodDamageMultiplier,
                _ => 1f
            };
            int damage = Mathf.RoundToInt(baseDamage * multiplier);
            bool hit = false;
            Vector2 center = body.position + Facing * attackDistance;
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(center, attackRadius);
            for (int i = 0; i < overlaps.Length; i++)
            {
                BeatTrainingDummy dummy = overlaps[i].GetComponentInParent<BeatTrainingDummy>();
                if (dummy == null)
                    continue;

                dummy.TakeDamage(damage, grade);
                hit = true;
                break;
            }

            StartCoroutine(PlayAttackFlash());
            cameraRig?.Kick(hit ? 0.055f : 0.025f);
            AttackPerformed?.Invoke(new AttackResult(grade, damage, hit));
            return true;
        }

        public bool TryDodge()
        {
            if (IsDodging || Time.time < nextDodgeAt)
                return false;

            Vector2 desired = MoveInput;
            dodgeDirection = desired.sqrMagnitude > 0.01f
                ? PixelFourDirectionPresenter.Cardinalize(desired)
                : Facing;
            Facing = dodgeDirection;
            presenter?.SetFacing(Facing);
            dodgeEndsAt = Time.time + dodgeDuration;
            nextDodgeAt = Time.time + dodgeCooldown;
            cameraRig?.Kick(0.035f, dodgeDuration);
            return true;
        }

        private IEnumerator PlayAttackFlash()
        {
            if (attackFlash == null)
                yield break;

            attackFlash.gameObject.SetActive(true);
            attackFlash.localPosition = (Vector3)(Facing * attackDistance);
            bool horizontal = Mathf.Abs(Facing.x) > 0.5f;
            attackFlash.localScale = horizontal
                ? new Vector3(0.8f, 0.18f, 1f)
                : new Vector3(0.18f, 0.8f, 1f);
            yield return new WaitForSecondsRealtime(0.09f);
            attackFlash.gameObject.SetActive(false);
        }

        private void CreateInputActions()
        {
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");

            attackAction = new InputAction("Attack", InputActionType.Button);
            attackAction.AddBinding("<Mouse>/leftButton");
            attackAction.AddBinding("<Keyboard>/j");
            attackAction.AddBinding("<Gamepad>/buttonWest");

            dodgeAction = new InputAction("Dodge", InputActionType.Button);
            dodgeAction.AddBinding("<Mouse>/rightButton");
            dodgeAction.AddBinding("<Keyboard>/space");
            dodgeAction.AddBinding("<Gamepad>/buttonSouth");
        }

        private void OnAttack(InputAction.CallbackContext _) => TryAttack();
        private void OnDodge(InputAction.CallbackContext _) => TryDodge();
    }
}
