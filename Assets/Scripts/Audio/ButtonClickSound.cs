using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Plays a click sound whenever any UI Button is clicked.
// Automatically hooks up every Button in each scene that loads,
// so you don't have to set anything up on individual buttons.
[RequireComponent(typeof(AudioSource))]
public class ButtonClickSound : MonoBehaviour
{
    [SerializeField] private AudioClip clickSound;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    private static ButtonClickSound instance;
    private AudioSource source;

    void Awake()
    {
        // Only keep one click-sound player across all scenes
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject); // so the click isn't cut off when a button changes scene

        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;

        SceneManager.sceneLoaded += OnSceneLoaded;
        HookUpButtons(); // buttons in the scene we started in
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HookUpButtons();
    }

    void HookUpButtons()
    {
        // Includes inactive buttons (e.g. panels that are hidden until opened)
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            button.onClick.RemoveListener(PlayClick); // avoid adding it twice
            button.onClick.AddListener(PlayClick);
        }
    }

    public void PlayClick()
    {
        if (clickSound != null)
        {
            source.PlayOneShot(clickSound, volume);
        }
    }

    // Call ButtonClickSound.Play() from other scripts,
    // e.g. for buttons you create at runtime.
    public static void Play()
    {
        if (instance != null)
        {
            instance.PlayClick();
        }
    }
}
