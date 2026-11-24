using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Dissolves this object away using NadoShader's existing "_AlphaClipping" input —
/// ramping the alpha-clip threshold up over time lets the shader's own swirl texture eat the
/// mesh away instead of a flat fade. Call <see cref="Dissolve"/> (e.g. from
/// ResearchManager.onResearchComplete) to start it.</summary>
public class TornadoDissolve : MonoBehaviour
{
    public enum EndBehavior { Deactivate, Destroy, None }

    [Tooltip("Shader float property driving the dissolve (NadoShader's Alpha Clip Threshold input).")]
    [SerializeField] private string alphaClipProperty = "_AlphaClipping";

    [Tooltip("Seconds for the dissolve to go from fully visible to fully clipped.")]
    [SerializeField] private float dissolveDuration = 4f;

    [SerializeField] private EndBehavior endBehavior = EndBehavior.Deactivate;

    [Tooltip("Seconds for the reverse Appear() dissolve-in to go from fully clipped to fully visible.")]
    [SerializeField] private float appearDuration = 4f;

    public UnityEvent onDissolveStarted;
    public UnityEvent onDissolveComplete;
    public UnityEvent onAppearStarted;
    public UnityEvent onAppearComplete;

    private Renderer[] _renderers;
    private ParticleSystem[] _particleSystems;
    private MaterialPropertyBlock _block;
    private Coroutine _running;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        _block = new MaterialPropertyBlock();
    }

    /// <summary>Starts the dissolve. Safe to wire directly to a UnityEvent.</summary>
    public void Dissolve()
    {
        if (_running != null) return;
        _running = StartCoroutine(DissolveRoutine());
    }

    /// <summary>Restores full visibility — call before reactivating/reusing the tornado.</summary>
    public void ResetDissolve()
    {
        if (_running != null)
        {
            StopCoroutine(_running);
            _running = null;
        }

        SetClipThreshold(0f);
        foreach (var ps in _particleSystems)
            ps.Play(true);
    }

    /// <summary>Reverse of <see cref="Dissolve"/> — starts fully clipped (invisible) and ramps
    /// down to fully visible, so the tornado materializes out of its own swirl texture. Call this
    /// right after spawning the tornado, before anyone's had a frame to see it unclipped.</summary>
    public void Appear()
    {
        if (_running != null) return;
        gameObject.SetActive(true);
        SetClipThreshold(1f);
        _running = StartCoroutine(AppearRoutine());
    }

    private IEnumerator AppearRoutine()
    {
        onAppearStarted?.Invoke();

        foreach (var ps in _particleSystems)
            ps.Play(true);

        float t = 0f;
        while (t < appearDuration)
        {
            t += Time.deltaTime;
            SetClipThreshold(1f - Mathf.Clamp01(t / appearDuration));
            yield return null;
        }

        SetClipThreshold(0f);
        onAppearComplete?.Invoke();

        _running = null;
    }

    private IEnumerator DissolveRoutine()
    {
        onDissolveStarted?.Invoke();

        foreach (var ps in _particleSystems)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        float t = 0f;
        while (t < dissolveDuration)
        {
            t += Time.deltaTime;
            SetClipThreshold(Mathf.Clamp01(t / dissolveDuration));
            yield return null;
        }

        SetClipThreshold(1f);
        onDissolveComplete?.Invoke();

        _running = null;

        switch (endBehavior)
        {
            case EndBehavior.Deactivate:
                gameObject.SetActive(false);
                break;
            case EndBehavior.Destroy:
                Destroy(gameObject);
                break;
        }
    }

    private void SetClipThreshold(float value)
    {
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_block);
            _block.SetFloat(alphaClipProperty, value);
            r.SetPropertyBlock(_block);
        }
    }
}
