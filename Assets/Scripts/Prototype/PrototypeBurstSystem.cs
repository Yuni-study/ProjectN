using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PrototypeBurstSystem : MonoBehaviour
{
    private const float GaugePerSniperHit = 0.05f;
    private const float GaugePerMachineGunHit = 0.01f;
    private const float GaugePerAssaultRifleHit = 0.01f;
    private const float LinkDuration = 10f;
    private const float StageDelay = 1f;
    private const float FailureCooldown = 4f;
    private const float BurstDuration = 7f;
    private const float FocusedFireInterval = 1f;
    private const int FocusedFireAmmoCost = 7;
    private const float TemporaryDamage = 40f;
    private const float MaxRayDistance = 100f;

    private readonly List<PrototypeEnemyTarget> targets = new List<PrototypeEnemyTarget>();
    private PrototypeSquadController squad;
    private PrototypeCharacter[] characters;
    private PrototypeEnemyTarget mostRecentlyHitTarget;
    private float gauge;
    private float linkRemaining;
    private float cooldownRemaining;
    private float burstRemaining;
    private float nextStageAt;
    private float toastRemaining;
    private float focusedFireCooldown;
    private int nextStage;
    private bool chainActive;
    private bool focusedFireMode;

    public System.Func<int, bool> ManualStageValidator { get; set; }
    public event System.Action<int, bool> StageUsed;
    public event System.Action FocusedFireShot;
    public event System.Action ChainFailed;

    public bool IsAutomatic { get; private set; }
    public float Gauge => gauge;
    public bool IsChainActive => chainActive;
    public bool IsBurstActive => burstRemaining > 0f;
    public bool IsFocusedFireMode => focusedFireMode;
    public float LinkFraction => Mathf.Clamp01(linkRemaining / LinkDuration);
    public float BurstFraction => Mathf.Clamp01(burstRemaining / BurstDuration);
    public bool ShowManualLinkTimer => !IsAutomatic && chainActive && nextStage > 0 && linkRemaining > 0f;
    public bool ShowNoTargetToast => toastRemaining > 0f;
    public int NextStage => nextStage;

    public bool IsStageAvailable(int stage) => CanAdvanceStage(stage);

    public void Initialize(PrototypeSquadController squadController, PrototypeCharacter[] squadCharacters)
    {
        squad = squadController;
        characters = squadCharacters;
    }

    public void RegisterTarget(PrototypeEnemyTarget target)
    {
        targets.Add(target);
        target.HitByCharacter += OnTargetHit;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);
        toastRemaining = Mathf.Max(0f, toastRemaining - Time.unscaledDeltaTime);
        focusedFireCooldown = Mathf.Max(0f, focusedFireCooldown - deltaTime);

        if (burstRemaining > 0f)
        {
            burstRemaining = Mathf.Max(0f, burstRemaining - deltaTime);
            if (burstRemaining == 0f)
            {
                SetBurstModifiers(false);
                focusedFireMode = false;
            }
        }

        if (squad != null && squad.IsMissionInputLocked)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || squad == null)
        {
            return;
        }

        if (keyboard.f8Key.wasPressedThisFrame)
        {
            squad.ApplyPrototypeDamageToActiveCharacter(TemporaryDamage);
        }

        if (keyboard.backquoteKey.wasPressedThisFrame)
        {
            IsAutomatic = !IsAutomatic;
        }

        if (chainActive)
        {
            linkRemaining = Mathf.Max(0f, linkRemaining - deltaTime);
            if (linkRemaining > 0f && IsAutomatic && Time.time >= nextStageAt && cooldownRemaining <= 0f && burstRemaining <= 0f)
            {
                TryAdvanceAutomatically();
            }

            if (linkRemaining == 0f)
            {
                FailChain(IsAutomatic || CanAdvanceStage(nextStage));
            }
        }
        else if (IsAutomatic && gauge >= 1f && cooldownRemaining <= 0f && burstRemaining <= 0f)
        {
            BeginAutomaticChain();
            TryAdvanceAutomatically();
        }

        if (!IsAutomatic && cooldownRemaining <= 0f && burstRemaining <= 0f)
        {
            if (keyboard.tKey.wasPressedThisFrame) TryManualStage(0);
            else if (keyboard.yKey.wasPressedThisFrame) TryManualStage(1);
            else if (keyboard.uKey.wasPressedThisFrame) TryManualStage(2);
        }

        if (IsAutomatic && focusedFireMode && burstRemaining > 0f && focusedFireCooldown <= 0f)
        {
            PrototypeEnemyTarget target = FindAutomaticTarget();
            if (target != null)
            {
                FireFocusedFire(characters[2], target, true);
            }

            focusedFireCooldown = FocusedFireInterval;
        }
    }

    private void TryManualStage(int requestedStage)
    {
        if (chainActive && Time.time < nextStageAt)
        {
            return;
        }

        if (!chainActive && gauge < 1f)
        {
            if (requestedStage != 0)
            {
                FailChain(true);
            }

            return;
        }

        if (ManualStageValidator != null && !ManualStageValidator(requestedStage))
        {
            return;
        }

        if (requestedStage != nextStage)
        {
            FailChain(true);
            return;
        }

        if (!CanAdvanceStage(requestedStage))
        {
            FailChain(true);
            return;
        }

        TryUseStage(requestedStage, false);
    }

    private void BeginAutomaticChain()
    {
        chainActive = true;
        nextStage = 0;
        linkRemaining = LinkDuration;
        nextStageAt = Time.time;
    }

    private void TryAdvanceAutomatically()
    {
        if (!CanAdvanceStage(nextStage))
        {
            return;
        }

        TryUseStage(nextStage, true);
    }

    private bool CanAdvanceStage(int stage)
    {
        for (int i = 0; i <= stage && i < characters.Length; i++)
        {
            if (!characters[i].IsAlive)
            {
                return false;
            }
        }

        return stage >= 0 && stage < characters.Length;
    }

    private void TryUseStage(int stage, bool automatic)
    {
        PrototypeCharacter caster = characters[stage];
        PrototypeEnemyTarget target = null;

        if (stage == 0 || stage == 2)
        {
            target = automatic ? FindAutomaticTarget() : squad.GetTargetUnderReticle();
            if (target == null || target.IsDefeated)
            {
                if (!automatic)
                {
                    toastRemaining = 2f;
                }

                return;
            }
        }

        switch (stage)
        {
            case 0:
                FirePrecisionMark(caster, target);
                break;
            case 1:
                foreach (PrototypeCharacter character in characters)
                {
                    if (character.IsAlive)
                    {
                        character.ApplyShield();
                    }
                }

                break;
            case 2:
                break;
        }

        if (stage == 0 && !chainActive)
        {
            chainActive = true;
        }

        nextStage = stage + 1;
        linkRemaining = LinkDuration;
        nextStageAt = Time.time + StageDelay;

        if (nextStage >= characters.Length)
        {
            CompleteChain();
        }

        StageUsed?.Invoke(stage, automatic);
        PrototypeAudioDirector.PlayBurst(stage);
    }

    private void FirePrecisionMark(PrototypeCharacter caster, PrototypeEnemyTarget target)
    {
        bool critical = Random.value < 0.2f;
        target.TakeDamage(180f * caster.OutgoingDamageMultiplier, critical, caster);
        if (!target.IsDefeated)
        {
            target.ApplyMark(8f);
        }
    }

    public bool TryFireFocusedFire()
    {
        if (!focusedFireMode || burstRemaining <= 0f || squad.ActiveSlot != 2)
        {
            return false;
        }

        if (focusedFireCooldown <= 0f)
        {
            PrototypeCharacter rook = characters[2];
            if (FireFocusedFire(rook, null, false))
            {
                focusedFireCooldown = FocusedFireInterval;
                PrototypeAudioDirector.PlayFocusedFire(rook.Muzzle.position);
                FocusedFireShot?.Invoke();
            }
        }

        return true;
    }

    private bool FireFocusedFire(PrototypeCharacter caster, PrototypeEnemyTarget aimedTarget, bool automatic)
    {
        if (automatic && (aimedTarget == null || aimedTarget.IsDefeated))
        {
            return false;
        }

        if (!caster.TryConsumeAmmo(FocusedFireAmmoCost))
        {
            return false;
        }

        caster.PlayWeaponFireAnimation();
        Vector3 aimDirection = automatic
            ? (aimedTarget.GetComponent<Collider>().bounds.center - caster.Muzzle.position).normalized
            : GetManualAimDirection(caster);
        Dictionary<PrototypeEnemyTarget, float> highestDamageByTarget = new Dictionary<PrototypeEnemyTarget, float>();
        PrototypeCombatFeedback.PlayShot(caster.Muzzle.position, caster.Slot);

        for (int pellet = 0; pellet < 7; pellet++)
        {
            int offsetIndex = pellet - 3;
            float angle = offsetIndex * 10f;
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * aimDirection;
            Ray ray = new Ray(caster.Muzzle.position, direction);
            Vector3 endPoint = caster.Muzzle.position + direction * MaxRayDistance;

            if (RaycastIgnoringCharacters(ray, out RaycastHit hit, MaxRayDistance))
            {
                endPoint = hit.point;
                PrototypeEnemyTarget target = hit.collider.GetComponentInParent<PrototypeEnemyTarget>();
                bool isCritical = false;
                if (target != null && !target.IsDefeated)
                {
                    target.RegisterHit(caster);
                    float baseDamage = caster.DamagePerShot * caster.OutgoingDamageMultiplier;
                    isCritical = Random.value < 0.2f;
                    float resolvedDamage = isCritical ? baseDamage * 2f : baseDamage;
                    squad.RegisterHitMarker(caster, isCritical);
                    if (!highestDamageByTarget.ContainsKey(target))
                    {
                        PrototypeCombatFeedback.PlayImpact(hit.point, isCritical);
                    }

                    if (!highestDamageByTarget.TryGetValue(target, out float existingDamage) || resolvedDamage > existingDamage)
                    {
                        highestDamageByTarget[target] = resolvedDamage;
                    }
                }

                PrototypeCombatFeedback.SpawnImpact(hit.point, hit.normal, target != null, isCritical);
            }

            PrototypeTracer.Spawn(caster.Muzzle.position, endPoint);
        }

        foreach (KeyValuePair<PrototypeEnemyTarget, float> hit in highestDamageByTarget)
        {
            hit.Key.ApplyResolvedDamage(hit.Value, false, caster);
        }

        squad.ApplyCameraRecoil(0.35f);
        return true;
    }

    private Vector3 GetManualAimDirection(PrototypeCharacter caster)
    {
        Ray cameraRay = squad.PlayerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 targetPoint = cameraRay.origin + cameraRay.direction * MaxRayDistance;
        if (RaycastIgnoringCharacters(cameraRay, out RaycastHit hit, MaxRayDistance))
        {
            targetPoint = hit.point;
        }

        return (targetPoint - caster.Muzzle.position).normalized;
    }

    private PrototypeEnemyTarget FindAutomaticTarget()
    {
        foreach (PrototypeEnemyTarget candidate in targets)
        {
            if (candidate != null && !candidate.IsDefeated && !candidate.IsTrainingDummy && candidate.IsThreateningPlayer)
            {
                return candidate;
            }
        }

        if (mostRecentlyHitTarget != null && !mostRecentlyHitTarget.IsDefeated && !mostRecentlyHitTarget.IsTrainingDummy)
        {
            return mostRecentlyHitTarget;
        }

        PrototypeEnemyTarget closestEnemy = null;
        float closestDistance = float.MaxValue;
        PrototypeEnemyTarget practiceTarget = null;
        foreach (PrototypeEnemyTarget candidate in targets)
        {
            if (candidate == null || candidate.IsDefeated)
            {
                continue;
            }

            if (candidate.IsTrainingDummy)
            {
                practiceTarget = candidate;
                continue;
            }

            float distance = (candidate.transform.position - squad.ActiveCharacter.transform.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = candidate;
            }
        }

        return closestEnemy != null ? closestEnemy : practiceTarget;
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

    private void OnTargetHit(PrototypeEnemyTarget target, PrototypeCharacter attacker)
    {
        mostRecentlyHitTarget = target;
        if (attacker == null) return;

        float gaugeGain = attacker.Slot == 0
            ? GaugePerSniperHit
            : attacker.Slot == 1 ? GaugePerMachineGunHit : GaugePerAssaultRifleHit;
        AddGauge(gaugeGain);
    }

    private void AddGauge(float amount)
    {
        if (!chainActive && burstRemaining <= 0f && cooldownRemaining <= 0f)
        {
            gauge = Mathf.Min(1f, gauge + amount);
        }
    }

    private void FailChain(bool applyCooldown)
    {
        if (!chainActive && gauge < 1f)
        {
            return;
        }

        gauge = 0f;
        chainActive = false;
        nextStage = 0;
        linkRemaining = 0f;
        if (applyCooldown)
        {
            cooldownRemaining = FailureCooldown;
        }

        ChainFailed?.Invoke();
    }

    private void CompleteChain()
    {
        chainActive = false;
        nextStage = 0;
        linkRemaining = 0f;
        gauge = 0f;
        burstRemaining = BurstDuration;
        focusedFireMode = true;
        focusedFireCooldown = 0f;
        SetBurstModifiers(true);
    }

    private void SetBurstModifiers(bool active)
    {
        foreach (PrototypeCharacter character in characters)
        {
            character.SetBurstState(active);
        }
    }

    private void OnDestroy()
    {
        foreach (PrototypeEnemyTarget target in targets)
        {
            if (target != null)
            {
                target.HitByCharacter -= OnTargetHit;
            }
        }

        if (characters != null)
        {
            SetBurstModifiers(false);
        }
    }
}
