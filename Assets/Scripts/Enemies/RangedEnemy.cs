using UnityEngine;

/*
 * Script: RangedEnemy
 *
 * Description:
 * Shooting enemy. Spawns a random gun from its list on start, aims and fires
 * at the player, and throws the weapon on death so the player can pick it up.
 * Per the GDD this is the only source of new weapons.
 *
 * Interacts With:
 * - EnemyBase (sight, roaming, death path)
 * - GunStats (the spawned weapon's stats and fire behaviour)
 * - PatrolPath (optional fixed route instead of free roaming)
 * - KillstreakManager (ghost protocol skews the aim)
 *
 * Notes:
 * - attackRate is overwritten from the spawned gun in Start, which is why it's
 *   a field on EnemyBase rather than read straight from EnemyConfig.
 */
public class RangedEnemy : EnemyBase
{
    [Header("Weapon")]
    [Tooltip("empty transform the gun parents to, rotated to aim at the player")]
    [SerializeField] Transform gunPivot;

    [Tooltip("how fast the gun swings onto target, higher snaps harder")]
    [Range(1, 30)][SerializeField] int gunRotateSpeed;

    [Tooltip("the model the gun mounts on, thrown on death")]
    public GameObject gunModel;

    [Tooltip("one is picked at random on spawn, this is where player weapons come from")]
    public WeaponStats[] gunPrefabs;

    [Tooltip("optional fixed patrol route, leave empty to use free roaming instead")]
    [SerializeField] PatrolPath patrol;

    [Tooltip("where bullets spawn from, found by name on the gun model")]
    public Transform gunBarrel;

    // the rolled gun's stats, read for fire rate and projectile
    GunStats activeGun;

    // the spawned model, thrown on death
    private GameObject spawnedWeaponModel;

    // rounds left, the enemy stops firing at zero
    int currentAmmo;

    protected override void Start()
    {
        base.Start();
        SetWeaponPrefab();
        currentAmmo = activeGun.startingBullets * 3;
        if (TryGetComponent<PatrolPath>(out patrol))
            agent.destination = patrol.CurrentWaypointPosition;
    }

    protected override void attack()
    {
        agent.stoppingDistance = stoppingDistOrig;

        if (gunPivot != null)
            rotateGun();
        if (attackTimer > attackRate && currentAmmo >= 0)
            shoot();
    }

    // fires one round at the player, skewed if ghost protocol is running
    void shoot()
    {
        attackTimer = 0f;

        if (AudioManager.instance != null)
            AudioManager.instance.PlaySpatialSFX(
                AudioManager.instance.PickRandomAudio(AudioManager.instance.enemyShoot),
                gunBarrel.position,
                AudioManager.instance.enemyShootVol);

        if (activeGun == null || activeGun.bullet == null || gunPivot == null || gunBarrel == null)
            return;

        bool isShotgun = activeGun.gunType == GunStats.GunType.Shotgun;

        int shotsToFire = isShotgun ? Mathf.Max(1, activeGun.pelletCount) : 1;
        float spread = isShotgun ? activeGun.spreadAngle : 0f;

        for (int i = 0; i < shotsToFire; i++)
        {
            float spreadX = Random.Range(-spread, spread);
            float spreadY = Random.Range(-spread, spread);

            Quaternion shotRotation = gunPivot.rotation * Quaternion.Euler(spreadX, spreadY, 0f);

            Transform bulletToFire = activeGun.enemyBullet != null ? activeGun.enemyBullet : activeGun.bullet;
            Instantiate(bulletToFire, gunBarrel.position, shotRotation);
        }
    }

    // swings the gun pivot onto the player, so shots look aimed rather than snapped
    void rotateGun()
    {
        Quaternion rot = Quaternion.LookRotation(playerDir);
        gunPivot.rotation = Quaternion.Lerp(gunPivot.rotation, rot, gunRotateSpeed * Time.deltaTime);
    }

    // rolls a gun from the list and spawns its model into the hand
    public void SetWeaponPrefab()
    {
        WeaponStats selectedGun = gunPrefabs[Random.Range(0, gunPrefabs.Length)];
        spawnedWeaponModel = Instantiate(selectedGun.weaponModel, gunModel.transform, false);

        spawnedWeaponModel.transform.localPosition = Vector3.zero;
        spawnedWeaponModel.transform.localRotation = Quaternion.identity;
        if (spawnedWeaponModel.TryGetComponent<WeaponWallAvoidance>(out var weaponClip))
            weaponClip.enabled = true;
        if (spawnedWeaponModel.TryGetComponent<PickWeapon>(out var picker))
            picker.enabled = false;

        string targetName = (selectedGun is GunStats) ? "Muzzle" : "HitPoint";
        gunBarrel = WeaponManager.instance.FindDeepChild(spawnedWeaponModel.transform, targetName);
        activeGun = (GunStats)selectedGun;

        // each weapon sets its own pacing
        if (activeGun.attackRate > 0f)
            attackRate = activeGun.attackRate;
    }
    public override void Die()
    {
        ThrowWeapon(spawnedWeaponModel, gunModel.transform);
        base.Die();
    }
}
