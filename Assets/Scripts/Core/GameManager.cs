using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/*
 * Script: GameManager
 *
 * Description:
 * Central game state and the hub for the pause, lose and win menus. Lives in
 * Bootstrap inside the GameManager-Base prefab alongside the UI it drives.
 *
 * Responsibilities:
 * - Own the pause, lose and win states and the menus that go with them
 * - Hold the two currencies, Bytes for the run and Files for the meta
 * - Track kill count and drive the HUD text
 * - Find the player after Bootstrap loads and raise PlayerReady
 * - Route menu navigation between challenges, settings and upgrades
 *
 * Interacts With:
 * - PlayerController (found by tag on Start, exposed as playerScript)
 * - TimeManager (pause and unpause)
 * - AudioManager (menu music and button clicks)
 * - WaveManager (reads wave number and countdown for the HUD)
 * - UpgradeManager (files carry over on run end)
 * - EnemyEvents (subscribes to Killed for kill count and bytes)
 * - WeaponManager, HeartbeatManager (wait on PlayerReady)
 *
 * Notes:
 * - The player lookup is in Start, not Awake. Awake order between root objects
 *   is not guaranteed and the player lives in the same scene.
 * - PlayerReady exists because managers used to read playerScript in their own
 *   Start and silently got null.
 * - This class does too much. Score, streaks and heartbeat all live in the same
 *   prefab and should eventually split out.
 */

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Menu")]
    [Tooltip("whichever menu is currently open, set at runtime not in the inspector")]
    [SerializeField] GameObject menuActive;

    [Tooltip("root of the pause menu")]
    [SerializeField] GameObject menuPause;

    [Tooltip("root of the lose screen, shown when bpm hits max")]
    [SerializeField] GameObject menuLose;

    [Tooltip("root of the win screen, shown after the boss dies")]
    [SerializeField] GameObject menuWin;

    [Header("UI Pages")]
    [Tooltip("challenge list page inside the pause menu")]
    public GameObject challengesCanvas;

    [Tooltip("settings page inside the pause menu")]
    public GameObject settingsCanvas;

    [Tooltip("upgrade shop page inside the pause menu")]
    public GameObject upgradesCanvas;

    [Header("Top Navigation Buttons")]
    [Tooltip("the row of tabs across the top of the pause menu")]
    public GameObject navTab;

    [Tooltip("tab that opens challengesCanvas")]
    public Button navChallengesButton;

    [Tooltip("tab that opens settingsCanvas")]
    public Button navSettingsButton;

    [Tooltip("tab that opens upgradesCanvas")]
    public Button navUpgradesButton;

    [Tooltip("the main pause menu button column, hidden when a page is open")]
    public GameObject buttons;

    [Tooltip("back button, only shown while a page is open")]
    public GameObject backButton;

    [Header("Settings Menu")]
    [Tooltip("volume sliders page")]
    [FormerlySerializedAs("soundMenu")]
    [SerializeField] public GameObject soundMenu;

    [Tooltip("controls reference page")]
    [SerializeField] public GameObject controlsMenu;

    [Header("Kills UI")]
    [Tooltip("score panel shown on the pause menu")]
    [SerializeField] public GameObject pauseScorePanel;

    [Tooltip("kill count text on the pause menu")]
    [SerializeField] private TMP_Text pauseScoreText;

    [Tooltip("kill count text on the lose screen")]
    [FormerlySerializedAs("loseScoreText")]
    [SerializeField] private TMP_Text loseScoreText;

    [Tooltip("live kill counter on the hud")]
    [SerializeField] TextMeshProUGUI killCounter;

    [Header("Wave UI")]
    [Tooltip("current wave number on the hud")]
    [SerializeField] TextMeshProUGUI waveCounter;

    [Tooltip("the words shown above the countdown, hidden mid wave")]
    [SerializeField] TextMeshProUGUI waveCountdownText;

    [Tooltip("seconds remaining until the next wave")]
    [SerializeField] TextMeshProUGUI waveCountdown;

    [Header("Interaction UI")]
    [Tooltip("root of the interact prompt, shown when looking at something usable")]
    public GameObject interactionUI;

    [Tooltip("what the interaction does, e.g. Open Shop")]
    public TMP_Text interactionText;

    [Tooltip("which key to press, e.g. E")]
    public TMP_Text interactionKey;

    [Header("Player")]
    [Tooltip("stamina bar on the hud")]
    [SerializeField] public Image playerStaminaBar;

    [Tooltip("popup shown when a checkpoint is reached")]
    [SerializeField] public GameObject checkpointPopup;

    [Header("Currency")]
    [Tooltip("bytes held this run, spent in the in-run shop, lost on death")]
    [SerializeField] public int totalBytes = 0;

    [Tooltip("files held this run, added to the meta total when the run ends")]
    [SerializeField] public int totalFiles = 0;

    [Tooltip("bytes counter on the hud")]
    [SerializeField] TextMeshProUGUI bytesText;

    [Header("Shop")]
    [Tooltip("warning shown when the player can't afford something")]
    public GameObject shopMessage;

    [Tooltip("root of the in-run shop panel")]
    public GameObject shopUI;

    [Header("Screen Flash")]
    [Tooltip("red overlay flashed when the player takes damage")]
    public GameObject damageFlashUI;

    [Header("Weapon UI")]
    [Tooltip("root of the ammo readout, hidden when unarmed")]
    public GameObject ammoPanel;

    [Tooltip("rounds left in the current weapon")]
    public TextMeshProUGUI magAmmoUI;

    [Tooltip("icon of the weapon currently held")]
    public Image activeWeapon;

    [Header("Runtime: Do not Change")]
    [Tooltip("true while any menu is open and time is paused")]
    public bool isPaused;

    [Tooltip("found by tag in Start, not assigned in the inspector")]
    public GameObject player;

    [Tooltip("cached controller off the player object, found in Start")]
    public PlayerController playerScript;
    int currentKill = 0;

    [Header("Bootstrap Shenanigans")]
    [SerializeField] private bool isBootstrapVersion = false;

    private void Awake()
    {
        // No conflict? Just take the slot.
        if (instance == null || instance == this)
        {
            instance = this;
            if (isBootstrapVersion)
                DontDestroyOnLoad(gameObject);
            return;
        }

        // Conflict: another GameManager already exists.
        // Rule: Level always beats Bootstrap.

        if (isBootstrapVersion)
        {
            // We are Bootstrap. The existing manager (level or another bootstrap) stays.
            // Destroy SELF (current).
            Destroy(gameObject);
            return;
        }

        // We are Level. Destroy the PREVIOUS manager (bootstrap or old level).
        Destroy(instance.gameObject);
        instance = this;
    }

    private void Start()
    {
        // player lives in bootstrap now and awake order between scenes is not guaranteed,
        // so find it here instead, start always runs after every awake
        GameObject tagged = GameObject.FindWithTag("Player");

        if (tagged != null)
        {
            playerScript = tagged.GetComponentInParent<PlayerController>();
            player = playerScript != null ? playerScript.gameObject : tagged;
        }
        else
        {
            Debug.LogWarning("GameManager: nothing tagged Player in the scene", this);
        }

        if (player != null)
        {
            PlayerReady?.Invoke();
        }

    }

    private void OnEnable() => EnemyEvents.Killed += handleKill;
    private void OnDisable() => EnemyEvents.Killed -= handleKill;

    private void handleKill(EnemyBase enemy)
    {
        AddKill();
        AddBytes(enemy.ByteValue);
    }


    public static event System.Action PlayerReady;

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    //Currency Stuff
    public void AddBytes(int amount)
    {
        totalBytes += amount;
        //Debug.Log("Current Bytes: " + totalBytes);
    }
    public void AddFiles(int amount)
    {
        totalFiles += amount;
        //Debug.Log("Current Files: " + totalFiles);
    }
    public void SubtractBytes(int amount)
    {
        totalBytes -= amount;
    }
    public void SubtractFiles(int amount)
    {
        totalFiles -= amount;
    }
    void Update()
    {
        bytesText.text = "Bytes: " + totalBytes.ToString();

        if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (AudioManager.instance != null)
                AudioManager.instance.PlayButtonClick();
            if (menuActive == null)
            {
                StatePause();
                menuActive = menuPause;
                menuActive.SetActive(true);
            }
            else if (menuActive == menuPause)
            {
                StateUnpause();
            }
        }


        UpdateUI();

        if (WeaponManager.instance != null && WeaponManager.instance.activeWeapon != null)
            magAmmoUI.text = WeaponManager.instance.CurrentAmmo.ToString();
    }

    // Pause the game
    public void StatePause()
    {
        isPaused = true;
        TimeManager.instance.PauseTime();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        pauseScoreText.text = currentKill.ToString("f0");
        ResetPauseUI();
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PauseMusic();
            AudioManager.instance.PlayPauseMenuMusicWithDelay(4.0f);
        }
    }

    public void ResetPauseUI()
    {
        if (challengesCanvas != null)
            challengesCanvas.SetActive(false);
        if (settingsCanvas != null)
            settingsCanvas.SetActive(false);
        if (upgradesCanvas != null)
            upgradesCanvas.SetActive(false);
        if (soundMenu != null)
            soundMenu.SetActive(false);
        if (controlsMenu != null)
            controlsMenu.SetActive(false);
        if (backButton != null)
            backButton.SetActive(false);
        if (navTab != null)
            navTab.SetActive(false);
        if (buttons != null)
            buttons.SetActive(true);
        if (pauseScorePanel != null)
            pauseScorePanel.SetActive(true);
    }

    // Unpause the game
    public void StateUnpause()
    {
        isPaused = false;
        if (TimeManager.instance != null)
            TimeManager.instance.UnpauseTime();
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        if (menuActive != null)
        {
            menuActive.SetActive(false);
            menuActive = null;
        }
        if (AudioManager.instance != null)
            AudioManager.instance.RestoreGameplayMusic();
    }

    // Handle the lose state
    public void StateLose()
    {

        EndRun(menuLose);
    }

    //Handes the win state aka when the boss dies
    public void StateWin()
    {
        EndRun(menuWin);
    }

    // Simple method so simplify states
    void EndRun(GameObject endMenu)
    {
        StatePause();

        if (menuActive != null && menuActive != endMenu)
        {
            menuActive.SetActive(false);
        }

        if (menuPause != null && menuPause != endMenu)
        {
            menuPause.SetActive(false);
        }

        menuActive = endMenu;

        if (menuActive != null)
        {
            menuActive.SetActive(true);
        }

        if (loseScoreText != null)
        {
            loseScoreText.text = currentKill.ToString("f0");
        }
        if (UpgradeManager.instance != null)
        {
            UpgradeManager.instance.files += totalFiles;
            UpgradeManager.instance.SaveUpgrades();
        }

        // lose screen gets its own music cue
        if (endMenu == menuLose && AudioManager.instance != null)
        {
            AudioManager.instance.PlayLoseMenuMusic();
        }
    }
    public void AddKill()
    {
        currentKill++;
    }

    void UpdateUI()
    {
        if (WaveManager.instance == null)
            return;

        if (waveCounter != null)
            waveCounter.text = WaveManager.instance.CurrentWave.ToString("f0");
        if (killCounter != null)
            killCounter.text = "Kills: " + WaveManager.instance.EnemiesKilled;

        if (WaveManager.instance.IsWaitingForNextWave)
        {
            int secondsLeft = WaveManager.instance.SecondsUntilNextWave;

            if (waveCountdownText != null)
            {
                waveCountdownText.gameObject.SetActive(true);
                waveCountdown.text = "" + secondsLeft;
                StartCoroutine(AnimateWaveText());
            }
        }
        else
        {
            if (waveCountdown != null)
            {
                waveCountdownText.gameObject.SetActive(false);
            }
        }
    }

    public IEnumerator WarningText()
    {
        if (shopMessage != null)
            shopMessage.SetActive(true);
        yield return new WaitForSecondsRealtime(5);
        if (shopMessage != null)
            shopMessage.SetActive(false);
    }

    public void ShowShopWarning()
    {
        StopCoroutine(nameof(WarningText));
        StartCoroutine(WarningText());
    }

    IEnumerator AnimateWaveText()
    {
        RectTransform rect = waveCountdown.rectTransform;
        Vector3 originalScale = Vector3.one;
        float duration = .1f;
        float timer = 0f;
        rect.localScale = originalScale * 1.3f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / duration);
            rect.localScale = Vector3.Lerp(originalScale * 1.3f, originalScale, t);
            yield return null;
        }

        rect.localScale = originalScale;
    }

}
