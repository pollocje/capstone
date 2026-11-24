using UnityEngine;

namespace LuigiGameDev.CarController.View
{
    public class CarEngineSound : MonoBehaviour
    {
        [SerializeField]
        private AudioSource m_AudioSource;

        // Settings ----------------------------------------------------------------------------------------------------
        [Header("Settings")]
        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float m_ThrottleAddVolume = 0.1f;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float m_VelocityAddVolume = 0.2f;

        [SerializeField]
        [Range(0.0f, 3.0f)]
        private float m_ThrottleAddPitch = 0.3f;

        [SerializeField]
        [Range(0.0f, 3.0f)]
        private float m_VelocityAddPitch = 0.8f;

        [SerializeField]
        [Range(0.0f, 3.0f)]
        private float m_TopSpeedAddPitch = 0.3f;

        [SerializeField]
        [Min(0.0f)]
        private float m_TopSpeedTime = 10.0f;

        [SerializeField]
        [Min(0.0f)]
        private float m_SmoothTime = 0.25f;

        // View State --------------------------------------------------------------------------------------------------
        [Header("View State")] 
        [SerializeField]
        private CarViewState m_ViewState;

        // -------------------------------------------------------------------------------------------------------------
        private float m_BaseVolume;
        private float m_BasePitch;

        private float m_VolumeDampVelocity;
        private float m_PitchDampVelocity;
        private float m_TopSpeedTimer;

        private void Start()
        {
            if (!m_ViewState)
            {
                Debug.LogError($"[{nameof(CarEngineSound)}] Missing reference to ViewState. Assign it in edit mode.");
            }
            
            m_BaseVolume = m_AudioSource.volume;
            m_BasePitch = m_AudioSource.pitch;
        }

        private void Update()
        {
            if (!m_ViewState) return;
            
            UpdateVolume();

            UpdatePitch();
        }

        private void UpdateVolume()
        {
            Vector3 carForward = m_ViewState.Forward;
            Vector3 linearVelocity = m_ViewState.LinearVelocity;
            float maxSpeed = m_ViewState.MaxSpeed;
            float throttleInput = m_ViewState.ThrottleInput;
            
            float speedRatio = Mathf.Clamp01(Mathf.Abs(Vector3.Dot(carForward, linearVelocity)) / maxSpeed);

            float targetVolume = m_BaseVolume;
            targetVolume += Mathf.Abs(throttleInput) * m_ThrottleAddVolume;
            targetVolume += speedRatio * m_VelocityAddVolume;
            targetVolume = Mathf.Clamp01(targetVolume);
            m_AudioSource.volume = Mathf.SmoothDamp(m_AudioSource.volume, targetVolume, ref m_VolumeDampVelocity, m_SmoothTime);
        }

        private void UpdatePitch()
        {
            Vector3 carForward = m_ViewState.Forward;
            Vector3 linearVelocity = m_ViewState.LinearVelocity;
            float maxSpeed = m_ViewState.MaxSpeed;
            float throttleInput = m_ViewState.ThrottleInput;
            
            float speedRatio = Mathf.Clamp01(Mathf.Abs(Vector3.Dot(carForward, linearVelocity)) / maxSpeed);

            const float TopSpeedThreshold = 0.98f;
            bool isAtTopSpeed = speedRatio > TopSpeedThreshold;

            if (isAtTopSpeed)
            {
                m_TopSpeedTimer += Time.deltaTime;
            }
            else
            {
                m_TopSpeedTimer -= Time.deltaTime * 10.0f; // Timer decreases 10x faster
            }

            m_TopSpeedTimer = Mathf.Clamp(m_TopSpeedTimer, 0.0f, m_TopSpeedTime);

            float topSpeedTimerNormalized = m_TopSpeedTimer / m_TopSpeedTime;

            float targetPitch = m_BasePitch;
            targetPitch += Mathf.Abs(throttleInput) * m_ThrottleAddPitch;
            targetPitch += speedRatio * m_VelocityAddPitch;
            targetPitch += topSpeedTimerNormalized * m_TopSpeedAddPitch;
            const float MaxPitch = 3.0f;
            targetPitch = Mathf.Clamp(targetPitch, 0.0f, MaxPitch);

            if (float.IsNaN(targetPitch)) return;
            
            m_AudioSource.pitch = Mathf.SmoothDamp(m_AudioSource.pitch, targetPitch, ref m_PitchDampVelocity, m_SmoothTime);
        }
    }
}
