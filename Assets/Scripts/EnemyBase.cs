using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Abstract base class for all enemies. Handles health, damage, and death.
/// Derive from this to create specific enemy types.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] protected int maxHealth = 3;
    [SerializeField] protected int currentHealth;

    [Header("Damage Feedback")]
    [SerializeField] protected float hitFlashDuration = 0.1f;
    [SerializeField] protected Color hitFlashColor = Color.red;

    [Header("Death")]
    [SerializeField] protected float deathDelay = 0.5f;
    [SerializeField] protected bool destroyOnDeath = true;

    [Header("Events")]
    public UnityEvent OnDamaged;
    public UnityEvent OnDeath;

    // Components
    protected Rigidbody2D rb;
    protected Collider2D col;
    protected SpriteRenderer spriteRenderer;

    // State
    protected bool isDead;
    protected float hitFlashTimer;
    protected Color originalColor;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        currentHealth = maxHealth;
    }

    protected virtual void Update()
    {
        HandleHitFlash();
    }

    /// <summary>
    /// Called by external systems (player whip, projectiles, etc.) to deal damage.
    /// </summary>
    public virtual void TakeDamage(int amount)
    {
        if (isDead)
            return;

        currentHealth -= amount;
        OnDamaged?.Invoke();

        // Visual feedback
        hitFlashTimer = hitFlashDuration;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        if (isDead)
            return;

        isDead = true;
        OnDeath?.Invoke();

        // Disable physics and collision
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        col.enabled = false;

        if (destroyOnDeath)
        {
            Destroy(gameObject, deathDelay);
        }
    }

    protected virtual void HandleHitFlash()
    {
        if (spriteRenderer == null)
            return;

        if (hitFlashTimer > 0f)
        {
            hitFlashTimer -= Time.deltaTime;
            spriteRenderer.color = hitFlashColor;
        }
        else
        {
            spriteRenderer.color = originalColor;
        }
    }

    /// <summary>
    /// Utility to find the player. Override if you have a different player tag or reference system.
    /// </summary>
    protected virtual Transform FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    /// <summary>
    /// Returns the horizontal direction toward a target (-1 or 1).
    /// </summary>
    protected int GetDirectionToward(Transform target)
    {
        if (target == null)
            return 0;

        float diff = target.position.x - transform.position.x;
        return diff > 0 ? 1 : -1;
    }

    [Header("Sprite Orientation")]
    [SerializeField] protected bool spriteDefaultFacesLeft = false;

    /// <summary>
    /// Flips the sprite to face a direction. Override if using scale-based flipping.
    /// </summary>
    protected virtual void FaceDirection(int direction)
    {
        if (spriteRenderer == null || direction == 0)
            return;

        // If sprite faces left by default, invert the flip logic
        if (spriteDefaultFacesLeft)
            spriteRenderer.flipX = direction > 0;
        else
            spriteRenderer.flipX = direction < 0;
    }

    // Public accessors
    public bool IsDead => isDead;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
}
