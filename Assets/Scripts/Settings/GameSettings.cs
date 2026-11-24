using UnityEngine;
using UnityEngine.InputSystem;

// Saves and loads the settings menu options with PlayerPrefs
public static class GameSettings
{
    // Window modes in the order the button cycles through them
    public static FullScreenMode[] displayModes = { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };
    public static string[] displayModeNames = { "Borderless", "Fullscreen", "Windowed" };

    public static float MouseSensitivity => PlayerPrefs.GetFloat("mouseSensitivity", 1f);

    // Runs once when the game starts, before the first scene loads
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadOnStart()
    {
        AudioListener.volume = PlayerPrefs.GetFloat("volume", 1f);

        if (PlayerPrefs.HasKey("vsync"))
            QualitySettings.vSyncCount = PlayerPrefs.GetInt("vsync");

        // Only change the window if the player picked something before
        if (PlayerPrefs.HasKey("resWidth"))
        {
            FullScreenMode mode = (FullScreenMode)PlayerPrefs.GetInt("displayMode");
            Screen.SetResolution(PlayerPrefs.GetInt("resWidth"), PlayerPrefs.GetInt("resHeight"), mode);
        }
    }

    public static void SaveDisplay(FullScreenMode mode, int width, int height)
    {
        Screen.SetResolution(width, height, mode);
        PlayerPrefs.SetInt("displayMode", (int)mode);
        PlayerPrefs.SetInt("resWidth", width);
        PlayerPrefs.SetInt("resHeight", height);
        PlayerPrefs.Save();
    }

    public static void SaveVSync(bool on)
    {
        QualitySettings.vSyncCount = on ? 1 : 0;
        PlayerPrefs.SetInt("vsync", QualitySettings.vSyncCount);
        PlayerPrefs.Save();
    }

    public static void SaveVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("volume", volume);
        PlayerPrefs.Save();
    }

    public static void SaveMouseSensitivity(float value)
    {
        PlayerPrefs.SetFloat("mouseSensitivity", value);
        PlayerPrefs.Save();
    }

    // Keybinds are saved as the input system's "override" text
    public static void SaveKeybinds(InputActionAsset actions)
    {
        PlayerPrefs.SetString("keybinds", actions.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    public static void LoadKeybinds(InputActionAsset actions)
    {
        actions.RemoveAllBindingOverrides();
        string saved = PlayerPrefs.GetString("keybinds", "");
        if (saved != "") actions.LoadBindingOverridesFromJson(saved);
    }
}
