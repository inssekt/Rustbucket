using UnityEngine;

public class AbilityPickup : MonoBehaviour
{
    [Header("Abilities Granted")]
    [SerializeField] private bool grantDash = false;
    [SerializeField] private bool grantGrapple = false;
    [SerializeField] private bool grantMeleeAttack = false;

    [Header("Pickup Settings")]
    [SerializeField] private bool destroyOnPickup = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerController>();
        }

        if (player == null)
            return;

        if (grantDash)
            player.UnlockDash();

        if (grantGrapple)
            player.UnlockGrapple();

        if (grantMeleeAttack)
            player.UnlockMeleeAttack();

        if (destroyOnPickup)
            Destroy(gameObject);
    }
}
