using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fades out sprites on a specified foreground layer when they block the view
/// between the camera and the target (player).
/// Attach this to your main camera.
/// </summary>
public class CameraOccluder2D : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target; // Player

    [Header("Occlusion Settings")]
    [SerializeField] private LayerMask foregroundLayer; // e.g. Foreground
    [SerializeField] private float fadedAlpha = 0.2f;
    [SerializeField] private float fadeSpeed = 10f;

    private Camera cam;

    // Track which renderers are currently faded
    private readonly HashSet<SpriteRenderer> fadedRenderers = new HashSet<SpriteRenderer>();

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }
    }

    private void LateUpdate()
    {
        if (target == null || cam == null)
            return;

        Vector3 camPos = cam.transform.position;
        Vector3 targetPos = target.position;
        Vector2 origin = camPos;
        Vector2 direction = (targetPos - camPos).normalized;
        float distance = Vector2.Distance(camPos, targetPos);

        // Find all foreground colliders between camera and target
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance, foregroundLayer);

        // Build a set of currently hit renderers
        HashSet<SpriteRenderer> hitRenderers = new HashSet<SpriteRenderer>();

        foreach (var hit in hits)
        {
            if (hit.collider == null)
                continue;

            SpriteRenderer sr = hit.collider.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = hit.collider.GetComponentInChildren<SpriteRenderer>();
            }

            if (sr != null)
            {
                hitRenderers.Add(sr);
                FadeTo(sr, fadedAlpha);
            }
        }

        // Any renderer that was faded before but is no longer hit should fade back in.
        // Renderers that are still hit stay faded.
        var toRemove = new List<SpriteRenderer>();
        foreach (var sr in fadedRenderers)
        {
            if (sr == null)
                continue;

            if (!hitRenderers.Contains(sr))
            {
                // Fade back to fully visible
                FadeTo(sr, 1f);

                // Once almost fully opaque, stop tracking it
                if (sr.color.a >= 0.99f)
                {
                    toRemove.Add(sr);
                }
            }
        }

        // Add any newly hit renderers to the faded set
        foreach (var sr in hitRenderers)
        {
            if (sr != null)
            {
                fadedRenderers.Add(sr);
            }
        }

        // Remove those that have fully faded back in
        foreach (var sr in toRemove)
        {
            fadedRenderers.Remove(sr);
        }
    }

    private void FadeTo(SpriteRenderer sr, float targetAlpha)
    {
        Color c = sr.color;
        float newAlpha = Mathf.MoveTowards(c.a, targetAlpha, fadeSpeed * Time.deltaTime);
        c.a = newAlpha;
        sr.color = c;
    }
}
