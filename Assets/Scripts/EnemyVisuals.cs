using UnityEngine;

/// <summary>
/// Handles visual feedback for enemies: sprite flipping, animations, damage flash.
/// Attach to the same GameObject as EnemyBase (or a derived class).
/// </summary>
[RequireComponent(typeof(EnemyBase))]
public class EnemyVisuals : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    [Header("Animation Parameters")]
    [SerializeField] private string horizontalSpeedParam = "HorizontalSpeed";
    [SerializeField] private string attackTriggerParam = "Attack";
    [SerializeField] private bool useAttackTrigger = true;

    private EnemyBase enemyBase;
    private Rigidbody2D rb;

    private void Awake()
    {
        enemyBase = GetComponent<EnemyBase>();
        rb = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Update()
    {
        HandleAnimations();
    }

    private void HandleAnimations()
    {
        if (animator == null)
            return;

        // Set horizontal speed for walk/idle blend
        if (!string.IsNullOrEmpty(horizontalSpeedParam))
        {
            float speed = rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;
            animator.SetFloat(horizontalSpeedParam, speed);
        }
    }

    /// <summary>
    /// Call this from the enemy AI when an attack is performed.
    /// </summary>
    public void TriggerAttackAnimation()
    {
        if (animator == null || !useAttackTrigger)
            return;

        if (!string.IsNullOrEmpty(attackTriggerParam))
        {
            animator.SetTrigger(attackTriggerParam);
        }
    }
}
