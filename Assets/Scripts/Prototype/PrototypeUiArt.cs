using UnityEngine;

public static class PrototypeUiArt
{
    public static readonly Sprite Solid = CreateSolid();
    public static readonly Sprite Circle = CreateCircleMask();
    public static readonly Sprite Ring = CreateRing();
    public static readonly Sprite Heart = CreateHeart();
    public static readonly Sprite Bullet = CreateBullet();

    public static Sprite CreatePortrait(Color accent, int style)
    {
        const int size = 128;
        Texture2D texture = CreateTexture(size, size, "Temporary Portrait");
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color hair = new Color(0.12f, 0.16f, 0.19f, 1f);
        Color skin = new Color(0.86f, 0.68f, 0.53f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - 64f;
                float dy = y - 64f;
                Color color = dx * dx + dy * dy <= 60f * 60f ? new Color(0.08f, 0.12f, 0.15f, 1f) : clear;

                bool shoulders = Ellipse(x, y, 64f, 24f, 45f, 31f);
                bool neck = Ellipse(x, y, 64f, 48f, 13f, 17f);
                bool face = Ellipse(x, y, 64f, 77f, style == 1 ? 22f : 20f, 27f);
                bool hairCap = Ellipse(x, y, 64f, 91f, style == 0 ? 29f : 25f, 20f) && y > 84f;
                bool sideHair = style == 2 && (Ellipse(x, y, 43f, 73f, 8f, 28f) || Ellipse(x, y, 85f, 73f, 8f, 28f));

                if (shoulders) color = accent;
                if (neck) color = skin;
                if (face) color = skin;
                if (hairCap || sideHair) color = hair;
                if (style == 1 && x > 58 && x < 70 && y > 15 && y < 50) color = new Color(0.9f, 0.8f, 0.32f, 1f);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    public static Sprite CreateCharacterPortrait(Transform characterModel)
    {
        if (characterModel == null) return CreatePortrait(Color.gray, 0);

        const int size = 128;
        const int portraitLayer = 31;
        int layerMask = 1 << portraitLayer;
        Renderer[] renderers = characterModel.GetComponentsInChildren<Renderer>();
        int[] originalLayers = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalLayers[i] = renderers[i].gameObject.layer;
            renderers[i].gameObject.layer = portraitLayer;
        }

        Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(characterModel.position, Vector3.one);
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        Vector3 facePoint = bounds.center + Vector3.up * (bounds.extents.y * 0.58f);
        foreach (Transform bone in characterModel.GetComponentsInChildren<Transform>())
        {
            if (bone.name == "Head" || bone.name.EndsWith("|Head", System.StringComparison.Ordinal))
            {
                facePoint = bone.position;
                break;
            }
        }

        GameObject cameraObject = new GameObject("Portrait Capture Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.cullingMask = layerMask;
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(bounds.extents.y * 0.38f, 0.24f);
        camera.aspect = 1f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 5f;
        camera.transform.SetPositionAndRotation(facePoint + characterModel.forward * 1.8f,
            Quaternion.LookRotation(-characterModel.forward, Vector3.up));
        RenderTexture target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        target.Create();
        camera.targetTexture = target;
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D texture = CreateTexture(size, size, "Character Face Portrait");
        texture.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;

        for (int i = 0; i < renderers.Length; i++) renderers[i].gameObject.layer = originalLayers[i];
        camera.targetTexture = null;
        target.Release();
        Object.Destroy(target);
        Object.Destroy(cameraObject);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateSolid()
    {
        Texture2D texture = CreateTexture(1, 1, "Solid UI Pixel");
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateCircleMask()
    {
        const int size = 256;
        Texture2D texture = CreateTexture(size, size, "Scope Circle Mask");
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - size * 0.5f;
                float dy = y - size * 0.5f;
                bool inside = dx * dx + dy * dy <= 126f * 126f;
                texture.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateRing()
    {
        const int size = 256;
        Texture2D texture = CreateTexture(size, size, "UI Ring");
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - size * 0.5f;
                float dy = y - size * 0.5f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                texture.SetPixel(x, y, distance > 119f && distance < 126f ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateHeart()
    {
        const int size = 48;
        Texture2D texture = CreateTexture(size, size, "Health Heart");
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - size * 0.5f) / (size * 0.34f);
                float ny = (y - size * 0.52f) / (size * 0.34f);
                float equation = Mathf.Pow(nx * nx + ny * ny - 1f, 3f) - nx * nx * ny * ny * ny;
                texture.SetPixel(x, y, equation <= 0f ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateBullet()
    {
        const int width = 32;
        const int height = 64;
        Texture2D texture = CreateTexture(width, height, "Ammo Bullet");
        for (int y = 0; y < height; y++)
        {
            float halfWidth = y < 44 ? 11f : Mathf.Lerp(11f, 1f, (y - 44f) / 19f);
            for (int x = 0; x < width; x++)
            {
                bool inside = Mathf.Abs(x - width * 0.5f) <= halfWidth;
                texture.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Texture2D CreateTexture(int width, int height, string name)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
    }

    private static bool Ellipse(float x, float y, float centerX, float centerY, float radiusX, float radiusY)
    {
        float dx = (x - centerX) / radiusX;
        float dy = (y - centerY) / radiusY;
        return dx * dx + dy * dy <= 1f;
    }
}
