using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps menu music playing seamlessly between the listed scenes,
// and stops it once the player enters any other scene (e.g. gameplay).
[RequireComponent(typeof(AudioSource))]
public class MenuMusic : MonoBehaviour
{
    // Must match your scene names exactly (as shown in File > Build Settings)
    [SerializeField] private string[] scenesWithMusic = { "Lobby", "JoinMenu" };

    private static MenuMusic instance;

    void Awake()
    {
        // If music is already playing from a previous scene, remove this duplicate
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject); // survive scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;
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
        // Stop the music when we leave the menu scenes
        if (System.Array.IndexOf(scenesWithMusic, scene.name) < 0)
        {
            Destroy(gameObject);
        }
    }
}