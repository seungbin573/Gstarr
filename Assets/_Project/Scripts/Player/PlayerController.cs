using UnityEngine;
using UnityEngine.InputSystem;
using Gstarr.Game;

namespace Gstarr.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Player 맵에 Move / Jump / Dash 액션이 있는 Input Action Asset. 비워두면 기본 바인딩을 코드로 생성")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Move")]
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float groundAcceleration = 90f;
        [SerializeField] private float groundDeceleration = 110f;
        [SerializeField] private float airAcceleration = 60f;
        [SerializeField] private float airDeceleration = 40f;
        [Range(0f, 1f)]
        [SerializeField] private float stickDeadZone = 0.2f;

        [Header("Jump")]
        [SerializeField] private float jumpVelocity = 15f;
        [Tooltip("점프 버튼을 일찍 떼면 상승 속도에 곱해지는 값 (가변 점프 높이)")]
        [Range(0f, 1f)]
        [SerializeField] private float jumpCutMultiplier = 0.5f;
        [SerializeField] private float coyoteTime = 0.1f;
        [SerializeField] private float jumpBufferTime = 0.12f;
        [Tooltip("지상 점프 후 공중에서 추가로 사용할 수 있는 점프 횟수 (더블 점프 기본값 1)")]
        [SerializeField, Min(0)] private int extraAirJumps = 1;

        [Header("Gravity")]
        [SerializeField] private float gravityScale = 4f;
        [SerializeField] private float fallGravityMultiplier = 1.6f;
        [SerializeField] private float maxFallSpeed = 22f;

        [Header("Dash")]
        [SerializeField] private float dashSpeed = 22f;
        [SerializeField] private float dashDuration = 0.15f;
        [SerializeField] private float dashCooldown = 0.25f;
        [Tooltip("공중에서 사용할 수 있는 대쉬 횟수 (착지 시 회복)")]
        [SerializeField] private int airDashCount = 1;
        [Tooltip("켜면 스틱 방향으로 8방향 대쉬, 끄면 좌우로만 대쉬")]
        [SerializeField] private bool eightDirectionDash = false;

        [Header("Ground Check")]
        [SerializeField] private LayerMask groundLayer;
        [Tooltip("이 각도 이하의 경사면까지 바닥으로 인식")]
        [Range(0f, 89f)]
        [SerializeField] private float maxGroundAngle = 45f;

        private Rigidbody2D rb;
        private ContactFilter2D groundFilter;

        private InputAction moveAction;
        private InputAction jumpAction;
        private InputAction dashAction;
        private InputActionMap ownedActionMap;

        private Vector2 moveInput;
        private float coyoteTimer;
        private float jumpBufferTimer;
        private int airJumpsLeft;
        private bool jumpCutRequested;
        private bool dashRequested;
        private bool isJumping;

        private float dashTimer;
        private float dashCooldownTimer;
        private int airDashesLeft;
        private Vector2 dashDirection;

        public bool IsGrounded { get; private set; }
        public bool IsDashing => dashTimer > 0f;
        public int FacingDirection { get; private set; } = 1;
        public Vector2 Velocity => rb.linearVelocity;
        public bool HasMoveInput => moveInput.sqrMagnitude > stickDeadZone * stickDeadZone;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            rb.gravityScale = gravityScale;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (groundLayer.value == 0)
                groundLayer = LayerMask.GetMask("Ground");

            groundFilter = new ContactFilter2D();
            groundFilter.SetLayerMask(groundLayer);
            groundFilter.SetNormalAngle(90f - maxGroundAngle, 90f + maxGroundAngle);

            SetupInput();
            ApplyTallHitbox();
            airJumpsLeft = extraAirJumps;
            if (GetComponent<SpriteMotion>() == null) gameObject.AddComponent<SpriteMotion>();
        }

        /// <summary>발 위치는 유지한 채, 몸통을 세로로 긴 직사각형으로 바꾼다.</summary>
        private void ApplyTallHitbox()
        {
            var existing = GetComponents<Collider2D>();
            PhysicsMaterial2D material = existing.Length > 0 ? existing[0].sharedMaterial : null;
            float foot = -0.72f;
            if (existing.Length > 0)
                foot = transform.InverseTransformPoint(existing[0].bounds.min).y;
            else
            {
                var art = GetComponent<SpriteRenderer>();
                if (art != null && art.sprite != null) foot = art.sprite.bounds.min.y;
            }

            BoxCollider2D box = null;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] is BoxCollider2D existingBox)
                    box = existingBox;
                else
                    existing[i].enabled = false;
            }

            const float height = 1.9f;
            const float width = 0.58f;
            if (box == null) box = gameObject.AddComponent<BoxCollider2D>();
            box.enabled = true;
            box.sharedMaterial = material;
            box.size = new Vector2(width, height);
            box.offset = new Vector2(0f, foot + height * 0.5f);
        }

        private void SetupInput()
        {
            if (inputActions != null)
            {
                var map = inputActions.FindActionMap("Player", throwIfNotFound: true);
                moveAction = map.FindAction("Move", throwIfNotFound: true);
                jumpAction = map.FindAction("Jump", throwIfNotFound: true);
                dashAction = map.FindAction("Dash", throwIfNotFound: true);
                return;
            }

            ownedActionMap = new InputActionMap("Player");
            moveAction = ownedActionMap.AddAction("Move", InputActionType.Value);
            moveAction.AddBinding("<Gamepad>/leftStick");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            jumpAction = ownedActionMap.AddAction("Jump", InputActionType.Button);
            jumpAction.AddBinding("<Gamepad>/buttonSouth");
            jumpAction.AddBinding("<Keyboard>/z");

            dashAction = ownedActionMap.AddAction("Dash", InputActionType.Button);
            dashAction.AddBinding("<Gamepad>/buttonWest");
            dashAction.AddBinding("<Keyboard>/x");
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            jumpAction?.Enable();
            dashAction?.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            jumpAction?.Disable();
            dashAction?.Disable();
        }

        private void OnDestroy()
        {
            ownedActionMap?.Dispose();
        }

        private void Update()
        {
            var health = GetComponent<Gstarr.Combat.Health>();
            if (health != null && health.Current <= 0)
            {
                StageHUD.ShowGameOver();
                moveInput = Vector2.zero;
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            if (!GamePause.BlocksGameplay && transform.position.y < -8f && health != null)
                health.TakeDamage(health.Current, Vector2.up);

            if (GamePause.BlocksGameplay)
            {
                moveInput = Vector2.zero;
                jumpBufferTimer = 0f;
                jumpCutRequested = false;
                dashRequested = false;
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            moveInput = moveAction.ReadValue<Vector2>();
            if (Mathf.Abs(moveInput.x) < stickDeadZone) moveInput.x = 0f;
            if (Mathf.Abs(moveInput.y) < stickDeadZone) moveInput.y = 0f;

            if (moveInput.x != 0f && !IsDashing)
                FacingDirection = moveInput.x > 0f ? 1 : -1;

            if (jumpAction.WasPressedThisFrame())
                jumpBufferTimer = jumpBufferTime;
            else
                jumpBufferTimer -= Time.deltaTime;

            if (jumpAction.WasReleasedThisFrame())
                jumpCutRequested = true;

            if (dashAction.WasPressedThisFrame())
                dashRequested = true;

            UpdateFacingVisual();
        }

        private void FixedUpdate()
        {
            if (GamePause.BlocksGameplay)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            float dt = Time.fixedDeltaTime;

            CheckGround();

            if (IsGrounded)
            {
                coyoteTimer = coyoteTime;
                airDashesLeft = airDashCount;
                airJumpsLeft = extraAirJumps;
            }
            else
            {
                coyoteTimer -= dt;
            }

            dashCooldownTimer -= dt;

            if (dashRequested)
            {
                dashRequested = false;
                TryStartDash();
            }

            if (IsDashing)
            {
                dashTimer -= dt;
                rb.linearVelocity = dashDirection * dashSpeed;
                if (dashTimer <= 0f)
                    EndDash();
                return;
            }

            HandleJump();
            HandleHorizontalMovement(dt);
            HandleGravity();
        }

        private void CheckGround()
        {
            IsGrounded = rb.IsTouching(groundFilter) && rb.linearVelocity.y <= 0.01f;

            if (IsGrounded)
                isJumping = false;
        }

        private void HandleJump()
        {
            if (jumpBufferTimer > 0f && (coyoteTimer > 0f || airJumpsLeft > 0))
            {
                bool isGroundJump = coyoteTimer > 0f;
                if (!isGroundJump) airJumpsLeft--;

                Vector2 v = rb.linearVelocity;
                v.y = jumpVelocity;
                rb.linearVelocity = v;

                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                isJumping = true;
                IsGrounded = false;
                GameAudio.Play(GameAudio.Id.Jump);
                GetComponent<SpriteMotion>()?.JumpPose();
            }

            if (jumpCutRequested)
            {
                jumpCutRequested = false;
                if (isJumping && rb.linearVelocity.y > 0f)
                {
                    Vector2 v = rb.linearVelocity;
                    v.y *= jumpCutMultiplier;
                    rb.linearVelocity = v;
                }
            }
        }

        private void HandleHorizontalMovement(float dt)
        {
            float targetSpeed = moveInput.x * moveSpeed;
            bool accelerating = Mathf.Abs(targetSpeed) > 0.01f;

            float rate = IsGrounded
                ? (accelerating ? groundAcceleration : groundDeceleration)
                : (accelerating ? airAcceleration : airDeceleration);

            Vector2 v = rb.linearVelocity;
            v.x = Mathf.MoveTowards(v.x, targetSpeed, rate * dt);
            rb.linearVelocity = v;
        }

        private void HandleGravity()
        {
            Vector2 v = rb.linearVelocity;
            rb.gravityScale = v.y < 0f ? gravityScale * fallGravityMultiplier : gravityScale;

            if (v.y < -maxFallSpeed)
            {
                v.y = -maxFallSpeed;
                rb.linearVelocity = v;
            }
        }

        private void TryStartDash()
        {
            if (IsDashing || dashCooldownTimer > 0f) return;
            if (!IsGrounded)
            {
                if (airDashesLeft <= 0) return;
                airDashesLeft--;
            }

            if (eightDirectionDash && moveInput != Vector2.zero)
                dashDirection = moveInput.normalized;
            else
                dashDirection = new Vector2(moveInput.x != 0f ? Mathf.Sign(moveInput.x) : FacingDirection, 0f);

            if (dashDirection.x != 0f)
                FacingDirection = dashDirection.x > 0f ? 1 : -1;

            dashTimer = dashDuration;
            dashCooldownTimer = dashDuration + dashCooldown;
            rb.gravityScale = 0f;
            GameAudio.Play(GameAudio.Id.Dash);
            isJumping = false;
            jumpBufferTimer = 0f;
        }

        private void EndDash()
        {
            dashTimer = 0f;
            rb.gravityScale = gravityScale;

            Vector2 v = rb.linearVelocity;
            v.x = Mathf.Clamp(v.x, -moveSpeed, moveSpeed);
            if (v.y > 0f) v.y *= 0.5f;
            rb.linearVelocity = v;
        }

        private void UpdateFacingVisual()
        {
            Vector3 s = transform.localScale;
            float x = Mathf.Abs(s.x) * FacingDirection;
            if (!Mathf.Approximately(s.x, x))
            {
                s.x = x;
                transform.localScale = s;
            }
        }
    }
}
