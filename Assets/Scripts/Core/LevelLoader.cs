using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/*
 * Script: LevelLoader
 *
 * Description:
 * Lives in Bootstrap and swaps which level scene is loaded while every manager
 * stays put. Only one level is ever loaded at a time.
 *
 * Responsibilities:
 * - Load the level the title screen asked for, additively on top of Bootstrap
 * - Unload the previous level before loading a new one
 * - Set the level as the active scene so it owns lighting and spawns
 * - Move the player to the level's Player Spawn Pos marker
 * - Detect when someone pressed play from a level scene and skip loading
 *
 * Interacts With:
 * - LevelBootstrapper (sits in each level scene)
 * - GameManager (player reference)
 * - TitleScreenManager (sets requestedLevel before loading Bootstrap)
 *
 * Notes:
 * - The player teleport disables the CharacterController first. It overrides
 *   transform writes, so without that the player silently stays at origin.
 * - requestedLevel is static because the title screen sets it before Bootstrap
 *   exists.
 */


// Lives in bootstrp, swaps which level scene is loaded while the managers stay put
// Only one level scene should be loaded at a time, and the level scene should be the only scene that is unloaded and loaded
public class LevelLoader : MonoBehaviour
{

    public static LevelLoader instance;

    [Header("Scenes")]
    [Tooltip("Scene name of the persistent scene that contains the managers")]
    [SerializeField] private string bootstrapSceneName = "Bootstrap";

    [Tooltip("Level to load when not asked, leave blank to load nothing")]
    [SerializeField] private string fallbackLevelName = "";

    [Header("Spawn")]
    [Tooltip("Object name in the level scene to spawn the player")]
    [SerializeField] private string playerSpawnObjectName = "Player Spawn Pos";

    // set by the title screen before it loads bootstrap.
    public static string requestedLevelName = "";

    // scene name of the level scene that is currently loaded, or empty if no level scene is loaded
    private string currentLevel = "";

    public string CurrentLevel => currentLevel;


    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        if (SceneManager.sceneCount > 1)
        {
            currentLevel = findOpenLevelName();
            placePlayerAtSpawn();
            return;
        }

        string wanted = string.IsNullOrEmpty(requestedLevelName) ? fallbackLevelName : requestedLevelName;

        if (string.IsNullOrEmpty(wanted))
        {
            return;
        }

        StartCoroutine(LoadLevel(wanted));
    }


    public IEnumerator LoadLevel(string levelName)
    {
        Scene previous = SceneManager.GetSceneByName(currentLevel);

        // something else can unload the level behind our back, and UnloadSceneAsync
        // throws on a name that is no longer loaded rather than returning null
        if (previous.IsValid() && previous.isLoaded)
        {
            AsyncOperation unload = SceneManager.UnloadSceneAsync(previous);

            while (!unload.isDone)
            {
                yield return null;
            }
        }

        currentLevel = "";

        AsyncOperation load = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        while (!load.isDone)
        {
            yield return null;
        }

        SceneManager.SetActiveScene(SceneManager.GetSceneByName(levelName));
        currentLevel = levelName;

        placePlayerAtSpawn();
    }


    private void placePlayerAtSpawn()
    {
        GameObject player = GameManager.instance != null ? GameManager.instance.player : null;

        if (player == null)
        {
            return;
        }

        GameObject spawnPoint = GameObject.Find(playerSpawnObjectName);

        if (spawnPoint == null)
        {
            Debug.LogWarning("LevelLoader: no '" + playerSpawnObjectName + "' found in " + currentLevel, this);
            return;
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        player.transform.position = spawnPoint.transform.position;
        player.transform.rotation = spawnPoint.transform.rotation;

        if (controller != null)
        {
            controller.enabled = true;
        }
    }


    private string findOpenLevelName()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name != bootstrapSceneName)
            {
                return scene.name;
            }
        }

        return "";
    }
}
