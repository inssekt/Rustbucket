using UnityEngine;

public class GrapplePoint : MonoBehaviour
{
    [SerializeField] private Transform landingPoint;

    public Transform LandingPoint => landingPoint;

    private void Reset()
    {
        if (landingPoint == null && transform.childCount > 0)
        {
            landingPoint = transform.GetChild(0);
        }
    }

    private void OnDrawGizmos()
    {
        if (landingPoint == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(landingPoint.position, 0.1f);
        Gizmos.DrawLine(transform.position, landingPoint.position);
    }
}
