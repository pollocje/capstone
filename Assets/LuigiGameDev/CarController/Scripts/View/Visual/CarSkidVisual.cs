using System;
using System.Collections.Generic;
using UnityEngine;

namespace LuigiGameDev.CarController.View
{
    // Depends on CarWheelsVisual
    public class CarSkidVisual : MonoBehaviour
    {
        [Serializable]
        private class GroundFxSettings
        {
            public PhysicsMaterial Material;
            public bool Trail;
            public Gradient TrailGradient;
            public bool Smoke;
        }
        
        [SerializeField]
        private CarWheelsVisual m_WheelsVisual;

        [SerializeField]
        private GameObject m_TrailEffectPrefab;
        
        [SerializeField]
        private GameObject m_SmokeParticlePrefab;
        
        // Settings ----------------------------------------------------------------------------------------------------
        [Header("Settings")] 
        [SerializeField] 
        [Min(0.0f)]
        private float m_StartDelay;

            
        [SerializeField]
        [Min(0.0f)]
        private float m_SkidWheelRadius = 0.25f;

        [SerializeField] 
        private List<GroundFxSettings> m_GroundEffectSettings;
        
        // -------------------------------------------------------------------------------------------------------------
        private TrailRenderer[] m_SkidTrailRenderers;
        private ParticleSystem[] m_SkidSmokeParticles;
        private float m_SkidTimer;
        private CarViewState m_ViewState;
        
        private const int WheelCount = 4;
        
        private void Start()
        {
            m_ViewState = transform.root.GetComponentInChildren<CarViewState>();
            
            if (!m_ViewState)
            {
                Debug.LogError($"[{nameof(CarSkidVisual)}] Missing CarViewState component.");
            }
            
            if (!m_WheelsVisual)
            {
                Debug.LogError($"[{nameof(CarSkidVisual)}] Missing reference to WheelsVisual. Assign it in edit mode.");
            }
            
            m_SkidTrailRenderers = new TrailRenderer[WheelCount];
            m_SkidSmokeParticles = new ParticleSystem[WheelCount];

            for (int i = 0; i < WheelCount; i++)
            {
                m_SkidTrailRenderers[i] = Instantiate(m_TrailEffectPrefab, transform).GetComponent<TrailRenderer>();
                m_SkidSmokeParticles[i] = Instantiate(m_SmokeParticlePrefab, transform).GetComponent<ParticleSystem>();
            }
        }

        private void Update()
        {
            if (!m_WheelsVisual) return;
            if (!m_ViewState) return;
            
            bool shouldSkid = m_ViewState.HandbrakeInput && IsAnyWheelGrounded();

            if (shouldSkid)
            {
                m_SkidTimer += Time.deltaTime;
            }
            else
            {
                m_SkidTimer = 0.0f;
            }

            bool skidActive = shouldSkid && (m_SkidTimer >= m_StartDelay);
            
            for (int i = 0; i < WheelCount; i++)
            {
                UpdateSkidTrailPosition(i);

                UpdateSkidSmokeParticlePosition(i);
                
                UpdateSkidEffects(i, skidActive);
            }
        }
        
        private bool IsAnyWheelGrounded()
        {
            for (int wheel = 0; wheel < WheelCount; wheel++)
            {
                if (m_ViewState.Springs[wheel].Length < m_ViewState.Springs[wheel].MaxLength)
                {
                    return true;
                }
            }
            return false;
        }
        
        private void UpdateSkidTrailPosition(int wheel)
        {
            if (wheel >= m_SkidTrailRenderers.Length || m_SkidTrailRenderers[wheel] == null)
            {
                return;
            }

            m_SkidTrailRenderers[wheel].transform.position = m_WheelsVisual.GetWheelPosition(wheel) + Vector3.down * m_SkidWheelRadius;
        }
        
        private void UpdateSkidSmokeParticlePosition(int wheel)
        {
            if (wheel >= m_SkidSmokeParticles.Length || m_SkidSmokeParticles[wheel] == null)
            {
                return;
            }

            m_SkidSmokeParticles[wheel].transform.position = m_WheelsVisual.GetWheelPosition(wheel) + Vector3.down * m_SkidWheelRadius;
        }

        private GroundFxSettings GetGroundEffectSettings(PhysicsMaterial material)
        {
            if (material == null) return null;

            foreach (var settings in m_GroundEffectSettings)
            {
                if (material == settings.Material)
                {
                    return settings;
                }
            }

            return null;
        }

        private void UpdateSkidEffects(int wheel, bool skidActive)
        {
            if (wheel >= m_SkidTrailRenderers.Length || wheel >= m_SkidSmokeParticles.Length ||
                m_SkidTrailRenderers[wheel] == null || m_SkidSmokeParticles[wheel] == null)
            {
                return;
            }

            if (skidActive)
            {
                var effectSettings = GetGroundEffectSettings(m_ViewState.Wheels[wheel].GroundMaterial);

                if (effectSettings != null)
                {
                    m_SkidTrailRenderers[wheel].colorGradient = effectSettings.TrailGradient;
                    m_SkidTrailRenderers[wheel].emitting = effectSettings.Trail;
                    PlaySmoke(wheel, effectSettings.Smoke);
                    return;
                }
            }

            // fallback: no skid
            m_SkidTrailRenderers[wheel].emitting = false;
            PlaySmoke(wheel, false);
        }

        private void PlaySmoke(int wheel, bool play)
        {
            if (play)
            {
                m_SkidSmokeParticles[wheel].Play();
            }
            else
            {
                m_SkidSmokeParticles[wheel].Stop();
            }
        }
    }
}
