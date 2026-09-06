using System.Collections;
using UnityEngine;

// spins the glitch cubes to random angles, either snapping or easing into each new target
public class GlitchCubeRotate : MonoBehaviour
{
    [Header("Cubes")]
    [Tooltip("every cube this script randomizes the rotation of")]
    [SerializeField] GameObject[] cubes;

    [Header("Timing")]
    [Tooltip("seconds a cube sits still before it picks a new target")]
    [SerializeField] float stagger = 1f;

    [Tooltip("seconds to travel to the new rotation, 0 = instant snap like before")]
    [SerializeField] float lerpTime = 0.35f;

    [Tooltip("on = cubes are offset from each other so the group ripples, off = they all move together")]
    [SerializeField] bool staggerPerCube = false;

    [Header("Feel")]
    [Tooltip("shapes the lerp, leave empty for plain linear")]
    [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // per cube state, all these stay the same length as cubes
    Quaternion[] fromRot;
    Quaternion[] toRot;
    float[] progress;   // 0 to 1 through the current lerp, 1 means done
    float[] waitLeft;   // time until this cube picks its next target


    [Header("Scale")]
    [Tooltip("smallest a cube can be scaled to")]
    [SerializeField] float minSize = .25f;

    [Tooltip("largest a cube can be scaled to")]
    [SerializeField] float maxSize = 1.5f;

    [Tooltip("on also randomizes scale alongside rotation, off leaves cubes at their prefab size")]
    [SerializeField] bool doScale = false;
    Vector3[] fromScale;
    Vector3[] toScale;

    Coroutine rotateRoutine;

    void OnEnable()
    {
        buildState();

        // one routine only, never stack them
        if (rotateRoutine == null)
            rotateRoutine = StartCoroutine(tickCubes());
    }

    void OnDisable()
    {
        // kill it so it doesn't survive a disable/enable and double up
        if (rotateRoutine != null)
        {
            StopCoroutine(rotateRoutine);
            rotateRoutine = null;
        }
    }

    // sets up the parallel arrays and gives each cube its starting delay
    void buildState()
    {
        if (cubes == null)
            return;

        // per cube state, all these stay the same length as cubes.
        // parallel arrays rather than a struct so the whole thing is one allocation.
        fromRot = new Quaternion[cubes.Length];
        toRot = new Quaternion[cubes.Length];
        fromScale = new Vector3[cubes.Length];
        toScale = new Vector3[cubes.Length];
        progress = new float[cubes.Length];
        waitLeft = new float[cubes.Length];

        for (int i = 0; i < cubes.Length; i++)
        {
            if (cubes[i] == null)
                continue;

            fromRot[i] = cubes[i].transform.localRotation;
            fromScale[i] = cubes[i].transform.localScale;
            toRot[i] = fromRot[i];
            toScale[i] = fromScale[i];
            progress[i] = 1f;

            // spread the cubes across one stagger window so they don't all fire together
            waitLeft[i] = staggerPerCube && cubes.Length > 1
                ? stagger * ((float)i / cubes.Length)
                : 0f;
        }
    }

    IEnumerator tickCubes()
    {
        while (true)
        {
            // nothing assigned yet, idle a frame so we don't spin forever
            if (cubes == null || cubes.Length == 0)
            {
                yield return null;
                continue;
            }

            // unscaled so the cubes keep glitching while the player freezes time
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < cubes.Length; i++)
            {
                if (cubes[i] == null)
                    continue;

                if (progress[i] < 1f)
                {
                    stepLerp(i, dt);
                }
                else
                {
                    // sitting still, count down to the next target
                    waitLeft[i] -= dt;
                    if (waitLeft[i] <= 0f)
                        pickTarget(i);
                }
            }

            yield return null;
        }
    }

    // moves one cube a frame further along its current lerp
    void stepLerp(int i, float dt)
    {
        progress[i] += dt / lerpTime;
        if (progress[i] > 1f)
            progress[i] = 1f;

        float t = ease != null && ease.length > 0
            ? ease.Evaluate(progress[i])
            : progress[i];

        // slerp so it takes the short way around instead of unwinding weird
        cubes[i].transform.localRotation = Quaternion.Slerp(fromRot[i], toRot[i], t);
        if (doScale)
        {
            cubes[i].transform.localScale = Vector3.Slerp(fromScale[i], toScale[i], t / 2);
        }
        // landed, start the wait for the next one
        if (progress[i] >= 1f)
            waitLeft[i] = stagger;
    }

    // rolls a new random rotation for one cube and kicks off its lerp
    void pickTarget(int i)
    {
        // float overload, the int one is max exclusive and gives whole degrees only
        float x = Random.Range(0f, 360f);
        float y = Random.Range(0f, 360f);
        float z = Random.Range(0f, 360f);

        float xS = Random.Range(minSize, maxSize);
        float yS = Random.Range(minSize, maxSize);
        float zS = Random.Range(minSize, maxSize);

        // local so the cubes still read right if the rift parent moves or spins
        fromRot[i] = cubes[i].transform.localRotation;
        toRot[i] = Quaternion.Euler(x, y, z);

        fromScale[i] = cubes[i].transform.localScale;
        toScale[i] = new Vector3(xS, yS, zS);

        // no lerp time means just put it there
        if (lerpTime <= 0f)
        {
            cubes[i].transform.localRotation = toRot[i];
            if (doScale)
            {
                cubes[i].transform.localScale = toScale[i];
            }
            progress[i] = 1f;
            waitLeft[i] = stagger;
            return;
        }

        progress[i] = 0f;
    }

    // lets the boss manager ramp the feel per phase
    public void SetTiming(float newStagger, float newLerpTime)
    {
        stagger = newStagger;
        lerpTime = newLerpTime;
    }
}
