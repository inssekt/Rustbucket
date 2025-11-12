using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
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
    
    // Movement
    private Vector2 velocity;
    private bool canMove = true;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        playerInput = GetComponent<PlayerInput>();
        
        // Setup Rigidbody for manual physics control
        rb.gravityScale = 0f; // I handle gravity manually
        rb.freezeRotation = true;
    }
    
    private void Update()
    {
        HandleInput();
        CheckGrounded();
        CheckWall();
        UpdateTimers();
        
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
    
    private void HandleMovement()
    {
        if (!canMove && wallJumpControlTimer > 0f)
            return;
        
        float targetSpeed = horizontalInput * moveSpeed;
        float accel = isGrounded ? acceleration : airAcceleration;
        float decel = isGrounded ? deceleration : airDeceleration;
        
        if (Mathf.Abs(targetSpeed) > 0.01f)
        {
            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, accel * Time.fixedDeltaTime);
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
        {
            dashCooldownTimer -= Time.deltaTime;
        }
        
        // Wall stick
        if (wallStickTimer > 0f && !isWallSliding)
        {
            wallStickTimer -= Time.deltaTime;
        }
        
        // Wall jump control delay
        if (wallJumpControlTimer > 0f)
        {
            wallJumpControlTimer -= Time.deltaTime;
            
            if (wallJumpControlTimer <= 0f)
            {
                canMove = true;
            }
        }
        
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
    }
    
    // Public methods for external systems
    public bool IsGrounded() => isGrounded;
    public bool IsWallSliding() => isWallSliding;
    public bool IsDashing() => isDashing;
    public Vector2 GetVelocity() => velocity;
    public float GetHorizontalInput() => horizontalInput;
}
