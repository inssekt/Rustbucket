using UnityEngine;

/// <summary>
/// Spider Boss with two attack patterns:
/// 1. Lunge Attack - Moves toward player, attacks, gets stuck in ground for a few seconds
/// 2. Egg Drop - Spawns spider eggs at the top of the arena that fall and spawn minions
/// </summary>
public class SpiderBossAI : EnemyBase
{
    [Header("Detection")]
    [SerializeField] private float detectionRange = 15f;
    [SerializeField] private float lungeAttackRange = 3f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask playerLayer;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float acceleration = 15f;

    [Header("Ground Check")]
    [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -1f);
    [SerializeField] private Vector2 groundCheckSize = new Vector2(1.5f, 0.2f);

    [Header("Lunge Attack")]
    [SerializeField] private float lungeSpeed = 12f;
    [SerializeField] private float lungeDuration = 0.4f;
    [SerializeField] private float lungeDamageDelay = 0.2f;
    [SerializeField] private float stuckDuration = 3f;
    [SerializeField] private int lungeDamage = 2;
    [SerializeField] private float lungeKnockback = 10f;
    [SerializeField] private Vector2 lungeHitboxSize = new Vector2(2f, 1.5f);
    [SerializeField] private Vector2 lungeHitboxOffset = new Vector2(1f, 0f);

    [Header("Egg Drop Attack")]
    [SerializeField] private GameObject spiderEggPrefab;
    [SerializeField] private int eggsPerWave = 5;
    [SerializeField] private float eggSpawnHeight = 10f;
    [SerializeField] private float arenaWidth = 20f;
    [SerializeField] private float eggSpawnInterval = 0.3f;
    [SerializeField] private float eggDropCooldown = 8f;

    [Header("Attack Pattern")]
    [SerializeField] private float lungeAttackCooldown = 2f;
    [SerializeField] private float patternSwitchTime = 10f;
    [SerializeField] [Range(0f, 1f)] private float eggDropChance = 0.3f;

    // State machine
    private enum BossState
    {
        Idle,
        Chase,
        PrepareLunge,
        Lunging,
        Stuck,
        EggDrop,
        Recovering
    }

    private BossState currentState = BossState.Idle;

    // Components
    private Transform player;
    private EnemyVisuals visuals;

    // State tracking
    private bool isGrounded;
    private int facingDirection = 1;
    private float stateTimer;
    private float lungeAttackCooldownTimer;
    private float eggDropCooldownTimer;
    private float patternTimer;
    private int eggsSpawned;
    private float eggSpawnTimer;
    private Vector2 lungeDirection;
    private bool hasDealtLungeDamage;
    private float lungeDamageDelayTimer;

    protected override void Awake()
    {
        base.Awake();
        player = FindPlayer();
        visuals = GetComponent<EnemyVisuals>();

        rb.freezeRotation = true;
        rb.gravityScale = 3f;
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
        UpdateStateMachine();
    }

    private void FixedUpdate()
    {
        if (isDead)
            return;

        switch (currentState)
        {
            case BossState.Idle:
            case BossState.Stuck:
            case BossState.EggDrop:
            case BossState.Recovering:
                ApplyDeceleration();
                break;
            case BossState.Chase:
                HandleChase();
                break;
            case BossState.Lunging:
                HandleLunge();
                break;
        }
    }

    private void UpdateStateMachine()
    {
        switch (currentState)
        {
            case BossState.Idle:
                UpdateIdleState();
                break;
            case BossState.Chase:
                UpdateChaseState();
                break;
            case BossState.PrepareLunge:
                UpdatePrepareLungeState();
                break;
            case BossState.Lunging:
                UpdateLungingState();
                break;
            case BossState.Stuck:
                UpdateStuckState();
                break;
            case BossState.EggDrop:
                UpdateEggDropState();
                break;
            case BossState.Recovering:
                UpdateRecoveringState();
                break;
        }
    }

    private void UpdateIdleState()
    {
        if (player == null)
            return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRange)
        {
            // Decide which attack pattern to use
            if (ShouldUseEggDrop())
            {
                TransitionToState(BossState.EggDrop);
            }
            else
            {
                TransitionToState(BossState.Chase);
            }
        }
    }

    private void UpdateChaseState()
    {
        if (player == null)
        {
            TransitionToState(BossState.Idle);
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // Check if we should do egg drop attack
        if (ShouldUseEggDrop())
        {
            TransitionToState(BossState.EggDrop);
            return;
        }

        // Check if in range for lunge attack
        if (distanceToPlayer <= lungeAttackRange && lungeAttackCooldownTimer <= 0f)
        {
            TransitionToState(BossState.PrepareLunge);
            return;
        }

        // If player too far, keep chasing
        if (distanceToPlayer > detectionRange)
        {
            TransitionToState(BossState.Idle);
        }
    }

    private void UpdatePrepareLungeState()
    {
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            // Calculate lunge direction toward player
            if (player != null)
            {
                lungeDirection = (player.position - transform.position).normalized;
                lungeDirection.y = Mathf.Clamp(lungeDirection.y, -0.3f, 0.3f); // Mostly horizontal
                lungeDirection.Normalize();
            }
            else
            {
                lungeDirection = new Vector2(facingDirection, 0f);
            }

            hasDealtLungeDamage = false;
            TransitionToState(BossState.Lunging);
        }
    }

    private void UpdateLungingState()
    {
        stateTimer -= Time.deltaTime;
        lungeDamageDelayTimer -= Time.deltaTime;

        // Check for player hit during lunge (only after delay has passed)
        if (!hasDealtLungeDamage && lungeDamageDelayTimer <= 0f)
        {
            CheckLungeHit();
        }

        if (stateTimer <= 0f)
        {
            TransitionToState(BossState.Stuck);
        }
    }

    private void UpdateStuckState()
    {
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            TransitionToState(BossState.Recovering);
        }
    }

    private void UpdateEggDropState()
    {
        // Spawn eggs over time
        eggSpawnTimer -= Time.deltaTime;

        if (eggSpawnTimer <= 0f && eggsSpawned < eggsPerWave)
        {
            SpawnEgg();
            eggsSpawned++;
            eggSpawnTimer = eggSpawnInterval;
        }

        // Done spawning eggs
        if (eggsSpawned >= eggsPerWave)
        {
            TransitionToState(BossState.Recovering);
        }
    }

    private void UpdateRecoveringState()
    {
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            if (player != null && Vector2.Distance(transform.position, player.position) <= detectionRange)
            {
                TransitionToState(BossState.Chase);
            }
            else
            {
                TransitionToState(BossState.Idle);
            }
        }
    }

    private void TransitionToState(BossState newState)
    {
        // Exit current state
        switch (currentState)
        {
            case BossState.Lunging:
                lungeAttackCooldownTimer = lungeAttackCooldown;
                break;
            case BossState.EggDrop:
                eggDropCooldownTimer = eggDropCooldown;
                patternTimer = 0f;
                break;
        }

        currentState = newState;

        // Enter new state
        switch (newState)
        {
            case BossState.PrepareLunge:
                stateTimer = 0.3f; // Brief wind-up
                if (player != null)
                {
                    facingDirection = GetDirectionToward(player);
                    FaceDirection(facingDirection);
                }
                if (visuals != null)
                {
                    visuals.TriggerAttackAnimation();
                }
                break;

            case BossState.Lunging:
                stateTimer = lungeDuration;
                lungeDamageDelayTimer = lungeDamageDelay;
                break;

            case BossState.Stuck:
                stateTimer = stuckDuration;
                rb.linearVelocity = Vector2.zero;
                break;

            case BossState.EggDrop:
                eggsSpawned = 0;
                eggSpawnTimer = 0f;
                rb.linearVelocity = Vector2.zero;
                break;

            case BossState.Recovering:
                stateTimer = 1f;
                break;
        }
    }

    private void HandleChase()
    {
        if (player == null || !isGrounded)
            return;

        int direction = GetDirectionToward(player);
        facingDirection = direction;
        FaceDirection(facingDirection);

        float targetVelocityX = direction * moveSpeed;
        float newVelocityX = Mathf.MoveTowards(rb.linearVelocity.x, targetVelocityX, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newVelocityX, rb.linearVelocity.y);
    }

    private void HandleLunge()
    {
        rb.linearVelocity = lungeDirection * lungeSpeed;
    }

    private void CheckLungeHit()
    {
        Vector2 hitboxPos = (Vector2)transform.position + new Vector2(lungeHitboxOffset.x * facingDirection, lungeHitboxOffset.y);
        Collider2D[] hits = Physics2D.OverlapBoxAll(hitboxPos, lungeHitboxSize, 0f, playerLayer);

        foreach (var hit in hits)
        {
            Vector2 knockbackDir = (hit.transform.position - transform.position).normalized;
            knockbackDir.y = Mathf.Max(knockbackDir.y, 0.4f);
            knockbackDir.Normalize();

            var playerController = hit.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.TakeDamageWithKnockback(lungeDamage, knockbackDir, lungeKnockback);
                hasDealtLungeDamage = true;
            }
            else
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(lungeDamage);
                    hasDealtLungeDamage = true;
                }
            }
        }
    }

    private void SpawnEgg()
    {
        if (spiderEggPrefab == null)
            return;

        // Random position at top of arena
        float randomX = transform.position.x + Random.Range(-arenaWidth / 2f, arenaWidth / 2f);
        Vector3 spawnPos = new Vector3(randomX, transform.position.y + eggSpawnHeight, 0f);

        Instantiate(spiderEggPrefab, spawnPos, Quaternion.identity);
    }

    private bool ShouldUseEggDrop()
    {
        if (eggDropCooldownTimer > 0f)
            return false;

        if (spiderEggPrefab == null)
            return false;

        // Use egg drop based on pattern timer or random chance
        patternTimer += Time.deltaTime;
        if (patternTimer >= patternSwitchTime)
        {
            return true;
        }

        return Random.value < eggDropChance * Time.deltaTime;
    }

    private void CheckGrounded()
    {
        Vector2 checkPos = (Vector2)transform.position + groundCheckOffset;
        isGrounded = Physics2D.OverlapBox(checkPos, groundCheckSize, 0f, groundLayer);
    }

    private void ApplyDeceleration()
    {
        float newVelocityX = Mathf.MoveTowards(rb.linearVelocity.x, 0f, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newVelocityX, rb.linearVelocity.y);
    }

    private void UpdateTimers()
    {
        if (lungeAttackCooldownTimer > 0f)
            lungeAttackCooldownTimer -= Time.deltaTime;

        if (eggDropCooldownTimer > 0f)
            eggDropCooldownTimer -= Time.deltaTime;
    }

    // Public state accessors for animations
    public bool IsStuck => currentState == BossState.Stuck;
    public bool IsLunging => currentState == BossState.Lunging;
    public bool IsEggDropping => currentState == BossState.EggDrop;

    private void OnDrawGizmosSelected()
    {
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Lunge attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, lungeAttackRange);

        // Ground check
        Gizmos.color = Color.green;
        Vector2 groundPos = (Vector2)transform.position + groundCheckOffset;
        Gizmos.DrawWireCube(groundPos, groundCheckSize);

        // Lunge hitbox
        Gizmos.color = Color.magenta;
        Vector2 hitboxPos = (Vector2)transform.position + new Vector2(lungeHitboxOffset.x * facingDirection, lungeHitboxOffset.y);
        Gizmos.DrawWireCube(hitboxPos, lungeHitboxSize);

        // Egg spawn area
        Gizmos.color = Color.cyan;
        Vector3 eggAreaCenter = transform.position + Vector3.up * eggSpawnHeight;
        Gizmos.DrawWireCube(eggAreaCenter, new Vector3(arenaWidth, 0.5f, 0f));
    }
}
