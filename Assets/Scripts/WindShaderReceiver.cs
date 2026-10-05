using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class WindShaderReceiver : MonoBehaviour
{
    [Tooltip("Sway speed used when no WindZone is affecting this plant.")]
    public float ambientWindSpeed = 1f;

    [Tooltip("Sway strength used when no WindZone is affecting this plant.")]
    public float ambientWindStrength = 0.03f;

    public Vector2 ambientWindDirection = new Vector2(1f, 0.3f);

    [Tooltip("Multiplies a WindZone's windMain value into the shader's Wind_Speed property.")]
    public float speedMultiplier = 0.3f;

    [Tooltip("Multiplies a WindZone's windMain value into the shader's Wind_Strength property.")]
    public float strengthMultiplier = 0.02f;

    static readonly int WindSpeedID = Shader.PropertyToID("_Wind_Speed");
    static readonly int WindStrengthID = Shader.PropertyToID("_Wind_Strength");
    static readonly int WindDirectionID = Shader.PropertyToID("_Wind_Direction");

    Renderer rend;
    MaterialPropertyBlock block;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
    }

    void Update()
    {
        float speed = ambientWindSpeed;
        float strength = ambientWindStrength;
        Vector2 direction = ambientWindDirection;

        if (WindSampler.TryGetWindAt(transform.position, out float zoneSpeed, out Vector2 zoneDirection))
        {
            speed = Mathf.Max(ambientWindSpeed, zoneSpeed * speedMultiplier);
            strength = Mathf.Max(ambientWindStrength, zoneSpeed * strengthMultiplier);
            direction = zoneDirection;
        }

        rend.GetPropertyBlock(block);
        block.SetFloat(WindSpeedID, speed);
        block.SetFloat(WindStrengthID, strength);
        block.SetVector(WindDirectionID, direction);
        rend.SetPropertyBlock(block);
    }
}
