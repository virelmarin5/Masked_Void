using System.Collections;
using UnityEngine;

/*
 * Script: WeaponManager
 *
 * Description:
 * Owns the player's currently equipped weapon: spawning the model, firing,
 * throwing, and swapping. Per the GDD the player carries one weapon with no
 * reload, and swaps by throwing the current one.
 *
 * Responsibilities:
 * - Equip a weapon from WeaponStats, spawn its model at the hold point
 * - Track ammo, fire on input, handle melee and ranged attack paths
 * - Throw the held weapon and re-enable its pickup and damage components
 * - Restore the saved weapon from PlayerPrefs on load
 *
 * Interacts With:
 * - GameManager (waits on PlayerReady for the weapon hold point)
 * - WeaponStats, GunStats, MeleeStats (weapon data assets)
 * - WeaponWallAvoidance (enabled while held, disabled once thrown)
 * - PickWeapon, DroppedWeapon, Damage (toggled on throw)
 * - HeartbeatManager (firing adds stress)
 * - UpgradeManager (fire rate upgrade)
 *
 * Notes:
 * - setupWeapons runs off GameManager.PlayerReady, not Start, because the
 *   player may not exist yet when this manager's Start runs.
 */

public class WeaponManager : MonoBehaviour
{
    public static WeaponManager instance { get; private set; }
    [Header("Weapon")]
    [Tooltip("what the player is holding right now, set at runtime")]
    public WeaponStats activeWeapon;

    [Tooltip("what the player spawns with, the gdd says a 3 round pistol")]
    public WeaponStats starterWeapon;

    [Tooltip("every weapon in the game, used to restore the saved one on load")]
    [SerializeField] private WeaponStats[] allWeapons;

    [Tooltip("icon shown in the hud when the player is unarmed")]
    public Sprite emptySlot;

    [Header("Dropped Weapons")]
    [Tooltip("layers a thrown weapon counts as landing on, so it doesn't stick to enemies")]
    [SerializeField] LayerMask groundLayers = ~0;

    [Header("Melee")]
    [Tooltip("seconds for the swing out")]
    [SerializeField] float meleeSwingDuration = .08f;

    [Tooltip("seconds to return to the resting pose")]
    [SerializeField] float meleeReturnDuration = .12f;

    [Tooltip("euler rotation offset at the peak of the swing")]
    [SerializeField] Vector3 meleeSwingRotation = new Vector3(35f, -70f, 20f);

    [Tooltip("position offset at the peak of the swing")]
    [SerializeField] Vector3 meleeSwingPosition = new Vector3(.08f, -.05f, .15f);

    [Header("Aim Rotation")]
    [Tooltip("layers the aim raycast can hit")]
    [SerializeField] private LayerMask aimTargetLayers = ~0;

    // runtime state
    bool isEquipping;
    bool isMeleeSwinging;
    GameObject spawnedWeaponModel;
    Transform gunBarrel;
    Transform weaponHolder;
    float attackTimer;
    int currentAmmo;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    void Start()
    {
        if (GameManager.instance == null || GameManager.instance.playerScript == null)
            return;

        // player might already be found by now, if so just go
        if (GameManager.instance != null && GameManager.instance.playerScript != null)
            setupWeapons();

        weaponHolder = GameManager.instance.playerScript.weaponHoldPos.transform;

        if (GameManager.instance != null && GameManager.instance.ammoPanel == gameObject)
        {
            Debug.LogError("WeaponManager: ammoPanel is wired to the weapon manager, fix reference on GameManager", this);
            GameManager.instance.ammoPanel = null;
        }

        // Load saved weapon
        string savedWeaponName = PlayerPrefs.GetString("EquippedWeapon", "");
        if (!string.IsNullOrEmpty(savedWeaponName) && allWeapons != null)
        {
            WeaponStats loadedWeapon = System.Array.Find(allWeapons, w => w != null && w.Name == savedWeaponName);
            if (loadedWeapon != null)
            {
                activeWeapon = loadedWeapon;
            }
        }
        else
        {
            if (starterWeapon != null)
                activeWeapon = starterWeapon;
        }

        if (activeWeapon != null)
            spawnWeapon(activeWeapon);
    }

    void Update()
    {

        attackTimer += Time.unscaledDeltaTime;
    }

    void OnDestroy()
    {
        if (activeWeapon != null)
            activeWeapon.isFromGround = false;
        if (instance == this)
            instance = null;
    }

    void OnEnable() => GameManager.PlayerReady += setupWeapons;

    void OnDisable() => GameManager.PlayerReady -= setupWeapons;

    void setupWeapons()
    {
        weaponHolder = GameManager.instance.playerScript.weaponHoldPos.transform;
        EquipWeapon(starterWeapon);
    }

    public void EquipWeapon(WeaponStats newWeapon, int ammoOverride = -1)
    {
        StartCoroutine(equip(newWeapon, ammoOverride));
    }

    IEnumerator equip(WeaponStats newWeapon, int ammoOverride)
    {
        if (newWeapon == null || isEquipping)
            yield break;

        isEquipping = true;
        if (spawnedWeaponModel != null)
            ThrowWeapon();
        yield return new WaitForSecondsRealtime(1f);
        if (AudioManager.instance != null)
            AudioManager.instance.PlayEquip();
        spawnWeapon(newWeapon, ammoOverride);
        isEquipping = false;
    }
    private void spawnWeapon(WeaponStats newWeapon, int ammoOverride = -1)
    {
        activeWeapon = newWeapon;

        if (activeWeapon is GunStats gun)
            currentAmmo = ammoOverride >= 0 ? ammoOverride : gun.startingBullets;
        else if (activeWeapon is MeleeStats)
            currentAmmo = 10_000;

        spawnedWeaponModel = Instantiate(activeWeapon.weaponModel, weaponHolder, false);
        spawnedWeaponModel.transform.localPosition = Vector3.zero;
        spawnedWeaponModel.transform.localRotation = Quaternion.identity;

        if (spawnedWeaponModel.TryGetComponent<Rigidbody>(out Rigidbody rb))
            rb.isKinematic = true;
        if (spawnedWeaponModel.TryGetComponent<PickWeapon>(out PickWeapon picker))
            picker.enabled = false;
        if (spawnedWeaponModel.TryGetComponent<WeaponWallAvoidance>(out WeaponWallAvoidance clip))
            clip.enabled = true;
        if (spawnedWeaponModel.TryGetComponent<Damage>(out Damage thrownDamage))
            thrownDamage.enabled = false;

        string targetName = (activeWeapon is GunStats) ? "Muzzle" : "HitPoint";
        gunBarrel = FindDeepChild(spawnedWeaponModel.transform, targetName);

        updateHUD();
    }

    public Transform Barrel => gunBarrel;
    public int CurrentAmmo => currentAmmo;

    public void ThrowWeapon()
    {
        if (spawnedWeaponModel == null)
            return;
        spawnedWeaponModel.transform.SetParent(null);
        if (spawnedWeaponModel.TryGetComponent<WeaponWallAvoidance>(out WeaponWallAvoidance clip))
            clip.enabled = false;
        if (spawnedWeaponModel.TryGetComponent<PickWeapon>(out PickWeapon picker))
        {
            picker.weapon = activeWeapon;
            picker.remainingAmmo = (activeWeapon is GunStats) ? currentAmmo : -1;
            picker.enabled = true;
        }
        if (!spawnedWeaponModel.TryGetComponent<Rigidbody>(out Rigidbody projectileRb))
            projectileRb = spawnedWeaponModel.AddComponent<Rigidbody>();

        activeWeapon.isFromGround = false;

        projectileRb.isKinematic = false;
        projectileRb.useGravity = true;

        // Calculate directional trajectory
        Vector3 forceDirection = Camera.main.transform.forward;
        RaycastHit hit;

        if (Physics.Raycast(GameManager.instance.playerScript.weaponHoldPos.transform.position,
                            GameManager.instance.playerScript.weaponHoldPos.transform.forward,
                            out hit, 500f))
        {
            forceDirection = (hit.point - GameManager.instance.playerScript.weaponHoldPos.transform.position).normalized;
        }

        // Apply forward and upward force
        Vector3 forceToAdd = forceDirection * GameManager.instance.playerScript.throwForce
                           + GameManager.instance.player.transform.up * GameManager.instance.playerScript.throwUpwardForce;

        projectileRb.AddForce(forceToAdd, ForceMode.Impulse);

        // Add subtle spin for realistic throwing physics
        projectileRb.AddTorque(Camera.main.transform.right * 10f, ForceMode.Impulse);

        if (spawnedWeaponModel.TryGetComponent<Collider>(out Collider weaponCollider))
            weaponCollider.enabled = true;
        if (spawnedWeaponModel.TryGetComponent<Damage>(out Damage thrownDamage))
            thrownDamage.enabled = true;

        if (!spawnedWeaponModel.TryGetComponent<DroppedWeapon>(out DroppedWeapon dropped))
            dropped = spawnedWeaponModel.AddComponent<DroppedWeapon>();

        dropped.groundLayers = groundLayers;
        activeWeapon = null;
        spawnedWeaponModel = null;
        gunBarrel = null;

        updateHUD();
    }

    // find nested children
    public Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            Transform result = FindDeepChild(child, name);
            if (result != null)
                return result;
        }
        return null;
    }

    public float UpgradeFireRate
    {
        get
        {
            if (activeWeapon == null)
                return 0f;

            float rate = activeWeapon.attackRate;

            // Check if fire rate upgrade is active
            if (UpgradeManager.instance != null && UpgradeManager.instance.IsUpgradeActive("fire_rate"))
                rate /= 1.5f;

            return rate;
        }
    }

    public void Attack()
    {
        if (activeWeapon == null || attackTimer < UpgradeFireRate)
            return;
        if (currentAmmo <= 0)
        { AudioManager.instance.PlayEmptyMag(); return; }
        if (HeartbeatManager.instance != null)
            HeartbeatManager.instance.PlayerShot();

        attackTimer = 0f;
        currentAmmo--;
        activeWeapon.Attack();
    }

    void updateHUD()
    {
        if (GameManager.instance == null)
            return;

        bool isGun = activeWeapon is GunStats;
        if (GameManager.instance.ammoPanel != null)
        {
            GameManager.instance.ammoPanel.SetActive(isGun);
        }

        if (isGun && GameManager.instance.magAmmoUI != null)
        {
            GameManager.instance.magAmmoUI.text = currentAmmo.ToString();
        }

        updateWeaponIcons();
    }

    void updateWeaponIcons()
    {
        if (GameManager.instance == null)
            return;

        if (activeWeapon != null && GameManager.instance.activeWeapon != null)
        {
            GameManager.instance.activeWeapon.sprite = activeWeapon.sprite;
        }
        else
        {
            GameManager.instance.magAmmoUI.text = "0";
            GameManager.instance.activeWeapon.sprite = emptySlot;
        }
    }

    [ContextMenu("Reset Saved Weapon")]
    public void ResetWeapon()
    {
        PlayerPrefs.DeleteKey("EquippedWeapon");
    }

    public void PlayMeleeSwing()
    {
        if (spawnedWeaponModel == null || isMeleeSwinging)
            return;

        StartCoroutine(meleeSwing());
    }

    IEnumerator meleeSwing()
    {
        isMeleeSwinging = true;

        Transform weaponTransform = spawnedWeaponModel.transform;
        Quaternion startRotation = weaponTransform.localRotation;
        Vector3 startPosition = weaponTransform.localPosition;

        Quaternion swingRotation = startRotation * Quaternion.Euler(meleeSwingRotation);
        Vector3 swingPosition = startPosition + meleeSwingPosition;

        float timer = 0f;
        while (timer < meleeSwingDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = timer / meleeSwingDuration;

            weaponTransform.localRotation = Quaternion.Lerp(startRotation, swingRotation, t);
            weaponTransform.localPosition = Vector3.Lerp(startPosition, swingPosition, t);

            yield return null;
        }

        timer = 0f;

        while (timer < meleeReturnDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = timer / meleeReturnDuration;

            weaponTransform.localRotation = Quaternion.Lerp(swingRotation, startRotation, t);
            weaponTransform.localPosition = Vector3.Lerp(startPosition, swingPosition, t);
            yield return null;
        }

        weaponTransform.localRotation = startRotation;
        weaponTransform.localPosition = startPosition;

        isMeleeSwinging = false;
    }
}
