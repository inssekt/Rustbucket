using UnityEngine;

/// <summary>
/// A ground-based enemy that chases the player when in range and attacks when close.
/// Requires a ground layer to walk on.
/// </summary>
public class EnemyChaserAI : EnemyBase
{
    [Header("Detection")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float acceleration = 20f;

    [Header("Ground Check")]
    [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.5f);
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.8f, 0.1f);

    [Header("Edge Detection")]
    [SerializeField] private bool avoidEdges = true;
    [SerializeField] private Vector2 edgeCheckOffset = new Vector2(0.5f, -0.6f);
    [SerializeField] private float edgeCheckDistance = 0.3f;

    [Header("Attack")]
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float attackDelay = 0.2f;
    [SerializeField] private float knockbackAmount = 8f;
    [SerializeField] private Vector2 attackSize = new Vector2(1f, 0.5f);
    [SerializeField] private Vector2 attackOffset = new Vector2(0.8f, 0f);
    [SerializeField] private LayerMask playerLayer;

    [Header("Patrol (Optional)")]
    [SerializeField] private bool patrolWhenIdle = true;
    [SerializeField] private float patrolDistance = 3f;
    [SerializeField] private float patrolPauseTime = 1f;

    // State
    private enum State { Idle, Patrol, Chase, Attack }
    private State currentState = State.Idle;

    private Transform player;
    private bool isGrounded;
    private float attackCooldownTimer;
    private float attackDelayTimer;
    private bool attackPending;
    private int facingDirection = 1;

    // Patrol
    private Vector2 patrolOrigin;
    private int patrolDirection = 1;
    private float patrolPauseTimer;

    // Visuals
    private EnemyVisuals visuals;

    protected override void Awake()
    {
        base.Awake();
        patrolOrigin = transform.position;
        player = FindPlayer();
        visuals = GetComponent<EnemyVisuals>();

        // Setup rigidbody for ground movement
        rb.freezeRotation = true;
        rb.gravityScale = 3f;
    }

    protected override void Update()
    {
        base.Update();

        if (isDead)
            return;

        // Try to find player if we don't have a reference
        if (player == null)
        {
            player = FindPlayer();
        }

        CheckGrounded();
        UpdateTimers();
        UpdateState();
    }

    private void FixedUpdate()
    {
        if (isDead)
            return;

        switch (currentState)
        {
            case State.Idle:
                ApplyDeceleration();
                break;
            case State.Patrol:
                HandlePatrol();
                break;
            case State.Chase:
                HandleChase();
                break;
            case State.Attack:
                ApplyDeceleration();
                break;
        }
    }

    private void UpdateState()
    {
        if (player == null)
        {
            currentState = patrolWhenIdle ? State.Patrol : State.Idle;
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // Attack if in range and cooldown ready
        if (distanceToPlayer <= attackRange && attackCooldownTimer <= 0f)
        {
            currentState = State.Attack;
            PerformAttack();
            return;
        }

        // Chase if player in detection range
        if (distanceToPlayer <= detectionRange)
        {
            currentState = State.Chase;
            return;
        }

        // Otherwise patrol or idle
        currentState = patrolWhenIdle ? State.Patrol : State.Idle;
    }

    private void HandleChase()
    {
        if (player == null || !isGrounded)
            return;

        int direction = GetDirectionToward(player);

        // Check for edge ahead
        if (avoidEdges && !HasGroundAhead(direction))
        {
            ApplyDeceleration();
            return;
        }

        facingDirection = direction;
        FaceDirection(facingDirection);

        // Move toward player
        float targetVelocityX = direction * moveSpeed;
        float newVelocityX = Mathf.MoveTowards(rb.linearVelocity.x, targetVelocityX, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newVelocityX, rb.linearVelocity.y);
    }

    private void HandlePatrol()
    {
        if (!isGrounded)
            return;

        // Pause at patrol endpoints
        if (patrolPauseTimer > 0f)
        {
            ApplyDeceleration();
            return;
        }

        // Check if we've reached patrol boundary or edge
        float distanceFromOrigin = transform.position.x - patrolOrigin.x;
        bool atBoundary = Mathf.Abs(distanceFromOrigin) >= patrolDistance;
        bool atEdge = avoidEdges && !HasGroundAhead(patrolDirection);

        if (atBoundary || atEdge)
        {
            patrolDirection *= -1;
            patrolPauseTimer = patrolPauseTime;
            return;
        }

        facingDirection = patrolDirection;
        FaceDirection(facingDirection);

        // Move in patrol direction
        float targetVelocityX = patrolDirection * moveSpeed * 0.5f; // Slower patrol speed
        float newVelocityX = Mathf.MoveTowards(rb.linearVelocity.x, targetVelocityX, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newVelocityX, rb.linearVelocity.y);
    }

    private void PerformAttack()
    {
        attackCooldownTimer = attackCooldown;

        // Trigger attack animation
        if (visuals != null)
        {
            visuals.TriggerAttackAnimation();
        }

        // Start attack delay timer
        attackDelayTimer = attackDelay;
        attackPending = true;
    }

    private void ExecuteAttackDamage()
    {
        attackPending = false;

        // Calculate attack position
        Vector2 attackPos = (Vector2)transform.position + new Vector2(attackOffset.x * facingDirection, attackOffset.y);

        // Check for player in attack area (box)
        Collider2D[] hits = Physics2D.OverlapBoxAll(attackPos, attackSize, 0f, playerLayer);

        foreach (var hit in hits)
        {
            // Calculate knockback direction away from enemy
            Vector2 knockbackDir = (hit.transform.position - transform.position).normalized;
            knockbackDir.y = Mathf.Max(knockbackDir.y, 0.3f); // Add upward force
            knockbackDir.Normalize();

            // Try to apply damage with knockback (PlayerController)
            var playerController = hit.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.TakeDamageWithKnockback(attackDamage, knockbackDir, knockbackAmount);
            }
            else
            {
                // Fallback for other damageable objects
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(attackDamage);
                }
            }
        }
    }

    private void CheckGrounded()
    {
        Vector2 checkPos = (Vector2)transform.position + groundCheckOffset;
        isGrounded = Physics2D.OverlapBox(checkPos, groundCheckSize, 0f, groundLayer);
        
    }

    private bool HasGroundAhead(int direction)
    {
        Vector2 checkPos = (Vector2)transform.position + new Vector2(edgeCheckOffset.x * direction, edgeCheckOffset.y);
        RaycastHit2D hit = Physics2D.Raycast(checkPos, Vector2.down, edgeCheckDistance, groundLayer);
        return hit.collider != null;
    }

    private void ApplyDeceleration()
    {
        float newVelocityX = Mathf.MoveTowards(rb.linearVelocity.x, 0f, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newVelocityX, rb.linearVelocity.y);
    }

    private void UpdateTimers()
    {
        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        if (patrolPauseTimer > 0f)
            patrolPauseTimer -= Time.deltaTime;

        // Attack delay - execute damage after delay expires
        if (attackPending)
        {
            attackDelayTimer -= Time.deltaTime;
            if (attackDelayTimer <= 0f)
            {
                ExecuteAttackDamage();
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Ground check
        Gizmos.color = Color.green;
        Vector2 groundPos = (Vector2)transform.position + groundCheckOffset;
        Gizmos.DrawWireCube(groundPos, groundCheckSize);

        // Edge check
        Gizmos.color = Color.cyan;
        Vector2 edgeCheckPos = (Vector2)transform.position + new Vector2(edgeCheckOffset.x * facingDirection, edgeCheckOffset.y);
        Gizmos.DrawLine(edgeCheckPos, edgeCheckPos + Vector2.down * edgeCheckDistance);

        // Attack area (box)
        Gizmos.color = Color.magenta;
        Vector2 attackPos = (Vector2)transform.position + new Vector2(attackOffset.x * facingDirection, attackOffset.y);
        Gizmos.DrawWireCube(attackPos, attackSize);

        // Patrol bounds
        if (patrolWhenIdle)
        {
            Gizmos.color = Color.blue;
            Vector2 origin = Application.isPlaying ? patrolOrigin : (Vector2)transform.position;
            Gizmos.DrawLine(origin + Vector2.left * patrolDistance, origin + Vector2.right * patrolDistance);
        }
    }
}
