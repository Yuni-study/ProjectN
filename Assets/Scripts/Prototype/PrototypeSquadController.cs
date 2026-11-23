using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PrototypeSquadController : MonoBehaviour
{
    private static readonly RaycastHit[] movementObstacleHits = new RaycastHit[16];
    private readonly string[] characterNames = { "Sable / Sniper", "Morrow / Support", "Rook / Assault" };
    private PrototypeCharacter[] characters;
    private PrototypeBurstSystem burstSystem;
    private readonly List<PrototypeCover> covers = new List<PrototypeCover>();
    private readonly List<PrototypeEnemyTarget> enemies = new List<PrototypeEnemyTarget>();
    private PrototypeCover[] assignedCovers;
    private Vector3[] heldFormationPositions;
    private Vector3[] companionRouteDirections;
    private float[] companionRouteRemaining;
    private float[] companionStuckSeconds;
    private float[] companionVerticalVelocity;
    private int[] companionAvoidanceSide;
    private bool[] companionRegrouping;
    private bool[] coverRequested;
    private bool[] lowHealthCoverHandled;
    private bool[] companionPeeking;
    private float[] coverEnteredAt;
    private float[] lastTargetedWarningAt;
    private float[] peekPhaseRemaining;
    private Camera playerCamera;
    private int activeSlot;
    private float yaw;
    private float pitch = 8f;
    private float cameraRecoilPitch;
    private float hitMarkerRemaining;
    private bool hitMarkerCritical;
    private bool cursorLocked = true;
    private Vector3 verticalVelocity;
    private const float MoveSpeed = 5f;
    private const float MouseSensitivity = 0.12f;
    private const float ReviveRange = 1.5f;
    private const float ReviveDuration = 3f;
    private const float ReviveCooldown = 45f;
    private PrototypeCharacter reviveTarget;
    private float reviveRemaining;
    private float reviveCooldownRemaining;
    private float coverRefreshRemaining;
    private float combatRemaining;
    private float playerStillSeconds;
    private int lastFormationPlayerSlot;
    private bool formationHoldActive;
    private const float CoverScanRadius = 14f;
    private const float CoverArrivalRadius = 0.8f;
    private const float CompanionMoveSpeed = 4.2f;
    private const float CompanionCatchUpSpeed = 6f;
    private const float CompanionCatchUpStartDistance = 2f;
    private const float CompanionCatchUpFullSpeedDistance = 4f;
    private const float CompanionRegroupDistance = 8f;
    private const float CompanionRegroupResumeDistance = 4.5f;
    private const float AutoEngagementDistance = 8f;
    private const float AimFieldOfView = 48f;
    private const float AutomaticReloadAmmoFraction = 0.25f;
    private const float MinimumCompanionSpacing = 1.8f;
    private const float FormationHoldDelay = 0.3f;
    private const float CombatPersistenceSeconds = 5f;
    private const float MinimumCoverSeconds = 5f;
    private const float DetourHoldSeconds = 0.6f;
    private const float StuckRepathDelay = 0.75f;

    public bool AutoReloadInCover { get; set; } = true;
    public bool IsMissionInputLocked { get; set; }
    public bool IsEnemyAttackWarning
    {
        get
        {
            foreach (PrototypeEnemyTarget enemy in enemies)
            {
                PrototypeEnemyAI enemyAI = enemy != null ? enemy.GetComponent<PrototypeEnemyAI>() : null;
                if (enemyAI != null && enemyAI.IsShowingAttackWarning)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public PrototypeCharacter ActiveCharacter => characters[activeSlot];
    public PrototypeCharacter[] Characters => characters;
    public int ActiveSlot => activeSlot;
    public string ActiveCharacterName => characterNames[activeSlot];
    public Camera PlayerCamera => playerCamera;
    public bool IsScopeAiming { get; private set; }
    public float HitMarkerRemaining => hitMarkerRemaining;
    public bool HitMarkerCritical => hitMarkerCritical;
    public bool IsReviving => reviveRemaining > 0f;
    public bool IsReviveCoolingDown => reviveCooldownRemaining > 0f;
    public float ReviveProgress => 1f - reviveRemaining / ReviveDuration;

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
        assignedCovers = new PrototypeCover[team.Length];
        heldFormationPositions = new Vector3[team.Length];
        companionRouteDirections = new Vector3[team.Length];
        companionRouteRemaining = new float[team.Length];
        companionStuckSeconds = new float[team.Length];
        companionVerticalVelocity = new float[team.Length];
        companionAvoidanceSide = new int[team.Length];
        companionRegrouping = new bool[team.Length];
        coverRequested = new bool[team.Length];
        lowHealthCoverHandled = new bool[team.Length];
        companionPeeking = new bool[team.Length];
        coverEnteredAt = new float[team.Length];
        lastTargetedWarningAt = new float[team.Length];
        peekPhaseRemaining = new float[team.Length];
        for (int i = 0; i < team.Length; i++)
        {
            lastTargetedWarningAt[i] = -1000f;
            coverEnteredAt[i] = -1f;
            companionAvoidanceSide[i] = i == 1 ? -1 : 1;
        }

        playerCamera = camera;
        lastFormationPlayerSlot = 0;
        characters[1].Damaged += OnCharacterDamaged;
        foreach (PrototypeCharacter character in characters)
        {
            character.ConfirmedHit += OnCharacterConfirmedHit;
        }
        yaw = camera.transform.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SetBurstSystem(PrototypeBurstSystem system)
    {
        burstSystem = system;
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null || characters == null)
        {
            return;
        }

        if (IsMissionInputLocked)
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

        if (burstSystem != null && burstSystem.IsAutomatic)
        {
            foreach (PrototypeCharacter teamMember in characters)
            {
                if (teamMember.IsAlive && !teamMember.IsReloading
                    && teamMember.Ammo < teamMember.MagazineSize
                    && teamMember.Ammo <= teamMember.MagazineSize * AutomaticReloadAmmoFraction)
                {
                    teamMember.BeginReload();
                }
            }
        }

        combatRemaining = Mathf.Max(0f, combatRemaining - Time.deltaTime);
        hitMarkerRemaining = Mathf.Max(0f, hitMarkerRemaining - Time.deltaTime);
        cameraRecoilPitch = Mathf.MoveTowards(cameraRecoilPitch, 0f, Time.deltaTime * 3.5f);

        PrototypeCharacter character = ActiveCharacter;
        if (!character.IsAlive)
        {
            int nextAlive = FindNextAliveSlot(activeSlot);
            if (nextAlive < 0)
            {
                return;
            }

            activeSlot = nextAlive;
            character = ActiveCharacter;
        }

        bool autoReviveControlsMorrow = UpdateRevive(Time.deltaTime);
        Vector3 positionBeforePlayerMove = character.transform.position;
        if (!(autoReviveControlsMorrow && activeSlot == 1))
        {
            if (burstSystem != null && burstSystem.IsAutomatic && !HasPlayerMovementInput())
            {
                MoveAutomaticPlayer(character, FindAutomaticPlayerTarget(character, false), Time.deltaTime);
            }
            else
            {
                MoveCharacter(character);
            }
        }

        Vector3 playerMovement = character.transform.position - positionBeforePlayerMove;
        playerMovement.y = 0f;
        UpdateFormationHold(playerMovement.sqrMagnitude > 0.0001f, Time.deltaTime);

        UpdatePlayerCoverState(character, false);
        UpdateCamera(character);
        float playerAnimationSpeed = playerMovement.magnitude / Mathf.Max(MoveSpeed * Time.deltaTime, 0.001f);
        bool playerAiming = character.IsAttacking
            || cursorLocked && (Mouse.current.rightButton.isPressed || Mouse.current.leftButton.isPressed)
            || burstSystem != null && burstSystem.IsAutomatic && combatRemaining > 0f;
        character.SetWeaponAimDirection(playerAiming ? playerCamera.transform.forward : character.transform.forward);
        character.SetCombatPose(playerAnimationSpeed, playerAiming);

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            character.BeginReload();
        }

        if (Keyboard.current.qKey.wasPressedThisFrame
            && burstSystem != null
            && !burstSystem.IsAutomatic
            && activeSlot == 1)
        {
            TryBeginManualRevive();
        }

        if (cursorLocked && Mouse.current.leftButton.isPressed)
        {
            combatRemaining = CombatPersistenceSeconds;
            character.SetCoverState(character.IsInCover, true);
            bool focusedFireControlsInput = burstSystem != null
                && burstSystem.IsFocusedFireMode
                && burstSystem.IsBurstActive
                && activeSlot == 2;

            if (focusedFireControlsInput)
            {
                burstSystem.TryFireFocusedFire();
            }
            else
            {
                if (character.TryFire(playerCamera))
                {
                    ApplyWeaponRecoil(character);
                }
            }
        }
        else if (burstSystem != null && burstSystem.IsAutomatic)
        {
            TryAutomaticPlayerShot(character);
        }

        if (character.IsInCover && AutoReloadInCover && !character.IsReloading && character.Ammo < character.MagazineSize)
        {
            character.BeginReload();
        }

        TickCompanionAi(Time.deltaTime);
    }

    public void RegisterCover(PrototypeCover cover)
    {
        if (cover != null && !covers.Contains(cover))
        {
            covers.Add(cover);
        }
    }

    public void RegisterEnemy(PrototypeEnemyTarget enemy)
    {
        if (enemy != null && !enemies.Contains(enemy))
        {
            enemies.Add(enemy);
            enemy.Hit += OnSquadEnemyHit;
        }
    }

    public PrototypeCover GetCoverForCharacter(PrototypeCharacter character)
    {
        if (character == null || !character.IsInCover || character.IsAttacking)
        {
            return null;
        }

        return FindCoverNear(character.transform.position);
    }

    private void UpdatePlayerCoverState(PrototypeCharacter character, bool attacking)
    {
        PrototypeCover cover = FindCoverNear(character.transform.position);
        character.SetCoverState(cover, attacking);
    }

    private PrototypeCover FindCoverNear(Vector3 position)
    {
        PrototypeCover closest = null;
        float closestDistance = CoverArrivalRadius * CoverArrivalRadius;
        foreach (PrototypeCover cover in covers)
        {
            if (cover == null || !cover.IsIntact)
            {
                continue;
            }

            float distance = FlatDistanceSquared(position, cover.Position);
            if (distance <= closestDistance)
            {
                closest = cover;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private void TickCompanionAi(float deltaTime)
    {
        if (characters == null)
        {
            return;
        }

        for (int i = 0; i < characters.Length; i++)
        {
            PrototypeCharacter companion = characters[i];
            if (i != activeSlot && companion.IsAlive)
            {
                ApplyCompanionGravity(companion, deltaTime);
            }

            if (assignedCovers[i] != null && !assignedCovers[i].IsIntact)
            {
                assignedCovers[i] = null;
                coverEnteredAt[i] = -1f;
                companionPeeking[i] = false;
            }

            if (!companion.IsAlive)
            {
                lowHealthCoverHandled[i] = false;
                companionRegrouping[i] = false;
                coverRequested[i] = false;
                companionRouteRemaining[i] = 0f;
                companionStuckSeconds[i] = 0f;
                continue;
            }

            if (i != activeSlot && !companion.IsReloading
                && companion.Ammo < companion.MagazineSize
                && companion.Ammo <= companion.MagazineSize * AutomaticReloadAmmoFraction)
            {
                companion.BeginReload();
            }

            if (companion.Health > companion.MaxHealth * 0.4f)
            {
                lowHealthCoverHandled[i] = false;
            }

            if (i != activeSlot)
            {
                float playerDistanceSquared = FlatDistanceSquared(companion.transform.position,
                    ActiveCharacter.transform.position);
                if (!companionRegrouping[i]
                    && playerDistanceSquared > CompanionRegroupDistance * CompanionRegroupDistance)
                {
                    companionRegrouping[i] = true;
                }
                else if (companionRegrouping[i]
                    && playerDistanceSquared <= CompanionRegroupResumeDistance * CompanionRegroupResumeDistance)
                {
                    companionRegrouping[i] = false;
                }

                if (companionRegrouping[i])
                {
                    coverRequested[i] = false;
                    assignedCovers[i] = null;
                    coverEnteredAt[i] = -1f;
                    companionPeeking[i] = false;
                }
            }

            if (i == activeSlot)
            {
                companionRegrouping[i] = false;
                coverRequested[i] = false;
                continue;
            }

            PrototypeEnemyTarget threateningEnemy = FindThreateningEnemy(companion);
            if (threateningEnemy != null)
            {
                lastTargetedWarningAt[i] = Time.time;
                coverRequested[i] = true;
            }

            if (companion.Health <= companion.MaxHealth * 0.4f && !lowHealthCoverHandled[i])
            {
                coverRequested[i] = true;
                lowHealthCoverHandled[i] = true;
            }

            if (companionRegrouping[i])
            {
                coverRequested[i] = false;
            }

        }

        coverRefreshRemaining -= deltaTime;
        if (coverRefreshRemaining <= 0f)
        {
            RefreshCoverAssignments();
            coverRefreshRemaining = 0.4f;
        }

        for (int i = 0; i < characters.Length; i++)
        {
            PrototypeCharacter companion = characters[i];
            if (i == activeSlot || !companion.IsAlive || i == 1 && IsAutoReviveControllingMorrow())
            {
                continue;
            }

            PrototypeEnemyTarget target = SelectTarget(companion);
            if (companionRegrouping[i])
            {
                MoveCompanion(companion, GetFollowPosition(i), deltaTime);
                companion.SetCoverState(false, false);
                if (IsCombatActive() && target != null && HasClearShot(companion.Muzzle.position, target))
                {
                    TryCompanionShot(companion, target);
                }

                continue;
            }

            if (coverRequested[i] && assignedCovers[i] == null)
            {
                MoveCompanion(companion, GetFormationPosition(i), deltaTime);
                companion.SetCoverState(false, false);
                if (IsCombatActive() && target != null && HasClearShot(companion.Muzzle.position, target))
                {
                    TryCompanionShot(companion, target);
                }

                continue;
            }

            if (!coverRequested[i])
            {
                assignedCovers[i] = null;
                coverEnteredAt[i] = -1f;
                companionPeeking[i] = false;
                MoveCompanion(companion, GetFormationPosition(i), deltaTime);
                companion.SetCoverState(false, false);
                if (IsCombatActive() && target != null && HasClearShot(companion.Muzzle.position, target))
                {
                    TryCompanionShot(companion, target);
                }

                continue;
            }

            PrototypeCover cover = assignedCovers[i];
            Vector3 destination = cover.Position;
            bool atCover = cover != null && FlatDistanceSquared(companion.transform.position, destination) <= CoverArrivalRadius * CoverArrivalRadius;
            if (atCover && coverEnteredAt[i] < 0f)
            {
                coverEnteredAt[i] = Time.time;
                companionPeeking[i] = false;
                peekPhaseRemaining[i] = 0.75f;
            }

            if (atCover && peekPhaseRemaining[i] <= 0f)
            {
                companionPeeking[i] = !companionPeeking[i];
                peekPhaseRemaining[i] = companionPeeking[i] ? 1.1f : 1.4f;
            }

            peekPhaseRemaining[i] = Mathf.Max(0f, peekPhaseRemaining[i] - deltaTime);

            if (atCover && companion.Ammo < companion.MagazineSize && !companion.IsReloading)
            {
                companion.BeginReload();
            }

            if (!atCover)
            {
                MoveCompanion(companion, destination, deltaTime);
            }

            bool canShoot = IsCombatActive() && target != null
                && HasClearShot(GetMuzzlePosition(companion, atCover), target);
            bool peekingToShoot = !atCover || companionPeeking[i];
            bool attacking = canShoot && peekingToShoot && !companion.IsReloading && companion.Ammo > 0;
            companion.SetCoverState(atCover ? cover : null, attacking);
            if (canShoot && peekingToShoot)
            {
                if (atCover)
                {
                    Vector3 aimDirection = target.GetComponent<Collider>().bounds.center - companion.transform.position;
                    aimDirection.y = 0f;
                    if (aimDirection.sqrMagnitude > 0.001f)
                    {
                        companion.transform.rotation = Quaternion.Slerp(companion.transform.rotation,
                            Quaternion.LookRotation(aimDirection), deltaTime * 10f);
                    }
                }

                TryCompanionShot(companion, target);
            }

            if (atCover && coverEnteredAt[i] >= 0f && Time.time - coverEnteredAt[i] >= MinimumCoverSeconds
                && (companion.Health > companion.MaxHealth * 0.4f
                    || Time.time - lastTargetedWarningAt[i] >= 5f))
            {
                coverRequested[i] = false;
                assignedCovers[i] = null;
                coverEnteredAt[i] = -1f;
                companionPeeking[i] = false;
                companion.SetCoverState(false, false);
            }
        }
    }

    private bool IsCombatActive()
    {
        return combatRemaining > 0f;
    }

    private PrototypeEnemyTarget FindThreateningEnemy(PrototypeCharacter character)
    {
        foreach (PrototypeEnemyTarget enemy in enemies)
        {
            if (enemy != null && !enemy.IsDefeated && enemy.IsThreateningCharacter(character))
            {
                return enemy;
            }
        }

        return null;
    }

    private void TryCompanionShot(PrototypeCharacter companion, PrototypeEnemyTarget target)
    {
        companion.SetAiming(true);
        float distance = Vector3.Distance(companion.Muzzle.position, target.GetComponent<Collider>().bounds.center);
        float spread = companion.Slot == 0 ? 0f : distance <= 8f ? 0.03f : distance <= 16f ? 0.2f : 0.5f;
        if (companion.TryFireAt(target.GetComponent<Collider>().bounds.center, spread))
        {
            combatRemaining = CombatPersistenceSeconds;
        }
    }

    private void TryAutomaticPlayerShot(PrototypeCharacter character)
    {
        if (!character.IsAlive || character.IsReloading)
        {
            return;
        }

        PrototypeEnemyTarget target = FindAutomaticPlayerTarget(character, true);
        if (target == null)
        {
            return;
        }

        if (character.Ammo <= 0)
        {
            character.BeginReload();
            return;
        }

        character.SetCoverState(character.IsInCover, true);
        float distance = Vector3.Distance(character.Muzzle.position, target.GetComponent<Collider>().bounds.center);
        float spread = character.Slot == 0 ? 0f : distance <= 8f ? 0.03f : distance <= 16f ? 0.2f : 0.5f;
        if (character.TryFireAt(target.GetComponent<Collider>().bounds.center, spread))
        {
            combatRemaining = CombatPersistenceSeconds;
            ApplyWeaponRecoil(character);
        }
    }

    private PrototypeEnemyTarget FindAutomaticPlayerTarget(PrototypeCharacter character, bool requireClearShot)
    {
        PrototypeEnemyTarget closest = null;
        PrototypeEnemyTarget threatening = null;
        float closestDistance = 100f * 100f;
        float threateningDistance = 100f * 100f;
        foreach (PrototypeEnemyTarget enemy in enemies)
        {
            if (enemy == null || enemy.IsDefeated || enemy.IsTrainingDummy)
            {
                continue;
            }

            float distance = (enemy.transform.position - character.transform.position).sqrMagnitude;
            if (distance > 100f * 100f || requireClearShot && !HasClearShot(character.Muzzle.position, enemy))
            {
                continue;
            }

            if (enemy.IsThreateningPlayer || enemy.IsThreateningCharacter(character))
            {
                if (distance < threateningDistance)
                {
                    threatening = enemy;
                    threateningDistance = distance;
                }

                continue;
            }

            if (distance < closestDistance)
            {
                closest = enemy;
                closestDistance = distance;
            }
        }

        return threatening != null ? threatening : closest;
    }

    private void MoveAutomaticPlayer(PrototypeCharacter character, PrototypeEnemyTarget target, float deltaTime)
    {
        if (target == null)
        {
            MoveCharacter(character);
            return;
        }

        Vector3 toTarget = target.transform.position - character.transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance > AutoEngagementDistance)
        {
            Vector3 destination = target.transform.position - toTarget.normalized * AutoEngagementDistance;
            destination.y = character.transform.position.y;
            MoveCompanion(character, destination, deltaTime, MoveSpeed);
        }
        else
        {
            if (toTarget.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized);
                character.transform.rotation = Quaternion.Slerp(character.transform.rotation, targetRotation, deltaTime * 8f);
            }
        }

        if (character.Controller.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
        }

        verticalVelocity.y += Physics.gravity.y * deltaTime;
        character.Controller.Move(Vector3.up * verticalVelocity.y * deltaTime);
    }

    private static bool HasPlayerMovementInput()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.wKey.isPressed || keyboard.aKey.isPressed
            || keyboard.sKey.isPressed || keyboard.dKey.isPressed);
    }

    private void RefreshCoverAssignments()
    {
        PrototypeCover playerCover = FindCoverNear(ActiveCharacter.transform.position);
        List<PrototypeCover> reserved = new List<PrototypeCover>();
        PrototypeCover[] occupiedByCharacters = new PrototypeCover[characters.Length];
        if (playerCover != null)
        {
            reserved.Add(playerCover);
        }

        for (int slot = 0; slot < characters.Length; slot++)
        {
            if (slot != activeSlot && characters[slot].IsAlive
                && !(slot == 1 && IsAutoReviveControllingMorrow()))
            {
                occupiedByCharacters[slot] = FindCoverNear(characters[slot].transform.position);
                if (occupiedByCharacters[slot] != null && !reserved.Contains(occupiedByCharacters[slot]))
                {
                    reserved.Add(occupiedByCharacters[slot]);
                }
            }
        }

        for (int slot = 0; slot < characters.Length; slot++)
        {
            PrototypeCharacter companion = characters[slot];
            if (slot == activeSlot || !companion.IsAlive || !coverRequested[slot]
                || slot == 1 && IsAutoReviveControllingMorrow())
            {
                assignedCovers[slot] = null;
                coverEnteredAt[slot] = -1f;
                companionPeeking[slot] = false;
                continue;
            }

            PrototypeEnemyTarget target = SelectTarget(companion);
            PrototypeCover current = assignedCovers[slot];
            PrototypeCover occupiedBySelf = occupiedByCharacters[slot];
            bool currentAvailable = current != null && current != playerCover
                && (!reserved.Contains(current) || current == occupiedBySelf);
            if (currentAvailable && (target == null || HasClearShot(current.Position + Vector3.up * 1.6f, target)))
            {
                if (!reserved.Contains(current))
                {
                    reserved.Add(current);
                }

                continue;
            }

            if (currentAvailable)
            {
                List<PrototypeCover> unavailable = new List<PrototypeCover>(reserved);
                if (occupiedBySelf != null)
                {
                    unavailable.Remove(occupiedBySelf);
                }

                if (!unavailable.Contains(current))
                {
                    unavailable.Add(current);
                }

                PrototypeCover alternate = FindBestCover(companion, target, unavailable, true);
                if (alternate != null)
                {
                    assignedCovers[slot] = alternate;
                    coverEnteredAt[slot] = -1f;
                    companionPeeking[slot] = false;
                    reserved.Add(alternate);
                }
                else
                {
                    assignedCovers[slot] = current;
                }

                continue;
            }

            PrototypeCover bestVisible = FindBestCover(companion, target, reserved, true);
            PrototypeCover bestAny = bestVisible != null ? bestVisible : FindBestCover(companion, target, reserved, false);
            if (bestAny != null)
            {
                if (assignedCovers[slot] != bestAny)
                {
                    coverEnteredAt[slot] = -1f;
                    companionPeeking[slot] = false;
                }

                assignedCovers[slot] = bestAny;
                reserved.Add(bestAny);
            }
            else
            {
                assignedCovers[slot] = null;
            }
        }
    }

    private PrototypeCover FindBestCover(PrototypeCharacter character, PrototypeEnemyTarget target,
        List<PrototypeCover> reserved, bool requireLineOfSight)
    {
        PrototypeCover best = null;
        float bestDistance = float.MaxValue;
        bool hasNearby = false;
        foreach (PrototypeCover cover in covers)
        {
            if (cover == null || !cover.IsIntact)
            {
                continue;
            }

            if (reserved.Contains(cover))
            {
                continue;
            }

            bool nearby = FlatDistanceSquared(cover.Position, ActiveCharacter.transform.position) <= CoverScanRadius * CoverScanRadius;
            if (nearby)
            {
                hasNearby = true;
            }
        }

        foreach (PrototypeCover cover in covers)
        {
            if (cover == null || !cover.IsIntact)
            {
                continue;
            }

            if (reserved.Contains(cover))
            {
                continue;
            }

            bool nearby = FlatDistanceSquared(cover.Position, ActiveCharacter.transform.position) <= CoverScanRadius * CoverScanRadius;
            if (hasNearby && !nearby)
            {
                continue;
            }

            if (requireLineOfSight && target != null && !HasClearShot(cover.Position + Vector3.up * 1.6f, target))
            {
                continue;
            }

            float distance = FlatDistanceSquared(character.transform.position, cover.Position);
            if (distance < bestDistance)
            {
                best = cover;
                bestDistance = distance;
            }
        }

        return best;
    }

    private PrototypeEnemyTarget SelectTarget(PrototypeCharacter character)
    {
        PrototypeEnemyTarget closest = null;
        float closestDistance = float.MaxValue;
        foreach (PrototypeEnemyTarget enemy in enemies)
        {
            if (enemy == null || enemy.IsDefeated || enemy.IsTrainingDummy)
            {
                continue;
            }

            if (enemy.IsThreateningPlayer || enemy.IsThreateningCharacter(character))
            {
                return enemy;
            }

            float distance = (enemy.transform.position - character.transform.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closest = enemy;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private bool HasClearShot(Vector3 origin, PrototypeEnemyTarget target)
    {
        if (target == null || target.IsDefeated)
        {
            return false;
        }

        Vector3 targetPoint = target.GetComponent<Collider>().bounds.center;
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, distance + 0.1f);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<PrototypeCharacter>() != null)
            {
                continue;
            }

            return hit.collider.GetComponentInParent<PrototypeEnemyTarget>() == target;
        }

        return false;
    }

    private static Vector3 GetMuzzlePosition(PrototypeCharacter character, bool peeking)
    {
        Vector3 muzzle = character.Muzzle.position;
        if (peeking)
        {
            muzzle.y = character.transform.position.y + 1.55f;
        }

        return muzzle;
    }

    private void MoveCompanion(PrototypeCharacter companion, Vector3 destination, float deltaTime, float moveSpeed = CompanionMoveSpeed)
    {
        int slot = companion.Slot;
        Vector3 startPosition = companion.transform.position;
        if (slot != activeSlot)
        {
            float followDistance = Mathf.Sqrt(FlatDistanceSquared(startPosition, destination));
            float catchUpFactor = Mathf.InverseLerp(CompanionCatchUpStartDistance,
                CompanionCatchUpFullSpeedDistance, followDistance);
            moveSpeed = Mathf.Lerp(moveSpeed, CompanionCatchUpSpeed, catchUpFactor);
            moveSpeed = Mathf.Min(moveSpeed, Mathf.Max(1f, followDistance * 2f));
        }

        Vector3 direction = destination - companion.transform.position;
        direction.y = 0f;
        bool reachedDestination = direction.sqrMagnitude <= 0.02f;
        if (!reachedDestination)
        {
            direction.Normalize();
        }

        Vector3 separation = Vector3.zero;
        float minSpacingSquared = MinimumCompanionSpacing * MinimumCompanionSpacing;
        foreach (PrototypeCharacter other in characters)
        {
            if (other == companion || other == ActiveCharacter || !other.IsAlive)
            {
                continue;
            }

            Vector3 away = companion.transform.position - other.transform.position;
            away.y = 0f;
            float distanceSquared = away.sqrMagnitude;
            if (distanceSquared >= minSpacingSquared)
            {
                continue;
            }

            float distance = Mathf.Sqrt(distanceSquared);
            if (distance < 0.001f)
            {
                away = ActiveCharacter.transform.right * (companion.Slot < other.Slot ? -1f : 1f);
                distance = 0f;
            }
            else
            {
                away /= distance;
            }

            separation += away * (MinimumCompanionSpacing - distance) / MinimumCompanionSpacing;
        }

        Vector3 movementOrigin = companion.transform.position + Vector3.up * 0.75f;
        Vector3 toDestination = destination - companion.transform.position;
        toDestination.y = 0f;
        toDestination.Normalize();
        if (!reachedDestination && companionRouteRemaining[slot] > 0f
            && (HasMovementObstacle(movementOrigin, companionRouteDirections[slot])
                || !HasMovementObstacle(movementOrigin, toDestination)
                || Vector3.Dot(companionRouteDirections[slot], toDestination) < -0.2f))
        {
            companionRouteRemaining[slot] = 0f;
        }

        bool directPathBlocked = !reachedDestination && HasMovementObstacle(movementOrigin, direction.normalized);
        if (!reachedDestination && companionRouteRemaining[slot] > 0f)
        {
            direction = companionRouteDirections[slot];
            companionRouteRemaining[slot] = Mathf.Max(0f, companionRouteRemaining[slot] - deltaTime);
        }
        else if (directPathBlocked)
        {
            direction = FindDetourDirection(movementOrigin, toDestination, slot);
            if (direction.sqrMagnitude > 0.001f)
            {
                companionRouteDirections[slot] = direction;
                companionRouteRemaining[slot] = DetourHoldSeconds;
            }
        }

        if (!reachedDestination && separation.sqrMagnitude > 0.001f)
        {
            Vector3 spacedDirection = direction + separation.normalized * 0.75f;
            if (!HasMovementObstacle(movementOrigin, spacedDirection.normalized))
            {
                direction = spacedDirection;
            }
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            TickCompanionStuck(slot, startPosition, destination, reachedDestination, deltaTime);
            return;
        }

        direction.Normalize();
        companion.transform.rotation = Quaternion.Slerp(companion.transform.rotation,
            Quaternion.LookRotation(direction), deltaTime * 8f);
        companion.SetWeaponAimDirection(companion.transform.forward);
        companion.Controller.Move(direction * moveSpeed * deltaTime);
        companion.SetCombatPose(Mathf.Clamp01(moveSpeed / MoveSpeed), companion.IsAttacking);
        TickCompanionStuck(slot, startPosition, destination, reachedDestination, deltaTime);
    }

    private void ApplyCompanionGravity(PrototypeCharacter companion, float deltaTime)
    {
        int slot = companion.Slot;
        CharacterController controller = companion.Controller;
        if (controller.isGrounded && companionVerticalVelocity[slot] < 0f)
        {
            companionVerticalVelocity[slot] = -2f;
        }

        companionVerticalVelocity[slot] += Physics.gravity.y * deltaTime;
        CollisionFlags flags = controller.Move(Vector3.up * companionVerticalVelocity[slot] * deltaTime);
        if ((flags & CollisionFlags.Below) != 0)
        {
            companionVerticalVelocity[slot] = -2f;
        }
    }

    private Vector3 FindDetourDirection(Vector3 origin, Vector3 towardDestination, int slot)
    {
        Vector3 bestDirection = Vector3.zero;
        float bestScore = float.NegativeInfinity;
        for (int angle = 35; angle <= 145; angle += 35)
        {
            for (int sidePass = 0; sidePass < 2; sidePass++)
            {
                int side = sidePass == 0 ? companionAvoidanceSide[slot] : -companionAvoidanceSide[slot];
                Vector3 candidate = Quaternion.AngleAxis(angle * side, Vector3.up) * towardDestination;
                if (HasMovementObstacle(origin, candidate))
                {
                    continue;
                }

                float score = Vector3.Dot(candidate, towardDestination) + (sidePass == 0 ? 0.05f : 0f);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDirection = candidate;
                }
            }
        }

        return bestDirection;
    }

    private void TickCompanionStuck(int slot, Vector3 startPosition, Vector3 destination,
        bool reachedDestination, float deltaTime)
    {
        if (reachedDestination)
        {
            companionRouteRemaining[slot] = 0f;
            companionStuckSeconds[slot] = 0f;
            return;
        }

        float distanceBeforeMove = Mathf.Sqrt(FlatDistanceSquared(startPosition, destination));
        float distanceAfterMove = Mathf.Sqrt(FlatDistanceSquared(characters[slot].transform.position, destination));
        if (distanceBeforeMove - distanceAfterMove > 0.04f)
        {
            companionStuckSeconds[slot] = 0f;
            return;
        }

        companionStuckSeconds[slot] += deltaTime;
        if (companionStuckSeconds[slot] >= StuckRepathDelay)
        {
            companionAvoidanceSide[slot] *= -1;
            companionRouteRemaining[slot] = 0f;
            companionStuckSeconds[slot] = 0f;
        }
    }

    private static bool HasMovementObstacle(Vector3 origin, Vector3 direction)
    {
        int hitCount = Physics.SphereCastNonAlloc(origin, 0.3f, direction, movementObstacleHits, 0.45f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            if (movementObstacleHits[i].collider.GetComponentInParent<PrototypeCharacter>() == null)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetFollowPosition(int slot)
    {
        Vector3 side = ActiveCharacter.transform.right * (slot == 1 ? -1.6f : 1.6f);
        return ActiveCharacter.transform.position - ActiveCharacter.transform.forward * 2f + side;
    }

    private Vector3 GetFormationPosition(int slot)
    {
        return formationHoldActive ? heldFormationPositions[slot] : GetFollowPosition(slot);
    }

    private void UpdateFormationHold(bool playerMoved, float deltaTime)
    {
        if (lastFormationPlayerSlot != activeSlot)
        {
            lastFormationPlayerSlot = activeSlot;
            playerStillSeconds = 0f;
            formationHoldActive = false;
        }

        if (playerMoved)
        {
            playerStillSeconds = 0f;
            formationHoldActive = false;
            return;
        }

        playerStillSeconds += deltaTime;
        if (!formationHoldActive && playerStillSeconds >= FormationHoldDelay)
        {
            formationHoldActive = true;
            for (int slot = 0; slot < characters.Length; slot++)
            {
                if (slot != activeSlot)
                {
                    heldFormationPositions[slot] = GetFollowPosition(slot);
                }
            }
        }
    }

    private bool IsAutoReviveControllingMorrow()
    {
        return burstSystem != null && burstSystem.IsAutomatic && reviveTarget != null && reviveCooldownRemaining <= 0f
            && characters[1].IsAlive;
    }

    private static float FlatDistanceSquared(Vector3 left, Vector3 right)
    {
        left.y = 0f;
        right.y = 0f;
        return (left - right).sqrMagnitude;
    }

    private void SetActiveCharacter(int slot)
    {
        if (slot >= 0 && slot < characters.Length && characters[slot].IsAlive)
        {
            activeSlot = slot;
        }
    }

    public void ApplyPrototypeDamageToActiveCharacter(float damage)
    {
        ActiveCharacter.TakeDamage(damage);
        if (!ActiveCharacter.IsAlive)
        {
            int nextAlive = FindNextAliveSlot(activeSlot);
            if (nextAlive >= 0)
            {
                activeSlot = nextAlive;
            }
        }
    }

    private bool UpdateRevive(float deltaTime)
    {
        reviveCooldownRemaining = Mathf.Max(0f, reviveCooldownRemaining - deltaTime);
        if (characters == null || burstSystem == null)
        {
            return false;
        }

        PrototypeCharacter morrow = characters[1];
        if (!morrow.IsAlive)
        {
            CancelRevive(false);
            return false;
        }

        bool automatic = burstSystem.IsAutomatic;
        if (automatic && reviveCooldownRemaining <= 0f
            && (reviveTarget == null || reviveTarget.IsAlive))
        {
            reviveTarget = FindNearestDownedCharacter();
        }

        bool autoMovingMorrow = automatic && reviveTarget != null && reviveCooldownRemaining <= 0f;
        if (autoMovingMorrow && DistanceToReviveTarget(morrow, reviveTarget) > ReviveRange)
        {
            morrow.SetCoverState(false, false);
            MoveMorrowToward(morrow, reviveTarget, deltaTime);
        }
        else if (autoMovingMorrow && reviveRemaining <= 0f)
        {
            StartRevive(reviveTarget);
        }

        if (reviveTarget != null && reviveRemaining > 0f)
        {
            if (reviveTarget.IsAlive || DistanceToReviveTarget(morrow, reviveTarget) > ReviveRange)
            {
                CancelRevive(false);
            }
            else
            {
                reviveRemaining = Mathf.Max(0f, reviveRemaining - deltaTime);
                if (reviveRemaining == 0f)
                {
                    reviveTarget.Revive(0.3f);
                    CancelRevive(true);
                }
            }
        }

        return autoMovingMorrow;
    }

    private void TryBeginManualRevive()
    {
        if (reviveCooldownRemaining > 0f || reviveRemaining > 0f || !characters[1].IsAlive)
        {
            return;
        }

        PrototypeCharacter target = FindNearestDownedCharacter();
        if (target != null && DistanceToReviveTarget(characters[1], target) <= ReviveRange)
        {
            StartRevive(target);
        }
    }

    private PrototypeCharacter FindNearestDownedCharacter()
    {
        PrototypeCharacter closest = null;
        float closestDistance = float.MaxValue;
        PrototypeCharacter morrow = characters[1];
        for (int i = 0; i < characters.Length; i++)
        {
            PrototypeCharacter candidate = characters[i];
            if (i == 1 || candidate.IsAlive)
            {
                continue;
            }

            float distance = DistanceToReviveTarget(morrow, candidate);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    private static float DistanceToReviveTarget(PrototypeCharacter source, PrototypeCharacter target)
    {
        Vector3 offset = source.transform.position - target.transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    private static void MoveMorrowToward(PrototypeCharacter morrow, PrototypeCharacter target, float deltaTime)
    {
        Vector3 direction = target.transform.position - morrow.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        direction.Normalize();
        morrow.transform.rotation = Quaternion.LookRotation(direction);
        morrow.Controller.Move(direction * MoveSpeed * deltaTime);
        morrow.SetCombatPose(1f, false);
    }

    private void StartRevive(PrototypeCharacter target)
    {
        reviveTarget = target;
        reviveRemaining = ReviveDuration;
    }

    private void CancelRevive(bool applyCooldown)
    {
        reviveTarget = null;
        reviveRemaining = 0f;
        if (applyCooldown)
        {
            reviveCooldownRemaining = ReviveCooldown;
        }
    }

    private void OnCharacterDamaged(PrototypeCharacter character, float damage)
    {
        if (character == characters[1] && reviveRemaining > 0f)
        {
            CancelRevive(true);
        }
    }

    private void OnSquadEnemyHit(PrototypeEnemyTarget target)
    {
        if (target != null && !target.IsTrainingDummy)
        {
            combatRemaining = CombatPersistenceSeconds;
        }
    }

    private int FindNextAliveSlot(int startingSlot)
    {
        for (int offset = 1; offset <= characters.Length; offset++)
        {
            int candidate = (startingSlot + offset) % characters.Length;
            if (characters[candidate].IsAlive)
            {
                return candidate;
            }
        }

        return -1;
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
        Quaternion rotation = Quaternion.Euler(pitch - cameraRecoilPitch, yaw, 0f);
        Vector3 target = character.transform.position + Vector3.up * 1.5f;
        Vector3 shoulderOffset = rotation * new Vector3(0.65f, 0f, -3.8f);
        Vector3 desiredOffset = shoulderOffset;
        float desiredDistance = desiredOffset.magnitude;
        Vector3 cameraDirection = desiredOffset / desiredDistance;
        int hitCount = Physics.SphereCastNonAlloc(target, 0.18f, cameraDirection, movementObstacleHits,
            desiredDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float allowedDistance = desiredDistance;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = movementObstacleHits[i];
            if (hit.collider.GetComponentInParent<PrototypeCharacter>() != null)
            {
                continue;
            }

            allowedDistance = Mathf.Min(allowedDistance, hit.distance - 0.12f);
        }

        Vector3 cameraPosition = target + cameraDirection * Mathf.Clamp(allowedDistance, 0.25f, desiredDistance);
        float cameraMoveSpeed = allowedDistance < desiredDistance ? 24f : 8f;
        float cameraBlend = 1f - Mathf.Exp(-cameraMoveSpeed * Time.deltaTime);
        playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, cameraPosition, cameraBlend);
        playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, rotation, Time.deltaTime * 16f);

        IsScopeAiming = character.HasScope && cursorLocked && Mouse.current.rightButton.isPressed;
        float targetFieldOfView = !character.HasScope && cursorLocked && Mouse.current.rightButton.isPressed
            ? AimFieldOfView
            : 60f;
        playerCamera.fieldOfView = Mathf.MoveTowards(playerCamera.fieldOfView, targetFieldOfView,
            Time.deltaTime * 90f);
    }

    public void ApplyCameraRecoil(float degrees)
    {
        cameraRecoilPitch = Mathf.Min(3f, cameraRecoilPitch + Mathf.Max(0f, degrees));
    }

    public void RegisterHitMarker(PrototypeCharacter shooter, bool critical)
    {
        if (shooter == ActiveCharacter)
        {
            hitMarkerRemaining = 0.14f;
            hitMarkerCritical = critical;
        }
    }

    private void ApplyWeaponRecoil(PrototypeCharacter character)
    {
        float degrees = character.Slot == 0 ? 0.72f : character.Slot == 1 ? 0.16f : 0.21f;
        ApplyCameraRecoil(degrees);
    }

    private void OnCharacterConfirmedHit(PrototypeCharacter character, bool critical)
    {
        RegisterHitMarker(character, critical);
    }

    private void OnDestroy()
    {
        if (characters != null && characters.Length > 1 && characters[1] != null)
        {
            characters[1].Damaged -= OnCharacterDamaged;
        }

        if (characters != null)
        {
            foreach (PrototypeCharacter character in characters)
            {
                if (character != null) character.ConfirmedHit -= OnCharacterConfirmedHit;
            }
        }

        foreach (PrototypeEnemyTarget enemy in enemies)
        {
            if (enemy != null)
            {
                enemy.Hit -= OnSquadEnemyHit;
            }
        }
    }
}
