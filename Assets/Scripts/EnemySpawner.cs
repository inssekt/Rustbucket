using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float respawnRadius = 10f;
    public float respawnDelay = 0f;

    private Transform player;
    private GameObject currentEnemy;
    private float enemyDeathTime;
    private bool waitingToRespawn;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        SpawnEnemy();
    }

    private void Update()
    {
        if (!waitingToRespawn && currentEnemy == null)
        {
            waitingToRespawn = true;
            enemyDeathTime = Time.time;
        }

        if (waitingToRespawn)
        {
            if (Time.time - enemyDeathTime >= respawnDelay &&
                player != null &&
                Vector3.Distance(player.position, transform.position) > respawnRadius)
            {
                SpawnEnemy();
                waitingToRespawn = false;
            }
        }
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null)
        {
            return;
        }

        currentEnemy = Instantiate(enemyPrefab, transform.position, transform.rotation);
    }

    private void OnDrawGizmos()
    {
        if (enemyPrefab != null)
        {
            SpriteRenderer sr = enemyPrefab.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
                Vector3 size = sr.bounds.size;
                Gizmos.DrawWireCube(transform.position, size);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, respawnRadius);
    }
}
