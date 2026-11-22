using UnityEngine;

public sealed class PrototypeTracer : MonoBehaviour
{
    private static Material sharedMaterial;
    private TrailRenderer trail;
    private Vector3 destination;
    private bool reachedDestination;
    private float destroyAt;

    public static void Spawn(Vector3 start, Vector3 end)
    {
        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "Visible Hitscan Round";
        bullet.transform.position = start;
        bullet.transform.localScale = Vector3.one * 0.085f;
        Object.Destroy(bullet.GetComponent<Collider>());

        Renderer renderer = bullet.GetComponent<Renderer>();
        renderer.material = GetSharedMaterial();

        PrototypeTracer tracer = bullet.AddComponent<PrototypeTracer>();
        tracer.destination = end;
        tracer.trail = bullet.AddComponent<TrailRenderer>();
        tracer.trail.time = 0.12f;
        tracer.trail.startWidth = 0.055f;
        tracer.trail.endWidth = 0.008f;
        tracer.trail.minVertexDistance = 0.02f;
        tracer.trail.numCapVertices = 2;
        tracer.trail.material = GetSharedMaterial();
        tracer.trail.startColor = new Color(1f, 0.85f, 0.42f, 1f);
        tracer.trail.endColor = new Color(1f, 0.28f, 0.08f, 0f);
    }

    private void Update()
    {
        if (reachedDestination)
        {
            if (Time.time >= destroyAt)
            {
                Destroy(gameObject);
            }

            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, destination, 140f * Time.deltaTime);
        if ((transform.position - destination).sqrMagnitude < 0.0001f)
        {
            transform.position = destination;
            trail.emitting = false;
            reachedDestination = true;
            destroyAt = Time.time + trail.time;
        }
    }

    private static Material GetSharedMaterial()
    {
        if (sharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            sharedMaterial = new Material(shader);
            sharedMaterial.color = new Color(1f, 0.72f, 0.25f, 1f);
        }

        return sharedMaterial;
    }
}
