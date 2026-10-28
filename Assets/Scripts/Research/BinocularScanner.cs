using UnityEngine;
using UnityEngine.UI;

/// <summary>While looking through the binoculars, fills a scan bar on whatever
/// Scannable is centered in view, then reports the result to ResearchManager.</summary>
[RequireComponent(typeof(Binoculars))]
public class BinocularScanner : MonoBehaviour
{
    [Header("Dependencies")]
    public Camera scanCamera;
    [Tooltip("Fillable Image (Filled type) showing scan progress.")]
    public Image scanProgressBar;
    [Tooltip("Parent object toggled on while a valid target is in view.")]
    public GameObject scanProgressUI;

    [Header("Settings")]
    public float scanRange = 200f;
    public LayerMask scanMask = ~0;

    private Binoculars _binoculars;
    private Scannable _currentTarget;
    private float _progress;
    private bool _uiShown;

    void Awake()
    {
        _binoculars = GetComponent<Binoculars>();
    }

    void Start()
    {
        if (scanCamera == null)
            scanCamera = Camera.main;

        if (scanProgressUI != null)
            scanProgressUI.SetActive(false);
    }

    void Update()
    {
        if (!_binoculars.IsUsing || (ResearchManager.Instance != null && ResearchManager.Instance.IsComplete))
        {
            ClearTarget();
            return;
        }

        Scannable target = FindTarget();

        if (target != _currentTarget)
        {
            _currentTarget = target;
            _progress = 0f;
        }

        if (_currentTarget == null || _currentTarget.IsScanned)
        {
            HideUI();
            return;
        }

        ShowUI();

        _progress += Time.deltaTime / _currentTarget.scanDuration;

        if (scanProgressBar != null)
            scanProgressBar.fillAmount = Mathf.Clamp01(_progress);

        if (_progress >= 1f)
        {
            _currentTarget.MarkScanned();
            ResearchManager.Instance?.AddResearch(_currentTarget.researchValue);
            ClearTarget();
        }
    }

    Scannable FindTarget()
    {
        if (scanCamera == null) return null;

        if (Physics.Raycast(scanCamera.transform.position, scanCamera.transform.forward, out RaycastHit hit, scanRange, scanMask))
            return hit.collider.GetComponentInParent<Scannable>();

        return null;
    }

    void ClearTarget()
    {
        _currentTarget = null;
        _progress = 0f;
        HideUI();
    }

    // scanProgressUI/scanProgressBar are shared with AnemometerResearch's UI, so only
    // SetActive(false) when we're the one who turned it on - otherwise we'd hide the
    // other tool's progress bar out from under it.
    void ShowUI()
    {
        if (scanProgressUI != null) scanProgressUI.SetActive(true);
        _uiShown = true;
    }

    void HideUI()
    {
        if (_uiShown && scanProgressUI != null) scanProgressUI.SetActive(false);
        _uiShown = false;
    }
}
