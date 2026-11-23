using UnityEngine;

public sealed class PrototypeTitleMenu : MonoBehaviour
{
    private bool hasSave;
    private bool startingMission;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle labelStyle;
    private GUIStyle panelStyle;
    private Texture2D panelTexture;
    private Texture2D buttonTexture;
    private Texture2D hoverTexture;
    private GameObject backdropRoot;

    private void Start()
    {
        Time.timeScale = 1f;
        hasSave = PrototypeMissionSaveSystem.HasSaveData();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        CreateTitleBackdrop();
    }

    private void OnGUI()
    {
        if (startingMission) return;
        EnsureStyles();
        float scale = Mathf.Min(1f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        Rect safeArea = Screen.safeArea;
        if (safeArea.width <= 0f || safeArea.height <= 0f)
        {
            safeArea = new Rect(0f, 0f, Screen.width, Screen.height);
        }

        float originX = safeArea.xMin / scale;
        float originY = (Screen.height - safeArea.yMax) / scale;
        float width = safeArea.width / scale;
        float height = safeArea.height / scale;
        Rect panel = new Rect(originX + width * 0.065f, originY + height * 0.16f,
            Mathf.Min(width * 0.43f, 560f), height * 0.69f);
        GUI.Box(panel, GUIContent.none, panelStyle);
        GUI.color = new Color(0.3f, 0.85f, 0.9f, 0.95f);
        GUI.DrawTexture(new Rect(panel.x, panel.y, 4f, panel.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(panel.x + 30f, panel.y + 56f, panel.width - 60f, 72f), "SURFACE ECHO", titleStyle);
        GUI.Label(new Rect(panel.x + 34f, panel.y + 138f, panel.width - 68f, 32f), "지상의 잔향", subtitleStyle);
        GUI.color = new Color(0.75f, 0.84f, 0.85f, 1f);
        GUI.Label(new Rect(panel.x + 34f, panel.y + 194f, panel.width - 68f, 58f),
            "끊긴 구조 신호를 추적하고,\n폐허가 된 지상에서 분대와 함께 귀환하라.", labelStyle);
        GUI.color = Color.white;

        float buttonWidth = Mathf.Min(panel.width - 68f, 380f);
        float buttonX = panel.x + 34f;
        float buttonY = panel.y + panel.height - (hasSave ? 150f : 82f);
        if (hasSave)
        {
            if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, 54f), "작전 이어가기     >", buttonStyle))
            {
                PrototypeAudioDirector.PlayUiConfirm();
                ContinueGame();
            }
            buttonY += 66f;
        }

        if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, 54f), "새 작전 시작     >", buttonStyle))
        {
            StartNewGame();
        }
        GUI.matrix = oldMatrix;
    }

    private void ContinueGame()
    {
        if (!PrototypeMissionSaveSystem.TryLoad(out PrototypeMissionSaveData data))
        {
            hasSave = false;
            return;
        }

        StartMission(data);
    }

    private void StartNewGame()
    {
        StartMission(PrototypeMissionSaveSystem.StartNewGame());
    }

    private void StartMission(PrototypeMissionSaveData data)
    {
        startingMission = true;
        PrototypeAudioDirector.PlayMissionMusic();
        GameObject missionRoot = new GameObject("Prototype Combat");
        PrototypeCombatScene mission = missionRoot.AddComponent<PrototypeCombatScene>();
        mission.Initialize(data);
        Destroy(gameObject);
    }

    private void CreateTitleBackdrop()
    {
        backdropRoot = new GameObject("Ashline Title Environment");
        Material ground = CreateMaterial("Title Dust Concrete", new Color(0.2f, 0.24f, 0.24f));
        GameObject groundBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        groundBlock.name = "Title Ground";
        groundBlock.transform.SetParent(backdropRoot.transform, false);
        groundBlock.transform.position = new Vector3(0f, -0.35f, 32f);
        groundBlock.transform.localScale = new Vector3(50f, 0.7f, 92f);
        groundBlock.GetComponent<Renderer>().material = ground;
        Destroy(groundBlock.GetComponent<Collider>());

        PlaceTitleAsset("ModularSciFiMegaKit/Walls/WallBand_Straight_Broken", "West Ruined Wall",
            new Vector3(-9f, 2.2f, 17f), new Vector3(8f, 4.4f, 17f), 0f);
        PlaceTitleAsset("ModularSciFiMegaKit/Walls/WallAstra_Straight_Flat", "East Ruined Wall",
            new Vector3(9f, 3.2f, 23f), new Vector3(8f, 6.4f, 18f), 0f);
        PlaceTitleAsset("ModularSciFiMegaKit/Walls/WallBand_Straight_Broken", "Distant Ruined Wall",
            new Vector3(-5f, 3.8f, 43f), new Vector3(12f, 7.6f, 20f), 0f);
        PlaceTitleAsset("ModularSciFiMegaKit/Columns/Column_Pipes", "Collapsed Support",
            new Vector3(6.5f, 2.4f, 8f), new Vector3(1.8f, 4.8f, 1.8f), 12f);
        PlaceTitleAsset("ModularSciFiMegaKit/Props/Prop_Cable_3", "Road Cable Debris",
            new Vector3(-7f, 0.35f, 11f), new Vector3(5f, 0.7f, 1.5f), -10f);
        PlaceTitleAsset("ModularSciFiMegaKit/Props/Prop_Barrel_Large", "Abandoned Field Drum",
            new Vector3(7.5f, 0.65f, 14f), new Vector3(1.3f, 1.3f, 1.3f), 15f);
        PlaceTitleAsset("SciFiEssentialsKit/Prop_Crate_Large", "Relay Cargo", new Vector3(-5f, 0.75f, 21f),
            new Vector3(2.6f, 1.5f, 2f), -8f);

        GameObject survivor = PrototypeArtAssets.CreateFitted("UltimateModularWomen/SciFi", backdropRoot.transform,
            "Ashline Scout Silhouette", 2.25f, new Color(0.52f, 0.64f, 0.67f));
        if (survivor != null)
        {
            survivor.transform.position = new Vector3(-2.5f, 0f, 6f);
            survivor.transform.rotation = Quaternion.Euler(0f, 18f, 0f);
            Animator animator = survivor.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>(
                    "Art/Quaternius/UltimateModularWomen/CharacterCombat");
            }
        }

        GameObject terminalLight = new GameObject("Distant Relay Beacon");
        terminalLight.transform.SetParent(backdropRoot.transform, false);
        terminalLight.transform.position = new Vector3(0f, 2.4f, 44f);
        Light beacon = terminalLight.AddComponent<Light>();
        beacon.type = LightType.Point;
        beacon.color = new Color(0.18f, 0.78f, 0.88f);
        beacon.range = 11f;
        beacon.intensity = 3f;

        GameObject keyLight = new GameObject("Ashline Sunset Light");
        keyLight.transform.SetParent(backdropRoot.transform, false);
        keyLight.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
        Light sunlight = keyLight.AddComponent<Light>();
        sunlight.type = LightType.Directional;
        sunlight.color = new Color(0.94f, 0.72f, 0.54f);
        sunlight.intensity = 1.25f;

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.23f, 0.3f, 0.33f);
        camera.fieldOfView = 52f;
        camera.transform.SetPositionAndRotation(new Vector3(0.5f, 4.2f, -12f), Quaternion.Euler(8f, 0f, 0f));
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.32f, 0.38f, 0.39f);
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.008f;
    }

    private void PlaceTitleAsset(string resourcePath, string objectName, Vector3 position, Vector3 size, float yaw)
    {
        GameObject model = PrototypeArtAssets.CreateFitted(resourcePath, backdropRoot.transform, objectName, size);
        if (model == null) return;
        model.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
    }

    private static Material CreateMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return new Material(shader) { name = name, color = color };
    }

    private void OnDestroy()
    {
        if (backdropRoot != null) Destroy(backdropRoot);
        if (panelTexture != null) Destroy(panelTexture);
        if (buttonTexture != null) Destroy(buttonTexture);
        if (hoverTexture != null) Destroy(hoverTexture);
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 44,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            normal = { textColor = new Color(0.96f, 0.98f, 0.97f) }
        };
        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.34f, 0.82f, 0.88f) }
        };
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            normal = { textColor = new Color(0.75f, 0.83f, 0.84f) }
        };
        panelTexture = CreateGuiTexture(new Color(0.015f, 0.035f, 0.045f, 0.88f));
        buttonTexture = CreateGuiTexture(new Color(0.08f, 0.2f, 0.23f, 0.95f));
        hoverTexture = CreateGuiTexture(new Color(0.12f, 0.37f, 0.4f, 0.98f));
        panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture } };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(20, 14, 8, 8),
            normal = { background = buttonTexture, textColor = Color.white },
            hover = { background = hoverTexture, textColor = Color.white },
            active = { background = hoverTexture, textColor = Color.white }
        };
    }

    private static Texture2D CreateGuiTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
