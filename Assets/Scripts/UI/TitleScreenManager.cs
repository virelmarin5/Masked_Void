using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/*
 * Script: TitleScreenManager
 *
 * Description:
 * The title screen and level select. Each level button records which level the
 * player picked, then loads Bootstrap, which brings that level in on top.
 *
 * Responsibilities:
 * - Switch between the home, weapon, challenge, settings, about and credits pages
 * - Set LevelLoader.requestedLevel and load Bootstrap behind a progress bar
 * - Quit the game
 *
 * Interacts With:
 * - LevelLoader (sets requestedLevel before Bootstrap exists)
 * - AudioManager (title music, button clicks)
 * - Bootstrap.unity
 *
 * Notes:
 * - Every public method here is wired to a Button in the inspector, so the
 *   names are stored as strings in the prefab. Renaming one breaks its button
 *   silently, with no compile error.
 * - The six openLevel methods are near identical and differ only by scene name.
 *   Worth collapsing into one that takes a parameter, but that means rewiring
 *   six buttons by hand.
 */
public class TitleScreenManager : MonoBehaviour
{
    [Header("UI Pages")]
    [Tooltip("landing page with the level select buttons")]
    public GameObject homePanel;

    [Tooltip("weapon list and their challenges")]
    public GameObject weaponPanel;

    [Tooltip("challenge progress page")]
    public GameObject challengePanel;

    [Tooltip("settings page, holds the sound and controls sub menus")]
    public GameObject settingsPanel;

    [Tooltip("about the game")]
    public GameObject aboutPanel;

    [Tooltip("credits page")]
    public GameObject creditsPanel;

    [Header("Top Navigation Buttons")]
    [Tooltip("the tab row across the top, hidden while a level loads")]
    [FormerlySerializedAs("Nav")]
    public GameObject nav;

    [Tooltip("tab that opens homePanel")]
    public Button navHomeButton;

    [Tooltip("tab that opens weaponPanel")]
    public Button navWeaponButton;

    [Tooltip("tab that opens settingsPanel")]
    public Button navSettingsButton;

    [Tooltip("tab that opens aboutPanel")]
    public Button navAboutButton;

    [Tooltip("tab that opens creditsPanel")]
    public Button navCreditsButton;

    [Header("Menus")]
    [Tooltip("root of the whole title menu, hidden during the load screen")]
    [SerializeField] private GameObject titleMenuPanel;

    [Tooltip("volume sliders sub menu")]
    [FormerlySerializedAs("soundMenu")]
    [SerializeField] private GameObject soundMenu;

    [Tooltip("controls reference sub menu")]
    [SerializeField] private GameObject controlsMenu;

    [Header("Loading")]
    [Tooltip("fills 0 to 1 while Bootstrap loads, then the level comes in on top")]
    [SerializeField] private Slider progressBar;

    void Start()
    {
        Time.timeScale = 1f;
        switchToHome();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        AudioManager.instance.PlayTitleScreenSound();
    }

    public void openLevelSamuel()
    {
        AudioManager.instance.PlayButtonClick();
        nav.SetActive(false);
        deactivateAllPanels();

        LevelLoader.requestedLevelName = "MK2";
        StartCoroutine(LoadSceneAsync("Bootstrap"));
    }
    public void openLevelDevinS()
    {
        AudioManager.instance.PlayButtonClick();
        nav.SetActive(false);
        deactivateAllPanels();

        LevelLoader.requestedLevelName = "Devin";
        StartCoroutine(LoadSceneAsync("Bootstrap"));
    }
    public void openLevelDevinC()
    {
        AudioManager.instance.PlayButtonClick();
        nav.SetActive(false);
        deactivateAllPanels();

        LevelLoader.requestedLevelName = "dclevel";
        StartCoroutine(LoadSceneAsync("Bootstrap"));
    }
    public void openLevelMark()
    {
        AudioManager.instance.PlayButtonClick();
        nav.SetActive(false);
        deactivateAllPanels();

        LevelLoader.requestedLevelName = "Mark";
        StartCoroutine(LoadSceneAsync("Bootstrap"));
    }
    public void openLevelKhurshed()
    {
        AudioManager.instance.PlayButtonClick();
        nav.SetActive(false);
        deactivateAllPanels();

        LevelLoader.requestedLevelName = "ColdStorage";
        StartCoroutine(LoadSceneAsync("Bootstrap"));
    }
    public void openLevelVirel()
    {
        AudioManager.instance.PlayButtonClick();
        nav.SetActive(false);
        deactivateAllPanels();

        LevelLoader.requestedLevelName = "LevelCreation-Virel";
        StartCoroutine(LoadSceneAsync("Bootstrap"));
    }

    public void openSettings()
    {
        AudioManager.instance.PlayButtonClick();
        deactivateAllSettings();
        soundMenu.SetActive(true);
    }

    public void controls()
    {
        AudioManager.instance.PlayButtonClick();
        deactivateAllSettings();
        controlsMenu.SetActive(true);
    }

    private IEnumerator LoadSceneAsync(String levelName)
    {
        deactivateAllSettings();
        deactivateAllPanels();

        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(true);
            progressBar.value = 0f;
        }

        AsyncOperation scene = SceneManager.LoadSceneAsync(levelName);
        scene.allowSceneActivation = false;

        while (scene.progress < 0.9f)
        {
            float progressValue = Mathf.Clamp01(scene.progress / 0.9f);

            if (progressBar != null)
            {
                progressBar.value = progressValue;
            }

            yield return null;
        }

        if (progressBar != null)
        {
            progressBar.value = 1f;
        }

        yield return new WaitForSecondsRealtime(0.2f);

        if (AudioManager.instance != null)
            AudioManager.instance.StopMusic();

        scene.allowSceneActivation = true;
    }

    public void quitGame()
    {
        AudioManager.instance.PlayButtonClick();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            Application.OpenURL("about:blank");
#else
            Application.Quit();
#endif
    }

    public void switchToHome()
    {
        AudioManager.instance.PlayButtonClick();
        deactivateAllPanels();
        homePanel.SetActive(true);
    }
    public void switchToChallenge()
    {
        deactivateAllPanels();
        challengePanel.SetActive(true);
    }
    public void switchToWeapon()
    {
        deactivateAllPanels();
        weaponPanel.SetActive(true);
    }

    public void switchToSettings()
    {
        AudioManager.instance.PlayButtonClick();
        deactivateAllPanels();
        settingsPanel.SetActive(true);
    }

    public void switchToAbout()
    {
        AudioManager.instance.PlayButtonClick();
        deactivateAllPanels();
        aboutPanel.SetActive(true);
    }

    public void switchToCredits()
    {
        AudioManager.instance.PlayButtonClick();
        deactivateAllPanels();

        if (creditsPanel != null)
        {
            creditsPanel.SetActive(true);
        }
    }

    private void deactivateAllPanels()
    {
        homePanel.SetActive(false);
        weaponPanel.SetActive(false);
        settingsPanel.SetActive(false);
        challengePanel.SetActive(false);

        if (aboutPanel != null)
        {
            aboutPanel.SetActive(false);
        }

        if (creditsPanel != null)
        {
            creditsPanel.SetActive(false);
        }
    }

    private void deactivateAllSettings()
    {
        soundMenu.SetActive(false);
        controlsMenu.SetActive(false);
    }
}
