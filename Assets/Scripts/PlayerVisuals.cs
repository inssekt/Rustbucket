using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerVisuals : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
    
    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem jumpParticles;
    [SerializeField] private ParticleSystem landParticles;
    [SerializeField] private ParticleSystem dashParticles;
    [SerializeField] private ParticleSystem wallSlideParticles;
    
    [Header("Sprite Flip")]
    [SerializeField] private bool flipOnDirection = true;
    
    [Header("Dash Effect")]
    [SerializeField] private TrailRenderer dashTrail;
    [SerializeField] private float dashTrailTime = 0.3f;
    
    [Header("Squash and Stretch")]
    [SerializeField] private bool enableSquashStretch = true;
    [SerializeField] private float jumpSquashAmount = 0.8f;
    [SerializeField] private float landSquashAmount = 0.6f;
    [SerializeField] private float squashDuration = 0.1f;

    [Header("Damage Feedback")]
    [SerializeField] private float invincibilityFlashRate = 0.1f;
    
    private PlayerController playerController;
    private Vector3 originalScale;
    private bool wasGrounded;
    private bool wasDashing;
    private float squashTimer;
    private Vector3 targetScale;
    private float flashTimer;
    
    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        
        originalScale = transform.localScale;
        targetScale = originalScale;
    }
    
    private void Update()
    {
        HandleSpriteFlip();
        HandleAnimations();
        HandleParticles();
        HandleSquashStretch();
        HandleDashTrail();
        HandleWhipAnimation();
        HandleInvincibilityFlash();
    }
    
    private void HandleSpriteFlip()
    {
        if (!flipOnDirection || spriteRenderer == null)
            return;
        
        float horizontalInput = playerController.GetHorizontalInput();
        
        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            spriteRenderer.flipX = horizontalInput < 0;
        }
    }
    
    private void HandleAnimations()
    {
        if (animator == null)
            return;
        
        // Set animator parameters
        animator.SetBool("IsGrounded", playerController.IsGrounded());
        animator.SetBool("IsWallSliding", playerController.IsWallSliding());
        animator.SetBool("IsDashing", playerController.IsDashing());
        
        Vector2 velocity = playerController.GetVelocity();
        animator.SetFloat("HorizontalSpeed", Mathf.Abs(velocity.x));
        animator.SetFloat("VerticalSpeed", velocity.y);
        animator.SetFloat("Speed", velocity.magnitude);
    }
    
    private void HandleParticles()
    {
        // Emit landing particles on land
        if (!wasGrounded && playerController.IsGrounded())
        {
            PlayParticles(landParticles);
            TriggerSquash(landSquashAmount);
        }
        
        // Emit dash particles when dash starts
        if (!wasDashing && playerController.IsDashing())
        {
            PlayParticles(dashParticles);
        }
        
        // Wall slide particles (loop while sliding)
        if (wallSlideParticles != null)
        {
            if (playerController.IsWallSliding() && !wallSlideParticles.isPlaying)
            {
                wallSlideParticles.Play();
            }
            else if (!playerController.IsWallSliding() && wallSlideParticles.isPlaying)
            {
                wallSlideParticles.Stop();
            }
        }
        
        wasGrounded = playerController.IsGrounded();
        wasDashing = playerController.IsDashing();
    }
    
    private void HandleSquashStretch()
    {
        if (!enableSquashStretch)
            return;
        
        if (squashTimer > 0f)
        {
            squashTimer -= Time.deltaTime;
            float t = 1f - (squashTimer / squashDuration);
            transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
        }
        else
        {
            transform.localScale = originalScale;
        }
    }
    
    private void HandleDashTrail()
    {
        if (dashTrail == null)
            return;
        
        if (playerController.IsDashing())
        {
            dashTrail.emitting = true;
            dashTrail.time = dashTrailTime;
        }
        else
        {
            dashTrail.emitting = false;
        }
    }
    
    private void HandleWhipAnimation()
    {
        if (animator == null)
            return;

        if (playerController.ConsumeAttackJustPerformed())
        {
            animator.SetTrigger("Whip");
        }
    }

    private void HandleInvincibilityFlash()
    {
        if (spriteRenderer == null)
            return;

        if (playerController.IsInvincible())
        {
            flashTimer += Time.deltaTime;
            bool visible = Mathf.FloorToInt(flashTimer / invincibilityFlashRate) % 2 == 0;
            spriteRenderer.enabled = visible;
        }
        else
        {
            flashTimer = 0f;
            spriteRenderer.enabled = true;
        }
    }
    
    private void PlayParticles(ParticleSystem particles)
    {
        if (particles != null)
        {
            particles.Play();
        }
    }
    
    private void TriggerSquash(float squashAmount)
    {
        if (!enableSquashStretch)
            return;
        
        targetScale = new Vector3(
            originalScale.x / squashAmount,
            originalScale.y * squashAmount,
            originalScale.z
        );
        squashTimer = squashDuration;
    }
    
    // Can be called from animation events
    public void OnJumpAnimationEvent()
    {
        PlayParticles(jumpParticles);
        TriggerSquash(jumpSquashAmount);
    }
}
