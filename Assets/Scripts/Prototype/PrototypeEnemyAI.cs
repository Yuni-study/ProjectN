using System.Collections.Generic;
using UnityEngine;

public enum PrototypeEnemyArchetype
{
    Raker,
    Glimmer,
    Dreadbore
}

public sealed class PrototypeEnemyAI : MonoBehaviour
{
    private enum ActionKind
    {
        None,
        RakerCharge,
        RakerSlam,
        GlimmerBurst,
        BossCannon,
        BossMelee,
        BossShockwave,
        BossCharge
    }

    private enum ActionPhase
    {
        Ready,
        Warning,
        Firing,
        Exposed,
        Dashing
    }

    private const float RakerStartRange = 8f;
    private const float RakerMeleeRange = 2.5f;
    private const float RakerChargeSpeed = 5.5f;
    private const float RakerChargeDistance = 8f;
    private const float GlimmerStartRange = 20f;
    private const float BossStartRange = 24f;
    private const float BossMeleeRange = 3.5f;
    private const float BossShockwaveRadius = 4.5f;
    private const int TelegraphSegments = 32;
    private const float TelegraphGroundHeight = 0.08f;

    private static Material telegraphMaterial;
    private static Material telegraphFillMaterial;

    private PrototypeEnemyTarget enemy;
    private PrototypeSquadController squad;
    private PrototypeEnemyArchetype archetype;
    private PrototypeCharacter target;
    private PrototypeCharacter actionTarget;
    private LineRenderer telegraph;
    private GameObject telegraphFillObject;
    private Mesh telegraphFillMesh;
    private MeshFilter telegraphFillFilter;
    private MeshRenderer telegraphFillRenderer;
    private ActionKind actionKind;
    private ActionPhase phase;
    private Vector3 lockedPoint;
    private Vector3 dashDirection;
    private float actionTimer;
    private float dashDistance;
    private float burstShotTimer;
    private int burstShotsRemaining;
    private bool engaged;
    private bool rakerChargeUsed;
    private bool shockwaveUsed;
    private bool bossChargeUsed;
    private GameObject exposedCore;
    private readonly HashSet<PrototypeCharacter> charactersHitThisAction = new HashSet<PrototypeCharacter>();
    private readonly HashSet<PrototypeCover> coversHitThisAction = new HashSet<PrototypeCover>();

    public bool IsShowingAttackWarning => phase == ActionPhase.Warning;

    public void Initialize(PrototypeEnemyArchetype enemyArchetype, PrototypeSquadController squadController)
    {
        archetype = enemyArchetype;
        squad = squadController;
        enemy = GetComponent<PrototypeEnemyTarget>();
        enemy.Hit += OnEnemyHit;
        Color bodyColor = enemyArchetype == PrototypeEnemyArchetype.Raker
            ? new Color(0.62f, 0.2f, 0.16f)
            : enemyArchetype == PrototypeEnemyArchetype.Glimmer
                ? new Color(0.7f, 0.5f, 0.2f)
                : new Color(0.34f, 0.24f, 0.42f);
        enemy.SetDisplayColor(bodyColor);

        if (enemyArchetype == PrototypeEnemyArchetype.Dreadbore)
        {
            exposedCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            exposedCore.name = "Exposed Core Indicator";
            exposedCore.transform.SetParent(transform, false);
            exposedCore.transform.localPosition = new Vector3(0f, 1.25f, 0.52f);
            exposedCore.transform.localScale = Vector3.one * 0.45f;
            Destroy(exposedCore.GetComponent<Collider>());
            exposedCore.GetComponent<Renderer>().material.color = new Color(0.25f, 0.9f, 1f);
            exposedCore.SetActive(false);
        }

        telegraph = gameObject.AddComponent<LineRenderer>();
        telegraph.useWorldSpace = true;
        telegraph.widthMultiplier = 0.055f;
        telegraph.positionCount = 0;
        telegraph.enabled = false;
        if (telegraphMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            telegraphMaterial = new Material(shader);
        }

        telegraph.sharedMaterial = telegraphMaterial;
        telegraph.startColor = new Color(1f, 0.2f, 0.05f, 0.9f);
        telegraph.endColor = new Color(1f, 0.65f, 0.08f, 0.6f);
        telegraph.numCapVertices = 2;
        telegraph.numCornerVertices = 2;

        telegraphFillObject = new GameObject("Attack Warning Sector");
        telegraphFillObject.transform.SetParent(transform, false);
        telegraphFillFilter = telegraphFillObject.AddComponent<MeshFilter>();
        telegraphFillRenderer = telegraphFillObject.AddComponent<MeshRenderer>();
        telegraphFillMesh = new Mesh { name = "Attack Warning Sector Mesh" };
        telegraphFillFilter.sharedMesh = telegraphFillMesh;
        if (telegraphFillMaterial == null)
        {
            Shader fillShader = Shader.Find("Sprites/Default");
            telegraphFillMaterial = new Material(fillShader)
            {
                name = "Attack Warning Sector Material",
                color = new Color(1f, 0.12f, 0.04f, 0.2f),
                renderQueue = 3000
            };
        }

        telegraphFillRenderer.sharedMaterial = telegraphFillMaterial;
        telegraphFillRenderer.enabled = false;
    }

    private void Update()
    {
        if (enemy == null || enemy.IsDefeated || squad == null)
        {
            HideTelegraph();
            exposedCore?.SetActive(false);
            return;
        }

        UpdateTarget();
        if (target == null)
        {
            enemy.SetThreateningCharacter(null, false, false);
            return;
        }

        enemy.SetThreateningCharacter(target, true, target == squad.ActiveCharacter);
        switch (archetype)
        {
            case PrototypeEnemyArchetype.Raker:
                UpdateRaker();
                break;
            case PrototypeEnemyArchetype.Glimmer:
                UpdateGlimmer();
                break;
            case PrototypeEnemyArchetype.Dreadbore:
                UpdateDreadbore();
                break;
        }
    }

    private void UpdateTarget()
    {
        PrototypeCharacter highestDamageDealer = enemy.GetTopDamageDealer();
        if (highestDamageDealer != null)
        {
            engaged = true;
            target = highestDamageDealer;
        }

        if (target != null && !target.IsAlive)
        {
            target = null;
        }

        if (target == null)
        {
            target = FindClosestLivingCharacter(engaged ? float.MaxValue : GetStartRange());
            if (target != null)
            {
                engaged = true;
            }
        }
    }

    private void OnEnemyHit(PrototypeEnemyTarget hitEnemy)
    {
        if (hitEnemy != enemy || enemy.IsDefeated)
        {
            return;
        }

        engaged = true;
        if (target == null || !target.IsAlive)
        {
            target = FindClosestLivingCharacter(float.MaxValue);
        }
    }

    private float GetStartRange()
    {
        switch (archetype)
        {
            case PrototypeEnemyArchetype.Raker: return RakerStartRange;
            case PrototypeEnemyArchetype.Glimmer: return GlimmerStartRange;
            default: return BossStartRange;
        }
    }

    private PrototypeCharacter FindClosestLivingCharacter(float maxDistance)
    {
        PrototypeCharacter closest = null;
        float closestDistance = maxDistance * maxDistance;
        foreach (PrototypeCharacter candidate in squad.Characters)
        {
            if (candidate == null || !candidate.IsAlive)
            {
                continue;
            }

            float distance = FlatDistanceSquared(transform.position, candidate.transform.position);
            if (distance <= closestDistance)
            {
                closestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    private void UpdateRaker()
    {
        if (phase == ActionPhase.Warning)
        {
            actionTimer -= Time.deltaTime;
            if (actionKind == ActionKind.RakerCharge)
            {
                SetTelegraphRectangle(dashDirection, Mathf.Min(RakerChargeDistance, FlatDistance(transform.position, lockedPoint)), 0.65f);
            }
            else if (actionTarget != null)
            {
                SetTelegraphSector(actionTarget.transform.position - transform.position, RakerMeleeRange, 28f);
            }
            else
            {
                SetTelegraphSegment(transform.position + Vector3.up, actionTarget != null ? actionTarget.transform.position + Vector3.up : lockedPoint);
            }

            if (actionTimer <= 0f)
            {
                if (actionKind == ActionKind.RakerCharge)
                {
                    BeginDash(lockedPoint, RakerChargeDistance);
                }
                else
                {
                    if (actionTarget != null && actionTarget.IsAlive
                        && FlatDistanceSquared(transform.position, actionTarget.transform.position) <= RakerMeleeRange * RakerMeleeRange)
                    {
                        actionTarget.TakeDamage(18f);
                    }

                    SetCooldown(1.5f);
                }
            }

            return;
        }

        if (phase == ActionPhase.Dashing)
        {
            TickDash(RakerChargeSpeed);
            return;
        }

        if (phase == ActionPhase.Exposed)
        {
            actionTimer -= Time.deltaTime;
            if (actionTimer <= 0f)
            {
                phase = ActionPhase.Ready;
            }

            return;
        }

        if (actionTimer > 0f)
        {
            actionTimer -= Time.deltaTime;
            return;
        }

        float distance = FlatDistance(transform.position, target.transform.position);
        if (!rakerChargeUsed)
        {
            actionTarget = target;
            lockedPoint = target.transform.position;
            dashDirection = lockedPoint - transform.position;
            dashDirection.y = 0f;
            dashDirection.Normalize();
            rakerChargeUsed = true;
            BeginWarning(ActionKind.RakerCharge, 1f);
            return;
        }

        if (distance <= RakerMeleeRange)
        {
            actionTarget = target;
            BeginWarning(ActionKind.RakerSlam, 0.8f);
        }
        else
        {
            MoveEnemyToward(target.transform.position, 3.4f, Time.deltaTime);
        }
    }

    private void UpdateGlimmer()
    {
        if (phase == ActionPhase.Exposed)
        {
            actionTimer -= Time.deltaTime;
            if (actionTimer <= 0f)
            {
                phase = ActionPhase.Ready;
            }

            return;
        }

        if (phase == ActionPhase.Warning)
        {
            if (!IsGlimmerTargetValid(actionTarget))
            {
                phase = ActionPhase.Ready;
                HideTelegraph();
                return;
            }

            actionTimer -= Time.deltaTime;
            SetTelegraphRectangle(actionTarget.transform.position - transform.position,
                Mathf.Min(GlimmerStartRange, FlatDistance(transform.position, actionTarget.transform.position)), 0.7f);
            if (actionTimer <= 0f)
            {
                phase = ActionPhase.Firing;
                burstShotsRemaining = 3;
                burstShotTimer = 0f;
            }

            return;
        }

        if (phase == ActionPhase.Firing)
        {
            TickGlimmerVolley();
            return;
        }

        if (IsGlimmerTargetValid(target))
        {
            actionTarget = target;
            BeginWarning(ActionKind.GlimmerBurst, 0.8f);
        }
    }

    private bool IsGlimmerTargetValid(PrototypeCharacter candidate)
    {
        if (candidate == null || !candidate.IsAlive)
        {
            return false;
        }

        float distance = FlatDistance(transform.position, candidate.transform.position);
        return distance >= 8f && distance <= GlimmerStartRange && HasLineOfSight(candidate);
    }

    private void UpdateDreadbore()
    {
        if (phase == ActionPhase.Warning)
        {
            TickDreadboreWarning();
            return;
        }

        if (phase == ActionPhase.Firing)
        {
            TickBossVolley();
            return;
        }

        if (phase == ActionPhase.Dashing)
        {
            TickDash(6f);
            return;
        }

        if (phase == ActionPhase.Exposed)
        {
            actionTimer -= Time.deltaTime;
            if (actionTimer <= 0f)
            {
                exposedCore?.SetActive(false);
                actionTimer = actionKind == ActionKind.BossShockwave ? 5f : 4f;
                phase = ActionPhase.Ready;
            }

            return;
        }

        if (actionTimer > 0f)
        {
            actionTimer -= Time.deltaTime;
            return;
        }

        float healthFraction = (float)(enemy.Health / enemy.MaxHealth);
        if (!shockwaveUsed && healthFraction <= 0.5f)
        {
            shockwaveUsed = true;
            coversHitThisAction.Clear();
            BeginWarning(ActionKind.BossShockwave, 1f);
            return;
        }

        if (shockwaveUsed && !bossChargeUsed && healthFraction <= 0.25f)
        {
            PrototypeCharacter highestDamageDealer = enemy.GetTopDamageDealer();
            if (highestDamageDealer != null)
            {
                target = highestDamageDealer;
            }

            if (target == null)
            {
                return;
            }

            actionTarget = target;
            lockedPoint = target.transform.position;
            dashDirection = lockedPoint - transform.position;
            dashDirection.y = 0f;
            dashDirection.Normalize();
            bossChargeUsed = true;
            coversHitThisAction.Clear();
            BeginWarning(ActionKind.BossCharge, 1.5f);
            return;
        }

        float distance = FlatDistance(transform.position, target.transform.position);
        if (distance > BossStartRange)
        {
            MoveEnemyToward(target.transform.position, 1.5f, Time.deltaTime);
        }
        else if (distance <= BossMeleeRange)
        {
            actionTarget = target;
            BeginWarning(ActionKind.BossMelee, 0.8f);
        }
        else if (distance < 8f)
        {
            MoveEnemyToward(target.transform.position, 1.5f, Time.deltaTime);
        }
        else
        {
            actionTarget = target;
            burstShotsRemaining = 3;
            burstShotTimer = 0f;
            BeginWarning(ActionKind.BossCannon, 0.8f);
        }
    }

    private void TickDreadboreWarning()
    {
        actionTimer -= Time.deltaTime;
        switch (actionKind)
        {
            case ActionKind.BossShockwave:
                SetTelegraphCircle(new Vector3(transform.position.x, 0f, transform.position.z), BossShockwaveRadius);
                break;
            case ActionKind.BossCharge:
                SetTelegraphRectangle(dashDirection, Mathf.Min(10f, FlatDistance(transform.position, lockedPoint)), 0.8f);
                break;
            case ActionKind.BossMelee:
                SetTelegraphSector(actionTarget != null ? actionTarget.transform.position - transform.position : dashDirection,
                    BossMeleeRange, 28f);
                break;
            default:
                SetTelegraphRectangle(actionTarget != null ? actionTarget.transform.position - transform.position : dashDirection,
                    actionTarget != null ? Mathf.Min(BossStartRange, FlatDistance(transform.position, actionTarget.transform.position)) : BossStartRange,
                    0.9f);
                break;
        }

        if (actionTimer > 0f)
        {
            return;
        }

        HideTelegraph();
        if (actionKind == ActionKind.BossShockwave)
        {
            ResolveShockwave();
            phase = ActionPhase.Exposed;
            actionTimer = 2.5f;
            exposedCore?.SetActive(true);
        }
        else if (actionKind == ActionKind.BossCharge)
        {
            BeginDash(lockedPoint, 10f);
        }
        else if (actionKind == ActionKind.BossCannon)
        {
            phase = ActionPhase.Firing;
            burstShotTimer = 0f;
        }
        else
        {
            if (actionTarget != null && actionTarget.IsAlive
                && FlatDistanceSquared(transform.position, actionTarget.transform.position) <= BossMeleeRange * BossMeleeRange)
            {
                ResolveBossHit(actionTarget, 20f);
            }

            SetCooldown(2f);
        }
    }

    private void TickBossVolley()
    {
        burstShotTimer -= Time.deltaTime;
        if (burstShotTimer > 0f)
        {
            return;
        }

        if (actionTarget != null && actionTarget.IsAlive)
        {
            coversHitThisAction.Clear();
            FireShot(actionTarget, 8f, true);
        }

        burstShotsRemaining--;
        if (burstShotsRemaining > 0)
        {
            burstShotTimer = 0.2f;
        }
        else
        {
            SetCooldown(3f);
        }
    }

    private void TickGlimmerVolley()
    {
        burstShotTimer -= Time.deltaTime;
        if (burstShotTimer > 0f)
        {
            return;
        }

        if (IsGlimmerTargetValid(actionTarget))
        {
            FireShot(actionTarget, 6f, false);
        }

        burstShotsRemaining--;
        if (burstShotsRemaining > 0)
        {
            burstShotTimer = 0.15f;
        }
        else
        {
            phase = ActionPhase.Exposed;
            actionTimer = 5f;
            HideTelegraph();
        }
    }

    private void ResolveShockwave()
    {
        coversHitThisAction.Clear();
        foreach (PrototypeCharacter character in squad.Characters)
        {
            if (character == null || !character.IsAlive
                || FlatDistanceSquared(transform.position, character.transform.position) > BossShockwaveRadius * BossShockwaveRadius)
            {
                continue;
            }

            ResolveBossHit(character, 15f);
        }

        coversHitThisAction.Clear();
    }

    private void ResolveBossHit(PrototypeCharacter character, float damage)
    {
        if (character == null || !character.IsAlive)
        {
            return;
        }

        PrototypeAudioDirector.PlayEnemyAttack(true, transform.position);

        PrototypeCover cover = squad.GetCoverForCharacter(character);
        if (cover != null)
        {
            if (coversHitThisAction.Add(cover))
            {
                cover.TakeBossHit();
            }

            return;
        }

        character.TakeDamage(damage);
    }

    private void FireShot(PrototypeCharacter victim, float damage, bool isBossAttack)
    {
        if (victim == null || !victim.IsAlive)
        {
            return;
        }

        Vector3 origin = transform.position + Vector3.up * 1.1f;
        Vector3 aimPoint = victim.transform.position + Vector3.up;
        Vector3 direction = (aimPoint - origin).normalized;
        float distance = Vector3.Distance(origin, aimPoint);
        Vector3 endPoint = aimPoint;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance + 0.2f);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<PrototypeEnemyTarget>() == enemy)
            {
                continue;
            }

            endPoint = hit.point;
            PrototypeCharacter character = hit.collider.GetComponentInParent<PrototypeCharacter>();
            if (character != null)
            {
                if (isBossAttack)
                {
                    ResolveBossHit(character, damage);
                }
                else
                {
                    PrototypeAudioDirector.PlayEnemyAttack(false, transform.position);
                    character.TakeDamage(damage);
                }

                break;
            }

            PrototypeCover cover = hit.collider.GetComponentInParent<PrototypeCover>();
            if (isBossAttack && cover != null && cover.IsIntact)
            {
                cover.TakeBossHit();
            }

            break;
        }

        PrototypeTracer.Spawn(origin, endPoint);
    }

    private void BeginWarning(ActionKind kind, float duration)
    {
        actionKind = kind;
        actionTimer = duration;
        phase = ActionPhase.Warning;
        lockedPoint = actionTarget != null ? actionTarget.transform.position : transform.position + transform.forward * 2f;
        dashDirection = lockedPoint - transform.position;
        dashDirection.y = 0f;
        if (dashDirection.sqrMagnitude > 0.001f)
        {
            dashDirection.Normalize();
        }
        coversHitThisAction.Clear();
    }

    private void BeginDash(Vector3 destination, float maxDistance)
    {
        lockedPoint = destination;
        dashDirection = lockedPoint - transform.position;
        dashDirection.y = 0f;
        if (dashDirection.sqrMagnitude <= 0.001f)
        {
            SetCooldown(1f);
            return;
        }

        dashDirection.Normalize();
        dashDistance = Mathf.Min(maxDistance, FlatDistance(transform.position, lockedPoint));
        phase = ActionPhase.Dashing;
        charactersHitThisAction.Clear();
    }

    private void TickDash(float speed)
    {
        float travel = Mathf.Min(speed * Time.deltaTime, dashDistance);
        Vector3 start = transform.position;
        Vector3 end = start + dashDirection * travel;
        Collider[] hits = Physics.OverlapCapsule(start + Vector3.up * 0.75f, end + Vector3.up * 1.5f, 0.45f);
        bool blocked = false;
        foreach (Collider hit in hits)
        {
            PrototypeCharacter character = hit.GetComponentInParent<PrototypeCharacter>();
            if (character != null)
            {
                if (charactersHitThisAction.Add(character))
                {
                    float damage = archetype == PrototypeEnemyArchetype.Raker ? 18f : 20f;
                    if (archetype == PrototypeEnemyArchetype.Dreadbore)
                    {
                        ResolveBossHit(character, damage);
                    }
                    else
                    {
                        PrototypeAudioDirector.PlayEnemyAttack(false, transform.position);
                        character.TakeDamage(damage);
                    }

                    character.ApplyKnockback(dashDirection, 0.5f);
                }

                continue;
            }

            if (hit.GetComponentInParent<PrototypeEnemyTarget>() != enemy && !hit.isTrigger)
            {
                PrototypeCover cover = hit.GetComponentInParent<PrototypeCover>();
                if (archetype == PrototypeEnemyArchetype.Dreadbore && cover != null && coversHitThisAction.Add(cover))
                {
                    cover.TakeBossHit();
                }

                blocked = true;
            }
        }

        if (blocked)
        {
            FinishDash();
            return;
        }

        transform.position = end;
        dashDistance -= travel;
        if (dashDistance <= 0f)
        {
            FinishDash();
        }
    }

    private void FinishDash()
    {
        HideTelegraph();
        if (archetype == PrototypeEnemyArchetype.Dreadbore)
        {
            phase = ActionPhase.Exposed;
            actionTimer = 2.5f;
            exposedCore?.SetActive(true);
        }
        else
        {
            SetCooldown(1.2f);
        }
    }

    private void MoveEnemyToward(Vector3 position, float speed, float deltaTime)
    {
        Vector3 direction = position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f)
        {
            return;
        }

        direction.Normalize();
        Vector3 origin = transform.position + Vector3.up * 0.8f;
        float step = speed * deltaTime;
        RaycastHit[] hits = Physics.SphereCastAll(origin, 0.45f, direction, step + 0.1f);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<PrototypeEnemyTarget>() == enemy)
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<PrototypeCharacter>() != null)
            {
                continue;
            }

            if (!hit.collider.isTrigger)
            {
                return;
            }
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), deltaTime * 5f);
        transform.position += direction * step;
    }

    private bool HasLineOfSight(PrototypeCharacter character)
    {
        Vector3 origin = transform.position + Vector3.up * 1.1f;
        Vector3 targetPoint = character.transform.position + Vector3.up;
        Vector3 direction = targetPoint - origin;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, direction.magnitude + 0.1f);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<PrototypeEnemyTarget>() == enemy)
            {
                continue;
            }

            return hit.collider.GetComponentInParent<PrototypeCharacter>() == character;
        }

        return false;
    }

    private void SetTelegraphSegment(Vector3 start, Vector3 end)
    {
        telegraphFillRenderer.enabled = false;
        telegraph.loop = false;
        telegraph.positionCount = 2;
        telegraph.SetPosition(0, start);
        telegraph.SetPosition(1, end);
        telegraph.enabled = true;
    }

    private void SetTelegraphCircle(Vector3 center, float radius)
    {
        telegraphFillRenderer.enabled = false;
        telegraph.loop = true;
        telegraph.positionCount = TelegraphSegments;
        for (int i = 0; i < TelegraphSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / TelegraphSegments;
            telegraph.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0.05f, Mathf.Sin(angle) * radius));
        }

        telegraph.enabled = true;
    }

    private void SetTelegraphSector(Vector3 direction, float radius, float halfAngleDegrees)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f) direction = transform.forward;
        direction.Normalize();

        Vector3 center = new Vector3(transform.position.x, TelegraphGroundHeight, transform.position.z);
        telegraphFillObject.transform.SetPositionAndRotation(center, Quaternion.LookRotation(direction, Vector3.up));

        int arcSegments = 20;
        float halfAngle = Mathf.Clamp(halfAngleDegrees, 1f, 80f) * Mathf.Deg2Rad;
        Vector3[] vertices = new Vector3[arcSegments + 2];
        vertices[0] = Vector3.zero;
        for (int i = 0; i <= arcSegments; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)arcSegments);
            vertices[i + 1] = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
        }

        int[] triangles = new int[arcSegments * 3];
        for (int i = 0; i < arcSegments; i++)
        {
            int triangle = i * 3;
            triangles[triangle] = 0;
            triangles[triangle + 1] = i + 1;
            triangles[triangle + 2] = i + 2;
        }

        telegraphFillMesh.Clear();
        telegraphFillMesh.vertices = vertices;
        telegraphFillMesh.triangles = triangles;
        telegraphFillMesh.RecalculateNormals();
        telegraphFillMesh.RecalculateBounds();
        telegraphFillRenderer.enabled = true;

        telegraph.loop = true;
        telegraph.positionCount = arcSegments + 3;
        telegraph.SetPosition(0, center);
        for (int i = 0; i <= arcSegments; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)arcSegments);
            Vector3 localPoint = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
            telegraph.SetPosition(i + 1, center + telegraphFillObject.transform.rotation * localPoint);
        }

        telegraph.SetPosition(arcSegments + 2, center);
        telegraph.enabled = true;
    }

    private void SetTelegraphRectangle(Vector3 direction, float length, float halfWidth)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f) direction = transform.forward;
        direction.Normalize();

        Vector3 center = new Vector3(transform.position.x, TelegraphGroundHeight, transform.position.z);
        telegraphFillObject.transform.SetPositionAndRotation(center, Quaternion.LookRotation(direction, Vector3.up));
        float safeLength = Mathf.Max(0.1f, length);
        Vector3[] vertices =
        {
            new Vector3(-halfWidth, 0f, 0f),
            new Vector3(-halfWidth, 0f, safeLength),
            new Vector3(halfWidth, 0f, safeLength),
            new Vector3(halfWidth, 0f, 0f)
        };
        telegraphFillMesh.Clear();
        telegraphFillMesh.vertices = vertices;
        telegraphFillMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        telegraphFillMesh.RecalculateNormals();
        telegraphFillMesh.RecalculateBounds();
        telegraphFillRenderer.enabled = true;

        telegraph.loop = true;
        telegraph.positionCount = 4;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 localPoint = vertices[i] + Vector3.up * 0.035f;
            telegraph.SetPosition(i, center + telegraphFillObject.transform.rotation * localPoint);
        }

        telegraph.enabled = true;
    }

    private void HideTelegraph()
    {
        if (telegraph != null)
        {
            telegraph.enabled = false;
            telegraph.loop = false;
            telegraph.positionCount = 0;
        }

        if (telegraphFillRenderer != null) telegraphFillRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (telegraphFillObject != null) Destroy(telegraphFillObject);
        if (telegraphFillMesh != null) Destroy(telegraphFillMesh);
    }

    private void SetCooldown(float duration)
    {
        HideTelegraph();
        phase = ActionPhase.Ready;
        actionKind = ActionKind.None;
        actionTimer = duration;
    }

    private static float FlatDistance(Vector3 left, Vector3 right)
    {
        left.y = 0f;
        right.y = 0f;
        return Vector3.Distance(left, right);
    }

    private static float FlatDistanceSquared(Vector3 left, Vector3 right)
    {
        left.y = 0f;
        right.y = 0f;
        return (left - right).sqrMagnitude;
    }

    private void OnDisable()
    {
        HideTelegraph();
        if (enemy != null)
        {
            enemy.Hit -= OnEnemyHit;
            enemy.SetThreateningCharacter(null, false, false);
        }
    }
}
