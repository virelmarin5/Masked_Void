using UnityEngine;

/*
 * Script: WeaponStats
 *
 * Description:
 * Base data asset for every weapon. GunStats and MeleeStats extend it with
 * their own firing behaviour. One asset per weapon, in Tunables/Weapons.
 *
 * Interacts With:
 * - WeaponManager (equips from these)
 * - ChallengeManager (challenges are tracked per weapon)
 */
public abstract class WeaponStats : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("key used to save and restore the equipped weapon, must be unique")]
    public string Name;

    [Tooltip("display name shown in the hud and shop")]
    public string weaponName;

    [Header("Model")]
    [Tooltip("prefab spawned into the player's hand when equipped")]
    public GameObject weaponModel;

    [Tooltip("icon shown in the weapon slot on the hud")]
    public Sprite sprite;

    [Header("Shop")]
    [Tooltip("price in Bytes if this is sold in the in-run shop")]
    public int cost;

    [Header("Damage")]
    [Tooltip("seconds between attacks")]
    [Range(.1f, 5)][SerializeField] public float attackRate;

    [Header("Challenge Source")]
    [Tooltip("set true when picked up off the ground, some challenges require it")]
    public bool isFromGround = false;

    // each weapon type implements its own firing
    public abstract void Attack();
}