using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PrototypeMissionHud : MonoBehaviour
{
    private PrototypeCombatScene mission;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle smallStyle;
    private GUIStyle helpStyle;
    private GUIStyle buttonStyle;
    private GUIStyle dialogueTextStyle;
    private GUIStyle speakerStyle;
    private GUIStyle centeredStyle;

    public void Initialize(PrototypeCombatScene controller) => mission = controller;

    private void OnGUI()
    {
        if (mission == null) return;
        EnsureStyles();
        float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        scale = Mathf.Max(0.65f, Mathf.Min(scale, 1.25f));
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
        float screenWidth = Screen.width / scale;
        float screenHeight = Screen.height / scale;
        float margin = 32f;

        if (mission.IsDialogueActive)
        {
            if (!mission.IsDialogueHidden) DrawDialogue(originX, originY, width, height);
            GUI.color = Color.white;
            GUI.matrix = oldMatrix;
            return;
        }

        if (mission.IsFastTravelMenuOpen)
        {
            DrawFastTravel(originX, originY, width, height, screenWidth, screenHeight);
            GUI.color = Color.white;
            GUI.matrix = oldMatrix;
            return;
        }

        GUI.color = new Color(0.025f, 0.045f, 0.055f, 0.82f);
        float objectiveWidth = Mathf.Min(560f, Mathf.Max(240f, width - margin * 2f));
        GUI.Box(new Rect(originX + margin, originY + margin, objectiveWidth, 92f), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(originX + margin + 18f, originY + margin + 10f, objectiveWidth - 36f, 28f), "ASHLINE  /  끊기지 않은 신호", titleStyle);
        GUI.Label(new Rect(originX + margin + 18f, originY + margin + 43f, objectiveWidth - 36f, 38f), mission.Objective, bodyStyle);

        if (!mission.IsClearScreenVisible)
        {
            float helpTop = originY + margin + 102f;
            bool showControls = Keyboard.current != null && Keyboard.current.tabKey.isPressed;
            if (showControls)
            {
                float helpHeight = 132f;
                Rect helpPanel = new Rect(originX + margin, helpTop, objectiveWidth, helpHeight);
                GUI.color = new Color(0.025f, 0.045f, 0.055f, 0.86f);
                GUI.Box(helpPanel, GUIContent.none);
                GUI.color = new Color(0.35f, 0.82f, 0.9f);
                GUI.Label(new Rect(helpPanel.x + 16f, helpPanel.y + 8f, helpPanel.width - 32f, 26f), "조작 안내", titleStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(helpPanel.x + 16f, helpPanel.y + 38f, helpPanel.width - 32f, 26f),
                    "이동 WASD · 조준 마우스 · 사격 좌클릭 · 조준 우클릭", helpStyle);
                GUI.Label(new Rect(helpPanel.x + 16f, helpPanel.y + 62f, helpPanel.width - 32f, 26f),
                    "재장전 R · 캐릭터 1/2/3 · 소생 Q · 버스트 T/Y/U", helpStyle);
                GUI.Label(new Rect(helpPanel.x + 16f, helpPanel.y + 86f, helpPanel.width - 32f, 26f),
                    "집중 포화 좌클릭 유지 · 자동/수동 ` · 피해 테스트 F8", helpStyle);
                bool autoReload = GUI.Toggle(new Rect(helpPanel.x + 16f, helpPanel.y + 108f, helpPanel.width - 32f, 22f),
                    mission.AutoReloadInCover, "엄폐 시 자동 재장전");
                if (autoReload != mission.AutoReloadInCover) PrototypeAudioDirector.PlayUiClick();
                mission.AutoReloadInCover = autoReload;
            }
            else
            {
                Rect hint = new Rect(originX + margin, helpTop, 170f, 28f);
                GUI.color = new Color(0.025f, 0.045f, 0.055f, 0.72f);
                GUI.Box(hint, GUIContent.none);
                GUI.color = new Color(0.82f, 0.9f, 0.91f);
                GUI.Label(new Rect(hint.x + 8f, hint.y + 2f, hint.width - 16f, 24f), "[TAB] 조작법 보기", smallStyle);
                GUI.color = Color.white;
            }
        }

        if (mission.ShowRelayPrompt || mission.ShowFastTravelPrompt)
        {
            float promptWidth = Mathf.Min(380f, width - 32f);
            Rect prompt = new Rect(originX + (width - promptWidth) * 0.5f, originY + height * 0.72f, promptWidth, 54f);
            GUI.color = new Color(0.02f, 0.05f, 0.06f, 0.9f);
            GUI.Box(prompt, GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(prompt, mission.InteractionPrompt, centeredStyle);
        }

        if (mission.IsClearScreenVisible)
        {
            GUI.color = new Color(0.015f, 0.025f, 0.035f, 0.9f);
            GUI.Box(new Rect(0f, 0f, screenWidth, screenHeight), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(originX + width * 0.2f, originY + height * 0.34f, width * 0.6f, 64f), "MISSION COMPLETE", centeredStyle);
            GUI.Label(new Rect(originX + width * 0.2f, originY + height * 0.44f, width * 0.6f, 48f), "잔향 데이터   +10", centeredStyle);
            GUI.Label(new Rect(originX + width * 0.2f, originY + height * 0.53f, width * 0.6f, 36f), "확인을 누르면 필드로 돌아갑니다.", centeredStyle);
            float buttonWidth = Mathf.Min(260f, width * 0.42f);
            Rect confirmButton = new Rect(originX + (width - buttonWidth) * 0.5f,
                originY + height * 0.63f, buttonWidth, 54f);
            if (GUI.Button(confirmButton, "확인  (Enter)", buttonStyle))
            {
                PrototypeAudioDirector.PlayUiClick();
                mission.ConfirmMissionClear();
            }
        }

        GUI.color = Color.white;
        GUI.matrix = oldMatrix;
    }

    private void DrawDialogue(float originX, float originY, float width, float height)
    {
        float top = originY + 22f;
        float buttonWidth = Mathf.Min(92f, (width - 40f) * 0.22f);
        string[] labels = { "HIDE", mission.IsDialogueAuto ? "AUTO ✓" : "AUTO", "LOG", "SKIP" };
        for (int i = 0; i < labels.Length; i++)
        {
            Rect rect = new Rect(originX + width - 20f - (labels.Length - i) * (buttonWidth + 6f), top, buttonWidth, 38f);
            if (GUI.Button(rect, labels[i], buttonStyle))
            {
                PrototypeAudioDirector.PlayUiClick();
                if (i == 0) mission.ToggleDialogueHidden();
                else if (i == 1) mission.ToggleDialogueAuto();
                else if (i == 2) mission.ToggleDialogueLog();
                else mission.SkipDialogue();
            }
        }

        if (mission.IsDialogueLogVisible)
        {
            GUI.color = new Color(0.015f, 0.025f, 0.035f, 0.94f);
            GUI.Box(new Rect(originX + width * 0.19f, originY + height * 0.12f, width * 0.62f, height * 0.55f), "DIALOGUE LOG");
            GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(originX + width * 0.21f, originY + height * 0.17f, width * 0.58f, height * 0.46f));
            mission.LogScroll = GUILayout.BeginScrollView(mission.LogScroll);
            foreach (string item in mission.DialogueLog) GUILayout.Label(item, bodyStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        float panelHeight = Mathf.Clamp(height * 0.28f, 205f, 285f);
        float panelTop = originY + height - panelHeight - 22f;
        float panelLeft = originX + width * 0.12f;
        float panelWidth = width * 0.76f;
        GUI.color = new Color(0.015f, 0.025f, 0.035f, 0.92f);
        GUI.Box(new Rect(panelLeft, panelTop, panelWidth, panelHeight), GUIContent.none);
        GUI.color = new Color(0.2f, 0.75f, 0.86f, 0.95f);
        GUI.Box(new Rect(panelLeft, panelTop, 6f, panelHeight), GUIContent.none);
        GUI.color = Color.white;

        Rect portrait = new Rect(originX + width * 0.145f, panelTop + 26f, Mathf.Min(105f, panelWidth * 0.19f), 132f);
        GUI.color = new Color(0.11f, 0.2f, 0.24f, 1f);
        GUI.Box(portrait, GUIContent.none);
        GUI.color = new Color(0.35f, 0.75f, 0.82f, 1f);
        GUI.Label(new Rect(portrait.x, portrait.y + 32f, portrait.width, 52f), Initials(mission.DialogueSpeaker), centeredStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(portrait.x - 8f, portrait.y + portrait.height + 8f, portrait.width + 16f, 26f), mission.DialogueSpeaker, centeredStyle);
        float dialogueLeft = originX + width * 0.245f;
        float dialogueWidth = width * 0.58f;
        GUI.Label(new Rect(dialogueLeft, panelTop + 24f, dialogueWidth, 36f), mission.DialogueSpeaker, speakerStyle);
        GUI.Label(new Rect(dialogueLeft, panelTop + 72f, dialogueWidth, panelHeight - 110f), mission.DialogueText, dialogueTextStyle);
        GUI.Label(new Rect(originX + width * 0.75f, panelTop + panelHeight - 42f, width * 0.1f, 24f), "SPACE / 클릭", smallStyle);

        if (Event.current.type == EventType.MouseDown && Event.current.button == 0
            && Event.current.mousePosition.x >= panelLeft && Event.current.mousePosition.x <= panelLeft + panelWidth
            && Event.current.mousePosition.y > panelTop)
        {
            mission.AdvanceDialogue();
            Event.current.Use();
        }
    }

    private void DrawFastTravel(float originX, float originY, float width, float height, float screenWidth, float screenHeight)
    {
        GUI.color = new Color(0.015f, 0.025f, 0.035f, 0.9f);
        GUI.Box(new Rect(0f, 0f, screenWidth, screenHeight), GUIContent.none);
        GUI.color = Color.white;
        float panelWidth = Mathf.Min(560f, width * 0.72f);
        float panelHeight = Mathf.Min(330f, height - 40f);
        Rect panel = new Rect(originX + (width - panelWidth) * 0.5f, originY + (height - panelHeight) * 0.5f, panelWidth, panelHeight);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(panel.x + 28f, panel.y + 24f, panel.width - 56f, 48f), "통신 단말 빠른 이동", titleStyle);

        string[] destinations = mission.FastTravelDestinationIds;
        float cancelTop = panelHeight - 56f;
        float buttonTop = 82f;
        float buttonGap = 10f;
        float buttonHeight = destinations.Length > 0
            ? Mathf.Min(52f, (cancelTop - buttonTop - 12f - Mathf.Max(0, destinations.Length - 1) * buttonGap) / destinations.Length)
            : 52f;
        for (int i = 0; i < destinations.Length; i++)
        {
            Rect button = new Rect(panel.x + 44f, panel.y + buttonTop + i * (buttonHeight + buttonGap),
                panel.width - 88f, buttonHeight);
            if (GUI.Button(button, mission.GetTerminalDisplayName(destinations[i]), buttonStyle))
            {
                PrototypeAudioDirector.PlayUiClick();
                mission.TravelToTerminal(destinations[i]);
            }
        }

        Rect cancel = new Rect(panel.x + 44f, panel.y + cancelTop, panel.width - 88f, 42f);
        if (GUI.Button(cancel, "닫기  (ESC)", buttonStyle))
        {
            PrototypeAudioDirector.PlayUiCancel();
            mission.CloseFastTravelMenu();
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, normal = { textColor = new Color(0.88f, 0.93f, 0.94f) } };
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(0.7f, 0.8f, 0.82f) } };
        helpStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.88f, 0.93f, 0.94f) } };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
        dialogueTextStyle = new GUIStyle(bodyStyle) { fontSize = 22, alignment = TextAnchor.UpperLeft, wordWrap = true };
        speakerStyle = new GUIStyle(titleStyle) { fontSize = 23, normal = { textColor = new Color(0.35f, 0.85f, 0.95f) } };
        centeredStyle = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 24 };
    }

    private static string Initials(string name)
    {
        if (string.IsNullOrEmpty(name)) return "?";
        string[] parts = name.Split(' ');
        return parts.Length > 1 ? parts[0].Substring(0, 1) + parts[1].Substring(0, 1) : name.Substring(0, 1);
    }
}
