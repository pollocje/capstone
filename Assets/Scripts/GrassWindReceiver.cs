using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class GrassWindReceiver : MonoBehaviour
{
    [Tooltip("BendSTR used when no WindZone is affecting this grass.")]
    public float ambientBendStr = 1.5f;

    [Tooltip("VertBendSTR used when no WindZone is affecting this grass.")]
    public float ambientVertBendStr = -1f;

    [Tooltip("Multiplies a WindZone's effective speed into the shader's BendSTR property.")]
    public float bendMultiplier = 1f;

    [Tooltip("Multiplies a WindZone's effective speed into the shader's VertBendSTR property.")]
    public float vertBendMultiplier = -0.5f;

    static readonly int BendStrID = Shader.PropertyToID("_BendSTR");
    static readonly int VertBendStrID = Shader.PropertyToID("_VertBendSTR");

    Renderer rend;
    MaterialPropertyBlock block;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
    }

    void Update()
    {
        float bendStr = ambientBendStr;
        float vertBendStr = ambientVertBendStr;

        if (WindSampler.TryGetWindAt(transform.position, out float zoneSpeed, out _))
        {
            bendStr = Mathf.Max(ambientBendStr, zoneSpeed * bendMultiplier);
            vertBendStr = Mathf.Min(ambientVertBendStr, zoneSpeed * vertBendMultiplier);
        }

        rend.GetPropertyBlock(block);
        block.SetFloat(BendStrID, bendStr);
        block.SetFloat(VertBendStrID, vertBendStr);
        rend.SetPropertyBlock(block);
    }
}
