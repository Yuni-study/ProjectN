using UnityEngine;

public static class PrototypeArtAssets
{
    private const string Root = "Art/Quaternius/";

    public static GameObject Create(string resourcePath, Transform parent, string objectName, Vector3 localPosition,
        Vector3 localScale, Color? tint = null)
    {
        GameObject prefab = Resources.Load<GameObject>(Root + resourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"Missing Quaternius model: {resourcePath}");
            return null;
        }

        GameObject instance = Object.Instantiate(prefab, parent, false);
        instance.name = objectName;
        instance.transform.localPosition = localPosition;
        instance.transform.localScale = localScale;
        if (tint.HasValue)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                foreach (Material material in renderer.materials)
                {
                    material.color *= tint.Value;
                }
            }
        }

        return instance;
    }

    public static GameObject CreateFitted(string resourcePath, Transform parent, string objectName,
        float targetHeight, Color? tint = null)
    {
        GameObject instance = Create(resourcePath, parent, objectName, Vector3.zero, Vector3.one, tint);
        if (instance == null)
        {
            return null;
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return instance;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        Vector3 center = parent.InverseTransformPoint(bounds.center);
        Vector3 bottom = parent.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        float currentHeight = bounds.size.y / Mathf.Max(parent.lossyScale.y, 0.001f);
        float scale = targetHeight / Mathf.Max(currentHeight, 0.001f);
        instance.transform.localScale = Vector3.one * scale;
        instance.transform.localPosition = new Vector3(-center.x * scale, -bottom.y * scale, -center.z * scale);
        return instance;
    }

    public static GameObject CreateFitted(string resourcePath, Transform parent, string objectName, Vector3 targetSize)
    {
        GameObject instance = Create(resourcePath, parent, objectName, Vector3.zero, Vector3.one);
        if (instance == null) return null;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return instance;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        Vector3 center = parent.InverseTransformPoint(bounds.center);
        Vector3 size = new Vector3(
            bounds.size.x / Mathf.Max(parent.lossyScale.x, 0.001f),
            bounds.size.y / Mathf.Max(parent.lossyScale.y, 0.001f),
            bounds.size.z / Mathf.Max(parent.lossyScale.z, 0.001f));
        Vector3 scale = new Vector3(targetSize.x / Mathf.Max(size.x, 0.001f),
            targetSize.y / Mathf.Max(size.y, 0.001f), targetSize.z / Mathf.Max(size.z, 0.001f));
        instance.transform.localScale = scale;
        instance.transform.localPosition = Vector3.Scale(-center, scale);
        return instance;
    }

    public static GameObject CreateCharacterWeapon(string resourcePath, Transform hand, string objectName,
        float targetLength, Vector3 localPosition, Vector3 localRotation)
    {
        GameObject instance = Create(resourcePath, hand, objectName, localPosition, Vector3.one);
        if (instance == null) return null;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return instance;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        float length = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        instance.transform.localScale = Vector3.one * (targetLength / Mathf.Max(length, 0.001f));
        instance.transform.localRotation = Quaternion.Euler(localRotation);
        instance.transform.localPosition = localPosition;
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
        return instance;
    }
}
