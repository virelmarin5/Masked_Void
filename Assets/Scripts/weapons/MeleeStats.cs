using UnityEngine;

/*
 * Script: MeleeStats
 *
 * Description:
 * Data and swing behaviour for a melee weapon. Raycasts on swing and plays a
 * different sound depending on whether it hit flesh, a shield, or a wall.
 *
 * Interacts With:
 * - WeaponManager (Barrel for the ray origin, PlayMeleeSwing for the animation)
 * - EnemyBase (RegisterDamageSource so the kill credits this weapon)
 * - IDamage (whatever it hits)
 *
 * Notes:
 * - The swing animation lives in WeaponManager because a ScriptableObject
 *   can't run a coroutine. The commented block below is the failed attempt.
 */
[CreateAssetMenu(menuName = "Weapons/Melee", order = 2)]
public class MeleeStats : WeaponStats
{
    [Header("Damage")]
    [Tooltip("damage per hit")]
    [Range(1, 10)][SerializeField] public int attackDamage;

    [Tooltip("how far the swing reaches, in metres")]
    [Range(5, 10)][SerializeField] public int attackDist;

    [Header("Audio")]
    [Tooltip("played on every swing, hit or miss")]
    public AudioClip swingSound;
    [Range(0, 1)] public float swingSoundVol = 1f;

    [Tooltip("played when the swing connects with something damageable")]
    public AudioClip hitFleshSound;
    [Range(0, 1)] public float hitFleshVol = 1f;

    [Tooltip("played when the swing hits level geometry")]
    public AudioClip hitWallSound;
    [Range(0, 1)] public float hitWallVol = 1f;

    [Tooltip("played when the swing hits the ice wall shield")]
    public AudioClip hitShieldSound;
    [Range(0, 1)] public float hitShieldVol = 1f;

    public override void Attack()
    {
        Transform gunBarrel = WeaponManager.instance.Barrel;
        if (gunBarrel == null)
            return;

        WeaponManager.instance.PlayMeleeSwing();
        AudioManager.instance.PlaySFX(swingSound, swingSoundVol);

        //StartCoroutine(katanaSwing());

        RaycastHit hit;
        if (Physics.Raycast(gunBarrel.position, gunBarrel.forward, out hit, attackDist))
        {
            // //register source for challenge manager
            EnemyBase eb = hit.transform.GetComponent<EnemyBase>();
            if (eb == null)
                eb = hit.transform.GetComponentInParent<EnemyBase>();
            if (eb != null)
                eb.RegisterDamageSource(this, isFromGround);

            IDamage dmg = hit.transform.GetComponent<IDamage>();
            if (dmg != null)
            {
                dmg.TakeDamage(attackDamage);
                AudioManager.instance.PlaySFX(hitFleshSound, hitFleshVol);
            }
            else if (hit.collider.CompareTag("Shield"))
            {
                AudioManager.instance.PlaySFX(hitShieldSound, hitShieldVol);
            }
            else
            {
                AudioManager.instance.PlaySFX(hitWallSound, hitWallVol);
            }

        }
    }
}