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
    public bool HasScope { get; private set; }
    public bool IsReloading => ReloadRemaining > 0f;
    public CharacterController Controller { get; private set; }
    public Transform Muzzle { get; private set; }

    private float shotCooldown;

    public void Initialize(int slot, float health, float damage, float shotsPerSecond, int magazineSize,
        float reloadSeconds, bool hasScope, Transform muzzle)
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
        Controller = GetComponent<CharacterController>();
    }

    public void TickWeapon(float deltaTime)
    {
        if (ReloadRemaining > 0f)
        {
            ReloadRemaining = Mathf.Max(0f, ReloadRemaining - deltaTime);
            if (ReloadRemaining == 0f)
            {
                Ammo = MagazineSize;
            }
        }

        shotCooldown = Mathf.Max(0f, shotCooldown - deltaTime);
    }

    public bool TryFire(Camera camera)
    {
        if (IsReloading || Ammo <= 0 || shotCooldown > 0f)
        {
            return false;
        }

        Ammo--;
        shotCooldown = 1f / ShotsPerSecond;
        FireHitscan(camera);
        return true;
    }

    public void BeginReload()
    {
        if (!IsReloading && Ammo < MagazineSize)
        {
            ReloadRemaining = ReloadSeconds;
        }
    }

    private void FireHitscan(Camera camera)
    {
        Ray cameraRay = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 targetPoint = cameraRay.origin + cameraRay.direction * 100f;

        if (RaycastIgnoringCharacters(cameraRay, out RaycastHit aimHit, 100f))
        {
            targetPoint = aimHit.point;
        }

        Vector3 direction = (targetPoint - Muzzle.position).normalized;
        Vector3 visualEndPoint = targetPoint;
        if (RaycastIgnoringCharacters(new Ray(Muzzle.position, direction), out RaycastHit hit, 100f))
        {
            visualEndPoint = hit.point;
            PrototypeEnemyTarget target = hit.collider.GetComponentInParent<PrototypeEnemyTarget>();
            if (target != null)
            {
                bool isCritical = Random.value < BaseCriticalChance;
                target.TakeDamage(DamagePerShot, isCritical);
            }
        }

        PrototypeTracer.Spawn(Muzzle.position, visualEndPoint);
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
