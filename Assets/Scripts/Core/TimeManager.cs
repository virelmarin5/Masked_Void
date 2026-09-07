using UnityEngine;

/*
 * Script: TimeManager
 *
 * Description:
 * The SUPERHOT-style time system. World time scales with how fast the player is
 * moving, so standing still nearly freezes everything. High stress pushes time
 * back toward full speed, so panicking costs you the slow motion. Player
 * movement runs on unscaled time so the player always feels responsive.
 *
 * Responsibilities:
 * - Blend player speed and stress into a target time scale each frame
 * - Smooth toward that target so time never snaps
 * - Keep the physics step in proportion to the time scale
 * - Support a hard override so scorestreaks can force a speed
 * - Pause and unpause for menus
 *
 * Interacts With:
 * - PlayerController (reads SpeedPercent)
 * - HeartbeatManager (reads StressPercent)
 * - GameManager (pause state)
 * - Scorestreaks (set and clear the override)
 *
 * Notes:
 * - Anything that must ignore time scale uses Time.unscaledDeltaTime. Player
 *   movement, stress decay, and every boss hazard are on unscaled time.
 * - fixedDeltaTime is scaled alongside timeScale so physics stays stable at
 *   low speeds. baseFixedDeltaTime remembers the project's original step.
 */
public class TimeManager : MonoBehaviour
{
    public static TimeManager instance;

    [Header("Time Scale Range")]
    [Tooltip("slowest the world ever runs, reached when standing still")]
    [SerializeField] private float minTimeScale = 0.1f;

    [Tooltip("fastest the world ever runs, only reached at high stress")]
    [SerializeField] private float maxTimeScale = 1f;

    [Header("Smoothing")]
    [Tooltip("how fast time reaches its target, higher snaps harder, 0 freezes the current scale")]
    [SerializeField] private float timeScaleSmoothing = 10f;

    [Header("Firing Pulse")]
    [Tooltip("How long a shot pushes time scale toward the pulse value, seconds")]
    [SerializeField] private float firePulseDuration = .15f;
    [Tooltip("Time scale target during a firing pulse")]
    [SerializeField] private float firePulseTimeScale = 1f;

    [Header("Runtime Override")]
    [Tooltip("true while a scorestreak is forcing the speed, set at runtime")]
    [SerializeField] private bool hasTimeScaleOverride;

    [Tooltip("the forced speed while an override is active, set at runtime")]
    [SerializeField] private float overrideTimeScale;

    // smoothed value we are actually applying, separate from the target
    private float currentTimeScale;

    // the project's physics step at timeScale 1, so scaling stays proportional
    private float baseFixedDeltaTime;

    private float firePulseTimer;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // Preserve the project's normal physics step.
        baseFixedDeltaTime = Time.fixedDeltaTime / Mathf.Max(Time.timeScale, 0.0001f);

        currentTimeScale = Mathf.Clamp(minTimeScale, 0.001f, maxTimeScale);
        ApplyTimeScale(currentTimeScale);

    }

    private void Update()
    {
        if (GameManager.instance != null && GameManager.instance.isPaused)
            return;

        float targetTimeScale;

        if (hasTimeScaleOverride)
        {
            // Scorestreaks such as Adrenaline temporarily take full control
            // over world speed. Movement and heartbeat cannot fight it.
            targetTimeScale = overrideTimeScale;
        }
        else if(firePulseTimer > 0f)
        {
            // A shot just fired. Push time toward the pulse value for a short window
            // regardless of bpm, then fall back to the bpm-driven target.

            firePulseTimer -= Time.unscaledDeltaTime;
            targetTimeScale = firePulseTimeScale;
        }
        else
        {
            float stress01 = HeartbeatManager.instance != null ? HeartbeatManager.instance.StressPercent : 0f;

            targetTimeScale = Mathf.Lerp(minTimeScale,maxTimeScale, stress01);
        }

        // Frame-rate-independent exponential smoothing.
        float blend = 1f - Mathf.Exp(-timeScaleSmoothing * Time.unscaledDeltaTime);

        currentTimeScale = Mathf.Lerp(
            currentTimeScale,
            targetTimeScale,
            blend
        );

        ApplyTimeScale(currentTimeScale);
    }

    // clamps, then writes both timeScale and the matching physics step.
    // early outs on tiny changes so we're not writing Time every frame.
    private void ApplyTimeScale(float newTimeScale)
    {
        newTimeScale = Mathf.Clamp(newTimeScale, minTimeScale, maxTimeScale);

        if (Mathf.Abs(Time.timeScale - newTimeScale) < 0.0001f)
            return;

        Time.timeScale = newTimeScale;
        Time.fixedDeltaTime = baseFixedDeltaTime * newTimeScale;
    }

    // Sets the normal world speed immediately. Persistent effects should
    // use setTimeScaleOverride instead so Update() does not overwrite them.
    public void SetTimeScale(float newTimeScale)
    {
        if (GameManager.instance != null && GameManager.instance.isPaused)
            return;

        currentTimeScale = Mathf.Clamp(newTimeScale, minTimeScale, maxTimeScale);
        ApplyTimeScale(currentTimeScale);
    }

    // Used by Overclock and any future scorestreak that needs to force world speed.

    public void SetTimeScaleOverride(float newTimeScale)
    {
        overrideTimeScale = Mathf.Clamp(newTimeScale, minTimeScale, maxTimeScale);
        hasTimeScaleOverride = true;

        // Overclock should feel immediate rather than taking several frames
        // Apply it instantly on activation.
        currentTimeScale = overrideTimeScale;

        if (GameManager.instance == null || !GameManager.instance.isPaused)
            ApplyTimeScale(currentTimeScale);
    }

    public void ClearTimeScaleOverride()
    {
        hasTimeScaleOverride = false;

        // Do not snap back here. The regular Update formula smoothly returns
        // from the override to movement + heartbeat controlled time.
    }

    // true while a scorestreak is holding the speed
    public bool ActiveTimeScaleOverride => hasTimeScaleOverride;

    // the smoothed scale we are applying, not necessarily Time.timeScale if paused
    public float TimeScale => currentTimeScale;

    // remembers the current scale before zeroing, so UnpauseTime can restore it
    public void PauseTime()
    {
        if (Time.timeScale > 0f)
            currentTimeScale = Time.timeScale;

        Time.timeScale = 0f;
    }

    public void UnpauseTime()
    {
        ApplyTimeScale(currentTimeScale);
    }

    public void PulseFireTimeScale()
    {
        firePulseTimer = firePulseDuration;
    }

    private void OnValidate()
    {
        minTimeScale = Mathf.Max(0.001f, minTimeScale);
        maxTimeScale = Mathf.Max(minTimeScale, maxTimeScale);
        timeScaleSmoothing = Mathf.Max(0f, timeScaleSmoothing);

        if (hasTimeScaleOverride)
            overrideTimeScale = Mathf.Clamp(overrideTimeScale, minTimeScale, maxTimeScale);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
