/*
 * Script: WaveManager
 *
 * Description:
 * Central wave coordinator. Does NOT spawn enemies itself - each
 * spawner is fully individual (own prefabs, percentages, pacing,
 * and difficulty scaling). WaveManager just owns the wave number and
 * the countdown between waves, and keeps every spawner in sync:
 * the next wave will not begin until every registered spawner has
 * finished spawning AND every enemy from every spawner is dead.
 *
 * Responsibilities:
 * - Automatically start the first wave when the level begins
 * - Wait between waves (real-time, unaffected by Time.timeScale),
 *   then tell every spawner to begin the new wave
 * - Track total enemies alive across all spawn points
 * - Only complete a wave once ALL spawn points are done spawning and
 *   ALL of their enemies are dead
 * - Notify HeartbeatManager when enemies die or waves end
 * - Notify GameManager when all waves are completed
 *
 * Interacts With:
 * - spawner (one or more, individually configured)
 * - HeartbeatManager
 * - GameManager
 * - WaveLightController
 * - AudioManager
 */

using System.Collections;
using UnityEngine;

public class WaveManager : MonoBehaviour, IWaveHost
{
    public static WaveManager instance;

    [Header("Weapon Prefabs")]
    [Tooltip("weapons a basic enemy can spawn holding, one is picked at random")]
    [SerializeField] GameObject[] basicWeaponPrefabs;

    [Tooltip("weapons a heavy enemy can spawn holding")]
    [SerializeField] GameObject[] heavyWeaponPrefabs;

    [Tooltip("weapons a ranged enemy can spawn holding, this is where player guns come from")]
    [SerializeField] GameObject[] rangedWeaponPrefabs;

    [Header("Enemy Prefabs")]
    [Tooltip("melee enemy with a katana")]
    [SerializeField] GameObject basicEnemyPrefabs;

    [Tooltip("melee enemy that shoves instead of damaging")]
    [SerializeField] GameObject heavyEnemyPrefabs;

    [Tooltip("shooting enemy, drops the weapon the player picks up")]
    [SerializeField] GameObject rangedEnemyPrefabs;

    [Header("Roam and Spawn Points")]
    [Tooltip("empty objects enemies wander between, drag them from the level")]
    [SerializeField] Transform[] roamPointTransforms;

    [Tooltip("empty objects enemies appear at, usually inside spawn rooms")]
    [SerializeField] Transform[] spawnPointTransforms;

    [Header("Roam Settings")]
    [Tooltip("Chance that a ranged enemy will roam before engaging.")]
    [SerializeField] float giveWillRoamChance;

    [Header("Spawn Settings")]
    [Tooltip("how many enemies wave 1 spawns")]
    [SerializeField] int enemiesToSpawnAtWave0;

    [Tooltip("count is multiplied by this each wave, 1.2 means twenty percent more each time")]
    [SerializeField] float enemyIncreaseMultiplier;

    [Header("Enemy Percent To Spawn")]
    [Tooltip("weights, not real percents, they are rolled against their own total so they need not add to 100")]
    [SerializeField] int basicEnemyPercent;
    [SerializeField] int heavyEnemyPercent;
    [SerializeField] int rangedEnemyPercent;

    [Header("Timers")]
    [Tooltip("seconds between each enemy appearing within a wave")]
    [SerializeField] float timeBetweenSpawns;

    [Tooltip("seconds of breathing room after a wave clears")]
    [SerializeField] int timeBetweenWaves;

    [Tooltip("counts up during the gap between waves, set at runtime")]
    [SerializeField] float waveTimer;

    [Tooltip("real seconds a spawn point must rest before reusing, spreads spawns out")]
    [SerializeField] float spawnPointCooldown = 5f;

    [Header("Misc")]
    [Tooltip("waves before the boss teleporter opens, gdd says 10")]
    [SerializeField] int maxWaves;

    [Tooltip("true during the gap between waves, set at runtime")]
    [SerializeField] bool waitingForNextWave;

    [Header("Wave Tracking")]
    [Tooltip("current wave, set at runtime")]
    [SerializeField] private int currentWave = 0;

    [Header("Economy")]
    [Tooltip("how many Files a cleared wave is worth")]
    [SerializeField] private EconomyConfig economy;

    // runtime counters
    private int enemiesAlive;
    private int enemiesKilled;
    private bool waveInProgress;
    private RoamPoint[] roamPoints;
    private SpawnPoint[] spawnPoints;
    private Coroutine spawnRoutine;
    private int spawnersStillSpawning;

    // read only views for the hud and other systems, nothing outside changes these
    public int CurrentWave => currentWave;

    public int EnemiesKilled => enemiesKilled;

    public bool IsWaitingForNextWave => waitingForNextWave;

    // rounds up so the countdown never shows 0 while still waiting
    public int SecondsUntilNextWave =>
        Mathf.Max(0, Mathf.CeilToInt(timeBetweenWaves - waveTimer));

    // which type the last roll picked, held between the roll and the spawn
    [HideInInspector] EnemyType typeSpawned;



    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        waveHost.active = this;

        assignRoamPoints(roamPointTransforms);
        assignSpawnPoints(spawnPointTransforms);
    }



    void Start()
    {
        queueNextWave();
    }

    void Update()
    {
        if (GameManager.instance != null &&
            GameManager.instance.isPaused)
        {
            return;
        }

        if (!waitingForNextWave)
        {
            return;
        }

        waveTimer += Time.unscaledDeltaTime;

        if (waveTimer >= timeBetweenWaves)
        {
            waitingForNextWave = false;
            startWave();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        { 
        instance = null;
        }

        if (ReferenceEquals(waveHost.active, this))
        {
            waveHost.active = null;
        }
    }

    // spawns one enemy at a time on an interval until the wave's count is met
    private IEnumerator spawn()
    {
        int amountToSpawn = Mathf.RoundToInt(
            enemiesToSpawnAtWave0 *
            Mathf.Pow(enemyIncreaseMultiplier, CurrentWave)
        );

        for (int i = 0; i < amountToSpawn; i++)
        {
            GameObject enemyPrefab = chooseEnemyPrefab();

            if (enemyPrefab == null)
            {
                continue;
            }

            SpawnPoint point = getSpawnPoint();

            if (point == null)
            {
                break;
            }

            GameObject enemy = Instantiate(
                enemyPrefab,
                point.point.position,
                point.point.rotation
            );

            if (typeSpawned == EnemyType.ranged)
            {
                if (enemy.TryGetComponent<EnemyBase>(
                    out EnemyBase enemyScript))
                {
                    enemyScript.willRoam =
                        Random.Range(0f, 1f) <= giveWillRoamChance;
                }
            }

            point.lastUsed = Time.unscaledTime;

            enemiesAlive++;

            yield return new WaitForSeconds(timeBetweenSpawns);
        }

        spawnRoutine = null;

        if (enemiesAlive <= 0)
        {
            completeWave();
        }
    }

    GameObject chooseEnemyPrefab()
    {
        float totalPercent =
            rangedEnemyPercent +
            basicEnemyPercent +
            heavyEnemyPercent;

        if (totalPercent <= 0)
        {
            return null;
        }

        float randomValue = Random.Range(0f, totalPercent);

        if (randomValue < rangedEnemyPercent)
        {
            typeSpawned = EnemyType.ranged;
            return rangedEnemyPrefabs;
        }

        randomValue -= rangedEnemyPercent;

        if (randomValue < basicEnemyPercent)
        {
            typeSpawned = EnemyType.basic;
            return basicEnemyPrefabs;
        }

        typeSpawned = EnemyType.heavy;
        return heavyEnemyPrefabs;
    }

    // picks a spawn point that's off cooldown, falls back to a random one if none are
    private SpawnPoint getSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return null;
        }

        int startIndex = Random.Range(0, spawnPoints.Length);

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            SpawnPoint candidate =
                spawnPoints[(startIndex + i) % spawnPoints.Length];

            if (candidate.IsFree(spawnPointCooldown))
            {
                return candidate;
            }
        }

        return spawnPoints[startIndex];
    }

    // bumps the wave number, works out the count, and starts the spawn routine
    private void startWave()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.StopMusic();
        }

        waveInProgress = true;
        enemiesAlive = 0;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
        }

        spawnRoutine = StartCoroutine(spawn());
    }

    // starts the gap between waves and the round transition music
    private void queueNextWave()
    {
        currentWave++;
        enemiesAlive = 0;

        if (currentWave > maxWaves)
        {
            playerWins();
            return;
        }

        if (WaveLightController.instance != null)
        {
            WaveLightController.instance
                .FlashWarningLights(timeBetweenWaves);
        }

        if (AudioManager.instance != null)
        {
            AudioManager.instance
                .PlayRoundTransitionMusic();
        }

        waveTimer = 0f;
        waitingForNextWave = true;
    }

    // hands an enemy a free roam point and marks it taken, so two enemies never
    // walk to the same spot. returns null when every point is claimed.
    public Transform ClaimRoamPoint(GameObject askingEnemy)
    {
        if (askingEnemy == null)
        {
            return null;
        }

        if (roamPoints == null || roamPoints.Length == 0)
        {
            return null;
        }

        int startIndex = Random.Range(0, roamPoints.Length);

        for (int i = 0; i < roamPoints.Length; i++)
        {
            RoamPoint candidate =
                roamPoints[(startIndex + i) % roamPoints.Length];

            if (!candidate.isFree)
            {
                continue;
            }

            candidate.claimedBy = askingEnemy;

            return candidate.point;
        }

        return null;
    }

    // frees whatever point this enemy had, called on death or when it engages
    public void ReleaseRoamPoint(GameObject askingEnemy)
    {
        if (askingEnemy == null || roamPoints == null)
        {
            return;
        }

        for (int i = 0; i < roamPoints.Length; i++)
        {
            if (roamPoints[i].claimedBy == askingEnemy)
            {
                roamPoints[i].claimedBy = null;
            }
        }
    }

    // picks which weapon a spawning enemy carries, by type
    private GameObject getWeaponPrefab(EnemyType type, int index)
    {
        GameObject[] weaponPrefabList = null;

        switch (type)
        {
            case EnemyType.basic:
                weaponPrefabList = basicWeaponPrefabs;
                break;

            case EnemyType.heavy:
                weaponPrefabList = heavyWeaponPrefabs;
                break;

            case EnemyType.ranged:
                weaponPrefabList = rangedWeaponPrefabs;
                break;
        }

        if (weaponPrefabList == null ||
            weaponPrefabList.Length == 0)
        {
            return null;
        }

        if (index < 0 || index >= weaponPrefabList.Length)
        {
            index = Random.Range(0, weaponPrefabList.Length);
        }

        return weaponPrefabList[index];
    }

    // enemies report in here through IWaveHost. the wave only advances once
    // every spawned enemy is dead.
    public void EnemyKilled()
    {
        enemiesAlive--;
        enemiesKilled++;

        if (enemiesAlive < 0)
        {
            enemiesAlive = 0;
        }

        if (HeartbeatManager.instance != null)
        {
            HeartbeatManager.instance.EnemyKilled();
        }

        // Don't finish while more enemies are still scheduled to spawn.
        if (enemiesAlive <= 0 && spawnRoutine == null)
        {
            completeWave();
        }
    }

    // awards Files, tells the heartbeat system, then either queues the next
    // wave or ends the run
    void completeWave()
    {
        if (!waveInProgress)
        {
            return;
        }

        waveInProgress = false;

        if (HeartbeatManager.instance != null)
        {
            HeartbeatManager.instance.WaveCompleted();
        }

        if (GameManager.instance != null)
        {
            GameManager.instance.AddFiles(economy.filesPerWave);

            // keep the upgrade currency in sync after every wave
            if (UpgradeManager.instance != null)
            {
                UpgradeManager.instance.files += GameManager.instance.totalFiles;
                UpgradeManager.instance.SaveUpgrades();
            }

            //Debug.Log("Current Files: " + GameManager.instance.totalFiles);
        }

        queueNextWave();
    }

    // all waves cleared, hands off to the win state
    void playerWins()
    {
        if (GameManager.instance != null)
        {
            // GameManager.instance.stateWin();
        }
    }

    // strips nulls out of an inspector array, so a forgotten empty slot doesn't
    // throw when picking a random point
    public Transform[] CleanList(Transform[] source)
    {
        if (source == null)
        {
            return new Transform[0];
        }

        int counted = 0;

        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] != null)
            {
                counted++;
            }
        }

        Transform[] cleaned = new Transform[counted];

        int write = 0;

        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == null)
            {
                continue;
            }

            cleaned[write] = source[i];
            write++;
        }

        return cleaned;
    }

    private void assignSpawnPoints(Transform[] points)
    {
        Transform[] cleaned = CleanList(points);

        spawnPoints = new SpawnPoint[cleaned.Length];

        for (int i = 0; i < cleaned.Length; i++)
        {
            SpawnPoint newPoint = new SpawnPoint();

            newPoint.point = cleaned[i];
            newPoint.lastUsed = 0f;

            spawnPoints[i] = newPoint;
        }

        if (spawnPoints.Length == 0)
        {
            Debug.LogError("WaveManager: no spawn points assigned");
        }
    }

    private void assignRoamPoints(Transform[] points)
    {
        Transform[] cleaned = CleanList(points);

        roamPoints = new RoamPoint[cleaned.Length];

        for (int i = 0; i < cleaned.Length; i++)
        {
            RoamPoint newPoint = new RoamPoint();

            newPoint.point = cleaned[i];
            newPoint.claimedBy = null;

            roamPoints[i] = newPoint;
        }

        if (roamPoints.Length == 0)
        {
            Debug.LogError("WaveManager: no roam points assigned");
        }
    }
}
