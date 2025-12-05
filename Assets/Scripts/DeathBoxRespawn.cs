using UnityEngine;

public class DeathBoxRespawn : MonoBehaviour
{
    [SerializeField] private Transform respawnPoint;

    private void Reset()
    {
        if (respawnPoint == null && transform.childCount > 0)
        {
            respawnPoint = transform.GetChild(0);
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        Vector3 targetPos = respawnPoint != null ? respawnPoint.position : transform.position;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerController>();
        }

        if (player != null)
        {
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = targetPos;
                rb.linearVelocity = Vector2.zero;
            }
            else
            {
                player.transform.position = targetPos;
            }
        }
        else
        {
            other.transform.position = targetPos;
        }
    }
}
