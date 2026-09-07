using UnityEngine;

/*
 * Script: GunStats
 *
 * Description:
 * Data and firing behaviour for a ranged weapon. Shotguns fire several pellets
 * in a cone, everything else fires one. Kunai gain a three-shot spread with
 * the matching upgrade.
 *
 * Interacts With:
 * - WeaponManager (reads Barrel for the spawn point)
 * - UpgradeManager (kunai spread and exploding bullets)
 * - Damage (spawned bullets carry it, tagged with this as sourceWeapon)
 */
[CreateAssetMenu(menuName = "Weapons/Gun")]
public class GunStats : WeaponStats
{
    // decides pellet count and which upgrades apply
    public enum GunType { Pistol, AR, Shotgun, Kunai }

    [Header("Gun Settings")]
    [Tooltip("shotgun fires pelletCount in a cone, kunai can gain a spread upgrade")]
    public GunType gunType;

    [Header("Projectile")]
    [Tooltip("bullet prefab, needs a Damage component on it")]
    [SerializeField] public Transform bullet;

    [Header("Spawn Position")]
    [Tooltip("offset of the model in the player's hand")]
    public Vector3 Position;

    [Tooltip("rotation of the model in the player's hand")]
    public Vector3 Rotation;

    [Header("Ammo")]
    [Tooltip("shots fired per trigger pull, only used by shotguns")]
    [Range(1, 20)] public int pelletCount;

    [Tooltip("cone width in degrees, only used by shotguns")]
    [Range(.2f, 20f)] public float spreadAngle;

    [Tooltip("rounds this weapon spawns with, gdd says the starting pistol has 3")]
    [Range(3, 30)] public int startingBullets;

    [Header("Audio")]
    [Tooltip("played on every shot")]
    public AudioClip shootSound;
    [Range(0, 1)] public float shootSoundVol;

    // spawns one bullet per pellet, each with its own random deviation inside
    // the spread cone. bullets are tagged with this weapon so kills credit
    // the right challenge.
    public override void Attack()
    {

        Transform gunBarrel = WeaponManager.instance.Barrel;
        if (gunBarrel == null)
            return;

        AudioManager.instance.PlaySFX(shootSound, shootSoundVol);

        bool hasKunaiSpread = UpgradeManager.instance != null &&
                      UpgradeManager.instance.IsUpgradeActive("kunai_spread");

        int shotsToFire = (gunType == GunType.Shotgun) ? pelletCount : 1;
        float spreadToUse = (gunType == GunType.Shotgun) ? spreadAngle : 0f;

        if (gunType == GunType.Kunai && hasKunaiSpread)
        {
            shotsToFire = 3;
            spreadToUse = 15f;
        }

        if (bullet != null)
        {
            for (int i = 0; i < shotsToFire; i++)
            {
                // Calculate random deviation within the spread angle cone
                float randomSpreadX = Random.Range(-spreadToUse, spreadToUse);
                float randomSpreadY = Random.Range(-spreadToUse, spreadToUse);

                // Combine the barrel's base rotation with our random offset angles
                Quaternion spreadRotation = gunBarrel.rotation * Quaternion.Euler(randomSpreadX, randomSpreadY, 0);

                // Spawn the bullet projectile flying out into its offset trajectory
                Transform spawnedBullet = Instantiate(bullet, gunBarrel.position, spreadRotation);
                spawnedBullet.gameObject.layer = LayerMask.NameToLayer("PlayerBullets");

                if (spawnedBullet.TryGetComponent<Damage>(out Damage dmg))
                {
                    dmg.sourceWeapon = this;

                    if (UpgradeManager.instance != null && UpgradeManager.instance.IsUpgradeActive("exploding_bullets"))
                        dmg.isExplosive = true;
                }
            }
        }
    }


}
