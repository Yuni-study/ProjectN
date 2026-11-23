using UnityEngine;
using UnityEngine.UI;

public sealed class PrototypeCombatHud : MonoBehaviour
{
    private PrototypeSquadController squad;
    private PrototypeBurstSystem burstSystem;
    private Camera mainCamera;
    private Camera scopeCamera;
    private RenderTexture scopeTexture;
    private GameObject scopePresentationRoot;
    private GameObject canvasRoot;
    private GameObject scopeCameraObject;
    private RectTransform safeAreaRect;
    private CanvasGroup scopeCanvasGroup;
    private SquadCard[] cards;
    private Sprite[] characterPortraits;
    private BurstDisplay burstDisplay;
    private GameObject bossHealthPanel;
    private Image bossHealthFill;
    private PrototypeEnemyTarget bossTarget;
    private float scopeBlend;
    private Rect appliedSafeArea;
    private int appliedScreenWidth;
    private int appliedScreenHeight;
    private bool hudVisible = true;
    private string characterTutorialMessage;
    private string modeTutorialMessage;
    private string modeTutorialKeycap;
    private GUIStyle modeStatusStyle;
    private GUIStyle tutorialTitleStyle;
    private GUIStyle tutorialBodyStyle;

    public void SetCharacterTutorial(string message)
    {
        characterTutorialMessage = message;
    }

    public void SetModeTutorial(string keycap, string message)
    {
        modeTutorialKeycap = keycap;
        modeTutorialMessage = message;
    }

    public void SetHudVisible(bool visible)
    {
        hudVisible = visible;
        if (canvasRoot != null) canvasRoot.SetActive(visible);
        if (scopeCamera != null) scopeCamera.enabled = visible && scopeBlend > 0.001f;
        if (visible) RefreshBossHealthHud();
    }

    public void Initialize(PrototypeSquadController controller, PrototypeBurstSystem burstController, Camera camera)
    {
        CleanupPresentation();
        hudVisible = true;
        squad = controller;
        burstSystem = burstController;
        mainCamera = camera;
        characterPortraits = new Sprite[squad.Characters.Length];
        for (int i = 0; i < characterPortraits.Length; i++)
            characterPortraits[i] = PrototypeUiArt.CreateCharacterPortrait(squad.Characters[i].VisualRoot);
        CreateCanvas();
        CreateCards();
        CreateBurstHud();
        CreateBossHealthHud();
        CreateScope();
    }

    private void LateUpdate()
    {
        if (!hudVisible) return;
        UpdateSafeArea();
        RefreshBossHealthHud();

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

        burstDisplay.Refresh(burstSystem);

        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].Refresh(squad.Characters[i], i == squad.ActiveSlot);
        }
    }

    private void OnGUI()
    {
        if (!hudVisible) return;
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
        float safeHeight = safeArea.height / guiScale;
        float safeBottom = (Screen.height - safeArea.yMin) / guiScale;

        float burstCanvasScale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        float statusScale = burstCanvasScale / guiScale;
        float statusWidth = 270f * statusScale;
        float statusHeight = 124f * statusScale;
        float statusX = safeLeft + safeWidth - 276f * statusScale;
        float statusY = safeTop + safeHeight * 0.5f - 307f * statusScale;
        GUI.color = new Color(0.04f, 0.06f, 0.07f, 0.82f);
        GUI.Box(new Rect(statusX, statusY, statusWidth, statusHeight), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(statusX + 10f * statusScale, statusY + 7f * statusScale,
            statusWidth - 20f * statusScale, 20f * statusScale), $"처치  {PrototypeEnemyTarget.DefeatedCount}");

        PrototypeEnemyTarget target = squad.GetTargetUnderReticle();
        GUI.Label(new Rect(statusX + 10f * statusScale, statusY + 28f * statusScale,
            statusWidth - 20f * statusScale, 30f * statusScale), target != null
            ? $"조준 대상  {target.GetDisplayName()}  {target.Health:0}/{target.MaxHealth:0}"
            : "조준 대상  없음");
        if (modeStatusStyle == null)
        {
            modeStatusStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        }
        modeStatusStyle.fontSize = Mathf.RoundToInt(18f * statusScale);
        GUI.color = burstSystem.IsAutomatic ? new Color(0.35f, 0.84f, 1f) : new Color(1f, 0.76f, 0.38f);
        GUI.Label(new Rect(statusX + 10f * statusScale, statusY + 52f * statusScale,
            statusWidth - 20f * statusScale, 25f * statusScale),
            burstSystem.IsAutomatic ? "AUTO  (`)" : "MANUAL  (`)", modeStatusStyle);
        GUI.color = Color.white;
        if (squad.IsEnemyAttackWarning)
        {
            GUI.Label(new Rect(statusX + 10f * statusScale, statusY + 80f * statusScale,
                statusWidth - 20f * statusScale, 34f * statusScale), "적 공격 예고 — 엄폐하거나 이동하세요");
        }
        else if (squad.IsReviving || squad.ActiveSlot == 1 && squad.IsReviveCoolingDown)
        {
            GUI.Label(new Rect(statusX + 10f * statusScale, statusY + 80f * statusScale,
                statusWidth - 20f * statusScale, 34f * statusScale), squad.IsReviving
                ? $"응급 소생 시전 중  {squad.ReviveProgress:P0}"
                : "응급 소생 재사용 대기 중");
        }
        else if (squad.ActiveCharacter.IsInCover)
        {
            GUI.color = squad.ActiveCharacter.IsAttacking
                ? new Color(1f, 0.58f, 0.28f)
                : new Color(0.45f, 0.9f, 0.72f);
            GUI.Label(new Rect(statusX + 10f * statusScale, statusY + 80f * statusScale,
                statusWidth - 20f * statusScale, 34f * statusScale), squad.ActiveCharacter.IsAttacking
                ? "사격 중 — 엄폐 보호 해제"
                : "엄폐 중 — 피해 보호");
            GUI.color = Color.white;
        }

        if (tutorialTitleStyle == null)
        {
            tutorialTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            tutorialBodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }

        tutorialTitleStyle.fontSize = Mathf.RoundToInt(23f / guiScale);
        tutorialBodyStyle.fontSize = Mathf.RoundToInt(21f / guiScale);

        if (!string.IsNullOrEmpty(characterTutorialMessage))
        {
            float tutorialWidth = Mathf.Min(900f / guiScale, safeWidth - 40f);
            float tutorialHeight = 142f / guiScale;
            float tutorialX = safeLeft + (safeWidth - tutorialWidth) * 0.5f;
            float tutorialY = safeTop + (safeHeight - tutorialHeight) * 0.5f;
            GUI.color = new Color(0.035f, 0.055f, 0.07f, 0.94f);
            GUI.Box(new Rect(tutorialX, tutorialY, tutorialWidth, tutorialHeight), GUIContent.none);
            GUI.color = new Color(0.35f, 0.84f, 1f);
            GUI.Label(new Rect(tutorialX + 20f, tutorialY + 12f, tutorialWidth - 40f, 38f), "분대 전환 가이드", tutorialTitleStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(tutorialX + 24f, tutorialY + 54f, tutorialWidth - 48f, tutorialHeight - 66f),
                characterTutorialMessage, tutorialBodyStyle);
        }

        if (!string.IsNullOrEmpty(modeTutorialMessage))
        {
            float panelWidth = Mathf.Min(930f / guiScale, safeWidth - 32f);
            float panelHeight = 144f / guiScale;
            float panelX = safeLeft + (safeWidth - panelWidth) * 0.5f;
            float panelY = safeTop + 22f / guiScale;
            float keyWidth = 94f / guiScale;
            GUI.color = new Color(0.035f, 0.055f, 0.07f, 0.94f);
            GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), GUIContent.none);
            GUI.color = new Color(0.35f, 0.84f, 1f);
            GUI.Box(new Rect(panelX + 12f / guiScale, panelY + 15f / guiScale,
                keyWidth, 96f / guiScale), GUIContent.none);
            GUIStyle keycapStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.RoundToInt((modeTutorialKeycap.Length > 4 ? 18f : 24f) / guiScale)
            };
            GUI.Label(new Rect(panelX + 12f / guiScale, panelY + 15f / guiScale,
                keyWidth, 96f / guiScale), modeTutorialKeycap, keycapStyle);
            GUI.color = new Color(0.35f, 0.84f, 1f);
            GUI.Label(new Rect(panelX + 122f / guiScale, panelY + 9f / guiScale,
                panelWidth - 140f / guiScale, 28f / guiScale), "자동·수동 조작 안내", tutorialTitleStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(panelX + 122f / guiScale, panelY + 38f / guiScale,
                panelWidth - 140f / guiScale, 96f / guiScale), modeTutorialMessage, tutorialBodyStyle);
        }

        GUI.color = new Color(1f, 1f, 1f, 1f - scopeBlend);
        Vector2 reticleCenter = new Vector2(guiWidth * 0.5f, guiHeight * 0.5f);
        if (squad.HitMarkerRemaining > 0f)
        {
            GUI.color = squad.HitMarkerCritical
                ? new Color(1f, 0.8f, 0.2f, 1f - scopeBlend)
                : new Color(1f, 1f, 1f, 1f - scopeBlend);
            Matrix4x4 reticleMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, reticleCenter);
            GUI.DrawTexture(new Rect(reticleCenter.x - 1.5f, reticleCenter.y - 9f, 3f, 18f), Texture2D.whiteTexture);
            GUI.matrix = reticleMatrix;
            GUIUtility.RotateAroundPivot(-45f, reticleCenter);
            GUI.DrawTexture(new Rect(reticleCenter.x - 1.5f, reticleCenter.y - 9f, 3f, 18f), Texture2D.whiteTexture);
            GUI.matrix = reticleMatrix;
        }
        else
        {
            GUI.Label(new Rect(reticleCenter.x - 12f, reticleCenter.y - 14f, 24f, 28f), "+");
        }
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
        DestroyCharacterPortraits();
    }

    private void CleanupPresentation()
    {
        if (scopeTexture != null)
        {
            scopeTexture.Release();
            Destroy(scopeTexture);
            scopeTexture = null;
        }

        if (scopeCameraObject != null) Destroy(scopeCameraObject);
        if (canvasRoot != null) Destroy(canvasRoot);
        DestroyCharacterPortraits();
        scopeCameraObject = null;
        canvasRoot = null;
        bossHealthPanel = null;
        bossHealthFill = null;
        bossTarget = null;
    }

    private void DestroyCharacterPortraits()
    {
        if (characterPortraits == null) return;
        foreach (Sprite portrait in characterPortraits)
        {
            if (portrait == null) continue;
            Texture texture = portrait.texture;
            Destroy(portrait);
            if (texture != null) Destroy(texture);
        }
        characterPortraits = null;
    }

    private void CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Prototype HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot = canvasObject;
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
        SetAnchors(row.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 68f), new Vector2(714f, 136f));

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
            SetAnchors(cardRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(113f + i * 238f, 0f), new Vector2(226f, 128f));

            Image background = AddImage(card, PrototypeUiArt.Solid, new Color(0.07f, 0.1f, 0.13f, 0.95f));
            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.32f, 0.36f, 1f);
            outline.effectDistance = new Vector2(2f, 2f);

            GameObject portrait = CreateImage("Portrait", card.transform, characterPortraits[i]);
            SetAnchors(portrait.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(46f, 3f), new Vector2(60f, 60f));
            CreateText("Character Name", card.transform, names[i], new Vector2(46f, -110f), new Vector2(92f, 16f), 12, FontStyle.Bold);

            Image heartIcon = CreateImage("Health Icon", card.transform, PrototypeUiArt.Heart).GetComponent<Image>();
            SetAnchors(heartIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(97f, -25f), new Vector2(18f, 18f));

            Image healthBackground = AddBar(card.transform, "Health Background", new Vector2(164f, -26f), new Vector2(100f, 11f), new Color(0.18f, 0.23f, 0.24f));
            Image healthFill = AddFill("Health Fill", healthBackground, new Color(0.25f, 0.86f, 0.49f));

            Image shieldBackground = AddBar(card.transform, "Shield Background", new Vector2(164f, -42f), new Vector2(100f, 7f), new Color(0.18f, 0.23f, 0.24f));
            Image shieldFill = AddFill("Shield Fill", shieldBackground, new Color(0.22f, 0.72f, 0.95f));

            CreateText("Weapon Type", card.transform, weaponNames[i], new Vector2(164f, -11f), new Vector2(112f, 18f), 13, FontStyle.Bold);

            Image bulletIcon = CreateImage("Ammo Icon", card.transform, PrototypeUiArt.Bullet).GetComponent<Image>();
            SetAnchors(bulletIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(97f, -76f), new Vector2(13f, 24f));

            Text ammoCount = CreateText("Ammo Count", card.transform, "", new Vector2(164f, -59f), new Vector2(112f, 20f), 15, FontStyle.Bold);
            Image ammoBackground = AddBar(card.transform, "Ammo Background", new Vector2(164f, -88f), new Vector2(100f, 9f), new Color(0.18f, 0.23f, 0.24f));
            Image ammoFill = AddFill("Ammo Fill", ammoBackground, colors[i]);

            Image reloadRing = CreateImage("Reload Progress", card.transform, PrototypeUiArt.Ring).GetComponent<Image>();
            reloadRing.type = Image.Type.Filled;
            reloadRing.fillMethod = Image.FillMethod.Radial360;
            reloadRing.fillOrigin = (int)Image.Origin360.Top;
            reloadRing.fillClockwise = true;
            reloadRing.enabled = false;
            SetAnchors(reloadRing.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(97f, -88f), new Vector2(30f, 30f));

            cards[i] = new SquadCard(cardRect, background, outline, portrait.GetComponent<Image>(),
                healthFill, shieldFill, ammoFill, reloadRing, ammoCount, colors[i]);
        }
    }

    private void CreateBurstHud()
    {
        Transform safeArea = transform.Find("Prototype HUD/Safe Area");
        GameObject panel = CreateRect("Burst HUD", safeArea);
        SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-116f, 0f), new Vector2(220f, 350f));
        AddImage(panel, PrototypeUiArt.Solid, new Color(0.04f, 0.07f, 0.09f, 0.82f));

        Text header = CreateText("Burst Header", panel.transform, "BURST", new Vector2(110f, -19f), new Vector2(196f, 26f), 18, FontStyle.Bold);
        header.alignment = TextAnchor.MiddleLeft;
        Text mode = CreateText("Burst Mode", panel.transform, "MANUAL  (`)", new Vector2(110f, -43f), new Vector2(196f, 24f), 18, FontStyle.Bold);
        mode.alignment = TextAnchor.MiddleLeft;

        Image gaugeBackground = AddBar(panel.transform, "Burst Gauge Background", new Vector2(110f, -70f), new Vector2(190f, 13f), new Color(0.18f, 0.23f, 0.24f));
        Image gaugeFill = AddFill("Burst Gauge Fill", gaugeBackground, new Color(0.1f, 0.7f, 0.94f));

        string[] stageNames = { "SABLE", "MORROW", "ROOK" };
        Color[] stageColors =
        {
            new Color(0.25f, 0.72f, 0.78f),
            new Color(0.42f, 0.75f, 0.45f),
            new Color(0.93f, 0.52f, 0.22f)
        };
        string[] numerals = { "I", "II", "III" };
        Image[] stageBackgrounds = new Image[3];
        Image[] stagePortraits = new Image[3];
        Text[] stageLabels = new Text[3];

        for (int i = 0; i < 3; i++)
        {
            GameObject row = CreateImage("Burst Stage " + numerals[i], panel.transform, PrototypeUiArt.Solid);
            stageBackgrounds[i] = row.GetComponent<Image>();
            SetAnchors(row.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, -108f - i * 52f), new Vector2(194f, 44f));

            GameObject portrait = CreateImage("Burst Portrait " + numerals[i], row.transform, characterPortraits[i]);
            stagePortraits[i] = portrait.GetComponent<Image>();
            SetAnchors(stagePortraits[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(34f, 34f));

            CreateText("Burst Numeral " + numerals[i], row.transform, numerals[i], new Vector2(64f, 0f), new Vector2(34f, 34f), 19, FontStyle.Bold);
            stageLabels[i] = CreateText("Burst Character " + stageNames[i], row.transform, stageNames[i], new Vector2(132f, 0f), new Vector2(112f, 32f), 13, FontStyle.Bold);
            stageLabels[i].alignment = TextAnchor.MiddleLeft;
        }

        Text linkLabel = CreateText("Link Timer Label", panel.transform, "LINK", new Vector2(110f, -270f), new Vector2(194f, 18f), 12, FontStyle.Bold);
        linkLabel.alignment = TextAnchor.MiddleLeft;
        Image linkBackground = AddBar(panel.transform, "Link Timer Background", new Vector2(110f, -289f), new Vector2(190f, 10f), new Color(0.18f, 0.23f, 0.24f));
        Image linkFill = AddFill("Link Timer Fill", linkBackground, new Color(0.95f, 0.65f, 0.2f));
        linkFill.fillOrigin = (int)Image.OriginHorizontal.Right;

        Text burstLabel = CreateText("Burst State Label", panel.transform, "BURST STATE", new Vector2(110f, -311f), new Vector2(194f, 18f), 12, FontStyle.Bold);
        burstLabel.alignment = TextAnchor.MiddleLeft;
        Image burstBackground = AddBar(panel.transform, "Burst State Background", new Vector2(110f, -331f), new Vector2(190f, 10f), new Color(0.18f, 0.23f, 0.24f));
        Image burstFill = AddFill("Burst State Fill", burstBackground, new Color(0.82f, 0.36f, 0.95f));

        GameObject toast = CreateImage("No Target Toast", safeArea, PrototypeUiArt.Solid);
        toast.GetComponent<Image>().color = new Color(0.04f, 0.07f, 0.09f, 0.9f);
        SetAnchors(toast.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 105f), new Vector2(340f, 48f));
        Text toastText = CreateText("No Target Text", toast.transform, "공격할 적이 없습니다", new Vector2(170f, 0f), new Vector2(320f, 42f), 18, FontStyle.Bold);

        burstDisplay = new BurstDisplay(gaugeFill, linkBackground.gameObject, linkFill, burstBackground.gameObject,
            burstFill, toast, mode, stageBackgrounds, stagePortraits, stageLabels, toastText);
    }

    private void CreateBossHealthHud()
    {
        Transform safeArea = transform.Find("Prototype HUD/Safe Area");
        bossHealthPanel = CreateRect("Boss Health HUD", safeArea);
        SetAnchors(bossHealthPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -48f), new Vector2(500f, 66f));
        AddImage(bossHealthPanel, PrototypeUiArt.Solid, new Color(0.035f, 0.055f, 0.07f, 0.86f));

        Text bossName = CreateText("Boss Name", bossHealthPanel.transform, "DREADBORE",
            Vector2.zero, new Vector2(460f, 24f), 16, FontStyle.Bold);
        bossName.alignment = TextAnchor.MiddleCenter;
        SetAnchors(bossName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 16f), new Vector2(460f, 24f));

        Image background = CreateImage("Boss Health Background", bossHealthPanel.transform, PrototypeUiArt.Solid).GetComponent<Image>();
        background.color = new Color(0.16f, 0.19f, 0.21f, 1f);
        SetAnchors(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -15f), new Vector2(460f, 18f));
        bossHealthFill = AddFill("Boss Health Fill", background, new Color(0.82f, 0.16f, 0.2f));
        bossHealthPanel.SetActive(false);
    }

    public void SetBossTarget(PrototypeEnemyTarget target)
    {
        bossTarget = target;
        RefreshBossHealthHud();
    }

    private void RefreshBossHealthHud()
    {
        if (bossHealthPanel == null) return;
        bool visible = bossTarget != null && !bossTarget.IsDefeated;
        bossHealthPanel.SetActive(visible);
        if (visible)
        {
            bossHealthFill.fillAmount = bossTarget.MaxHealth > 0d
                ? Mathf.Clamp01((float)(bossTarget.Health / bossTarget.MaxHealth))
                : 0f;
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
        this.scopeCameraObject = scopeCameraObject;
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

    private sealed class BurstDisplay
    {
        private readonly Image gaugeFill;
        private readonly GameObject linkBackground;
        private readonly Image linkFill;
        private readonly GameObject burstBackground;
        private readonly Image burstFill;
        private readonly GameObject toast;
        private readonly Text modeText;
        private readonly Image[] stageBackgrounds;
        private readonly Image[] stagePortraits;
        private readonly Text[] stageLabels;
        private readonly Text toastText;

        public BurstDisplay(Image gaugeFill, GameObject linkBackground, Image linkFill, GameObject burstBackground,
            Image burstFill, GameObject toast, Text modeText, Image[] stageBackgrounds,
            Image[] stagePortraits, Text[] stageLabels, Text toastText)
        {
            this.gaugeFill = gaugeFill;
            this.linkBackground = linkBackground;
            this.linkFill = linkFill;
            this.burstBackground = burstBackground;
            this.burstFill = burstFill;
            this.toast = toast;
            this.modeText = modeText;
            this.stageBackgrounds = stageBackgrounds;
            this.stagePortraits = stagePortraits;
            this.stageLabels = stageLabels;
            this.toastText = toastText;
        }

        public void Refresh(PrototypeBurstSystem system)
        {
            gaugeFill.fillAmount = system.Gauge;
            linkBackground.SetActive(system.ShowManualLinkTimer);
            linkFill.fillAmount = system.LinkFraction;
            burstBackground.SetActive(system.IsBurstActive);
            burstFill.fillAmount = system.BurstFraction;
            toast.SetActive(system.ShowNoTargetToast);

            string mode = system.IsAutomatic ? "AUTO" : "MANUAL";
            modeText.text = system.IsFocusedFireMode ? "FOCUSED FIRE" : mode + "  (`)";
            modeText.color = system.IsAutomatic ? new Color(0.35f, 0.84f, 1f) : new Color(1f, 0.76f, 0.38f);
            toastText.text = "공격할 적이 없습니다";

            for (int i = 0; i < stageBackgrounds.Length; i++)
            {
                bool available = system.IsStageAvailable(i);
                bool completed = system.IsBurstActive || system.IsChainActive && i < system.NextStage;
                bool isNext = !completed && (system.IsChainActive
                    ? i == system.NextStage
                    : !system.IsAutomatic && system.Gauge >= 1f && i == 0);

                Color backgroundColor = completed
                    ? new Color(0.08f, 0.34f, 0.45f, 0.95f)
                    : isNext
                        ? new Color(0.48f, 0.31f, 0.1f, 0.95f)
                        : new Color(0.08f, 0.12f, 0.15f, 0.88f);
                Color foregroundColor = available ? Color.white : new Color(0.38f, 0.42f, 0.44f, 0.8f);
                stageBackgrounds[i].color = backgroundColor;
                stagePortraits[i].color = foregroundColor;
                stageLabels[i].color = foregroundColor;
            }
        }
    }

    private sealed class SquadCard
    {
        private readonly RectTransform rectTransform;
        private readonly Image background;
        private readonly Outline outline;
        private readonly Image portrait;
        private readonly Image healthFill;
        private readonly Image shieldFill;
        private readonly Image ammoFill;
        private readonly Image reloadRing;
        private readonly Text ammoCount;
        private readonly Color accent;

        public SquadCard(RectTransform rectTransform, Image background, Outline outline, Image portrait,
            Image healthFill, Image shieldFill, Image ammoFill, Image reloadRing, Text ammoCount, Color accent)
        {
            this.rectTransform = rectTransform;
            this.background = background;
            this.outline = outline;
            this.portrait = portrait;
            this.healthFill = healthFill;
            this.shieldFill = shieldFill;
            this.ammoFill = ammoFill;
            this.reloadRing = reloadRing;
            this.ammoCount = ammoCount;
            this.accent = accent;
        }

        public void Refresh(PrototypeCharacter character, bool selected)
        {
            Vector2 anchoredPosition = rectTransform.anchoredPosition;
            anchoredPosition.y = selected ? 16f : 0f;
            rectTransform.anchoredPosition = anchoredPosition;
            background.color = selected ? new Color(0.15f, 0.19f, 0.22f, 0.98f) : new Color(0.07f, 0.1f, 0.13f, 0.9f);
            outline.effectColor = selected ? accent : new Color(0.25f, 0.32f, 0.36f, 1f);
            outline.effectDistance = selected ? new Vector2(3f, 3f) : new Vector2(2f, 2f);
            portrait.color = selected ? Color.white : new Color(0.62f, 0.66f, 0.68f);
            healthFill.fillAmount = Mathf.Clamp01(character.Health / character.MaxHealth);
            shieldFill.fillAmount = Mathf.Clamp01(character.Shield / (character.MaxHealth * 0.25f));
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
