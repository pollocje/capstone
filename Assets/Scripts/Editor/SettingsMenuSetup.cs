using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Tools > Menus > Set Up Settings Menu
// Builds the settings submenu (nickname / display / audio / controls) in MainMenu
// Every button is a copy of Create Game so they all match
// Safe to re-run, it deletes the old copy first and puts the nickname stuff back
public static class SettingsMenuSetup
{
    const string ScenePath = "Assets/Scenes/Prototype/Menus/MainMenu.unity";
    const string ActionsPath = "Assets/StarterAssets/InputSystem/StarterAssets.inputactions";

    // Positions are from the middle of the SETTINGS panel
    const float SubmenuX = -60f;    // Same column as create / join game
    const float PageX = 644f;       // Column to the right of the submenu
    const float LeftColumnX = 446f; // Two columns for keybinds
    const float RightColumnX = 841f;
    const float NicknameShiftX = 713f;
    static readonly float[] Rows = { 355f, 271f, 187f, 103f, 19f, -65f };

    const float ButtonHeight = 77.3f;
    const float FullWidth = 757f;
    const float HalfWidth = 374f;

    static GameObject template;
    static Color outlineColor;

    [MenuItem("Tools/Menus/Set Up Settings Menu")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Transform settings = Find("SETTINGS");
        template = Find("Btn_CreateGame").gameObject;
        outlineColor = template.GetComponent<Image>().color;
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);

        Cleanup(settings);

        var menu = settings.GetComponent<SettingsMenu>();
        if (menu == null) menu = settings.gameObject.AddComponent<SettingsMenu>();
        menu.actions = actions;

        // Pages
        Transform nicknamePage = MakePage("Page_Nickname", settings, NicknameShiftX);
        Transform displayPage = MakePage("Page_Display", settings, 0);
        Transform audioPage = MakePage("Page_Audio", settings, 0);
        Transform controlsPage = MakePage("Page_Controls", settings, 0);
        menu.pages = new GameObject[] { nicknamePage.gameObject, displayPage.gameObject, audioPage.gameObject, controlsPage.gameObject };

        // The nickname box moves into its page as is
        foreach (string name in new[] { "BTN_SetNickname", "VerticalLayout", "NIcknamePopup" })
            settings.Find(name).SetParent(nicknamePage, false);

        // Submenu buttons
        Transform submenu = MakePage("SettingsButtons", settings, 0);
        string[] names = { "Nickname", "Display", "Audio", "Controls" };
        for (int i = 0; i < names.Length; i++)
        {
            Button b = MakeButton(names[i], submenu, SubmenuX, Rows[i + 2], 522f);
            UnityEventTools.AddIntPersistentListener(b.onClick, menu.ShowPage, i);
        }

        // Display page
        Button mode = MakeButton("Display Mode: Borderless", displayPage, PageX, Rows[2], FullWidth);
        UnityEventTools.AddVoidPersistentListener(mode.onClick, menu.NextDisplayMode);
        menu.displayModeText = Text(mode);

        Button res = MakeButton("Resolution: 1920 x 1080", displayPage, PageX, Rows[3], FullWidth);
        UnityEventTools.AddVoidPersistentListener(res.onClick, menu.NextResolution);
        menu.resolutionText = Text(res);

        Button vsync = MakeButton("VSync: On", displayPage, PageX, Rows[4], FullWidth);
        UnityEventTools.AddVoidPersistentListener(vsync.onClick, menu.ToggleVSync);
        menu.vsyncText = Text(vsync);

        // Audio page
        menu.volumeSlider = MakeSliderRow("Volume: 100%", audioPage, Rows[2], 0f, 1f, out menu.volumeText);
        UnityEventTools.AddPersistentListener(menu.volumeSlider.onValueChanged, menu.SetVolume);

        // Controls page
        menu.sensitivitySlider = MakeSliderRow("Mouse Sensitivity: 1.0", controlsPage, Rows[0], 0.2f, 3f, out menu.sensitivityText);
        UnityEventTools.AddPersistentListener(menu.sensitivitySlider.onValueChanged, menu.SetSensitivity);

        menu.keybindButtons = new KeybindButton[]
        {
            MakeKeybind("Forward", "Move", "up", controlsPage, LeftColumnX, Rows[1], actions),
            MakeKeybind("Back", "Move", "down", controlsPage, LeftColumnX, Rows[2], actions),
            MakeKeybind("Left", "Move", "left", controlsPage, LeftColumnX, Rows[3], actions),
            MakeKeybind("Right", "Move", "right", controlsPage, LeftColumnX, Rows[4], actions),
            MakeKeybind("Jump", "Jump", "", controlsPage, RightColumnX, Rows[1], actions),
            MakeKeybind("Sprint", "Sprint", "", controlsPage, RightColumnX, Rows[2], actions),
            MakeKeybind("Interact", "Interact", "", controlsPage, RightColumnX, Rows[3], actions),
            MakeKeybind("Throw", "Throw", "", controlsPage, RightColumnX, Rows[4], actions),
        };

        Button reset = MakeButton("Reset Keybinds", controlsPage, PageX, Rows[5], FullWidth);
        UnityEventTools.AddVoidPersistentListener(reset.onClick, menu.ResetKeybinds);

        // Only the nickname page shows at first
        displayPage.gameObject.SetActive(false);
        audioPage.gameObject.SetActive(false);
        controlsPage.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SettingsMenuSetup] settings submenu built");
    }

    // Undo a previous run
    static void Cleanup(Transform settings)
    {
        Transform oldNickname = settings.Find("Page_Nickname");
        if (oldNickname != null)
        {
            while (oldNickname.childCount > 0)
                oldNickname.GetChild(0).SetParent(settings, false);
        }

        foreach (string name in new[] { "Page_Nickname", "Page_Display", "Page_Audio", "Page_Controls", "SettingsButtons" })
        {
            Transform old = settings.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
    }

    // An empty holder the same size as SETTINGS so things keep their positions
    static Transform MakePage(string name, Transform settings, float x)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = settings.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(settings, false);
        rect.sizeDelta = ((RectTransform)settings).sizeDelta;
        rect.anchoredPosition = new Vector2(x, 0);
        return rect;
    }

    static Button MakeButton(string label, Transform parent, float x, float y, float width)
    {
        GameObject copy = Object.Instantiate(template, parent);
        copy.name = "Btn_" + label.Split(':')[0].Replace(" ", "");

        var rect = (RectTransform)copy.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, ButtonHeight);
        rect.localScale = Vector3.one * 1.03f; // Create game sits in a 1.03 scaled layout

        // Clear the create game click and keep the hover sound
        Button button = copy.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();

        Text(button).text = label;
        return button;
    }

    static TMP_Text Text(Button button)
    {
        return button.GetComponentInChildren<TMP_Text>(true);
    }

    // A button outline with a label on the left and a slider on the right
    static Slider MakeSliderRow(string label, Transform parent, float y, float min, float max, out TMP_Text text)
    {
        Button row = MakeButton(label, parent, PageX, y, FullWidth);
        text = Text(row);
        row.name = "Row_" + label.Split(':')[0].Replace(" ", "");
        Object.DestroyImmediate(row.GetComponent<UnityEngine.EventSystems.EventTrigger>());
        Object.DestroyImmediate(row); // Just the look, not clickable

        var resources = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        };
        GameObject sliderObject = DefaultControls.CreateSlider(resources);
        sliderObject.layer = parent.gameObject.layer;
        foreach (Transform child in sliderObject.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = parent.gameObject.layer;

        var rect = (RectTransform)sliderObject.transform;
        rect.SetParent(text.transform.parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-25f, 0f);
        rect.sizeDelta = new Vector2(300f, 20f);

        sliderObject.transform.Find("Background").GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        sliderObject.transform.Find("Fill Area/Fill").GetComponent<Image>().color = outlineColor;
        ((RectTransform)sliderObject.transform.Find("Handle Slide Area/Handle")).sizeDelta = new Vector2(30f, 0f);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = max > 1f ? 1f : max;
        return slider;
    }

    static KeybindButton MakeKeybind(string label, string action, string direction, Transform parent, float x, float y, InputActionAsset actions)
    {
        Button button = MakeButton(label + ": -", parent, x, y, HalfWidth);
        button.name = "Key_" + label;

        KeybindButton key = button.gameObject.AddComponent<KeybindButton>();
        key.actions = actions;
        key.label = label;
        key.actionName = action;
        key.moveDirection = direction;
        key.text = Text(button);

        UnityEventTools.AddVoidPersistentListener(button.onClick, key.StartRebind);
        return key;
    }

    static Transform Find(string name)
    {
        foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t;
        return null;
    }
}
