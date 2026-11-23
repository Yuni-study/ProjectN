using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PrototypeCombatScene : MonoBehaviour
{
    private enum MissionState
    {
        Opening, ToFirstArea, FirstFight, FirstAftermath, ToRelay, RelayFight,
        RelayInteraction, RelayStory, ToThirdArea, ThirdFight, BossIntro, BossFight,
        Ending, Cleared
    }

    private sealed class DialogueLine
    {
        public readonly string Speaker;
        public readonly string Text;
        public DialogueLine(string speaker, string text) { Speaker = speaker; Text = text; }
    }

    private static readonly DialogueLine[] Opening =
    {
        new DialogueLine("Lumen", "저 호출 부호는 Vela가 마지막으로 보낸 거야. 지난 작전에서 난 그 호출에 답하지 못했어."),
        new DialogueLine("Lumen", "응답을 기다리며 중계기를 고치는 동안, 내 분대는 신호 너머로 사라졌어."),
        new DialogueLine("Sable", "Vela가 살아 있다는 증거는 아직 없어. 적이 목소리를 흉내 낸 유인 신호일 수도 있어."),
        new DialogueLine("Lumen", "그래도 이번엔 확인해야 해. 살아 있다면, 이번에는 내가 먼저 찾아갈게."),
        new DialogueLine("Morrow", "신호가 적의 이동을 유도하고 있습니다. 송신이 계속되면 Ashline의 위치도 노출될 수 있습니다."),
        new DialogueLine("Rook", "그럼 길을 열고 송신을 끊자. 출발해.")
    };
    private static readonly DialogueLine[] FirstAftermath =
    {
        new DialogueLine("Lumen", "내가 응답을 보내자 신호가 바로 방향을 바꿨어. 구조 요청이 아니라 우리를 시험한 거야."),
        new DialogueLine("Morrow", "적들의 이동도 신호 방향과 일치합니다. 이 송신은 적을 끌어들이고 있습니다."),
        new DialogueLine("Sable", "중계소에서 송신 경로를 끊는다. Ashline이 다음 표적이 되기 전에 서둘러.")
    };
    private static readonly DialogueLine[] RelayStory =
    {
        new DialogueLine("Morrow", "기록은 32일 전, 이전 회수부대의 통신 기술자 Vela가 보낸 마지막 송신입니다."),
        new DialogueLine("기록", "Lumen, 내 목소리를 따라오지 마. Dreadbore가 중계 기록을 복제해서 사람들을 끌어들여—"),
        new DialogueLine("Morrow", "기록 뒤에 실시간 신호가 덧붙었습니다. 송신에는 Ashline으로 향하는 접근 경로가 포함돼 있습니다."),
        new DialogueLine("Lumen", "Vela의 목소리가 함정이어도 상관없어. 이 송신을 두면 Ashline까지 위험해져."),
        new DialogueLine("Sable", "송신 시설에서 증폭기를 끊고 원본을 회수한다. 신호의 정체는 그 뒤에 판단하자."),
        new DialogueLine("Rook", "적들이 몰려드는 이유도 거기 있겠네. 이번엔 발신기를 부숴 버리자.")
    };
    private static readonly DialogueLine[] BossIntro =
    {
        new DialogueLine("Lumen", "Dreadbore가 신호를 증폭하고 있어. 다음 송신이 시작되면 Ashline의 접근로까지 적에게 넘어가."),
        new DialogueLine("Morrow", "증폭기가 멈추면 원본 기록을 회수할 수 있습니다. 지금 차단하지 않으면 추적 경로도 사라집니다."),
        new DialogueLine("Sable", "Dreadbore를 멈추고 기록을 확보한다. 분대, 교전 개시."),
        new DialogueLine("Rook", "코어가 열리면 내가 쏜다. 그때까지 버텨!")
    };
    private static readonly DialogueLine[] Ending =
    {
        new DialogueLine("Morrow", "송신은 중단됐습니다. 원본 기록 마지막에 과거 기록 이후 생성된 응답이 붙어 있습니다."),
        new DialogueLine("Morrow", "목소리는 Vela와 일치하지만, 생존 여부까지 확인할 수는 없습니다."),
        new DialogueLine("Lumen", "그럼 이건 끝난 기록이 아니라 아직 답을 기다리는 호출이야. 이번엔 놓치지 않을게."),
        new DialogueLine("Sable", "오늘은 기록과 좌표를 가지고 철수한다. 다음엔 신호가 어디서 왔는지 확인하자.")
    };

    private GameObject worldRoot;
    private PrototypeSquadController squad;
    private PrototypeBurstSystem burstSystem;
    private PrototypeCombatHud combatHud;
    private PrototypeMissionHud missionHud;
    private PrototypeCharacter[] characters;
    private readonly List<PrototypeEnemyTarget> encounter = new List<PrototypeEnemyTarget>();
    private MissionState state;
    private PrototypeMissionSaveData saveData;
    private bool initialized;
    private bool relayCheckpointActivated;
    private bool dialogueActive;
    private bool fastTravelMenuOpen;
    private string fastTravelOriginTerminalId;
    private bool encounterSpawned;
    private bool restarting;
    private bool clearScreenDismissed;
    private int characterTutorialStage = -1;
    private bool characterTutorialBurstObserved;
    private int autoManualTutorialStage = -1;
    private float autoManualTutorialRemaining;
    private bool waitingForBurstTutorial;
    private PrototypeEnemyTarget burstTutorialTarget;
    private int defeatedInEncounter;
    private float dialogueAdvanceAt;
    private float displayedReward;
    private string objective = "Lumen의 호출 신호를 듣고 발신지를 추적한다.";
    private Vector3 terminalPosition = new Vector3(0f, 0f, 34f);
    private readonly Vector3 ashlineTerminalPosition = new Vector3(3.5f, 0f, 2.5f);
    private PrototypeCover[] covers;
    private Renderer terminalRenderer;
    private Renderer ashlineTerminalRenderer;
    private GameObject relayTerminalModel;

    public bool AutoReloadInCover
    {
        get => squad != null && squad.AutoReloadInCover;
        set
        {
            if (squad != null) squad.AutoReloadInCover = value;
        }
    }

    private void Start()
    {
        if (initialized) return;
        Initialize(PrototypeMissionSaveSystem.StartNewGame());
    }

    public void Initialize(PrototypeMissionSaveData data)
    {
        if (initialized) return;
        initialized = true;
        PrototypeAudioDirector.PlayMissionMusic();
        saveData = data;
        relayCheckpointActivated = saveData.checkpointTerminalId == "RELAY";
        BuildWorld();
        missionHud = gameObject.AddComponent<PrototypeMissionHud>();
        missionHud.Initialize(this);
        if (saveData.missionCleared)
        {
            state = MissionState.Cleared;
            displayedReward = 10f;
            objective = "임무 완료 — 잔향 데이터 10 획득";
            ShowMissionClearScreen();
        }
        else if (relayCheckpointActivated)
        {
            objective = "중계소 체크포인트에서 재집결했다. Dreadbore의 송신을 차단한다.";
        }
        else
        {
            BeginDialogue(Opening, MissionState.ToFirstArea);
        }
    }

    private void Update()
    {
        if (characters == null || IsClearScreenVisible || dialogueActive || fastTravelMenuOpen)
        {
            return;
        }

        if (state != MissionState.Cleared && AllCharactersDown())
        {
            if (!restarting) StartCoroutine(RestartAtCheckpoint());
            return;
        }

        UpdateCharacterSwitchTutorial();
        UpdateAutoManualTutorial();

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            if (state == MissionState.RelayInteraction
                && FlatDistance(squad.ActiveCharacter.transform.position, terminalPosition) <= 3.5f)
            {
                relayCheckpointActivated = true;
                PrototypeMissionSaveSystem.ActivateRelay(saveData);
                SetModelColor(relayTerminalModel, new Color(0.2f, 0.82f, 0.48f));
                BeginDialogue(RelayStory, MissionState.RelayStory);
                return;
            }

            string nearbyTerminal = FindNearbyActiveTerminal();
            if (!IsCombatState() && nearbyTerminal != null && saveData.activeTerminalIds.Count > 1)
            {
                OpenFastTravelMenu();
                return;
            }
        }

        float z = squad.ActiveCharacter.transform.position.z;
        switch (state)
        {
            case MissionState.ToFirstArea:
                if (z >= 9f) StartEncounter(MissionState.FirstFight, "1차 교전: Raker 2 / Glimmer 1");
                break;
            case MissionState.FirstAftermath:
                state = MissionState.ToRelay;
                objective = "적을 끌어들이는 송신을 차단하려 중계소로 이동한다.";
                break;
            case MissionState.ToRelay:
                if (z >= 27f) StartEncounter(MissionState.RelayFight, "2차 교전: Raker 1 / Glimmer 2");
                break;
            case MissionState.RelayInteraction:
                objective = "중계소 단말을 복구해 실시간 송신을 끊는다. (E)";
                break;
            case MissionState.RelayStory:
                state = MissionState.ToThirdArea;
                objective = "Dreadbore의 다음 송신 전에 증폭기를 차단한다.";
                break;
            case MissionState.ToThirdArea:
                if (z >= 53f) StartEncounter(MissionState.ThirdFight, "3차 교전: Raker 1 / Glimmer 1");
                break;
            case MissionState.BossFight:
                if (encounterSpawned && defeatedInEncounter >= encounter.Count)
                {
                    BeginDialogue(Ending, MissionState.Ending);
                }
                break;
        }
    }

    private void BuildWorld()
    {
        PrototypeEnemyTarget.ResetDefeatedCount();
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.32f, 0.38f, 0.39f);
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.0065f;
        RenderSettings.ambientLight = new Color(0.48f, 0.52f, 0.52f);
        worldRoot = new GameObject("Ashline Mission Field");
        worldRoot.transform.SetParent(transform, false);
        Material floor = CreateMaterial("Ashline Concrete", new Color(0.18f, 0.22f, 0.23f));
        Material wall = CreateMaterial("Ashline Ruins", new Color(0.27f, 0.31f, 0.32f));
        Material coverMaterial = CreateMaterial("Field Cover", new Color(0.34f, 0.38f, 0.38f));
        Material support = CreateMaterial("Morrow", new Color(0.42f, 0.75f, 0.45f));
        Material sniper = CreateMaterial("Sable", new Color(0.25f, 0.72f, 0.78f));
        Material assault = CreateMaterial("Rook", new Color(0.93f, 0.52f, 0.22f));
        Material weapon = CreateMaterial("Weapons", new Color(0.12f, 0.14f, 0.15f));

        GameObject fieldCollider = CreateBox("Ashline Field", new Vector3(0f, -0.3f, 43f), new Vector3(22f, 0.6f, 112f), floor);
        fieldCollider.GetComponent<Renderer>().enabled = false;
        fieldCollider.GetComponent<BoxCollider>().center = new Vector3(0f, -0.1f, 0f);
        GameObject fieldDeck = PrototypeArtAssets.CreateFitted("ModularSciFiMegaKit/Platforms/Platform_Metal2", fieldCollider.transform,
            "Ashline Field Deck", Vector3.one * 0.96f);
        if (fieldDeck != null) fieldDeck.transform.position += Vector3.down * 0.06f;
        CreateBoundary("West Boundary", -11f, wall);
        CreateBoundary("East Boundary", 11f, wall);
        CreateRuin("Ashline Forward Base", new Vector3(-7f, 2f, 1f), new Vector3(5f, 4f, 6f), wall);
        CreateLumen(new Vector3(-2f, 0f, 2.5f));
        CreateRuin("Collapsed Overpass", new Vector3(7f, 3f, 17f), new Vector3(5f, 6f, 10f), wall);
        CreateRuin("Relay Structure", new Vector3(-7f, 3f, 35f), new Vector3(5f, 6f, 12f), wall);
        CreateRuin("Signal Plant", new Vector3(7f, 4f, 70f), new Vector3(6f, 8f, 18f), wall);
        CreateTravelDebris();
        Material terminalMaterial = CreateMaterial("Terminal", new Color(0.18f, 0.42f, 0.45f));
        GameObject ashlineTerminal = CreateBox("Ashline Start Terminal", ashlineTerminalPosition + Vector3.up * 1.1f,
            new Vector3(1.2f, 2.2f, 0.8f), terminalMaterial);
        ashlineTerminalRenderer = ashlineTerminal.GetComponent<Renderer>();
        ashlineTerminalRenderer.enabled = false;
        GameObject ashlineModel = PrototypeArtAssets.CreateFitted("ModularSciFiMegaKit/Props/Prop_AccessPoint",
            ashlineTerminal.transform, "Access Point Terminal Model", new Vector3(1.2f, 2.2f, 0.8f));
        SetModelColor(ashlineModel, new Color(0.2f, 0.82f, 0.48f));
        GameObject relayTerminal = CreateBox("Relay Terminal", terminalPosition + Vector3.up * 1.1f,
            new Vector3(1.2f, 2.2f, 0.8f), terminalMaterial);
        terminalRenderer = relayTerminal.GetComponent<Renderer>();
        terminalRenderer.enabled = false;
        GameObject relayModel = PrototypeArtAssets.CreateFitted("ModularSciFiMegaKit/Props/Prop_AccessPoint",
            relayTerminal.transform, "Relay Terminal Model", new Vector3(1.2f, 2.2f, 0.8f));
        relayTerminalModel = relayModel;
        if (relayModel != null)
        {
            terminalRenderer = relayModel.GetComponentInChildren<Renderer>();
            if (saveData.activeTerminalIds.Contains("RELAY")) SetModelColor(relayModel, new Color(0.2f, 0.82f, 0.48f));
        }
        if (saveData.activeTerminalIds.Contains("RELAY"))
        {
            terminalRenderer.material.color = new Color(0.2f, 0.82f, 0.48f);
        }
        covers = new[]
        {
            CreateCover("Ashline Cover A", new Vector3(-3f, 0.65f, 8f), new Vector3(2.4f, 1.3f, 0.8f), new Vector3(-3f, 0f, 6.9f), coverMaterial),
            CreateCover("Ashline Cover B", new Vector3(3f, 0.65f, 11f), new Vector3(2.4f, 1.3f, 0.8f), new Vector3(3f, 0f, 9.9f), coverMaterial),
            CreateCover("Relay Cover A", new Vector3(-3f, 0.65f, 29f), new Vector3(2.4f, 1.3f, 0.8f), new Vector3(-3f, 0f, 27.9f), coverMaterial),
            CreateCover("Relay Cover B", new Vector3(3f, 0.65f, 34f), new Vector3(2.4f, 1.3f, 0.8f), new Vector3(3f, 0f, 32.9f), coverMaterial),
            CreateCover("Signal Cover A", new Vector3(-3f, 0.65f, 55f), new Vector3(2.4f, 1.3f, 0.8f), new Vector3(-3f, 0f, 53.9f), coverMaterial),
            CreateCover("Signal Cover B", new Vector3(3f, 0.65f, 61f), new Vector3(2.4f, 1.3f, 0.8f), new Vector3(3f, 0f, 59.9f), coverMaterial),
            CreateCover("Boss Cover A", new Vector3(-4f, 0.8f, 76f), new Vector3(3f, 1.6f, 0.9f), new Vector3(-4f, 0f, 74.8f), coverMaterial),
            CreateCover("Boss Cover B", new Vector3(4f, 0.8f, 82f), new Vector3(3f, 1.6f, 0.9f), new Vector3(4f, 0f, 80.8f), coverMaterial)
        };

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.31f, 0.39f, 0.43f);
        camera.farClipPlane = 180f;
        camera.fieldOfView = 60f;
        camera.transform.position = new Vector3(0f, 2f, -5f);

        characters = new PrototypeCharacter[3];
        characters[0] = CreateCharacter("Sable - Sniper", 0, new Vector3(0f, 0f, 0f), sniper, weapon, 100f, 60f, 1f, 10, 3f, true);
        characters[1] = CreateCharacter("Morrow - Support", 1, new Vector3(-2f, 0f, -2f), support, weapon, 120f, 10f, 6f, 30, 2f, false);
        characters[2] = CreateCharacter("Rook - Assault", 2, new Vector3(2f, 0f, -2f), assault, weapon, 110f, 14f, 7f, 50, 2.5f, false);
        squad = worldRoot.AddComponent<PrototypeSquadController>();
        squad.Initialize(characters, camera);
        foreach (PrototypeCover cover in covers) squad.RegisterCover(cover);
        burstSystem = worldRoot.AddComponent<PrototypeBurstSystem>();
        burstSystem.Initialize(squad, characters);
        burstSystem.ManualStageValidator = CanUseTutorialBurstStage;
        burstSystem.StageUsed += OnTutorialBurstStageUsed;
        burstSystem.FocusedFireShot += OnTutorialFocusedFireShot;
        burstSystem.ChainFailed += OnTutorialBurstChainFailed;
        squad.SetBurstSystem(burstSystem);
        combatHud = GetComponent<PrototypeCombatHud>();
        if (combatHud == null) combatHud = gameObject.AddComponent<PrototypeCombatHud>();
        combatHud.Initialize(squad, burstSystem, camera);
        if (relayCheckpointActivated)
        {
            characters[0].transform.position = new Vector3(0f, 0f, 37f);
            characters[1].transform.position = new Vector3(-2f, 0f, 35f);
            characters[2].transform.position = new Vector3(2f, 0f, 35f);
            state = MissionState.ToThirdArea;
        }
        else
        {
            state = MissionState.Opening;
        }
    }

    private void StartEncounter(MissionState encounterState, string label)
    {
        PrototypeAudioDirector.PlayCombatMusic();
        state = encounterState;
        encounter.Clear();
        defeatedInEncounter = 0;
        encounterSpawned = true;
        objective = label;
        int rakers = encounterState == MissionState.FirstFight ? 2 : 1;
        int glimmers = encounterState == MissionState.RelayFight ? 2 : 1;
        if (encounterState == MissionState.ThirdFight) { rakers = 1; glimmers = 1; }
        float z = encounterState == MissionState.FirstFight ? 14f
            : encounterState == MissionState.RelayFight ? 33f : 60f;
        for (int i = 0; i < rakers; i++)
        {
            Vector3 position = encounterState == MissionState.RelayFight
                ? new Vector3(-2f, 0f, 31f)
                : new Vector3((i == 0 ? -2f : 4f), 0f, z + i * 2f);
            SpawnEnemy("Raker", position, false, PrototypeEnemyArchetype.Raker);
        }

        for (int i = 0; i < glimmers; i++)
        {
            Vector3 position = encounterState == MissionState.RelayFight
                ? (i == 0 ? new Vector3(0f, 0f, 39f) : new Vector3(5f, 0f, 37f))
                : new Vector3((i == 0 ? 2f : -5f), 0f, z + 4f + i * 2f);
            SpawnEnemy("Glimmer", position, false, PrototypeEnemyArchetype.Glimmer);
        }
    }

    private void SpawnBoss()
    {
        PrototypeAudioDirector.PlayCombatMusic();
        state = MissionState.BossFight;
        encounter.Clear();
        defeatedInEncounter = 0;
        encounterSpawned = true;
        objective = "Dreadbore를 처치해 송신을 끊고 원본 기록을 회수한다.";
        SpawnEnemy("Dreadbore", new Vector3(0f, 0f, 86f), true, PrototypeEnemyArchetype.Dreadbore);
    }

    private void SpawnEnemy(string enemyName, Vector3 position, bool boss, PrototypeEnemyArchetype archetype)
    {
        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemy.name = enemyName;
        enemy.transform.SetParent(worldRoot.transform, false);
        enemy.transform.localScale = boss ? new Vector3(2.5f, 2.5f, 2.5f) : Vector3.one;
        CapsuleCollider capsule = enemy.GetComponent<CapsuleCollider>();
        float verticalScale = Mathf.Abs(enemy.transform.lossyScale.y);
        float colliderCenterY = capsule.center.y * enemy.transform.lossyScale.y;
        float groundOffset = capsule.height * verticalScale * 0.5f - colliderCenterY;
        enemy.transform.position = position + Vector3.up * groundOffset;
        Renderer placeholderRenderer = enemy.GetComponent<Renderer>();
        placeholderRenderer.enabled = false;
        string enemyModel = archetype == PrototypeEnemyArchetype.Glimmer
            ? "Enemy_EyeDrone"
            : archetype == PrototypeEnemyArchetype.Raker ? "Enemy_Trilobite" : "Enemy_QuadShell";
        float modelHeight = boss ? 1.84f : archetype == PrototypeEnemyArchetype.Glimmer ? 1.5f : 1.8f;
        GameObject enemyVisual = PrototypeArtAssets.CreateFitted("SciFiEssentialsKit/" + enemyModel,
            enemy.transform, enemyName + " Model", modelHeight);
        if (enemyVisual != null) enemyVisual.transform.localPosition += Vector3.down * modelHeight * 0.5f;
        PrototypeEnemyTarget target = enemy.AddComponent<PrototypeEnemyTarget>();
        target.Initialize(enemyName, boss ? 3000f : 100f);
        enemy.AddComponent<PrototypeEnemyAI>().Initialize(archetype, squad);
        squad.RegisterEnemy(target);
        burstSystem.RegisterTarget(target);
        encounter.Add(target);
        target.Defeated += OnEncounterEnemyDefeated;
        if (boss) combatHud.SetBossTarget(target);
    }

    private void OnEncounterEnemyDefeated(PrototypeEnemyTarget target)
    {
        defeatedInEncounter++;
        if (defeatedInEncounter < encounter.Count) return;
        if (state != MissionState.BossFight) PrototypeAudioDirector.PlayMissionMusic();
        if (state == MissionState.FirstFight)
        {
            if (!burstSystem.IsAutomatic && autoManualTutorialStage == 0)
            {
                combatHud.SetModeTutorial(null, null);
            }
            BeginDialogue(FirstAftermath, MissionState.FirstAftermath);
        }
        else if (state == MissionState.RelayFight)
        {
            if (!saveData.characterSwitchTutorialCompleted && burstSystem.IsAutomatic
                && !burstSystem.IsChainActive && !burstSystem.IsBurstActive)
            {
                CompleteCharacterSwitchTutorial();
            }

            if (!saveData.characterSwitchTutorialCompleted)
            {
                waitingForBurstTutorial = true;
                objective = "첫 버스트 연계를 사용해 효과를 확인한다.";
                SpawnBurstTutorialTarget();
                UpdateCharacterSwitchTutorial();
            }
            else
            {
                EnterRelayInteraction();
            }
        }
        else if (state == MissionState.ThirdFight)
        {
            BeginDialogue(BossIntro, MissionState.BossIntro);
        }
        else if (state == MissionState.BossFight)
        {
            PrototypeAudioDirector.PlayEndingMusic();
            PrototypeMissionSaveSystem.MarkMissionCleared(saveData);
            displayedReward = 10f;
            BeginDialogue(Ending, MissionState.Ending);
        }
    }

    private void UpdateCharacterSwitchTutorial()
    {
        if (saveData == null || saveData.characterSwitchTutorialCompleted || state == MissionState.Cleared)
        {
            combatHud.SetCharacterTutorial(null);
            return;
        }

        if (characterTutorialStage < 0 && !burstSystem.IsAutomatic && burstSystem.Gauge >= 1f)
        {
            characterTutorialStage = 0;
        }

        if (characterTutorialStage < 0 || burstSystem.IsAutomatic || dialogueActive)
        {
            combatHud.SetCharacterTutorial(null);
            return;
        }

        if (characterTutorialStage == 3)
        {
            if (burstSystem.IsBurstActive) characterTutorialBurstObserved = true;
            else if (characterTutorialBurstObserved)
            {
                characterTutorialStage = 0;
                characterTutorialBurstObserved = false;
            }
        }

        if (!burstSystem.IsChainActive && !burstSystem.IsBurstActive && burstSystem.Gauge < 1f)
        {
            combatHud.SetCharacterTutorial($"{characterTutorialStage + 1} / 4  ·  버스트 게이지를 채우세요. 적 또는 신호 표적 명중 ({burstSystem.Gauge:P0})");
            return;
        }

        int expectedSlot = Mathf.Min(characterTutorialStage, 2);
        string characterName = expectedSlot == 0 ? "Sable" : expectedSlot == 1 ? "Morrow" : "Rook";
        string role = expectedSlot == 0
            ? "원거리 표식으로 분대 피해 증가"
            : expectedSlot == 1
                ? "방벽으로 위협 순간 분대 보호"
                : "여러 적을 부채꼴로 공격";
        string action = characterTutorialStage == 0 ? "T로 1버스트"
            : characterTutorialStage == 1 ? "Y로 2버스트"
            : characterTutorialStage == 2 ? "U로 3버스트"
            : "좌클릭으로 조준 방향에 집중 포화 발사";
        string instruction = squad.ActiveSlot == expectedSlot
            ? $"{characterName} — {role} {action}"
            : $"{characterName}으로 전환 ({expectedSlot + 1}) — {role} 전환한 뒤 {action}";
        combatHud.SetCharacterTutorial($"{characterTutorialStage + 1} / 4  ·  {instruction}");
    }

    private bool CanUseTutorialBurstStage(int stage)
    {
        UpdateCharacterSwitchTutorial();
        if (characterTutorialStage < 0 || burstSystem.IsAutomatic || stage != characterTutorialStage)
        {
            return true;
        }

        return squad.ActiveSlot == stage;
    }

    private void OnTutorialBurstStageUsed(int stage, bool automatic)
    {
        if (saveData.characterSwitchTutorialCompleted)
        {
            return;
        }

        if (automatic)
        {
            characterTutorialStage = stage + 1;
            if (stage == 2)
            {
                CompleteCharacterSwitchTutorial();
                return;
            }
        }
        else if (characterTutorialStage == stage)
        {
            characterTutorialStage++;
        }
        else
        {
            return;
        }

        if (characterTutorialStage == 3) characterTutorialBurstObserved = false;
        UpdateCharacterSwitchTutorial();
    }

    private void OnTutorialFocusedFireShot()
    {
        if (characterTutorialStage != 3 || burstSystem.IsAutomatic || saveData.characterSwitchTutorialCompleted)
        {
            return;
        }

        CompleteCharacterSwitchTutorial();
    }

    private void OnTutorialBurstChainFailed()
    {
        if (saveData.characterSwitchTutorialCompleted) return;
        if (waitingForBurstTutorial && burstSystem.IsAutomatic)
        {
            CompleteCharacterSwitchTutorial();
            return;
        }

        if (characterTutorialStage < 0) return;

        characterTutorialStage = 0;
        characterTutorialBurstObserved = false;
        UpdateCharacterSwitchTutorial();
    }

    private void CompleteCharacterSwitchTutorial()
    {
        if (saveData.characterSwitchTutorialCompleted) return;
        saveData.characterSwitchTutorialCompleted = true;
        PrototypeMissionSaveSystem.Save(saveData);
        characterTutorialStage = -1;
        combatHud.SetCharacterTutorial(null);
        if (waitingForBurstTutorial) EnterRelayInteraction();
    }

    private void EnterRelayInteraction()
    {
        if (state != MissionState.RelayFight) return;
        waitingForBurstTutorial = false;
        if (burstTutorialTarget != null)
        {
            Destroy(burstTutorialTarget.gameObject);
            burstTutorialTarget = null;
        }

        state = MissionState.RelayInteraction;
        objective = "중계소 단말을 복구해 실시간 송신을 끊는다. (E)";
    }

    private void SpawnBurstTutorialTarget()
    {
        if (burstTutorialTarget != null) return;
        Vector3 position = squad.ActiveCharacter.transform.position + squad.ActiveCharacter.transform.forward * 7f;
        position.y = 1.1f;
        GameObject targetObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        targetObject.name = "Temporary Burst Tutorial Target";
        targetObject.transform.SetParent(worldRoot.transform, false);
        targetObject.transform.position = position;
        targetObject.transform.localScale = Vector3.one * 1.4f;
        burstTutorialTarget = targetObject.AddComponent<PrototypeEnemyTarget>();
        burstTutorialTarget.Initialize("신호 훈련 표적", 1000000000f, true);
        burstTutorialTarget.SetDisplayColor(new Color(0.22f, 0.82f, 0.9f));
        burstSystem.RegisterTarget(burstTutorialTarget);
    }

    private void BeginDialogue(DialogueLine[] lines, MissionState afterState)
    {
        dialogueActive = true;
        dialogueLines = lines;
        dialogueIndex = 0;
        missionDialogueLog.Add(lines[0].Speaker + "\n" + lines[0].Text);
        stateAfterDialogue = afterState;
        dialogueAdvanceAt = Time.unscaledTime + 2.2f;
        if (squad != null) squad.IsMissionInputLocked = true;
        if (combatHud != null) combatHud.SetHudVisible(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        if (afterState == MissionState.ToFirstArea) objective = "Vela의 호출 발신지를 추적하고 적 유도 송신을 차단한다.";
        else if (afterState == MissionState.FirstAftermath) objective = "교전 결과를 확인한다.";
        else if (afterState == MissionState.RelayStory) objective = "중계소 기록을 확인한다.";
        else if (afterState == MissionState.BossIntro) objective = "Dreadbore를 멈춰 Ashline으로 향하는 송신을 차단한다.";
        else if (afterState == MissionState.Ending) objective = "통신 기록을 회수한다.";
    }

    private DialogueLine[] dialogueLines;
    private MissionState stateAfterDialogue;
    private int dialogueIndex;
    private bool dialogueHidden;
    private bool dialogueAuto;
    private bool showDialogueLog;
    private Vector2 logScroll;
    private readonly List<string> missionDialogueLog = new List<string>();

    public void AdvanceDialogue()
    {
        if (!dialogueActive) return;
        if (dialogueIndex + 1 < dialogueLines.Length)
        {
            dialogueIndex++;
            missionDialogueLog.Add(dialogueLines[dialogueIndex].Speaker + "\n" + dialogueLines[dialogueIndex].Text);
            dialogueAdvanceAt = Time.unscaledTime + 2.2f;
        }
        else FinishDialogue();
    }

    public void SkipDialogue()
    {
        if (!dialogueActive) return;
        FinishDialogue();
    }

    public void ToggleDialogueAuto() => dialogueAuto = !dialogueAuto;
    public void ToggleDialogueHidden() => dialogueHidden = !dialogueHidden;
    public void ToggleDialogueLog() => showDialogueLog = !showDialogueLog;

    private void FinishDialogue()
    {
        dialogueActive = false;
        dialogueHidden = false;
        showDialogueLog = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (squad != null) squad.IsMissionInputLocked = false;
        if (combatHud != null) combatHud.SetHudVisible(true);
        state = stateAfterDialogue;
        if (state == MissionState.BossIntro) SpawnBoss();
        else if (state == MissionState.Ending)
        {
            displayedReward = 10f;
            state = MissionState.Cleared;
            objective = "임무 완료 — 잔향 데이터 10 획득";
            ShowMissionClearScreen();
        }
        else if (state == MissionState.ToFirstArea)
        {
            objective = "첫 교전 구역을 돌파해 Vela의 호출 신호를 추적한다.";
            if (!saveData.autoManualTutorialCompleted)
            {
                autoManualTutorialStage = 0;
                combatHud.SetModeTutorial("`", "자동 모드로 바꿔 표적 탐지와 자동 전투를 확인할 수 있습니다.");
            }
        }
        else if (state == MissionState.FirstAftermath) objective = "적을 끌어들이는 송신을 차단하려 중계소로 이동한다.";
        else if (state == MissionState.RelayStory) objective = "Dreadbore의 다음 송신 전에 증폭기를 차단한다.";
    }

    private void UpdateAutoManualTutorial()
    {
        if (saveData == null || saveData.autoManualTutorialCompleted || autoManualTutorialStage < 0)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        bool movementInput = keyboard != null && (keyboard.wKey.isPressed || keyboard.aKey.isPressed
            || keyboard.sKey.isPressed || keyboard.dKey.isPressed);

        if (autoManualTutorialStage == 0 && burstSystem.IsAutomatic)
        {
            autoManualTutorialStage = 1;
            autoManualTutorialRemaining = 14f;
            combatHud.SetModeTutorial("WASD", "AUTO는 적 탐지·이동·조준·사격·재장전·버스트를 수행합니다. WASD를 누르면 직접 이동하고, 손을 떼면 자동 이동이 이어집니다. 모드는 `로 바꾸기 전까지 AUTO로 유지됩니다.");
        }
        else if (autoManualTutorialStage == 1)
        {
            if (!burstSystem.IsAutomatic)
            {
                autoManualTutorialStage = 2;
                autoManualTutorialRemaining = 4f;
                combatHud.SetModeTutorial("MANUAL", "수동 모드가 선택되었습니다. `로 다시 바꾸기 전까지 이동·조준·사격을 직접 조작합니다.");
            }
            else if (movementInput)
            {
                autoManualTutorialStage = 2;
                autoManualTutorialRemaining = 4f;
                combatHud.SetModeTutorial("AUTO", "직접 이동 중에도 AUTO는 유지됩니다. WASD를 놓으면 자동 이동이 이어집니다.");
            }
            else
            {
                autoManualTutorialRemaining -= Time.deltaTime;
                if (autoManualTutorialRemaining <= 0f)
                {
                    autoManualTutorialStage = 2;
                    autoManualTutorialRemaining = 4f;
                    combatHud.SetModeTutorial("AUTO", "WASD는 이동만 직접 조작합니다. 모드와 자동 전투는 그대로 유지됩니다.");
                }
            }
        }
        else if (autoManualTutorialStage == 2)
        {
            autoManualTutorialRemaining -= Time.deltaTime;
            if (autoManualTutorialRemaining <= 0f)
            {
                CompleteAutoManualTutorial();
            }
        }
    }

    private void CompleteAutoManualTutorial()
    {
        if (saveData == null || saveData.autoManualTutorialCompleted)
        {
            return;
        }

        saveData.autoManualTutorialCompleted = true;
        PrototypeMissionSaveSystem.Save(saveData);
        autoManualTutorialStage = -1;
        combatHud.SetModeTutorial(null, null);
    }

    private void ShowMissionClearScreen()
    {
        PrototypeAudioDirector.PlayMissionClear();
        clearScreenDismissed = false;
        if (squad != null) squad.IsMissionInputLocked = true;
        if (combatHud != null) combatHud.SetHudVisible(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void ConfirmMissionClear()
    {
        if (!IsClearScreenVisible) return;
        PrototypeAudioDirector.PlayUiConfirm();
        clearScreenDismissed = true;
        Time.timeScale = 1f;
        if (squad != null) squad.IsMissionInputLocked = false;
        if (combatHud != null) combatHud.SetHudVisible(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (fastTravelMenuOpen)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseFastTravelMenu();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (IsClearScreenVisible)
        {
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            {
                ConfirmMissionClear();
            }

            return;
        }

        if (!dialogueActive) return;
        if (keyboard != null)
        {
            if (keyboard.hKey.wasPressedThisFrame) ToggleDialogueHidden();
            if (keyboard.leftShiftKey.wasPressedThisFrame) ToggleDialogueAuto();
            if (keyboard.tabKey.wasPressedThisFrame) ToggleDialogueLog();
            if (keyboard.backspaceKey.wasPressedThisFrame) SkipDialogue();
            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) AdvanceDialogue();
        }
        if (dialogueActive && dialogueAuto && Time.unscaledTime >= dialogueAdvanceAt) AdvanceDialogue();
    }

    public string Objective => objective;
    public bool IsDialogueActive => dialogueActive;
    public bool IsDialogueHidden => dialogueHidden;
    public bool IsDialogueAuto => dialogueAuto;
    public bool IsDialogueLogVisible => showDialogueLog;
    public Vector2 LogScroll { get => logScroll; set => logScroll = value; }
    public string DialogueSpeaker => dialogueLines != null && dialogueLines.Length > 0 ? dialogueLines[dialogueIndex].Speaker : "";
    public string DialogueText => dialogueLines != null && dialogueLines.Length > 0 ? dialogueLines[dialogueIndex].Text : "";
    public IReadOnlyList<string> DialogueLog
    {
        get => missionDialogueLog;
    }
    public bool IsMissionCleared => state == MissionState.Cleared;
    public bool IsClearScreenVisible => IsMissionCleared && !clearScreenDismissed;
    public float Reward => displayedReward;
    public int EchoData => saveData != null ? saveData.echoData : 0;
    public bool IsFastTravelMenuOpen => fastTravelMenuOpen;
    public string[] FastTravelDestinationIds
    {
        get
        {
            List<string> destinations = new List<string>();
            if (saveData == null) return destinations.ToArray();
            foreach (string terminalId in saveData.activeTerminalIds)
            {
                if (terminalId != fastTravelOriginTerminalId) destinations.Add(terminalId);
            }

            return destinations.ToArray();
        }
    }
    public bool ShowRelayPrompt => state == MissionState.RelayInteraction && squad != null
        && FlatDistance(squad.ActiveCharacter.transform.position, terminalPosition) <= 5f;
    public bool ShowFastTravelPrompt => (!IsMissionCleared || clearScreenDismissed) && !IsCombatState()
        && !dialogueActive && !fastTravelMenuOpen
        && saveData != null && saveData.activeTerminalIds.Count > 1 && FindNearbyActiveTerminal() != null;
    public string InteractionPrompt => ShowRelayPrompt ? "E   중계소 단말 활성화" : "E   빠른 이동";

    public void TravelToTerminal(string terminalId)
    {
        if (!fastTravelMenuOpen || saveData == null || !saveData.activeTerminalIds.Contains(terminalId)
            || terminalId == fastTravelOriginTerminalId)
        {
            return;
        }

        Vector3 destination = GetTerminalPosition(terminalId) + Vector3.forward * 3f;
        for (int i = 0; i < characters.Length; i++)
        {
            Vector3 offset = i == squad.ActiveSlot
                ? Vector3.zero
                : new Vector3(i == 1 ? -2f : 2f, 0f, -2f);
            CharacterController controller = characters[i].Controller;
            controller.enabled = false;
            characters[i].transform.position = destination + offset;
            controller.enabled = true;
        }

        CloseFastTravelMenu();
    }

    public void CloseFastTravelMenu()
    {
        if (!fastTravelMenuOpen) return;
        fastTravelMenuOpen = false;
        Time.timeScale = 1f;
        if (squad != null) squad.IsMissionInputLocked = false;
        if (combatHud != null) combatHud.SetHudVisible(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public string GetTerminalDisplayName(string terminalId)
    {
        return terminalId == "ASHLINE" ? "Ashline 전진 거점" : terminalId == "RELAY" ? "통신 중계소" : terminalId;
    }

    private void OpenFastTravelMenu()
    {
        fastTravelOriginTerminalId = FindNearbyActiveTerminal();
        if (fastTravelOriginTerminalId == null || FastTravelDestinationIds.Length == 0) return;
        fastTravelMenuOpen = true;
        if (squad != null) squad.IsMissionInputLocked = true;
        if (combatHud != null) combatHud.SetHudVisible(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    private string FindNearbyActiveTerminal()
    {
        if (saveData == null || squad == null) return null;
        foreach (string terminalId in saveData.activeTerminalIds)
        {
            if (FlatDistance(squad.ActiveCharacter.transform.position, GetTerminalPosition(terminalId)) <= 4.5f)
            {
                return terminalId;
            }
        }

        return null;
    }

    private static Vector3 GetTerminalPosition(string terminalId)
    {
        return terminalId == "ASHLINE" ? new Vector3(3.5f, 0f, 2.5f) : new Vector3(0f, 0f, 34f);
    }

    private bool IsCombatState()
    {
        return state == MissionState.FirstFight || state == MissionState.RelayFight
            || state == MissionState.ThirdFight || state == MissionState.BossFight;
    }

    private IEnumerator RestartAtCheckpoint()
    {
        restarting = true;
        Time.timeScale = 1f;
        if (worldRoot != null) Destroy(worldRoot);
        characters = null;
        encounter.Clear();
        encounterSpawned = false;
        yield return null;
        BuildWorld();
        if (relayCheckpointActivated)
        {
            objective = "중계소 체크포인트에서 재집결했다. Dreadbore의 송신을 차단한다.";
        }
        else
        {
            BeginDialogue(Opening, MissionState.ToFirstArea);
        }
        restarting = false;
    }

    private bool AllCharactersDown()
    {
        foreach (PrototypeCharacter character in characters) if (character != null && character.IsAlive) return false;
        return true;
    }

    private PrototypeCharacter CreateCharacter(string name, int slot, Vector3 position, Material bodyMaterial,
        Material weaponMaterial, float health, float damage, float shotsPerSecond, int magazineSize,
        float reloadSeconds, bool hasScope)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(worldRoot.transform, false);
        obj.transform.position = position;
        CharacterController controller = obj.AddComponent<CharacterController>();
        controller.height = 1.8f; controller.radius = 0.35f; controller.center = new Vector3(0f, 0.9f, 0f);
        controller.skinWidth = 0.02f;
        controller.stepOffset = 0.3f; controller.slopeLimit = 45f;
        string characterModelPath = slot == 0 ? "UltimateModularWomen/SciFi"
            : slot == 1 ? "UltimateModularWomen/Suit" : "UltimateModularWomen/Soldier";
        Color characterTint = slot == 0 ? new Color(0.82f, 0.9f, 0.94f)
            : slot == 1 ? new Color(0.75f, 0.9f, 0.72f) : new Color(1f, 0.79f, 0.62f);
        GameObject characterModel = PrototypeArtAssets.CreateFitted(characterModelPath, obj.transform,
            name + " Model", 1.8f, characterTint);
        if (characterModel != null) characterModel.transform.localPosition += Vector3.down * 0.035f;
        Animator animator = characterModel != null ? characterModel.GetComponentInChildren<Animator>() : null;
        if (characterModel != null && animator == null) animator = characterModel.AddComponent<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>(
                "Art/Quaternius/UltimateModularWomen/CharacterCombat");
        }
        string weaponPath = slot == 0 ? "Gun_Sniper" : "Gun_Rifle";
        float length = slot == 0 ? 1.05f : 0.85f;
        Transform weaponHand = FindCharacterHand(animator, characterModel != null ? characterModel.transform : obj.transform);
        GameObject gun = PrototypeArtAssets.CreateCharacterWeapon("SciFiEssentialsKit/" + weaponPath,
            obj.transform, name + " Weapon", length, Vector3.zero, Vector3.zero);
        if (gun != null)
        {
            gun.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        }
        if (gun == null)
        {
            gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gun.name = name + " Weapon Placeholder";
            gun.transform.SetParent(obj.transform, false);
            gun.transform.localScale = new Vector3(0.12f, 0.12f, length);
            gun.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            Destroy(gun.GetComponent<Collider>());
            gun.GetComponent<Renderer>().material = weaponMaterial;
        }
        Transform muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(gun.transform, false);
        muzzle.localPosition = new Vector3(-length * 0.52f, 0f, 0f);
        PrototypeCharacter character = obj.AddComponent<PrototypeCharacter>();
        character.Initialize(slot, health, damage, shotsPerSecond, magazineSize, reloadSeconds, hasScope,
            muzzle, animator, weaponHand, gun.transform, characterModel != null ? characterModel.transform : null);
        return character;
    }

    private void CreateTravelDebris()
    {
        PlaceFieldProp("Column_Pipes", "West Cable Support", new Vector3(-8.2f, 1.1f, 9f),
            new Vector3(1.2f, 2.2f, 1.2f), 0f);
        PlaceFieldProp("Prop_Barrel_Large", "West Abandoned Drum", new Vector3(-7.6f, 0.65f, 13f),
            new Vector3(1.1f, 1.3f, 1.1f), 14f);
        PlaceFieldProp("Prop_Cable_3", "Overpass Cable Debris", new Vector3(7.8f, 0.35f, 25f),
            new Vector3(3.2f, 0.7f, 1.5f), -18f);
        PlaceFieldProp("Column_Astra", "Relay Ruin Support", new Vector3(8.1f, 1.4f, 45f),
            new Vector3(1.3f, 2.8f, 1.3f), 8f);
        PlaceFieldProp("Prop_Cable_1", "Relay Cable Debris", new Vector3(-8.1f, 0.35f, 49f),
            new Vector3(2.8f, 0.7f, 1.4f), 16f);
        PlaceFieldProp("Prop_Vent_Wide", "Signal Plant Vent", new Vector3(-7.9f, 0.8f, 64f),
            new Vector3(2.3f, 1.6f, 1.4f), -12f);
        PlaceFieldProp("Prop_Barrel_Large", "Signal Plant Drum", new Vector3(8f, 0.65f, 65f),
            new Vector3(1.1f, 1.3f, 1.1f), -9f);
        PlaceFieldProp("Column_MetalSupport", "Boss Approach Support", new Vector3(-8f, 1.5f, 89f),
            new Vector3(1.4f, 3f, 1.4f), 0f);
    }

    private void PlaceFieldProp(string assetName, string objectName, Vector3 position, Vector3 size, float yaw)
    {
        GameObject prop = PrototypeArtAssets.CreateFitted("ModularSciFiMegaKit/" +
            (assetName.StartsWith("Column_") ? "Columns/" : "Props/") + assetName,
            worldRoot.transform, objectName, size);
        if (prop == null) return;
        prop.transform.position = position;
        prop.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private static Transform FindCharacterHand(Animator animator, Transform modelRoot)
    {
        if (animator != null && animator.isHuman)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand != null) return hand;
        }

        Transform wrist = FindChildByName(modelRoot, "Wrist.R");
        if (wrist != null) return wrist;
        Transform upperArm = FindChildByName(modelRoot, "UpperArm.R");
        return upperArm != null ? upperArm : modelRoot;
    }

    private static Transform FindChildByName(Transform parent, string targetName)
    {
        if (parent.name == targetName || parent.name.EndsWith(targetName, System.StringComparison.OrdinalIgnoreCase)) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform match = FindChildByName(parent.GetChild(i), targetName);
            if (match != null) return match;
        }
        return null;
    }

    private void CreateLumen(Vector3 position)
    {
        GameObject body = new GameObject("Lumen - Noncombat NPC");
        body.transform.SetParent(worldRoot.transform, false);
        body.transform.position = position;
        PrototypeArtAssets.CreateFitted("UltimateModularWomen/SciFi",
            body.transform, "Lumen Model", 1.8f, new Color(0.78f, 0.82f, 0.9f));
    }

    private PrototypeCover CreateCover(string name, Vector3 position, Vector3 scale, Vector3 coverPoint, Material material)
    {
        GameObject obj = CreateBox(name, position, scale, material);
        obj.GetComponent<Renderer>().enabled = false;
        string crateModel = name.EndsWith("B") ? "Prop_Crate_Large" : "Prop_Crate";
        PrototypeArtAssets.CreateFitted("SciFiEssentialsKit/" + crateModel, obj.transform,
            name + " Model", Vector3.one * 0.92f);
        PrototypeCover cover = obj.AddComponent<PrototypeCover>(); cover.Initialize(coverPoint); return cover;
    }

    private void CreateRuin(string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject structure = CreateBox(name, position, scale, material);
        structure.GetComponent<Renderer>().enabled = false;
        string wallModel = name.Contains("Relay") ? "WallAstra_Straight_Broken" : "WallBand_Straight_Broken";
        PrototypeArtAssets.CreateFitted("ModularSciFiMegaKit/Walls/" + wallModel, structure.transform,
            name + " Modular Wall Model", Vector3.one * 0.96f);
    }

    private void CreateBoundary(string name, float x, Material material)
    {
        GameObject boundary = CreateBox(name, new Vector3(x, 2f, 43f), new Vector3(0.5f, 4f, 112f), material);
        const int segmentCount = 28;
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject wallModel = PrototypeArtAssets.CreateFitted("ModularSciFiMegaKit/Walls/WallAstra_Straight_Flat",
                worldRoot.transform, name + " Segment " + i, new Vector3(4f, 4f, 0.5f));
            if (wallModel != null)
            {
                wallModel.transform.SetPositionAndRotation(new Vector3(x, 2f, -11f + i * 4f),
                    Quaternion.Euler(0f, 90f, 0f));
            }
        }
    }

    private static void SetModelColor(GameObject model, Color color)
    {
        if (model == null) return;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            foreach (Material material in renderer.materials) material.color = color;
        }
    }
    private GameObject CreateBox(string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name;
        obj.transform.SetParent(worldRoot.transform, false); obj.transform.position = position; obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().material = material; return obj;
    }

    private static Material CreateMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
        return new Material(shader) { name = name, color = color };
    }

    private static float FlatDistance(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
