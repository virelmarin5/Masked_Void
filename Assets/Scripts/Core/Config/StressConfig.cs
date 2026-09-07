using UnityEngine;

/*
 * Script: StressConfig
 *
 * Description:
 * Every number the bpm health system runs on. One asset, referenced by
 * HeartbeatManager. Nothing hardcodes these.
 *
 */

[CreateAssetMenu(menuName = "Config/StressConfig")]
public class StressConfig : ScriptableObject
{

    [Header("Range")]
    [Tooltip("Bpm with no stress at all: default is 20")]
    public int restingBpm = 20;

    [Tooltip("Bpm with max stress: default is 200")]
    public int maxBpm = 200;


    [Header("Stress")]
    [Tooltip("Stress level when at max stress: default is 100")]
    public float maxStress = 100f;

    [Tooltip("Stress removed every second while not being hit: default is 3")]
    public float stressDecayRate = 3f;

    [Header("Decay")]
    [Tooltip("Seconds after any stress gain before decay resumes: default is 1")]
    public float decayDelay = 1f;

    [Tooltip("Decay rate while fully stopped: default is 6 (Faster than 3/s combat rate")]
    public float idleDecayRate = 6f;

    [Header("Stress Sources")]
    [Tooltip("Stress added when hit by a projectile: default is 20")]
    public float damagedStress = 20f;

    [Tooltip("Stress when a projectile is near without hitting: default is 5")]
    public float nearMissStress = 5f;

    [Tooltip("Stress when the player shoots while seeing an enemy: default is 4")]
    public float shootingStress = 4f;

    [Header("Movement Speed")]
    [Tooltip("Bpm movement alone cann reach: default is 120")]
    public int maxMovementBpm = 120;

    [Tooltip("Stress added per second while moving: default is 11.55f (Reaches movement cap in ~6.5s of walkijng")]
    public float movementStress = 11.55f;

    [Tooltip("Landing speed below this adds no stress: default is 6")]
    public float minFallSpeedForStress = 6f;

    [Tooltip("Stress per unit of landing speed above the threshold: default is 1 (A standard jump ~= 4 stress, tied with shooting")]
    public float fallStressPerUnitSpeed = 1f;

    [Header("Stress Relief")]
    [Tooltip("Stress removed when player kills an enemy: default is 10")]
    public float killStressRelief = 10f;
    [Tooltip("Stress removed when a wave ends: default is 30")]
    public float waveEndStressRelief = 30f;


}
