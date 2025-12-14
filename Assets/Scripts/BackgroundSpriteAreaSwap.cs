using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BackgroundSpriteAreaSwap : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    [Header("Background")]
    [SerializeField] private SpriteRenderer backgroundSpriteRenderer;
    [SerializeField] private Sprite insideAreaSprite;
    [SerializeField] private bool revertOnExit = true;

    private Sprite originalSprite;
    private bool hasOriginal;

    private void Log(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log($"[BackgroundSpriteAreaSwap] {message}", this);
    }

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;

        if (backgroundSpriteRenderer == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                backgroundSpriteRenderer = cam.GetComponentInChildren<SpriteRenderer>();
            }
        }

        if (backgroundSpriteRenderer == null)
        {
            Log("No backgroundSpriteRenderer set and could not find one under Camera.main.");
        }

        if (insideAreaSprite == null)
        {
            Log("insideAreaSprite is not set.");
        }

        if (backgroundSpriteRenderer != null)
        {
            originalSprite = backgroundSpriteRenderer.sprite;
            hasOriginal = true;
            Log($"Initialized. Original sprite='{(originalSprite != null ? originalSprite.name : "<null>")}'. RevertOnExit={revertOnExit}");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (backgroundSpriteRenderer == null || insideAreaSprite == null)
            return;

        if (!hasOriginal)
        {
            originalSprite = backgroundSpriteRenderer.sprite;
            hasOriginal = true;
        }

        backgroundSpriteRenderer.sprite = insideAreaSprite;
        Log($"Player entered. Switched sprite to '{insideAreaSprite.name}'.");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!revertOnExit)
            return;

        if (!other.CompareTag(playerTag))
            return;

        if (backgroundSpriteRenderer == null)
            return;

        if (hasOriginal)
        {
            backgroundSpriteRenderer.sprite = originalSprite;
            Log($"Player exited. Reverted sprite to '{(originalSprite != null ? originalSprite.name : "<null>")}'.");
        }
    }
}
