using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Settings menu: lets the player type and save a nickname.
/// Put this on the settings panel and assign a TextMeshPro Input Field.
/// Hook the input field's On End Edit, or a Save button's On Click, to Save().
///
/// Optional confirmation popup: a panel (with a Canvas Group) that fades and pops in,
/// stays for a moment, then fades out.
/// </summary>
public class NicknameSettings : MonoBehaviour
{
    [SerializeField] TMP_InputField nameInput;

    [Header("Confirmation Popup")]
    // The popup panel. Needs a Canvas Group component (used to fade it).
    [SerializeField] CanvasGroup popup;
    // The text inside the popup.
    [SerializeField] TextMeshProUGUI statusText;
    // How long the popup stays fully visible (seconds).
    [SerializeField] float messageDuration = 2f;
    // How long fading in / out takes (seconds).
    [SerializeField] float fadeTime = 0.25f;
    // Starting size of the "pop" (1 = no pop). 0.85 = grows from 85% to full size.
    [SerializeField] float popStartScale = 0.85f;

    [Header("Optional Sound")]
    [SerializeField] AudioSource popupSound;

    Vector3 _popupScale = Vector3.one;

    void Awake()
    {
        if (popup != null) _popupScale = popup.transform.localScale;
    }

    void OnEnable()
    {
        // Fill in the current name whenever the settings menu opens.
        nameInput.characterLimit = PlayerNameStore.MaxLength;
        nameInput.text = PlayerNameStore.Get();

        HidePopupInstantly();
    }

    public void Save()
    {
        string saved = PlayerNameStore.Set(nameInput.text);
        nameInput.text = saved;

        if (statusText != null)
            statusText.text = $"Nickname set to <b>{saved}</b>";

        if (popupSound != null) popupSound.Play();

        // Restart the animation if Save is pressed again.
        StopAllCoroutines();
        if (popup != null) StartCoroutine(ShowPopup());
    }

    IEnumerator ShowPopup()
    {
        // Fade + pop in.
        yield return Animate(0f, 1f, popStartScale, 1f);

        // Stay visible. Realtime so it still works if the game is paused.
        yield return new WaitForSecondsRealtime(messageDuration);

        // Fade out (no pop).
        yield return Animate(1f, 0f, 1f, 1f);
    }

    IEnumerator Animate(float fromAlpha, float toAlpha, float fromScale, float toScale)
    {
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            // Ease-out curve: fast start, gentle finish.
            float p = 1f - Mathf.Pow(1f - t / fadeTime, 3f);
            popup.alpha = Mathf.Lerp(fromAlpha, toAlpha, p);
            popup.transform.localScale = _popupScale * Mathf.Lerp(fromScale, toScale, p);
            yield return null;
        }

        popup.alpha = toAlpha;
        popup.transform.localScale = _popupScale * toScale;
    }

    void HidePopupInstantly()
    {
        StopAllCoroutines();
        if (popup != null)
        {
            popup.alpha = 0f;
            popup.transform.localScale = _popupScale;
            popup.blocksRaycasts = false; // never blocks clicks on the menu
        }
        if (statusText != null) statusText.text = "";
    }
}