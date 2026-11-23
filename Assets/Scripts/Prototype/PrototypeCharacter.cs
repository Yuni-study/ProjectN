using UnityEngine;

public sealed class PrototypeCharacter : MonoBehaviour
{
    private const float BaseCriticalChance = 0.2f;

    public int Slot { get; private set; }
    public float MaxHealth { get; private set; }
    public float Health { get; private set; }
    public float DamagePerShot { get; private set; }
    public float ShotsPerSecond { get; private set; }
    public int MagazineSize { get; private set; }
    public int Ammo { get; private set; }
    public float ReloadSeconds { get; private set; }
    public float ReloadRemaining { get; private set; }
    public float Shield { get; private set; }
    public float ShieldRemaining { get; private set; }
    public bool IsAlive => Health > 0f;
    public float OutgoingDamageMultiplier { get; private set; } = 1f;
    public float ReloadSpeedMultiplier { get; private set; } = 1f;
    public bool HasScope { get; private set; }
    public bool IsReloading => ReloadRemaining > 0f;
    public bool IsInCover => currentCover != null && currentCover.IsIntact;
    public bool IsAttacking => IsInCover && attackingFromCover;
    public CharacterController Controller { get; private set; }
    public Transform Muzzle { get; private set; }
    public Transform VisualRoot { get; private set; }
    private Animator animator;
    private Transform weaponHand;
    private Transform weaponObject;
    private Vector3 weaponAimDirection = Vector3.forward;
    private Transform supportHand;
    private Vector3 stockPoint, gripPoint, supportPoint;
    private bool wantsAim;
    private bool visualGroundAligned;
    private float aimBlend, firePoseRemaining, weaponRecoil;
    private static readonly int SpeedParameter = Animator.StringToHash("Speed");
    private static readonly int AimParameter = Animator.StringToHash("Aim");
    private static readonly int FireTrigger = Animator.StringToHash("Fire");
    private PrototypeCover currentCover;
    private bool attackingFromCover;

    private void LateUpdate()
    {
        AlignVisualToGroundOnce();
        if (weaponHand == null || weaponObject == null) return;
        if (supportHand == null)
        {
            weaponObject.SetPositionAndRotation(weaponHand.position,
                Quaternion.LookRotation(weaponAimDirection, Vector3.up) * Quaternion.Euler(0f, -90f, 0f));
            return;
        }
        firePoseRemaining = Mathf.Max(0f, firePoseRemaining - Time.deltaTime);
        bool aiming = IsAlive && !IsReloading && (wantsAim || firePoseRemaining > 0f);
        aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1f : 0f, Time.deltaTime * 8f);
        Vector3 carryDirection = (transform.forward - Vector3.up * 0.35f).normalized;
        Vector3 direction = Vector3.Slerp(carryDirection, weaponAimDirection, aimBlend);
        Quaternion aimRotation = Quaternion.LookRotation(direction, transform.up)
            * Quaternion.Euler(-weaponRecoil * (Slot == 0 ? 3f : 1.3f), 0f, 0f);
        Quaternion gunRotation = aimRotation * Quaternion.Euler(0f, -90f, 0f);
        // Keep the stock at the shoulder; solve both arms after the Animator has moved the body.
        Vector3 shoulder = weaponHand.parent.parent.position;
        Vector3 stock = shoulder + transform.up * Mathf.Lerp(-0.27f, -0.04f, aimBlend)
            + transform.forward * 0.025f + transform.right * -0.04f
            - aimRotation * Vector3.forward * (weaponRecoil * 0.035f);
        weaponObject.rotation = gunRotation;
        weaponObject.position += stock - weaponObject.TransformPoint(stockPoint);
        SolveWeaponArm(weaponHand, weaponObject.TransformPoint(gripPoint), transform.right - transform.up);
        SolveWeaponArm(supportHand, weaponObject.TransformPoint(supportPoint), -transform.right - transform.up);
        weaponRecoil = Mathf.MoveTowards(weaponRecoil, 0f, Time.deltaTime * 12f);
    }

    private void AlignVisualToGroundOnce()
    {
        if (visualGroundAligned || VisualRoot == null) return;
        Renderer[] renderers = VisualRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        float feetY = renderers[0].bounds.min.y;
        for (int i = 1; i < renderers.Length; i++) feetY = Mathf.Min(feetY, renderers[i].bounds.min.y);
        VisualRoot.position += Vector3.up * (transform.position.y - feetY);
        visualGroundAligned = true;
    }

    private float shotCooldown;

    public event System.Action<PrototypeCharacter, float> Damaged;
    public event System.Action<PrototypeCharacter, bool> ConfirmedHit;

    public void Initialize(int slot, float health, float damage, float shotsPerSecond, int magazineSize,
        float reloadSeconds, bool hasScope, Transform muzzle, Animator characterAnimator,
        Transform characterWeaponHand, Transform weaponTransform, Transform visualRoot)
    {
        Slot = slot;
        MaxHealth = health;
        Health = health;
        DamagePerShot = damage;
        ShotsPerSecond = shotsPerSecond;
        MagazineSize = magazineSize;
        Ammo = magazineSize;
        ReloadSeconds = reloadSeconds;
        HasScope = hasScope;
        Muzzle = muzzle;
        animator = characterAnimator;
        weaponHand = characterWeaponHand;
        weaponObject = weaponTransform;
        VisualRoot = visualRoot;
        ConfigureWeaponPose();
        Controller = GetComponent<CharacterController>();
        SetCombatPose(0f, true);
    }

    public void TickWeapon(float deltaTime)
    {
        if (ReloadRemaining > 0f)
        {
            ReloadRemaining = Mathf.Max(0f, ReloadRemaining - deltaTime * ReloadSpeedMultiplier);
            if (ReloadRemaining == 0f)
            {
                Ammo = MagazineSize;
            }
        }

        if (ShieldRemaining > 0f)
        {
            ShieldRemaining = Mathf.Max(0f, ShieldRemaining - deltaTime);
            if (ShieldRemaining == 0f)
            {
                Shield = 0f;
            }
        }

        shotCooldown = Mathf.Max(0f, shotCooldown - deltaTime);
    }

    public bool TryFire(Camera camera)
    {
        if (!IsAlive || IsReloading || Ammo <= 0 || shotCooldown > 0f)
        {
            return false;
        }

        Ammo--;
        shotCooldown = 1f / ShotsPerSecond;
        PlayWeaponFireAnimation();
        PrototypeCombatFeedback.PlayShot(Muzzle.position, Slot);
        FireHitscan(camera);
        return true;
    }

    public bool TryFireAt(Vector3 targetPoint, float spreadRadius)
    {
        if (!IsAlive || IsReloading || Ammo <= 0 || shotCooldown > 0f)
        {
            return false;
        }

        Ammo--;
        shotCooldown = 1f / ShotsPerSecond;
        SetWeaponAimDirection(targetPoint - Muzzle.position);
        PlayWeaponFireAnimation();
        PrototypeCombatFeedback.PlayShot(Muzzle.position, Slot);
        if (spreadRadius > 0f)
        {
            Vector2 spread = Random.insideUnitCircle * spreadRadius;
            targetPoint += transform.right * spread.x + Vector3.up * spread.y;
        }

        FireRay(new Ray(Muzzle.position, (targetPoint - Muzzle.position).normalized));
        return true;
    }

    public void SetCoverState(bool inCover, bool attacking)
    {
        if (!inCover)
        {
            currentCover = null;
        }

        attackingFromCover = inCover && attacking;
        UpdateCoverVisuals();
    }

    public void SetCoverState(PrototypeCover cover, bool attacking)
    {
        currentCover = cover;
        attackingFromCover = cover != null && attacking;
        UpdateCoverVisuals();
    }

    private void UpdateCoverVisuals()
    {
        SetAiming(IsAttacking);
    }

    public void SetCombatPose(float movementSpeed, bool aiming)
    {
        if (animator == null) return;
        animator.SetFloat(SpeedParameter, movementSpeed, 0.12f, Time.deltaTime);
        SetAiming(aiming);
    }

    public void SetAiming(bool aiming)
    {
        wantsAim = aiming;
        if (animator != null) animator.SetBool(AimParameter, aiming);
    }

    public void SetWeaponAimDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            weaponAimDirection = direction.normalized;
        }
    }

    public void PlayWeaponFireAnimation()
    {
        firePoseRemaining = 0.22f;
        weaponRecoil = 1f;
        if (animator != null)
        {
            animator.SetBool(AimParameter, true);
            animator.SetTrigger(FireTrigger);
        }
    }

    private void ConfigureWeaponPose()
    {
        if (animator == null || weaponHand == null || weaponHand.parent == null
            || weaponHand.parent.parent == null || weaponObject == null) return;
        foreach (Transform bone in animator.GetComponentsInChildren<Transform>())
            if (bone.name == "Wrist.L" || bone.name.EndsWith("|Wrist.L")) supportHand = bone;
        if (supportHand == null) return;
        Bounds bounds = new Bounds();
        bool first = true;
        foreach (Renderer renderer in weaponObject.GetComponentsInChildren<Renderer>())
        {
            Bounds local = renderer.localBounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 point = weaponObject.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
        }
        stockPoint = new Vector3(bounds.min.x, bounds.center.y, bounds.center.z);
        gripPoint = new Vector3(bounds.min.x + bounds.size.x * 0.23f,
            bounds.min.y + bounds.size.y * 0.3f, bounds.center.z);
        supportPoint = new Vector3(bounds.min.x + bounds.size.x * 0.34f,
            bounds.min.y + bounds.size.y * 0.4f, bounds.center.z);
        Muzzle.localPosition = new Vector3(bounds.max.x, bounds.center.y + bounds.size.y * 0.15f, bounds.center.z);
    }

    private static void SolveWeaponArm(Transform hand, Vector3 target, Vector3 elbowHint)
    {
        Transform elbow = hand.parent;
        Transform shoulder = elbow.parent;
        float upperLength = Vector3.Distance(shoulder.position, elbow.position);
        float lowerLength = Vector3.Distance(elbow.position, hand.position);
        Vector3 toTarget = target - shoulder.position;
        float distance = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.001f,
            upperLength + lowerLength - 0.001f);
        Vector3 direction = toTarget.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(elbowHint, direction).normalized;
        float along = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * distance);
        Vector3 elbowTarget = shoulder.position + direction * along
            + bend * Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        shoulder.rotation = Quaternion.FromToRotation(elbow.position - shoulder.position,
            elbowTarget - shoulder.position) * shoulder.rotation;
        elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position,
            target - elbow.position) * elbow.rotation;
    }

    public bool TryConsumeAmmo(int amount)
    {
        if (!IsAlive || IsReloading || amount <= 0 || Ammo < amount)
        {
            return false;
        }

        Ammo -= amount;
        return true;
    }

    public void BeginReload()
    {
        if (!IsReloading && Ammo < MagazineSize)
        {
            ReloadRemaining = ReloadSeconds;
            PrototypeAudioDirector.PlayReload(Slot);
        }
    }

    public void TakeDamage(float damage)
    {
        if (!IsAlive || damage <= 0f || IsInCover && !IsAttacking)
        {
            return;
        }

        float absorbed = Mathf.Min(Shield, damage);
        Shield -= absorbed;
        Health = Mathf.Max(0f, Health - (damage - absorbed));
        Damaged?.Invoke(this, damage);
        if (Shield <= 0f)
        {
            ShieldRemaining = 0f;
        }
    }

    public void ApplyKnockback(Vector3 direction, float distance)
    {
        if (!IsAlive || IsInCover && !IsAttacking || direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        direction.y = 0f;
        Controller.Move(direction.normalized * distance);
    }

    public void Revive(float healthFraction)
    {
        if (IsAlive)
        {
            return;
        }

        Health = Mathf.Clamp(MaxHealth * healthFraction, 1f, MaxHealth);
        PrototypeAudioDirector.PlayRevive();
    }

    public void ApplyShield()
    {
        Shield = MaxHealth * 0.25f;
        ShieldRemaining = 10f;
    }

    public void SetBurstState(bool active)
    {
        OutgoingDamageMultiplier = active ? 1.05f : 1f;
        ReloadSpeedMultiplier = active ? 1.05f : 1f;
    }

    private void FireHitscan(Camera camera)
    {
        Ray cameraRay = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 targetPoint = cameraRay.origin + cameraRay.direction * 100f;

        if (RaycastIgnoringCharacters(cameraRay, out RaycastHit aimHit, 100f))
        {
            targetPoint = aimHit.point;
        }

        SetWeaponAimDirection(targetPoint - Muzzle.position);
        FireRay(new Ray(Muzzle.position, (targetPoint - Muzzle.position).normalized));
    }

    private void FireRay(Ray ray)
    {
        Vector3 visualEndPoint = ray.origin + ray.direction * 100f;
        if (RaycastIgnoringCharacters(ray, out RaycastHit hit, 100f))
        {
            visualEndPoint = hit.point;
            PrototypeEnemyTarget target = hit.collider.GetComponentInParent<PrototypeEnemyTarget>();
            bool isCritical = false;
            if (target != null)
            {
                isCritical = Random.value < BaseCriticalChance;
                target.TakeDamage(DamagePerShot * OutgoingDamageMultiplier, isCritical, this);
                ConfirmedHit?.Invoke(this, isCritical);
            }

            PrototypeCombatFeedback.SpawnImpact(hit.point, hit.normal, target != null, isCritical);
            PrototypeCombatFeedback.PlayImpact(hit.point, isCritical);
        }

        PrototypeTracer.Spawn(ray.origin, visualEndPoint);
    }

    private bool RaycastIgnoringCharacters(Ray ray, out RaycastHit closestHit, float maxDistance)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<PrototypeCharacter>() == null)
            {
                closestHit = hit;
                return true;
            }
        }

        closestHit = default;
        return false;
    }
}
