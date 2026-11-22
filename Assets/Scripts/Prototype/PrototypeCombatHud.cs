using UnityEngine;
using UnityEngine.UI;

public sealed class PrototypeCombatHud : MonoBehaviour
{
    private PrototypeSquadController squad;
    private Camera mainCamera;
    private Camera scopeCamera;
    private RenderTexture scopeTexture;
    private GameObject scopePresentationRoot;
    private RectTransform safeAreaRect;
    private CanvasGroup scopeCanvasGroup;
    private SquadCard[] cards;
    private float scopeBlend;
    private Rect appliedSafeArea;
    private int appliedScreenWidth;
    private int appliedScreenHeight;

    public void Initialize(PrototypeSquadController controller, Camera camera)
    {
        squad = controller;
        mainCamera = camera;
        CreateCanvas();
        CreateCards();
        CreateScope();
    }

    private void LateUpdate()
    {
        UpdateSafeArea();

        if (scopeCamera == null || squad == null)
        {
            return;
        }

        float targetBlend = squad.IsScopeAiming ? 1f : 0f;
        scopeBlend = Mathf.MoveTowards(scopeBlend, targetBlend, Time.unscaledDeltaTime * 4f);
        float easedBlend = Mathf.SmoothStep(0f, 1f, scopeBlend);
        bool scopeVisible = targetBlend > 0f || scopeBlend > 0.001f;
        scopePresentationRoot.SetActive(scopeVisible);
        scopeCanvasGroup.alpha = easedBlend;
        scopeCamera.enabled = scopeBlend > 0.001f;

        if (scopeCamera.enabled)
        {
            scopeCamera.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
            scopeCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, 12f, easedBlend);
        }

        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].Refresh(squad.Characters[i], i == squad.ActiveSlot);
        }
    }

    private void OnGUI()
    {
        float guiScale = Mathf.Min(1f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
        if (guiScale <= 0f)
        {
            return;
        }

        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(guiScale, guiScale, 1f));
        Rect safeArea = Screen.safeArea;
        if (safeArea.width <= 0f || safeArea.height <= 0f)
        {
            safeArea = new Rect(0f, 0f, Screen.width, Screen.height);
        }

        float guiWidth = Screen.width / guiScale;
        float guiHeight = Screen.height / guiScale;
        float safeLeft = safeArea.xMin / guiScale;
        float safeTop = (Screen.height - safeArea.yMax) / guiScale;
        float safeWidth = safeArea.width / guiScale;
        float safeBottom = (Screen.height - safeArea.yMin) / guiScale;

        GUI.color = new Color(0.04f, 0.06f, 0.07f, 0.78f);
        GUI.Box(new Rect(safeLeft + 18f, safeTop + 18f, Mathf.Min(350f, safeWidth - 36f), 76f), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(safeLeft + 32f, safeTop + 25f, Mathf.Min(330f, safeWidth - 64f), 25f), $"처치  {PrototypeEnemyTarget.DefeatedCount}");

        PrototypeEnemyTarget target = squad.GetTargetUnderReticle();
        GUI.Label(new Rect(safeLeft + 32f, safeTop + 52f, Mathf.Min(330f, safeWidth - 64f), 25f), target != null
            ? $"조준 대상  {target.GetDisplayName()}  {target.Health:0}/{target.MaxHealth:0}"
            : "조준 대상  없음");

        GUI.color = new Color(0.04f, 0.06f, 0.07f, 0.75f);
        float controlsWidth = Mathf.Min(560f, safeWidth - 32f);
        float controlsX = safeLeft + (safeWidth - controlsWidth) * 0.5f;
        float controlsY = safeBottom - 174f;
        GUI.Box(new Rect(controlsX, controlsY, controlsWidth, 36f), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(controlsX + 12f, controlsY + 5f, controlsWidth - 24f, 30f),
            "WASD 이동   마우스 조준   좌클릭 사격   R 재장전   1 / 2 / 3 전환   저격형 우클릭 조준경");

        GUI.color = new Color(1f, 1f, 1f, 1f - scopeBlend);
        GUI.Label(new Rect(guiWidth * 0.5f - 12f, guiHeight * 0.5f - 14f, 24f, 28f), "+");
        GUI.color = Color.white;
        GUI.matrix = previousMatrix;
    }

    private void OnDestroy()
    {
        if (scopeTexture != null)
        {
            scopeTexture.Release();
            Destroy(scopeTexture);
        }
    }

    private void CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Prototype HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        GameObject safeAreaObject = CreateRect("Safe Area", canvasObject.transform);
        safeAreaRect = safeAreaObject.GetComponent<RectTransform>();
        UpdateSafeArea();
    }

    private void CreateCards()
    {
        Transform safeArea = transform.Find("Prototype HUD/Safe Area");
        GameObject row = CreateRect("Squad Cards", safeArea);
        SetAnchors(row.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(714f, 120f));

        string[] names = { "Sable", "Morrow", "Rook" };
        string[] weaponNames = { "저격총", "기관총", "돌격소총" };
        Color[] colors =
        {
            new Color(0.25f, 0.72f, 0.78f),
            new Color(0.42f, 0.75f, 0.45f),
            new Color(0.93f, 0.52f, 0.22f)
        };

        cards = new SquadCard[3];
        for (int i = 0; i < cards.Length; i++)
        {
            GameObject card = CreateRect(names[i] + " Card", row.transform);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            SetAnchors(cardRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(113f + i * 238f, 0f), new Vector2(226f, 96f));

            Image background = AddImage(card, PrototypeUiArt.Solid, new Color(0.07f, 0.1f, 0.13f, 0.95f));
            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.32f, 0.36f, 1f);
            outline.effectDistance = new Vector2(2f, 2f);

            GameObject portrait = CreateImage("Portrait", card.transform, PrototypeUiArt.CreatePortrait(colors[i], i));
            SetAnchors(portrait.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(46f, 3f), new Vector2(60f, 60f));
            CreateText("Character Name", card.transform, names[i], new Vector2(46f, -84f), new Vector2(92f, 16f), 12, FontStyle.Bold);

            Image heartIcon = CreateImage("Health Icon", card.transform, PrototypeUiArt.Heart).GetComponent<Image>();
            SetAnchors(heartIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(97f, -25f), new Vector2(18f, 18f));

            Image healthBackground = AddBar(card.transform, "Health Background", new Vector2(164f, -26f), new Vector2(100f, 11f), new Color(0.18f, 0.23f, 0.24f));
            Image healthFill = AddFill("Health Fill", healthBackground, new Color(0.25f, 0.86f, 0.49f));

            Text weaponLabel = CreateText("Weapon Type", card.transform, weaponNames[i], new Vector2(164f, -11f), new Vector2(112f, 18f), 13, FontStyle.Bold);

            Image bulletIcon = CreateImage("Ammo Icon", card.transform, PrototypeUiArt.Bullet).GetComponent<Image>();
            SetAnchors(bulletIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(97f, -60f), new Vector2(13f, 24f));

            Text ammoCount = CreateText("Ammo Count", card.transform, "", new Vector2(164f, -43f), new Vector2(112f, 20f), 15, FontStyle.Bold);
            Image ammoBackground = AddBar(card.transform, "Ammo Background", new Vector2(164f, -70f), new Vector2(100f, 9f), new Color(0.18f, 0.23f, 0.24f));
            Image ammoFill = AddFill("Ammo Fill", ammoBackground, colors[i]);

            Image reloadRing = CreateImage("Reload Progress", card.transform, PrototypeUiArt.Ring).GetComponent<Image>();
            reloadRing.type = Image.Type.Filled;
            reloadRing.fillMethod = Image.FillMethod.Radial360;
            reloadRing.fillOrigin = (int)Image.Origin360.Top;
            reloadRing.fillClockwise = true;
            reloadRing.enabled = false;
            SetAnchors(reloadRing.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(97f, -70f), new Vector2(30f, 30f));

            cards[i] = new SquadCard(background, portrait.GetComponent<Image>(), healthFill, ammoFill, reloadRing, ammoCount, colors[i]);
        }
    }

    private void CreateScope()
    {
        Transform safeArea = transform.Find("Prototype HUD/Safe Area");
        scopePresentationRoot = CreateRect("Sniper Scope Presentation", safeArea);
        SetAnchors(scopePresentationRoot.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 720f));
        scopeCanvasGroup = scopePresentationRoot.AddComponent<CanvasGroup>();
        scopeCanvasGroup.alpha = 0f;
        scopeCanvasGroup.interactable = false;
        scopeCanvasGroup.blocksRaycasts = false;

        GameObject scopeMask = CreateRect("Sniper Scope Mask", scopePresentationRoot.transform);
        Stretch(scopeMask.GetComponent<RectTransform>());

        Image maskImage = scopeMask.AddComponent<Image>();
        maskImage.sprite = PrototypeUiArt.Circle;
        maskImage.color = Color.white;
        Mask mask = scopeMask.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject scopeImage = CreateRect("Scope Image", scopeMask.transform);
        Stretch(scopeImage.GetComponent<RectTransform>());
        RawImage rawImage = scopeImage.AddComponent<RawImage>();
        rawImage.color = Color.white;

        GameObject ring = CreateImage("Scope Ring", scopePresentationRoot.transform, PrototypeUiArt.Ring);
        Image scopeRing = ring.GetComponent<Image>();
        SetAnchors(scopeRing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 720f));
        scopeRing.color = new Color(0.86f, 0.92f, 0.9f, 0.9f);

        Image scopeCrosshairHorizontal = CreateImage("Scope Crosshair H", scopePresentationRoot.transform, PrototypeUiArt.Solid).GetComponent<Image>();
        scopeCrosshairHorizontal.color = new Color(0.88f, 0.96f, 0.92f, 0.85f);
        SetAnchors(scopeCrosshairHorizontal.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 2f));

        Image scopeCrosshairVertical = CreateImage("Scope Crosshair V", scopePresentationRoot.transform, PrototypeUiArt.Solid).GetComponent<Image>();
        scopeCrosshairVertical.color = new Color(0.88f, 0.96f, 0.92f, 0.85f);
        SetAnchors(scopeCrosshairVertical.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 170f));

        scopeTexture = new RenderTexture(1024, 1024, 16, RenderTextureFormat.ARGB32)
        {
            name = "Prototype Scope View",
            antiAliasing = 2
        };
        scopeTexture.Create();
        rawImage.texture = scopeTexture;

        GameObject scopeCameraObject = new GameObject("Sniper Scope Camera");
        scopeCameraObject.transform.SetParent(transform, false);
        scopeCamera = scopeCameraObject.AddComponent<Camera>();
        scopeCamera.CopyFrom(mainCamera);
        scopeCamera.targetTexture = scopeTexture;
        scopeCamera.aspect = 1f;
        scopeCamera.fieldOfView = 12f;
        scopeCamera.enabled = false;

        scopePresentationRoot.SetActive(false);
    }

    private static Image AddBar(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject bar = CreateImage(name, parent, PrototypeUiArt.Solid);
        RectTransform rect = bar.GetComponent<RectTransform>();
        SetAnchors(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
        Image image = bar.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Image AddFill(string name, Image background, Color color)
    {
        GameObject fillObject = CreateImage(name, background.transform, PrototypeUiArt.Solid);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(1f, 1f);
        fillRect.offsetMax = new Vector2(-1f, -1f);
        Image image = fillObject.GetComponent<Image>();
        image.color = color;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        return image;
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static GameObject CreateImage(string name, Transform parent, Sprite sprite)
    {
        GameObject result = CreateRect(name, parent);
        Image image = result.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return result;
    }

    private static Image AddImage(GameObject target, Sprite sprite, Color color)
    {
        Image image = target.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize, FontStyle fontStyle)
    {
        GameObject textObject = CreateRect(name, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        SetAnchors(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
        return text;
    }

    private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void UpdateSafeArea()
    {
        if (safeAreaRect == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        Rect safeArea = Screen.safeArea;
        if (safeArea.width <= 0f || safeArea.height <= 0f)
        {
            safeArea = new Rect(0f, 0f, Screen.width, Screen.height);
        }

        if (safeArea == appliedSafeArea && Screen.width == appliedScreenWidth && Screen.height == appliedScreenHeight)
        {
            return;
        }

        safeAreaRect.anchorMin = new Vector2(safeArea.xMin / Screen.width, safeArea.yMin / Screen.height);
        safeAreaRect.anchorMax = new Vector2(safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
        safeAreaRect.offsetMin = Vector2.zero;
        safeAreaRect.offsetMax = Vector2.zero;
        appliedSafeArea = safeArea;
        appliedScreenWidth = Screen.width;
        appliedScreenHeight = Screen.height;
    }

    private sealed class SquadCard
    {
        private readonly Image background;
        private readonly Image portrait;
        private readonly Image healthFill;
        private readonly Image ammoFill;
        private readonly Image reloadRing;
        private readonly Text ammoCount;
        private readonly Color accent;

        public SquadCard(Image background, Image portrait, Image healthFill, Image ammoFill, Image reloadRing,
            Text ammoCount, Color accent)
        {
            this.background = background;
            this.portrait = portrait;
            this.healthFill = healthFill;
            this.ammoFill = ammoFill;
            this.reloadRing = reloadRing;
            this.ammoCount = ammoCount;
            this.accent = accent;
        }

        public void Refresh(PrototypeCharacter character, bool selected)
        {
            background.color = selected ? new Color(0.15f, 0.19f, 0.22f, 0.98f) : new Color(0.07f, 0.1f, 0.13f, 0.9f);
            portrait.color = selected ? Color.white : new Color(0.62f, 0.66f, 0.68f);
            healthFill.fillAmount = Mathf.Clamp01(character.Health / character.MaxHealth);
            ammoFill.fillAmount = Mathf.Clamp01((float)character.Ammo / character.MagazineSize);
            ammoCount.text = $"{character.Ammo} / {character.MagazineSize}";
            ammoFill.color = character.IsReloading ? new Color(0.45f, 0.49f, 0.51f) : accent;
            reloadRing.enabled = character.IsReloading;
            if (character.IsReloading)
            {
                reloadRing.fillAmount = 1f - character.ReloadRemaining / character.ReloadSeconds;
            }
        }
    }
}
