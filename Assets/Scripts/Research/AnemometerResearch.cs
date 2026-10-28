using UnityEngine;
using UnityEngine.UI;

/// <summary>While the anemometer is equipped, fills a detection bar as long as the wind speed at
/// the player's position stays above windThreshold, then reports a reading to ResearchManager.
/// Resets (and can fire again) if the wind drops below the threshold before completing, same
/// pattern as BinocularScanner/Scannable but for a sustained reading instead of a fixed target.</summary>
[RequireComponent(typeof(AnemometerItem))]
public class AnemometerResearch : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Wind speed (m/s) that must be sustained to register a reading.")]
    public float windThreshold = 15f;
    [Tooltip("Seconds of continuous high wind needed to complete a reading.")]
    public float detectDuration = 3f;
    [Tooltip("Research % added to the bar each time a reading completes.")]
    public float researchValue = 5f;

    [Header("UI")]
    [Tooltip("Fillable Image (Filled type) showing detection progress.")]
    public Image detectProgressBar;
    [Tooltip("Parent object toggled on while wind is above threshold.")]
    public GameObject detectProgressUI;

    private AnemometerItem _anemometer;
    private float _progress;
    private bool _uiShown;

    void Awake()
    {
        _anemometer = GetComponent<AnemometerItem>();
    }

    void Start()
    {
        if (detectProgressUI != null)
            detectProgressUI.SetActive(false);
    }

    void Update()
    {
        if (!_anemometer.IsEquipped || (ResearchManager.Instance != null && ResearchManager.Instance.IsComplete))
        {
            ClearProgress();
            return;
        }

        WindSampler.TryGetWindAt(transform.position, out float speed, out _);

        if (speed < windThreshold)
        {
            ClearProgress();
            return;
        }

        ShowUI();

        _progress += Time.deltaTime / detectDuration;

        if (detectProgressBar != null)
            detectProgressBar.fillAmount = Mathf.Clamp01(_progress);

        if (_progress >= 1f)
        {
            ResearchManager.Instance?.AddResearch(researchValue);
            _progress = 0f; // wind is still above threshold, so let it start building again
        }
    }

    void ClearProgress()
    {
        _progress = 0f;
        HideUI();
    }

    // detectProgressUI/detectProgressBar are shared with BinocularScanner's UI, so only
    // SetActive(false) when we're the one who turned it on - otherwise we'd hide the
    // other tool's progress bar out from under it.
    void ShowUI()
    {
        if (detectProgressUI != null) detectProgressUI.SetActive(true);
        _uiShown = true;
    }

    void HideUI()
    {
        if (_uiShown && detectProgressUI != null) detectProgressUI.SetActive(false);
        _uiShown = false;
    }
}
