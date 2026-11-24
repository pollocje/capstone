using UnityEngine;

namespace LuigiGameDev.CarController.View
{
    public class CarBrakeLightsVisual : MonoBehaviour
    {
        [SerializeField]
        private MeshRenderer m_MeshRenderer;
        
        [SerializeField]
        private int m_MaterialIndex;
        
        // Settings ----------------------------------------------------------------------------------------------------
        [Header("Settings")]
        [SerializeField]
        [Min(0.0f)]
        private float m_Cooldown = 0.5f;

        [SerializeField]
        [Min(0.0f)]
        private float m_DeadStopSpeed = 2.0f;

        // -------------------------------------------------------------------------------------------------------------
        private CarViewState m_ViewState;
        
        private Material m_LightsMaterial;
        private float m_CooldownTimer;

        private void Start()
        {
            m_ViewState = transform.root.GetComponentInChildren<CarViewState>();
            
            if (!m_ViewState)
            {
                Debug.LogError($"[{nameof(CarWheelsVisual)}] Missing CarViewState component.");
            }
            
            if (m_MeshRenderer != null)
            {
                Material[] materials = m_MeshRenderer.materials;
                if (m_MaterialIndex >= 0 && m_MaterialIndex < materials.Length)
                {
                    m_LightsMaterial = materials[m_MaterialIndex];
                }
                else
                {
                    Debug.LogWarning($"[{nameof(CarBrakeLightsVisual)}] Material index {m_MaterialIndex} is out of range. Available materials: {materials.Length}");
                }
            }
        }

        private void Update()
        {
            float forwardVelocity = Vector3.Dot(m_ViewState.Forward, m_ViewState.LinearVelocity);

            bool brakesOn;

            if (Mathf.Abs(forwardVelocity) > 1.0f)
            {
                bool isAtDeadStopSpeed = Mathf.Abs(forwardVelocity) < m_DeadStopSpeed;
                bool accelerating = !Mathf.Approximately(m_ViewState.ThrottleInput, 0.0f);
                bool acceleratingContrary = accelerating && Vector3.Dot(m_ViewState.ThrottleInput * m_ViewState.Forward, m_ViewState.LinearVelocity) < 0.0f;

                if (isAtDeadStopSpeed && !accelerating)
                {
                    brakesOn = true;
                }
                else if (acceleratingContrary)
                {
                    brakesOn = true;
                }
                else
                {
                    brakesOn = false;
                }
            }
            else
            {
                brakesOn = false;
            }

            if (brakesOn)
            {
                m_CooldownTimer = m_Cooldown;
                ToggleEmission(true);
            }
            else
            {
                if (m_CooldownTimer > 0.0f)
                {
                    m_CooldownTimer -= Time.deltaTime;
                }
                else
                {
                    m_CooldownTimer = 0.0f;
                    ToggleEmission(false);
                }
            }
        }

        private void ToggleEmission(bool on)
        {
            if (!m_LightsMaterial) return;
            
            if (on)
            {
                m_LightsMaterial.EnableKeyword("_EMISSION");
            }
            else
            {
                m_LightsMaterial.DisableKeyword("_EMISSION");
            }
        }
    }
}
