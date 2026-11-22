using UnityEngine;

public sealed class PrototypeCombatScene : MonoBehaviour
{
    private void Start()
    {
        BuildScene();
    }

    private void BuildScene()
    {
        Material floorMaterial = CreateMaterial("Floor", new Color(0.19f, 0.22f, 0.24f));
        Material coverMaterial = CreateMaterial("Cover", new Color(0.31f, 0.35f, 0.37f));
        Material sniperMaterial = CreateMaterial("Sniper", new Color(0.25f, 0.72f, 0.78f));
        Material supportMaterial = CreateMaterial("Support", new Color(0.42f, 0.75f, 0.45f));
        Material assaultMaterial = CreateMaterial("Assault", new Color(0.93f, 0.52f, 0.22f));
        Material enemyMaterial = CreateMaterial("Enemy", new Color(0.69f, 0.25f, 0.22f));
        Material dummyMaterial = CreateMaterial("Training Dummy", new Color(0.2f, 0.65f, 0.72f));
        Material weaponMaterial = CreateMaterial("Weapon", new Color(0.13f, 0.14f, 0.15f));

        CreateBox("Test Ground", new Vector3(0f, -0.25f, 4f), new Vector3(24f, 0.5f, 30f), floorMaterial);
        CreateBox("Left Boundary", new Vector3(-12f, 1f, 4f), new Vector3(0.5f, 2f, 30f), coverMaterial);
        CreateBox("Right Boundary", new Vector3(12f, 1f, 4f), new Vector3(0.5f, 2f, 30f), coverMaterial);
        CreateBox("Low Cover A", new Vector3(-3f, 0.6f, 2f), new Vector3(2.2f, 1.2f, 0.8f), coverMaterial);
        CreateBox("Low Cover B", new Vector3(2f, 0.6f, 5f), new Vector3(2.4f, 1.2f, 0.8f), coverMaterial);
        CreateBox("High Cover", new Vector3(6f, 1.1f, 8f), new Vector3(1f, 2.2f, 2.2f), coverMaterial);

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        camera.fieldOfView = 60f;
        camera.transform.position = new Vector3(0f, 2f, -5f);

        PrototypeCharacter[] characters = new PrototypeCharacter[3];
        characters[0] = CreateCharacter("Sable - Sniper", 0, new Vector3(0f, 0f, -4f), sniperMaterial, weaponMaterial,
            100f, 60f, 1f, 10, 3f, true);
        characters[1] = CreateCharacter("Morrow - Support", 1, new Vector3(-2f, 0f, -6f), supportMaterial, weaponMaterial,
            120f, 10f, 6f, 30, 2f, false);
        characters[2] = CreateCharacter("Rook - Assault", 2, new Vector3(2f, 0f, -6f), assaultMaterial, weaponMaterial,
            110f, 14f, 7f, 50, 2.5f, false);

        CreateEnemy("Raker Test 01", new Vector3(-7f, 0f, 11f), enemyMaterial, 100f);
        CreateEnemy("Glimmer Test 01", new Vector3(-2f, 0f, 11f), enemyMaterial, 100f);
        CreateEnemy("Raker Test 02", new Vector3(3f, 0f, 11f), enemyMaterial, 100f);
        CreateEnemy("Glimmer Test 02", new Vector3(8f, 0f, 11f), enemyMaterial, 100f);
        CreateEnemy("Raker Test 03", new Vector3(-7f, 0f, 16f), enemyMaterial, 100f);
        CreateEnemy("Glimmer Test 03", new Vector3(-2f, 0f, 16f), enemyMaterial, 100f);
        CreateEnemy("Raker Test 04", new Vector3(3f, 0f, 16f), enemyMaterial, 100f);
        CreateEnemy("Glimmer Test 04", new Vector3(8f, 0f, 16f), enemyMaterial, 100f);
        CreateEnemy("Practice Target", new Vector3(0f, 0f, 19f), dummyMaterial, 1_000_000_000f, true);

        PrototypeSquadController squad = gameObject.AddComponent<PrototypeSquadController>();
        squad.Initialize(characters, camera);
        gameObject.AddComponent<PrototypeCombatHud>().Initialize(squad, camera);
    }

    private PrototypeCharacter CreateCharacter(string characterName, int slot, Vector3 position, Material bodyMaterial,
        Material weaponMaterial, float health, float damage, float shotsPerSecond, int magazineSize,
        float reloadSeconds, bool hasScope)
    {
        GameObject characterObject = new GameObject(characterName);
        characterObject.transform.SetParent(transform);
        characterObject.transform.position = position;
        CharacterController controller = characterObject.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.3f;
        controller.slopeLimit = 45f;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Temporary Character Model";
        body.transform.SetParent(characterObject.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        Object.Destroy(body.GetComponent<Collider>());
        body.GetComponent<Renderer>().material = bodyMaterial;

        GameObject gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gun.name = "Temporary Weapon";
        gun.transform.SetParent(characterObject.transform, false);
        gun.transform.localPosition = new Vector3(0.38f, 1.2f, 0.38f);
        float weaponLength = slot == 0 ? 1.25f : slot == 1 ? 0.55f : 0.85f;
        gun.transform.localScale = new Vector3(0.14f, 0.14f, weaponLength);
        Object.Destroy(gun.GetComponent<Collider>());
        gun.GetComponent<Renderer>().material = weaponMaterial;

        Transform muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(characterObject.transform, false);
        muzzle.localPosition = new Vector3(0.38f, 1.2f, 0.38f + weaponLength * 0.5f);

        PrototypeCharacter character = characterObject.AddComponent<PrototypeCharacter>();
        character.Initialize(slot, health, damage, shotsPerSecond, magazineSize, reloadSeconds, hasScope, muzzle);
        return character;
    }

    private void CreateEnemy(string enemyName, Vector3 position, Material bodyMaterial, float health, bool isTrainingDummy = false)
    {
        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemy.name = enemyName;
        enemy.transform.SetParent(transform);
        enemy.transform.position = position + Vector3.up;
        enemy.GetComponent<Renderer>().material = bodyMaterial;
        enemy.AddComponent<PrototypeEnemyTarget>().Initialize(enemyName, health, isTrainingDummy);
    }

    private void CreateBox(string objectName, Vector3 position, Vector3 scale, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.SetParent(transform);
        box.transform.position = position;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().material = material;
    }

    private Material CreateMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return new Material(shader)
        {
            name = materialName,
            color = color
        };
    }
}
