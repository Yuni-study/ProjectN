using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PrototypeSquadController : MonoBehaviour
{
    private readonly string[] characterNames = { "Sable / Sniper", "Morrow / Support", "Rook / Assault" };
    private PrototypeCharacter[] characters;
    private Camera playerCamera;
    private int activeSlot;
    private float yaw;
    private float pitch = 8f;
    private bool cursorLocked = true;
    private Vector3 verticalVelocity;
    private const float MoveSpeed = 5f;
    private const float MouseSensitivity = 0.12f;

    public PrototypeCharacter ActiveCharacter => characters[activeSlot];
    public PrototypeCharacter[] Characters => characters;
    public int ActiveSlot => activeSlot;
    public string ActiveCharacterName => characterNames[activeSlot];
    public bool IsScopeAiming { get; private set; }

    public PrototypeEnemyTarget GetTargetUnderReticle()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<PrototypeCharacter>() != null)
            {
                continue;
            }

            return hit.collider.GetComponentInParent<PrototypeEnemyTarget>();
        }

        return null;
    }

    public void Initialize(PrototypeCharacter[] team, Camera camera)
    {
        characters = team;
        playerCamera = camera;
        yaw = camera.transform.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null || characters == null)
        {
            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            cursorLocked = !cursorLocked;
            Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !cursorLocked;
        }

        if (cursorLocked)
        {
            Vector2 look = Mouse.current.delta.ReadValue();
            yaw += look.x * MouseSensitivity;
            pitch = Mathf.Clamp(pitch - look.y * MouseSensitivity, -25f, 45f);
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) SetActiveCharacter(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) SetActiveCharacter(1);
        if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) SetActiveCharacter(2);

        foreach (PrototypeCharacter teamMember in characters)
        {
            teamMember.TickWeapon(Time.deltaTime);
        }

        PrototypeCharacter character = ActiveCharacter;
        MoveCharacter(character);
        UpdateCamera(character);

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            character.BeginReload();
        }

        if (cursorLocked && Mouse.current.leftButton.isPressed)
        {
            character.TryFire(playerCamera);
        }
    }

    private void SetActiveCharacter(int slot)
    {
        if (slot >= 0 && slot < characters.Length)
        {
            activeSlot = slot;
        }
    }

    private void MoveCharacter(PrototypeCharacter character)
    {
        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        Quaternion cameraRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 move = cameraRotation * new Vector3(input.x, 0f, input.y);
        character.transform.rotation = cameraRotation;

        if (character.Controller.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
        }

        verticalVelocity.y += Physics.gravity.y * Time.deltaTime;
        character.Controller.Move((move * MoveSpeed + verticalVelocity) * Time.deltaTime);
    }

    private void UpdateCamera(PrototypeCharacter character)
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 target = character.transform.position + Vector3.up * 1.45f;
        Vector3 shoulderOffset = rotation * new Vector3(0.65f, 0f, -3.8f);
        playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, target + shoulderOffset, Time.deltaTime * 12f);
        playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, rotation, Time.deltaTime * 16f);

        IsScopeAiming = character.HasScope && cursorLocked && Mouse.current.rightButton.isPressed;
        playerCamera.fieldOfView = 60f;
    }
}
