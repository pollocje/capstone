using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Settings submenu: nickname / display / audio / controls pages
// The buttons call these functions from their On Click in the inspector
public class SettingsMenu : MonoBehaviour
{
    // Order matches the submenu buttons: nickname, display, audio, controls
    public GameObject[] pages;

    [Header("Display")]
    public TMP_Text displayModeText;
    public TMP_Text resolutionText;
    public TMP_Text vsyncText;

    [Header("Audio")]
    public Slider volumeSlider;
    public TMP_Text volumeText;

    [Header("Controls")]
    public Slider sensitivitySlider;
    public TMP_Text sensitivityText;
    public InputActionAsset actions;
    public KeybindButton[] keybindButtons;

    List<Vector2Int> resolutions = new List<Vector2Int>();
    int modeIndex;
    int resolutionIndex;

    void OnEnable()
    {
        // Fill in the saved values every time settings opens
        GameSettings.LoadKeybinds(actions);
        FindResolutions();

        modeIndex = System.Array.IndexOf(GameSettings.displayModes, Screen.fullScreenMode);
        if (modeIndex < 0) modeIndex = 0;

        volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);

        UpdateTexts();
        ShowPage(0);
    }

    public void ShowPage(int index)
    {
        for (int i = 0; i < pages.Length; i++)
            pages[i].SetActive(i == index);
    }

    // ---- display ----

    void FindResolutions()
    {
        // The list repeats each size for every refresh rate, so only keep each size once
        resolutions.Clear();
        foreach (Resolution r in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(r.width, r.height);
            if (!resolutions.Contains(size)) resolutions.Add(size);
        }

        Vector2Int current = new Vector2Int(Screen.width, Screen.height);
        if (!resolutions.Contains(current)) resolutions.Add(current);
        resolutionIndex = resolutions.IndexOf(current);
    }

    public void NextDisplayMode()
    {
        modeIndex = (modeIndex + 1) % GameSettings.displayModes.Length;
        ApplyDisplay();
    }

    public void NextResolution()
    {
        resolutionIndex = (resolutionIndex + 1) % resolutions.Count;
        ApplyDisplay();
    }

    void ApplyDisplay()
    {
        Vector2Int size = resolutions[resolutionIndex];
        GameSettings.SaveDisplay(GameSettings.displayModes[modeIndex], size.x, size.y);
        UpdateTexts();
    }

    public void ToggleVSync()
    {
        GameSettings.SaveVSync(QualitySettings.vSyncCount == 0);
        UpdateTexts();
    }

    // ---- audio / mouse ----

    public void SetVolume(float value)
    {
        GameSettings.SaveVolume(value);
        UpdateTexts();
    }

    public void SetSensitivity(float value)
    {
        GameSettings.SaveMouseSensitivity(value);
        UpdateTexts();
    }

    // ---- keybinds ----

    public void ResetKeybinds()
    {
        actions.RemoveAllBindingOverrides();
        GameSettings.SaveKeybinds(actions);
        UpdateTexts();
    }

    public void UpdateTexts()
    {
        displayModeText.text = "Display Mode: " + GameSettings.displayModeNames[modeIndex];
        Vector2Int size = resolutions[resolutionIndex];
        resolutionText.text = "Resolution: " + size.x + " x " + size.y;
        vsyncText.text = "VSync: " + (QualitySettings.vSyncCount > 0 ? "On" : "Off");
        volumeText.text = "Volume: " + Mathf.RoundToInt(volumeSlider.value * 100) + "%";
        sensitivityText.text = "Mouse Sensitivity: " + sensitivitySlider.value.ToString("0.0");

        foreach (KeybindButton button in keybindButtons)
            button.UpdateText();
    }
}
