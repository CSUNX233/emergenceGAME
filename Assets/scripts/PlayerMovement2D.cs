using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMovement2D : MonoBehaviour
{
    [Header("Move")]
    public float maxRunSpeed = 8f;
    public float groundAcceleration = 95f;
    public float groundDeceleration = 140f;
    public float airAcceleration = 48f;
    public float airDeceleration = 60f;
    public float turnAccelerationMultiplier = 1.7f;

    [Header("Push")]
    public float pushProbeDistance = 0.08f;
    public float pushWeightStart = 0.8f;
    public float pushSlowdownPerWeight = 0.22f;
    public float minimumPushSpeedMultiplier = 0.2f;

    [Header("Jump")]
    public float jumpVelocity = 10.8f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.14f;
    public float maxJumpHoldTime = 0.18f;
    public float jumpHoldGravityMultiplier = 0.5f;
    public float fallGravityMultiplier = 1.9f;
    public float jumpCutGravityMultiplier = 2.6f;
    public float maxFallSpeed = 17f;
    public float apexControlThreshold = 1.6f;
    public float apexAccelerationMultiplier = 1.12f;
    public float apexSpeedBonus = 0.35f;

    [Header("Wall Jump")]
    public bool enableWallSlideAndJump = true;
    public Vector2 wallCheckBox = new Vector2(0.08f, 0.9f);
    public float wallCheckExtraOffset = 0.03f;
    public bool wallSlideRequiresInput = true;
    public float wallSlideMaxFallSpeed = 7.2f;
    public float wallNoInputMaxFallSpeed = 15f;
    public float wallSlideBrakeAcceleration = 38f;
    public float wallCoyoteTime = 0.12f;
    public Vector2 wallJumpVelocity = new Vector2(4.4f, 10.8f);
    public float wallJumpHorizontalLockTime = 0.11f;
    public float wallJumpLockedAccelerationMultiplier = 0.55f;

    [Header("Ground Check")]
    public LayerMask groundLayer = ~0;
    public Vector2 groundCheckBox = new Vector2(0.6f, 0.08f);
    public float groundCheckExtraOffset = 0.02f;
    public float groundNormalThreshold = 0.55f;

    [Header("Animation")]
    public string speedParameter = "speed";
    public string jumpParameter = "Isjump";
    public float animatorSpeedScale = 1f;

    private Rigidbody2D cachedRigidbody;
    private Animator cachedAnimator;
    private SpriteRenderer cachedSpriteRenderer;
    private Collider2D cachedCollider;
    private readonly RaycastHit2D[] pushHits = new RaycastHit2D[8];
    private readonly Collider2D[] groundHits = new Collider2D[12];
    private readonly Collider2D[] wallHits = new Collider2D[12];
    private float moveInput;
    private bool isGrounded;
    private bool wasGrounded;
    private bool isTouchingWall;
    private bool isWallSliding;
    private int wallDirection;
    private int wallCoyoteDirection;
    private bool jumpHeld;
    private bool jumpPressedThisFrame;
    private bool jumpReleasedThisFrame;
    private bool isJumping;
    private float coyoteCounter;
    private float wallCoyoteCounter;
    private float jumpBufferCounter;
    private float jumpHoldCounter;
    private float wallJumpLockCounter;
    private float wallJumpDirection;
    private float baseGravityScale;
    private int speedParameterHash;
    private int jumpParameterHash;

    void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody2D>();
        cachedAnimator = GetComponent<Animator>();
        cachedSpriteRenderer = GetComponent<SpriteRenderer>();
        cachedCollider = GetComponent<Collider2D>();
        speedParameterHash = Animator.StringToHash(speedParameter);
        jumpParameterHash = Animator.StringToHash(jumpParameter);
        baseGravityScale = cachedRigidbody.gravityScale;

        cachedRigidbody.freezeRotation = true;
        cachedRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        cachedRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Update()
    {
        UpdateGroundedState();
        moveInput = Input.GetAxisRaw("Horizontal");
        UpdateWallState();
        ReadJumpInput();
        UpdateJumpTimers();
        TryConsumeBufferedJump();
        ApplyJumpGravity();
        UpdateFacing();
        UpdateAnimator();
    }

    void FixedUpdate()
    {
        UpdateGroundedState();
        UpdateWallState();
        ApplyHorizontalMovement();
    }

    void ApplyHorizontalMovement()
    {
        float pushMultiplier = GetPushSpeedMultiplier();
        float speedLimit = maxRunSpeed;
        float currentSpeed = cachedRigidbody.velocity.x;
        float inputForMovement = moveInput;

        bool hasInput = Mathf.Abs(moveInput) > 0.01f;
        bool reversing = hasInput && Mathf.Sign(moveInput) != Mathf.Sign(currentSpeed) && Mathf.Abs(currentSpeed) > 0.05f;

        if (wallJumpLockCounter > 0f)
        {
            inputForMovement = wallJumpDirection;
            hasInput = true;
            reversing = false;
            wallJumpLockCounter = Mathf.Max(0f, wallJumpLockCounter - Time.fixedDeltaTime);
        }

        float acceleration = isGrounded
            ? (hasInput ? groundAcceleration : groundDeceleration)
            : (hasInput ? airAcceleration : airDeceleration);

        if (!isGrounded && wallJumpLockCounter > 0f)
            acceleration *= wallJumpLockedAccelerationMultiplier;

        if (!isGrounded)
        {
            float apexFactor = GetApexFactor();
            if (apexFactor > 0f && hasInput)
            {
                acceleration *= Mathf.Lerp(1f, apexAccelerationMultiplier, apexFactor);
                speedLimit += apexSpeedBonus * apexFactor;
            }
        }

        if (reversing)
            acceleration *= turnAccelerationMultiplier;

        float targetSpeed = inputForMovement * speedLimit * pushMultiplier;
        float nextSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);
        cachedRigidbody.velocity = new Vector2(nextSpeed, cachedRigidbody.velocity.y);
    }

    float GetApexFactor()
    {
        if (isGrounded || cachedRigidbody == null)
            return 0f;

        float absVerticalSpeed = Mathf.Abs(cachedRigidbody.velocity.y);
        if (absVerticalSpeed >= apexControlThreshold)
            return 0f;

        return 1f - absVerticalSpeed / Mathf.Max(0.01f, apexControlThreshold);
    }

    float GetPushSpeedMultiplier()
    {
        if (cachedCollider == null || Mathf.Abs(moveInput) < 0.01f)
            return 1f;

        Vector2 direction = moveInput > 0f ? Vector2.right : Vector2.left;
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(groundLayer);

        int hitCount = cachedCollider.Cast(direction, filter, pushHits, pushProbeDistance);
        float heaviestWeight = 0f;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = pushHits[i];
            Collider2D hitCollider = hit.collider;
            if (hitCollider == null || hitCollider == cachedCollider)
                continue;

            if (Mathf.Abs(hit.normal.x) < 0.35f)
                continue;

            float weight = GetWeightForCollider(hitCollider);
            if (weight > heaviestWeight)
                heaviestWeight = weight;
        }

        if (heaviestWeight <= pushWeightStart)
            return 1f;

        float overload = heaviestWeight - pushWeightStart;
        float multiplier = 1f - overload * pushSlowdownPerWeight;
        return Mathf.Clamp(multiplier, minimumPushSpeedMultiplier, 1f);
    }

    float GetWeightForCollider(Collider2D hitCollider)
    {
        if (hitCollider == null)
            return 0f;

        PhysicalWeight weight = hitCollider.GetComponent<PhysicalWeight>();
        if (weight != null)
            return weight.CurrentWeight;

        Rigidbody2D hitBody = hitCollider.attachedRigidbody;
        if (hitBody == null)
            return 0f;

        if (hitBody.bodyType == RigidbodyType2D.Static)
            return 999f;

        return hitBody.mass;
    }

    void UpdateGroundedState()
    {
        wasGrounded = isGrounded;
        Vector2 checkCenter = GetGroundCheckCenter();
        isGrounded = IsTouchingGround(checkCenter);

        if (isGrounded && !wasGrounded && cachedRigidbody.velocity.y <= 0f)
            isJumping = false;
    }

    void UpdateWallState()
    {
        isTouchingWall = false;
        isWallSliding = false;
        wallDirection = 0;

        if (!enableWallSlideAndJump || cachedCollider == null)
            return;

        bool touchingLeft = IsTouchingWallSide(-1);
        bool touchingRight = IsTouchingWallSide(1);

        if (touchingLeft && touchingRight)
            wallDirection = Mathf.Abs(moveInput) > 0.01f ? (moveInput > 0f ? 1 : -1) : 0;
        else if (touchingRight)
            wallDirection = 1;
        else if (touchingLeft)
            wallDirection = -1;

        isTouchingWall = wallDirection != 0;
        if (!isTouchingWall || isGrounded || cachedRigidbody.velocity.y > 0.1f)
            return;

        bool pressingTowardWall = Mathf.Abs(moveInput) > 0.01f && Mathf.Sign(moveInput) == wallDirection;
        if (pressingTowardWall || !wallSlideRequiresInput)
            isWallSliding = true;
    }

    bool IsTouchingWallSide(int side)
    {
        Bounds bounds = cachedCollider.bounds;
        Vector2 checkCenter = new Vector2(
            bounds.center.x + side * (bounds.extents.x + wallCheckBox.x * 0.5f + wallCheckExtraOffset),
            bounds.center.y
        );

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(groundLayer);

        int hitCount = Physics2D.OverlapBox(checkCenter, wallCheckBox, 0f, filter, wallHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = wallHits[i];
            if (hit == null || hit == cachedCollider)
                continue;

            if (side > 0 && hit.bounds.min.x < bounds.max.x - 0.08f)
                continue;

            if (side < 0 && hit.bounds.max.x > bounds.min.x + 0.08f)
                continue;

            float verticalOverlap = Mathf.Min(bounds.max.y, hit.bounds.max.y) - Mathf.Max(bounds.min.y, hit.bounds.min.y);
            if (verticalOverlap > 0.18f)
                return true;
        }

        return false;
    }

    bool IsTouchingGround(Vector2 checkCenter)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(groundLayer);

        int hitCount = Physics2D.OverlapBox(checkCenter, groundCheckBox, 0f, filter, groundHits);
        float feetY = cachedCollider != null ? cachedCollider.bounds.min.y : checkCenter.y;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = groundHits[i];
            if (hit == null || hit == cachedCollider)
                continue;

            ColliderDistance2D distance = cachedCollider.Distance(hit);
            Vector2 normal = distance.normal;
            float surfaceTop = hit.bounds.max.y;

            // 脚底检测框碰到角色脚下附近的平台表面，就视为接地。
            if (surfaceTop <= feetY + 0.14f && surfaceTop >= feetY - 0.2f)
                return true;

            if (normal.y >= groundNormalThreshold)
                return true;
        }

        return false;
    }

    void ReadJumpInput()
    {
        jumpPressedThisFrame = Input.GetKeyDown(KeyCode.Space);
        jumpReleasedThisFrame = Input.GetKeyUp(KeyCode.Space);
        jumpHeld = Input.GetKey(KeyCode.Space);

        if (jumpPressedThisFrame)
            jumpBufferCounter = jumpBufferTime;
    }

    void UpdateJumpTimers()
    {
        coyoteCounter = isGrounded ? coyoteTime : Mathf.Max(0f, coyoteCounter - Time.deltaTime);

        if (isWallSliding || (isTouchingWall && !isGrounded))
        {
            wallCoyoteCounter = wallCoyoteTime;
            wallCoyoteDirection = wallDirection;
        }
        else
        {
            wallCoyoteCounter = Mathf.Max(0f, wallCoyoteCounter - Time.deltaTime);
        }

        if (jumpBufferCounter > 0f)
            jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter - Time.deltaTime);

        if (isJumping && jumpHeld)
            jumpHoldCounter = Mathf.Max(0f, jumpHoldCounter - Time.deltaTime);
        else
            jumpHoldCounter = 0f;
    }

    void TryConsumeBufferedJump()
    {
        if (jumpBufferCounter <= 0f)
            return;

        if (coyoteCounter > 0f)
        {
            PerformJump();
            return;
        }

        if (enableWallSlideAndJump && wallCoyoteCounter > 0f && wallCoyoteDirection != 0)
            PerformWallJump(wallCoyoteDirection);
    }

    void PerformJump()
    {
        Vector2 velocity = cachedRigidbody.velocity;
        if (velocity.y < 0f)
            velocity.y = 0f;

        velocity.y = jumpVelocity;
        cachedRigidbody.velocity = velocity;

        isGrounded = false;
        isJumping = true;
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        jumpHoldCounter = maxJumpHoldTime;
    }

    void PerformWallJump(int jumpWallDirection)
    {
        float awayDirection = -Mathf.Sign(jumpWallDirection);
        bool pressingAwayFromWall = Mathf.Abs(moveInput) > 0.01f && Mathf.Sign(moveInput) == awayDirection;
        float horizontalVelocity = pressingAwayFromWall
            ? awayDirection * wallJumpVelocity.x
            : cachedRigidbody.velocity.x;

        cachedRigidbody.velocity = new Vector2(horizontalVelocity, wallJumpVelocity.y);

        isGrounded = false;
        isWallSliding = false;
        isJumping = true;
        wallJumpDirection = pressingAwayFromWall ? awayDirection : 0f;
        wallJumpLockCounter = pressingAwayFromWall ? wallJumpHorizontalLockTime : 0f;
        coyoteCounter = 0f;
        wallCoyoteCounter = 0f;
        jumpBufferCounter = 0f;
        jumpHoldCounter = maxJumpHoldTime;
    }

    void ApplyJumpGravity()
    {
        float gravityMultiplier = 1f;
        float verticalVelocity = cachedRigidbody.velocity.y;

        if (verticalVelocity < 0f)
            gravityMultiplier = fallGravityMultiplier;
        else if (isJumping && jumpHeld && jumpHoldCounter > 0f)
        {
            gravityMultiplier = jumpHoldGravityMultiplier;
        }
        else if (verticalVelocity > 0f && (!jumpHeld || jumpReleasedThisFrame))
        {
            gravityMultiplier = jumpCutGravityMultiplier;
        }

        cachedRigidbody.gravityScale = baseGravityScale * gravityMultiplier;

        if (isTouchingWall && !isGrounded && cachedRigidbody.velocity.y < 0f)
        {
            float targetWallFallSpeed = isWallSliding ? wallSlideMaxFallSpeed : wallNoInputMaxFallSpeed;
            float y = Mathf.MoveTowards(
                cachedRigidbody.velocity.y,
                -targetWallFallSpeed,
                wallSlideBrakeAcceleration * Time.deltaTime
            );
            cachedRigidbody.velocity = new Vector2(cachedRigidbody.velocity.x, y);
        }

        if (cachedRigidbody.velocity.y < -maxFallSpeed)
            cachedRigidbody.velocity = new Vector2(cachedRigidbody.velocity.x, -maxFallSpeed);
    }

    Vector2 GetGroundCheckCenter()
    {
        if (cachedCollider == null)
            return transform.position;

        Bounds bounds = cachedCollider.bounds;
        return new Vector2(bounds.center.x, bounds.min.y + groundCheckBox.y * 0.5f + groundCheckExtraOffset);
    }

    void UpdateFacing()
    {
        if (Mathf.Abs(moveInput) < 0.01f)
            return;

        cachedSpriteRenderer.flipX = moveInput < 0f;
    }

    void UpdateAnimator()
    {
        if (cachedAnimator == null)
            return;

        float horizontalSpeed = Mathf.Abs(cachedRigidbody != null ? cachedRigidbody.velocity.x : 0f);
        cachedAnimator.SetFloat(speedParameterHash, horizontalSpeed * animatorSpeedScale);
        cachedAnimator.SetBool(jumpParameterHash, !isGrounded);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Collider2D drawCollider = cachedCollider != null ? cachedCollider : GetComponent<Collider2D>();
        Vector3 center;
        if (drawCollider != null)
        {
            Bounds bounds = drawCollider.bounds;
            center = new Vector3(bounds.center.x, bounds.min.y + groundCheckBox.y * 0.5f + groundCheckExtraOffset, transform.position.z);
        }
        else
        {
            center = transform.position;
        }
        Gizmos.DrawWireCube(center, groundCheckBox);

        if (drawCollider != null)
        {
            Bounds bounds = drawCollider.bounds;
            Gizmos.color = Color.cyan;
            Vector3 leftCenter = new Vector3(
                bounds.center.x - (bounds.extents.x + wallCheckBox.x * 0.5f + wallCheckExtraOffset),
                bounds.center.y,
                transform.position.z
            );
            Vector3 rightCenter = new Vector3(
                bounds.center.x + bounds.extents.x + wallCheckBox.x * 0.5f + wallCheckExtraOffset,
                bounds.center.y,
                transform.position.z
            );
            Gizmos.DrawWireCube(leftCenter, wallCheckBox);
            Gizmos.DrawWireCube(rightCenter, wallCheckBox);
        }
    }
}
