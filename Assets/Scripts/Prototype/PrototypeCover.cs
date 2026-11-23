using UnityEngine;

public sealed class PrototypeCover : MonoBehaviour
{
    public Vector3 Position { get; private set; }
    public bool IsIntact { get; private set; } = true;
    public int BossHitsRemaining { get; private set; } = 2;
    private Renderer[] coverRenderers;
    private Collider[] coverColliders;

    public void Initialize(Vector3 position)
    {
        Position = position;
        coverRenderers = GetComponentsInChildren<Renderer>();
        coverColliders = GetComponentsInChildren<Collider>();
    }

    public void TakeBossHit()
    {
        if (!IsIntact)
        {
            return;
        }

        BossHitsRemaining--;
        if (BossHitsRemaining <= 0)
        {
            IsIntact = false;
            foreach (Renderer coverRenderer in coverRenderers)
            {
                coverRenderer.enabled = false;
            }

            foreach (Collider coverCollider in coverColliders)
            {
                coverCollider.enabled = false;
            }
        }
    }
}
