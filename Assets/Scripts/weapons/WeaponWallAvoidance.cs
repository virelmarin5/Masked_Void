using UnityEngine;

/*
 * Script: WeaponWallAvoidance
 *
 * Description:
 * Stops the held weapon clipping through walls. Raycasts forward from a
 * projector on the player and rotates the weapon down as a wall gets closer.
 *
 * Interacts With:
 * - WeaponManager (enables this while held, disables it once thrown)
 *
 * Notes:
 * - Finds the projector by the Clipper tag on Start. If nothing carries that
 *   tag this throws, so the tag has to exist on the player prefab.
 */
public class WeaponWallAvoidance : MonoBehaviour
{
    [Tooltip("layers that count as a wall, keep enemies and pickups out of this")]
    public LayerMask clipLayer;

    [Header("Settings")]
    [Tooltip("how far ahead to check, the weapon starts rotating at this distance")]
    public float checkDist = 1f;

    [Tooltip("rotation applied when fully against a wall, default swings the barrel down")]
    public Vector3 newDir = new Vector3(0f, -90f, 0f);

    // found by the Clipper tag on Start, the ray fires from here
    Transform clipProjector;

    // 0 clear, 1 flat against a wall
    private float lerpPos;
    private Quaternion defaultRot;
    private Quaternion clippedRot;

    private void Start()
    {
        defaultRot = Quaternion.identity;
        clippedRot = Quaternion.Euler(newDir);
        clipProjector = GameObject.FindWithTag("Clipper").transform;
    }

    private void Update()
    {
        if (Physics.Raycast(clipProjector.position, clipProjector.forward, out RaycastHit hit, checkDist, clipLayer))
        {
            lerpPos = 1f - (hit.distance / checkDist);
        }
        else
        {
            lerpPos = 0f;
        }

        lerpPos = Mathf.Clamp01(lerpPos);

        transform.localRotation = Quaternion.Lerp(defaultRot, clippedRot, lerpPos);
    }
}