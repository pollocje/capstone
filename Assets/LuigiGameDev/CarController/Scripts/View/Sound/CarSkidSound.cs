using System;
using System.Collections.Generic;
using UnityEngine;

namespace LuigiGameDev.CarController.View
{
    public class CarSkidSound : MonoBehaviour
    {
        [Serializable]
        private class GroundSfxSettings
        {
            public PhysicsMaterial Material;
            public AudioClip AudioClip;
            public float Volume;
            public float Pitch;
        }
        
        [SerializeField] 
        private AudioSource m_AudioSource;

        // Settings ----------------------------------------------------------------------------------------------------
        [Header("Settings")]
        [SerializeField]
        [Tooltip("Delay before sound starts (in seconds)")]
        [Min(0.0f)]
        private float m_StartDelay;
        
        [SerializeField]
        [Min(0.0001f)] 
        private float m_SmoothTime = 0.1f;
        
        [SerializeField] 
        private List<GroundSfxSettings> m_GroundEffectSettings;

        // View State --------------------------------------------------------------------------------------------------
        [Header("View State")] 
        [SerializeField]
        private CarViewState m_ViewState;
        
        // -------------------------------------------------------------------------------------------------------------
        private const int WheelCount = 4;

        private float m_SkidTimer;
        private float m_VolumeDampVelocity;
        private PhysicsMaterial[] m_MaterialBuffer;
        private int[] m_MaterialCounts;

        private void Start()
        {
            if (!m_ViewState)
            {
                Debug.LogError($"[{nameof(CarSkidSound)}] Missing reference to ViewState. Assign it in edit mode.");
            }
            
            m_MaterialBuffer = new PhysicsMaterial[WheelCount];
            m_MaterialCounts = new int[WheelCount];
        }

        private bool IsGrounded()
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

        private GroundSfxSettings GetGroundSfxSettings(PhysicsMaterial material)
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

        private void Update()
        {
            if (!m_ViewState) return;

            float volumeTarget = 0.0f;

            var dominantMaterial = GetDominantGroundMaterial();
            var settings = GetGroundSfxSettings(dominantMaterial);

            bool canPlay = IsGrounded() && m_ViewState.HandbrakeInput && settings != null;

            if (canPlay)
            {
                m_SkidTimer += Time.deltaTime;

                if (m_SkidTimer >= m_StartDelay)
                {
                    volumeTarget = settings.Volume;
                    m_AudioSource.clip = settings.AudioClip;
                    m_AudioSource.pitch = settings.Pitch;
                }
            }
            else
            {
                m_SkidTimer = 0.0f;
            }

            m_AudioSource.volume = Mathf.SmoothDamp(
                m_AudioSource.volume, volumeTarget, ref m_VolumeDampVelocity, m_SmoothTime);

            SetPlaying(m_AudioSource.volume > 0.0f);
        }

        private PhysicsMaterial GetDominantGroundMaterial()
        {
            int uniqueCount = 0;

            // Reset counts
            for (int i = 0; i < WheelCount; i++)
            {
                m_MaterialBuffer[i] = null;
                m_MaterialCounts[i] = 0;
            }

            // Count materials
            for (int wheel = 0; wheel < WheelCount; wheel++)
            {
                var mat = m_ViewState.Wheels[wheel].GroundMaterial;
                if (mat == null) continue;

                bool found = false;
                for (int j = 0; j < uniqueCount; j++)
                {
                    if (m_MaterialBuffer[j] == mat)
                    {
                        m_MaterialCounts[j]++;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    m_MaterialBuffer[uniqueCount] = mat;
                    m_MaterialCounts[uniqueCount] = 1;
                    uniqueCount++;
                }
            }

            // Find dominant
            PhysicsMaterial dominant = null;
            int maxCount = 0;
            for (int i = 0; i < uniqueCount; i++)
            {
                if (m_MaterialCounts[i] > maxCount)
                {
                    maxCount = m_MaterialCounts[i];
                    dominant = m_MaterialBuffer[i];
                }
            }

            return dominant;
        }

        private void SetPlaying(bool play)
        {
            if (play)
            {
                if (!m_AudioSource.isPlaying)
                {
                    m_AudioSource.Play();
                }
            }
            else
            {
                if (m_AudioSource.isPlaying)
                {
                    m_AudioSource.Stop();
                    if (m_AudioSource.clip)
                    {
                        m_AudioSource.time = 0.0f;
                    }
                }
            }
        }
    }
}
