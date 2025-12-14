using UnityEngine;

/// <summary>
/// Spider egg projectile that falls from above and spawns a spider minion on ground impact.
/// Used by the Spider Boss's egg drop attack.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SpiderEgg : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] private GameObject spiderMinionPrefab;
    [SerializeField] private int spidersToSpawn = 3;
    [SerializeField] private float spawnSpread = 0.5f;
    [SerializeField] private float spawnDelay = 0.2f;

    [Header("Physics")]
    [SerializeField] private float fallSpeed = 8f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Damage")]
    [SerializeField] private bool damagePlayerOnImpact = true;
    [SerializeField] private int impactDamage = 1;
    [SerializeField] private float impactRadius = 0.5f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Visuals")]
    [SerializeField] private float rotationSpeed = 180f;

    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 10f;

    private Rigidbody2D rb;
    private bool hasLanded;
    private float lifetimeTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f; // We control the fall speed manually
        rb.freezeRotation = false;
    }

    private void Start()
    {
        lifetimeTimer = maxLifetime;
    }

    private void Update()
    {
        if (hasLanded)
            return;

        // Rotate while falling for visual effect
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

        // Lifetime check
        lifetimeTimer -= Time.deltaTime;
        if (lifetimeTimer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (hasLanded)
            return;

        // Apply constant downward velocity
        rb.linearVelocity = Vector2.down * fallSpeed;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasLanded)
            return;

        // Check if we hit ground
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            OnLanded();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasLanded)
            return;

        // Check if we hit ground (for trigger colliders)
        if (((1 << other.gameObject.layer) & groundLayer) != 0)
        {
            OnLanded();
        }
    }

    private void OnLanded()
    {
        hasLanded = true;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        // Check for player damage on impact
        if (damagePlayerOnImpact)
        {
            CheckImpactDamage();
        }

        // Spawn spider minion after delay
        Invoke(nameof(SpawnMinion), spawnDelay);
    }

    private void CheckImpactDamage()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, impactRadius, playerLayer);

        foreach (var hit in hits)
        {
            var playerController = hit.GetComponent<PlayerController>();
            if (playerController != null)
            {
                Vector2 knockbackDir = (hit.transform.position - transform.position).normalized;
                knockbackDir.y = Mathf.Max(knockbackDir.y, 0.3f);
                knockbackDir.Normalize();
                playerController.TakeDamageWithKnockback(impactDamage, knockbackDir, 5f);
            }
            else
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(impactDamage);
                }
            }
        }
    }

    private void SpawnMinion()
    {
        if (spiderMinionPrefab != null)
        {
            for (int i = 0; i < spidersToSpawn; i++)
            {
                // Spawn slightly above ground with horizontal spread
                float offsetX = (i - (spidersToSpawn - 1) / 2f) * spawnSpread;
                Vector3 spawnPos = transform.position + Vector3.up * 0.2f + Vector3.right * offsetX;
                Instantiate(spiderMinionPrefab, spawnPos, Quaternion.identity);
            }
        }

        // Destroy the egg
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        // Impact damage radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, impactRadius);
    }
}
