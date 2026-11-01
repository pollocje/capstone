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
    // Optional: shows "Saved!" after saving.
    [SerializeField] TextMeshProUGUI statusText;

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

        if (statusText != null) statusText.text = "Saved!";
    }
}
