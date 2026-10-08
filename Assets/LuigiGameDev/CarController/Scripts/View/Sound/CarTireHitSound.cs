using UnityEngine;
using Random = UnityEngine.Random;

namespace LuigiGameDev.CarController.View
{
    public class CarTireHitSound : MonoBehaviour
    {
        [SerializeField]
        private AudioSource m_AudioSource;

        [Header("Settings")]
        [SerializeField]
        [Min(0.0f)]
        private float m_HitMin;

        [SerializeField]
        [Min(0.0f)]
        private float m_HitMax;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float m_VolumeMin;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float m_VolumeMax;

        [SerializeField]
        [Range(0.0f, 3.0f)]
        private float m_PitchMin;

        [SerializeField]
        [Range(0.0f, 3.0f)]
        private float m_PitchMax;

        [SerializeField]
        [Min(0.0f)]
        private float m_MinReplayTime;

        [Header("View State")] 
        [SerializeField]
        private CarViewState m_ViewState;
        
        [SerializeField]
        private int m_WheelIndex;

        private void Start()
        {
            if (!m_ViewState)
            {
                Debug.LogError($"[{nameof(CarTireHitSound)}] Missing reference to ViewState. Assign it in edit mode.");
            }
        }

        private void Update()
        {
            if (!m_ViewState) return;
            
            float springVelocity = m_ViewState.Springs[m_WheelIndex].Velocity;
            
            if (springVelocity < -m_HitMin)
            {
                float hitVelocity = -springVelocity;
                float hitNormalized = Mathf.Clamp01(hitVelocity / m_HitMax);

                if (!m_AudioSource.isPlaying || m_AudioSource.time > m_MinReplayTime)
                {
                    m_AudioSource.volume = Mathf.Lerp(m_VolumeMin, m_VolumeMax, hitNormalized);
                    m_AudioSource.pitch = Random.Range(m_PitchMin, m_PitchMax);
                    m_AudioSource.Play();
                }
            }
        }
    }
}
