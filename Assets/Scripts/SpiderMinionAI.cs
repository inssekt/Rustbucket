using UnityEngine;

/// <summary>
/// Small spider minion that swarms toward the player aggressively.
/// Spawned from spider eggs during the Spider Boss fight.
/// Simpler and more aggressive than the standard chaser enemy.
/// </summary>
public class SpiderMinionAI : EnemyBase
{
    [Header("Detection")]
    [SerializeField] private float detectionRange = 20f;
    [SerializeField] private float attackRange = 0.8f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask playerLayer;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 25f;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float jumpCooldown = 1f;

    [Header("Ground Check")]
    [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.3f);
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.4f, 0.1f);

    [Header("Wall Check")]
    [SerializeField] private Vector2 wallCheckOffset = new Vector2(0.3f, 0f);
    [SerializeField] private float wallCheckDistance = 0.2f;

    [Header("Attack")]
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackCooldown = 0.8f;
    [SerializeField] private float knockbackAmount = 5f;
    [SerializeField] private Vector2 attackSize = new Vector2(0.6f, 0.4f);
    [SerializeField] private Vector2 attackOffset = new Vector2(0.4f, 0f);

    [Header("Swarming Behavior")]
    [SerializeField] private float swarmSpreadForce = 2f;
    [SerializeField] private float swarmDetectionRadius = 1.5f;

    [Header("Lifetime")]
    [SerializeField] private bool hasLifetime = true;
    [SerializeField] private float lifetime = 30f;

    // State
    private enum State { Idle, Chase, Attack }
    private State currentState = State.Idle;

    private Transform player;
    private bool isGrounded;
    private float attackCooldownTimer;
    private float jumpCooldownTimer;
    private int facingDirection = 1;
    private float lifetimeTimer;

    private EnemyVisuals visuals;

    protected override void Awake()
    {
        base.Awake();
        player = FindPlayer();
        visuals = GetComponent<EnemyVisuals>();

        rb.freezeRotation = true;
        rb.gravityScale = 3f;

        lifetimeTimer = lifetime;
    }

    protected override void Update()
    {
        base.Update();

        if (isDead)
            return;

        if (player == null)
        {
            player = FindPlayer();
        }

        CheckGrounded();
        UpdateTimers();
        UpdateState();

        // Lifetime check
        if (hasLifetime)
        {
            lifetimeTimer -= Time.deltaTime;
            if (lifetimeTimer <= 0f)
            {
                Die();
            }
        }
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
            case State.Chase:
                HandleChase();
                break;
            case State.Attack:
                ApplyDeceleration();
                break;
        }

        // Apply swarm spreading to avoid stacking
        ApplySwarmBehavior();
    }

    private void UpdateState()
    {
        if (player == null)
        {
            currentState = State.Idle;
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

        // Chase if player in detection range (always chase - they're aggressive)
        if (distanceToPlayer <= detectionRange)
        {
            currentState = State.Chase;
            return;
        }

        currentState = State.Idle;
    }

    private void HandleChase()
    {
        if (player == null || !isGrounded)
            return;

        int direction = GetDirectionToward(player);
        facingDirection = direction;
        FaceDirection(facingDirection);

        // Check for wall ahead and try to jump over
        if (IsWallAhead(direction) && jumpCooldownTimer <= 0f)
        {
            Jump();
        }

        // Move toward player
        float targetVelocityX = direction * moveSpeed;
        float newVelocityX = Mathf.MoveTowards(rb.linearVelocity.x, targetVelocityX, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newVelocityX, rb.linearVelocity.y);

        // Jump if player is above
        if (player.position.y > transform.position.y + 1f && isGrounded && jumpCooldownTimer <= 0f)
        {
            Jump();
        }
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        jumpCooldownTimer = jumpCooldown;
    }

    private void PerformAttack()
    {
        attackCooldownTimer = attackCooldown;

        if (visuals != null)
        {
            visuals.TriggerAttackAnimation();
        }

        // Immediate damage for fast spider attacks
        ExecuteAttackDamage();
    }

    private void ExecuteAttackDamage()
    {
        Vector2 attackPos = (Vector2)transform.position + new Vector2(attackOffset.x * facingDirection, attackOffset.y);
        Collider2D[] hits = Physics2D.OverlapBoxAll(attackPos, attackSize, 0f, playerLayer);

        foreach (var hit in hits)
        {
            Vector2 knockbackDir = (hit.transform.position - transform.position).normalized;
            knockbackDir.y = Mathf.Max(knockbackDir.y, 0.3f);
            knockbackDir.Normalize();

            var playerController = hit.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.TakeDamageWithKnockback(attackDamage, knockbackDir, knockbackAmount);
            }
            else
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(attackDamage);
                }
            }
        }
    }

    private void ApplySwarmBehavior()
    {
        // Find nearby spider minions and spread out slightly
        Collider2D[] nearbyMinions = Physics2D.OverlapCircleAll(transform.position, swarmDetectionRadius);

        Vector2 spreadForce = Vector2.zero;
        int minionCount = 0;

        foreach (var col in nearbyMinions)
        {
            if (col.gameObject == gameObject)
                continue;

            SpiderMinionAI otherMinion = col.GetComponent<SpiderMinionAI>();
            if (otherMinion != null && !otherMinion.IsDead)
            {
                Vector2 awayDir = (transform.position - col.transform.position).normalized;
                float distance = Vector2.Distance(transform.position, col.transform.position);
                float force = (swarmDetectionRadius - distance) / swarmDetectionRadius;
                spreadForce += awayDir * force;
                minionCount++;
            }
        }

        if (minionCount > 0)
        {
            spreadForce /= minionCount;
            spreadForce.y = 0f; // Only spread horizontally
            rb.linearVelocity += spreadForce * swarmSpreadForce * Time.fixedDeltaTime;
        }
    }

    private void CheckGrounded()
    {
        Vector2 checkPos = (Vector2)transform.position + groundCheckOffset;
        isGrounded = Physics2D.OverlapBox(checkPos, groundCheckSize, 0f, groundLayer);
    }

    private bool IsWallAhead(int direction)
    {
        Vector2 checkPos = (Vector2)transform.position + new Vector2(wallCheckOffset.x * direction, wallCheckOffset.y);
        RaycastHit2D hit = Physics2D.Raycast(checkPos, Vector2.right * direction, wallCheckDistance, groundLayer);
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

        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer -= Time.deltaTime;
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

        // Wall check
        Gizmos.color = Color.cyan;
        Vector2 wallCheckPos = (Vector2)transform.position + new Vector2(wallCheckOffset.x * facingDirection, wallCheckOffset.y);
        Gizmos.DrawLine(wallCheckPos, wallCheckPos + Vector2.right * facingDirection * wallCheckDistance);

        // Attack area
        Gizmos.color = Color.magenta;
        Vector2 attackPos = (Vector2)transform.position + new Vector2(attackOffset.x * facingDirection, attackOffset.y);
        Gizmos.DrawWireCube(attackPos, attackSize);

        // Swarm detection radius
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, swarmDetectionRadius);
    }
}
