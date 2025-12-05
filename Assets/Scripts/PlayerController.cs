using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour, IDamageable
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 50f;
    [SerializeField] private float deceleration = 50f;
    [SerializeField] private float airAcceleration = 30f;
    [SerializeField] private float airDeceleration = 30f;
    
    [Header("Jump")]
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private float jumpCutMultiplier = 0.5f;
    [SerializeField] private float fallGravityMultiplier = 1.5f;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    
    [Header("Wall Jump")]
    [SerializeField] private bool enableWallJump = true;
    [SerializeField] private float wallJumpForce = 15f;
    [SerializeField] private Vector2 wallJumpDirection = new Vector2(1f, 1.5f);
    [SerializeField] private float wallSlideSpeed = 2f;
    [SerializeField] private float wallStickTime = 0.15f;
    [SerializeField] private float wallJumpControlDelay = 0.05f;
    
    [Header("Dash")]
    [SerializeField] private bool enableDash = true;
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;
    [SerializeField] private int maxAirDashes = 1;
    [SerializeField] private bool dashThroughWalls = false;
    
    [Header("Grapple")]
    [SerializeField] private bool enableGrapple = true;
    [SerializeField] private float grappleRange = 10f;
    [SerializeField] private float grappleDuration = 0.4f;
    [SerializeField] private float grappleArcHeight = 2f;
    [SerializeField] private LayerMask grappleLayer;
    
    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.9f, 0.1f);
    [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.5f);
    
    [Header("Wall Detection")]
    [SerializeField] private Vector2 wallCheckSize = new Vector2(0.1f, 0.9f);
    [SerializeField] private Vector2 wallCheckOffset = new Vector2(0.5f, 0f);
    
    [Header("Physics")]
    [SerializeField] private float gravity = 30f;
    [SerializeField] private float maxFallSpeed = 20f;

    [Header("Player Health")]
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private int currentHealth;
    [SerializeField] private float invincibilityDuration = 1f;
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float knockbackStunDuration = 0.2f;

    [Header("Melee Attack")]
    [SerializeField] private bool enableMeleeAttack = true;
    [SerializeField] private float attackCooldown = 0.4f;
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float attackRadius = 0.4f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private LayerMask attackHitLayer;
    [SerializeField] private bool attackPressed;
    [SerializeField] private float attackCooldownTimer;
    [SerializeField] private Vector2 attackDirection;
    private bool attackJustPerformed;

    // Health state
    private bool isInvincible;
    private float invincibilityTimer;
    private float knockbackStunTimer;

    // Components
    private Rigidbody2D rb;
    private Collider2D col;
    private PlayerInput playerInput;
    
    // Input values
    private Vector2 moveInput;
    
    // State
    private float horizontalInput;
    private bool jumpPressed;
    private bool jumpHeld;
    private bool dashPressed;
    private bool grapplePressed;
    
    // Ground state
    private bool isGrounded;
    private bool wasGrounded;
    private float lastGroundedTime;
    
    // Jump state
    private float lastJumpPressedTime;
    private bool isJumping;
    private bool isFalling;
    
    // Wall state
    private bool isTouchingWall;
    private bool isWallSliding;
    private int wallDirection;
    private float wallStickTimer;
    private float wallJumpControlTimer;
    
    // Dash state
    private bool isDashing;
    private float dashTimeLeft;
    private float dashCooldownTimer;
    private int airDashesRemaining;
    private Vector2 dashDirection;
    
    // Grapple state
    private bool isGrappling;
    private float grappleTimer;
    private Vector2 grappleStartPos;
    private Vector2 grappleEndPos;
    
    // Movement
    private Vector2 velocity;
    private bool canMove = true;
    private float lastFacingDirection = 1f;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        playerInput = GetComponent<PlayerInput>();
        
        // Setup Rigidbody for manual physics control
        rb.gravityScale = 0f; // I handle gravity manually
        rb.freezeRotation = true;

        currentHealth = maxHealth;
    }
    
    private void Update()
    {
        HandleInput();
        CheckGrounded();
        CheckWall();
        UpdateTimers();
        UpdateInvincibility();

        HandleMeleeRequest();
        HandleGrappleRequest();

        // Handle state transitions
        if (isGrounded && !wasGrounded)
        {
            OnLanded();
        }
        
        wasGrounded = isGrounded;
    }
    
    private void FixedUpdate()
    {
        // Get current velocity from rigidbody
        velocity = rb.linearVelocity;
        
        if (isDashing)
        {
            HandleDash();
        }
        else if (isGrappling)
        {
            HandleGrappleMovement();
        }
        else
        {
            HandleMovement();
            HandleJump();
            HandleWallSlide();
            HandleGravity();
        }
        
        ApplyVelocity();
    }
    
    private void HandleInput()
    {
        horizontalInput = moveInput.x;
        
        if (playerInput != null)
        {
            var jumpAction = playerInput.actions.FindAction("Jump");
            if (jumpAction != null && !jumpAction.IsPressed())
            {
                jumpHeld = false;
            }
        }
    }

    private void HandleMeleeRequest()
    {
        if (!enableMeleeAttack)
        {
            attackPressed = false;
            return;
        }

        if (attackCooldownTimer > 0f)
            return;

        if (attackPressed)
        {
            PerformMeleeAttack();
            attackPressed = false;
        }
    }

    private void PerformMeleeAttack()
    {
        // attack direction based on last horizontal input
        float faceDir = lastFacingDirection;
        attackDirection = new Vector2(faceDir, 0f);

        // attack origin
        Vector2 origin = (Vector2)transform.position + attackDirection * attackRange;

        // target detection
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius, attackHitLayer);

        foreach (var hit in hits)
        {
            // Optional: requires a damageable interface or component
            var damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(attackDamage);
            }
        }

        // cooldown
        attackCooldownTimer = attackCooldown;

        attackJustPerformed = true;

    }

    private void HandleGrappleRequest()
    {
        if (!enableGrapple)
        {
            grapplePressed = false;
            return;
        }

        if (!grapplePressed)
            return;

        grapplePressed = false;

        if (isGrappling || isDashing || knockbackStunTimer > 0f)
            return;

        GrapplePoint target = FindBestGrapplePoint();
        if (target == null)
            return;

        StartGrapple(target);
    }

    private void StartGrapple(GrapplePoint point)
    {
        if (point == null)
            return;

        Transform landing = point.LandingPoint;
        if (landing == null)
            return;

        grappleStartPos = rb.position;
        grappleEndPos = landing.position;
        grappleTimer = 0f;
        isGrappling = true;
        isDashing = false;
        velocity = Vector2.zero;
    }

    private void HandleGrappleMovement()
    {
        grappleTimer += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(grappleTimer / grappleDuration);

        Vector2 p0 = grappleStartPos;
        Vector2 p2 = grappleEndPos;
        Vector2 p1 = (p0 + p2) * 0.5f + Vector2.up * grappleArcHeight;

        float oneMinusT = 1f - t;
        Vector2 newPos = oneMinusT * oneMinusT * p0 +
                         2f * oneMinusT * t * p1 +
                         t * t * p2;

        rb.MovePosition(newPos);

        if (t >= 1f)
        {
            isGrappling = false;
        }
    }

    private GrapplePoint FindBestGrapplePoint()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, grappleRange, grappleLayer);

        GrapplePoint best = null;
        float bestDist = Mathf.Infinity;

        for (int i = 0; i < hits.Length; i++)
        {
            GrapplePoint gp = hits[i].GetComponent<GrapplePoint>();
            if (gp == null)
                continue;

            Transform landing = gp.LandingPoint;
            if (landing == null)
                continue;

            float dist = Vector2.Distance(transform.position, gp.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = gp;
            }
        }

        return best;
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }
    
    public void OnJump(InputValue value)
    {
        bool isPressed = value.isPressed;
        
        if (isPressed && !jumpHeld)
        {
            jumpPressed = true;
            lastJumpPressedTime = Time.time;
        }

        jumpHeld = isPressed;
    }
    
    public void OnSprint(InputValue value)
    {
        if (enableDash && value.isPressed)
        {
            dashPressed = true;
        }
    }

    public void OnAttack(InputValue value)
    {
        if (value.isPressed)
            attackPressed = true;
    }

    public void OnGrapple(InputValue value)
    {
        if (value.isPressed)
            grapplePressed = true;
    }

    private void HandleMovement()
    {
        // Skip movement during knockback stun
        if (knockbackStunTimer > 0f)
            return;

        if (!canMove && wallJumpControlTimer > 0f)
            return;
        
        float targetSpeed = horizontalInput * moveSpeed;
        float accel = isGrounded ? acceleration : airAcceleration;
        float decel = isGrounded ? deceleration : airDeceleration;
        
        if (Mathf.Abs(targetSpeed) > 0.01f)
        {
            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, accel * Time.fixedDeltaTime);
            lastFacingDirection = Mathf.Sign(horizontalInput);
        }
        else
        {
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, decel * Time.fixedDeltaTime);
        }
    }
    
    private void HandleJump()
    {
        // Coyote time and jump buffer
        bool canJump = (Time.time - lastGroundedTime) <= coyoteTime;
        bool wantsToJump = (Time.time - lastJumpPressedTime) <= jumpBufferTime;
        
        // Regular jump
        if (jumpPressed && canJump && !isJumping)
        {
            Jump(Vector2.up * jumpForce);
        }
        // Wall jump
        else if (jumpPressed && enableWallJump && isTouchingWall && !isGrounded)
        {
            WallJump();
        }
        
        // Reset jump pressed flag after checking
        if (jumpPressed)
        {
            jumpPressed = false;
        }
        
        if (!jumpHeld && isJumping && velocity.y > 0f)
        {
            velocity.y *= jumpCutMultiplier;
            isJumping = false;
        }
        
        // Update jump/fall state
        if (velocity.y < 0f)
        {
            isJumping = false;
            isFalling = true;
        }
        else if (isGrounded)
        {
            isJumping = false;
            isFalling = false;
        }
    }
    
    private void Jump(Vector2 force)
    {
        velocity.y = force.y;
        isJumping = true;
        isFalling = false;
        isWallSliding = false;
    }
    
    private void WallJump()
    {
        // Calculate wall jump direction
        Vector2 jumpDir = wallJumpDirection.normalized;
        jumpDir.x *= -wallDirection;
        
        velocity = jumpDir * wallJumpForce;
        
        isJumping = true;
        isFalling = false;
        isWallSliding = false;
        
        // Temporarily disable player control
        wallJumpControlTimer = wallJumpControlDelay;
        canMove = false;
    }
    
    private void HandleWallSlide()
    {
        if (!enableWallJump)
            return;
        
        // Wall slide conditions
        bool canWallSlide = isTouchingWall && !isGrounded && velocity.y < 0f;
        
        if (canWallSlide)
        {
            // Check if player is pressing towards wall
            bool pressingIntoWall = Mathf.Sign(horizontalInput) == wallDirection;
            
            if (pressingIntoWall || wallStickTimer > 0f)
            {
                isWallSliding = true;
                velocity.y = Mathf.Max(velocity.y, -wallSlideSpeed);
                
                // Wall stick
                if (pressingIntoWall)
                {
                    wallStickTimer = wallStickTime;
                }
            }
            else
            {
                isWallSliding = false;
            }
        }
        else
        {
            isWallSliding = false;
        }
    }
    
    private void HandleDash()
    {
        dashTimeLeft -= Time.fixedDeltaTime;
        
        if (dashTimeLeft <= 0f)
        {
            // End dash
            isDashing = false;
            velocity *= 0.5f; // Slight slowdown after dash
        }
        else
        {
            // Maintain dash velocity
            velocity = dashDirection * dashSpeed;
        }
    }
    
    private void StartDash()
    {
        if (dashCooldownTimer > 0f)
            return;
        
        // Check if we have dashes available
        if (!isGrounded && airDashesRemaining <= 0)
            return;
        
        Vector2 inputDir = moveInput;
        
        if (inputDir.magnitude < 0.1f)
        {
            // Default to facing direction
            inputDir = new Vector2(transform.localScale.x > 0 ? 1 : -1, 0f);
        }
        
        dashDirection = inputDir.normalized;
        
        // Start dash
        isDashing = true;
        dashTimeLeft = dashDuration;
        dashCooldownTimer = dashCooldown;
        
        if (!isGrounded)
        {
            airDashesRemaining--;
        }
        
        if (dashThroughWalls)
        {
            Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Default"), true);
        }
    }
    
    private void HandleGravity()
    {
        if (isWallSliding)
            return;
        
        float gravityMultiplier = 1f;
        
        if (isFalling)
        {
            gravityMultiplier = fallGravityMultiplier;
        }
        
        velocity.y -= gravity * gravityMultiplier * Time.fixedDeltaTime;
        
        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
    }
    
    private void ApplyVelocity()
    {
        rb.linearVelocity = velocity;
    }
    
    private void CheckGrounded()
    {
        Vector2 checkPos = (Vector2)transform.position + groundCheckOffset;
        isGrounded = Physics2D.OverlapBox(checkPos, groundCheckSize, 0f, groundLayer);
        
        if (isGrounded)
        {
            lastGroundedTime = Time.time;
        }
    }
    
    private void CheckWall()
    {
        // Check left wall
        Vector2 leftCheckPos = (Vector2)transform.position + new Vector2(-wallCheckOffset.x, wallCheckOffset.y);
        bool touchingLeftWall = Physics2D.OverlapBox(leftCheckPos, wallCheckSize, 0f, groundLayer);
        
        // Check right wall
        Vector2 rightCheckPos = (Vector2)transform.position + new Vector2(wallCheckOffset.x, wallCheckOffset.y);
        bool touchingRightWall = Physics2D.OverlapBox(rightCheckPos, wallCheckSize, 0f, groundLayer);
        
        isTouchingWall = touchingLeftWall || touchingRightWall;
        
        if (touchingLeftWall)
        {
            wallDirection = -1;
        }
        else if (touchingRightWall)
        {
            wallDirection = 1;
        }
    }

    private void UpdateTimers()
    {
        // Dash cooldown
        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;

        // Wall stick
        if (wallStickTimer > 0f && !isWallSliding)
            wallStickTimer -= Time.deltaTime;

        // Wall jump control delay
        if (wallJumpControlTimer > 0f)
        {
            wallJumpControlTimer -= Time.deltaTime;
            if (wallJumpControlTimer <= 0f)
                canMove = true;
        }

        // Attack cooldown
        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        // Invincibility
        if (invincibilityTimer > 0f)
            invincibilityTimer -= Time.deltaTime;

        // Knockback stun
        if (knockbackStunTimer > 0f)
            knockbackStunTimer -= Time.deltaTime;

        // Trigger dash when requested
        if (dashPressed)
        {
            StartDash();
            dashPressed = false;
        }
    }


    private void OnLanded()
    {
        // Reset air dashes
        airDashesRemaining = maxAirDashes;
        
        // Re-enable collision if dashing through walls
        if (dashThroughWalls)
        {
            Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Default"), false);
        }
    }
    
    private void OnDrawGizmos()
    {
        // Draw ground check
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector2 groundPos = (Vector2)transform.position + groundCheckOffset;
        Gizmos.DrawWireCube(groundPos, groundCheckSize);
        
        // Draw wall checks
        Gizmos.color = isTouchingWall ? Color.blue : Color.yellow;
        Vector2 leftWallPos = (Vector2)transform.position + new Vector2(-wallCheckOffset.x, wallCheckOffset.y);
        Gizmos.DrawWireCube(leftWallPos, wallCheckSize);
        
        Vector2 rightWallPos = (Vector2)transform.position + new Vector2(wallCheckOffset.x, wallCheckOffset.y);
        Gizmos.DrawWireCube(rightWallPos, wallCheckSize);
        
        // Draw velocity
        Gizmos.color = Color.magenta;
        if (Application.isPlaying && rb != null)
        {
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)rb.linearVelocity * 0.1f);
        }

        // Draw melee attack area
        if (enableMeleeAttack)
        {
            Gizmos.color = Color.white;
            float faceDir = Application.isPlaying ? lastFacingDirection : 1f;
            Vector2 origin = (Vector2)transform.position + new Vector2(faceDir * attackRange, 0f);
            Gizmos.DrawWireSphere(origin, attackRadius);
        }

        // Draw grapple range
        if (enableGrapple)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, grappleRange);
        }

    }

    // IDamageable implementation
    public void TakeDamage(int amount)
    {
        TakeDamageWithKnockback(amount, Vector2.zero, 0f);
    }

    /// <summary>
    /// Take damage with knockback from a specific direction.
    /// </summary>
    public void TakeDamageWithKnockback(int amount, Vector2 knockbackDirection, float knockbackForceAmount)
    {
        if (isInvincible || currentHealth <= 0)
            return;

        currentHealth -= amount;
        isInvincible = true;
        invincibilityTimer = invincibilityDuration;

        // Apply knockback if provided, otherwise use default
        if (knockbackForceAmount > 0f)
        {
            ApplyKnockback(knockbackDirection, knockbackForceAmount);
        }
        else
        {
            // Default knockback away from facing direction
            float faceDir = lastFacingDirection;
            ApplyKnockback(new Vector2(-faceDir, 0.5f), knockbackForce);
        }

        if (currentHealth <= 0)
        {
            OnPlayerDeath();
        }
    }

    /// <summary>
    /// Apply knockback to the player from an external source.
    /// </summary>
    /// <param name="direction">Direction of knockback (will be normalized)</param>
    /// <param name="force">Strength of the knockback</param>
    public void ApplyKnockback(Vector2 direction, float force)
    {
        velocity = direction.normalized * force;
        rb.linearVelocity = velocity; // Apply immediately to rigidbody
        knockbackStunTimer = knockbackStunDuration; // Briefly disable movement
    }

    /// <summary>
    /// Apply knockback away from a source position.
    /// </summary>
    /// <param name="sourcePosition">Position of the damage source</param>
    /// <param name="force">Strength of the knockback</param>
    public void ApplyKnockbackFromSource(Vector2 sourcePosition, float force)
    {
        Vector2 direction = ((Vector2)transform.position - sourcePosition).normalized;
        // Add some upward force
        direction.y = Mathf.Max(direction.y, 0.3f);
        velocity = direction.normalized * force;
    }

    private void OnPlayerDeath()
    {
        // For now, just log. You can add respawn, game over screen, etc.
        Debug.Log("Player died!");
        // Optionally disable controls:
        // canMove = false;
    }

    private void UpdateInvincibility()
    {
        isInvincible = invincibilityTimer > 0f;
    }

    // Public methods for external systems
    public bool IsGrounded() => isGrounded;
    public bool IsWallSliding() => isWallSliding;
    public bool IsDashing() => isDashing;
    public bool IsInvincible() => isInvincible;
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public Vector2 GetVelocity() => velocity;
    public float GetHorizontalInput() => horizontalInput;
    public bool ConsumeAttackJustPerformed()
    {
        if (!attackJustPerformed)
            return false;

        attackJustPerformed = false;
        return true;
    }

    public void UnlockDash()
    {
        enableDash = true;
    }

    public void UnlockGrapple()
    {
        enableGrapple = true;
    }

    public void UnlockMeleeAttack()
    {
        enableMeleeAttack = true;
    }
}
