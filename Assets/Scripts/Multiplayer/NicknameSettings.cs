using TMPro;
using UnityEngine;

/// <summary>
/// Settings menu: lets the player type and save a nickname.
/// Put this on the settings panel and assign a TextMeshPro Input Field.
/// Hook the input field's On End Edit, or a Save button's On Click, to Save().
/// </summary>
public class NicknameSettings : MonoBehaviour
{
    [SerializeField] TMP_InputField nameInput;
    // Optional: shows a confirmation message after saving.
    [SerializeField] TextMeshProUGUI statusText;
    // How long the confirmation stays on screen (seconds). 0 = stays until the menu closes.
    [SerializeField] float messageDuration = 3f;

    void OnEnable()
    {
        // Fill in the current name whenever the settings menu opens.
        nameInput.characterLimit = PlayerNameStore.MaxLength;
        nameInput.text = PlayerNameStore.Get();
        if (statusText != null) statusText.text = "";
    }

    public void Save()
    {
        string saved = PlayerNameStore.Set(nameInput.text);
        nameInput.text = saved;

        if (statusText != null)
        {
            statusText.text = $"Nickname set to {saved}";

            // Restart the timer if Save is pressed again.
            StopAllCoroutines();
            if (messageDuration > 0f) StartCoroutine(ClearMessageAfterDelay());
        }
    }

    System.Collections.IEnumerator ClearMessageAfterDelay()
    {
        // Realtime so it still clears if the game is paused.
        yield return new WaitForSecondsRealtime(messageDuration);
        if (statusText != null) statusText.text = "";
    }
}
