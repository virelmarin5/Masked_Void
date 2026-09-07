using UnityEngine;

// cleans up thrown weapons. once one lands on actual ground it deletes itself
// after a second, so arenas don't fill with debris.
public class DroppedWeapon : MonoBehaviour
{
    [Tooltip("Which layers count as ground. Enemies and props should be excluded.")]
    public LayerMask groundLayers = ~0;

    // only fires the cleanup once, no matter how many times it bounces
    bool hasLanded;

    void OnCollisionEnter(Collision collision)
    {
        if (hasLanded)
            return;

        // Hitting an enemy shouldn't count as landing.
        if (((1 << collision.gameObject.layer) & groundLayers) == 0)
            return;

        hasLanded = true;
        Destroy(gameObject, 1f);
    }
}
