using UnityEngine;

namespace LuigiGameDev.CarController.View
{
    /// <summary>
    /// Animates wheel position, spin, and steering visuals based on car state.
    /// Reads state from <see cref="CarViewState"/> - does not communicate directly with logic.
    /// </summary>
    public class CarWheelsVisual : MonoBehaviour
    {
        [Header("0: FrontLeft, 1: FrontRight, 2: RearRight, 3: RearLeft")]
        [SerializeField]
        private Transform[] m_WheelTransforms = new Transform[WheelCount];

        // Settings ----------------------------------------------------------------------------------------------------
        [Header("SETTINGS")]

        [Tooltip("Wheel Y offset when spring is fully compressed. Match to your wheel mesh position at full compression.")]
        [SerializeField]
        private float m_FullyCompressedY = -0.15f;

        [Tooltip("Wheel Y offset when spring is fully extended. Match to your wheel mesh position at full extension.")]
        [SerializeField]
        private float m_FullyExtendedY = -0.4f;

        [Tooltip("Multiplies car velocity to simulate wheel spin. Adjust to match your wheel mesh radius.")]
        [SerializeField]
        [Min(0.0f)]
        private float m_WheelSpinRate = 130.0f;

        [Tooltip("Smoothing time for wheel vertical movement.")]
        [SerializeField]
        [Min(0.0f)]
        private float m_WheelOffsetSmoothTime = 0.05f;

        // View State --------------------------------------------------------------------------------------------------
        [Header("VIEW STATE")]
        [Tooltip("Spring extension per wheel (0 = fully compressed, 1 = fully extended). " +
                 "Populated at runtime. Drag sliders in edit mode to preview and tune wheel Y offsets.")]
        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float[] m_SpringExtensions = new float[WheelCount];
        
        // -------------------------------------------------------------------------------------------------------------
        private const int WheelCount = 4;
        private const int FrontLeftWheel = 0;
        private const int FrontRightWheel = 1;

        private CarViewState m_ViewState;

        private Quaternion[] m_WheelSpinQuats = new Quaternion[WheelCount];
        private Quaternion[] m_WheelTurnQuats = new Quaternion[WheelCount];

        // Internal smoothing state
        private float[] m_WheelOffsetVelocities = new float[WheelCount];
        private float[] m_WheelCurrentY = new float[WheelCount];

        public Vector3 GetWheelPosition(int wheel)
        {
            return m_WheelTransforms[wheel].position;
        }
        
        private void Start()
        {
            m_ViewState = transform.root.GetComponentInChildren<CarViewState>();
            
            if (!m_ViewState)
            {
                Debug.LogError($"[{nameof(CarWheelsVisual)}] Missing CarViewState component.");
            }
            
            for (int wheel = 0; wheel < WheelCount; wheel++)
            {
                m_WheelSpinQuats[wheel] = m_WheelTransforms[wheel].localRotation;
                m_WheelTurnQuats[wheel] = Quaternion.identity;

                // Initialize smoothed Y with actual wheel Y
                Vector3 localPos = transform.InverseTransformPoint(m_WheelTransforms[wheel].position);
                m_WheelCurrentY[wheel] = localPos.y;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            for (int wheel = 0; wheel < WheelCount; wheel++)
            {
                UpdateWheelOffset(wheel, false);
            }
        }
#endif

        private void Update()
        {
            for (int wheel = 0; wheel < WheelCount; wheel++)
            {
                UpdateWheelOffset(wheel, true);
            }
            
            if (!m_ViewState) return;
            
            float forwardVelocity = Vector3.Dot(m_ViewState.Forward, m_ViewState.LinearVelocity);

            for (int wheel = 0; wheel < WheelCount; wheel++)
            {
                bool isGrounded = m_ViewState.Springs[wheel].Length < m_ViewState.Springs[wheel].MaxLength;
                if (isGrounded)
                {
                    m_WheelSpinQuats[wheel] *= Quaternion.AngleAxis(forwardVelocity * m_WheelSpinRate * Time.deltaTime, Vector3.right);
                }

                bool isFrontWheel = wheel == FrontLeftWheel || wheel == FrontRightWheel;
                if (isFrontWheel)
                {
                    m_WheelTurnQuats[wheel] = Quaternion.AngleAxis(m_ViewState.Wheels[wheel].SteerAngle, Vector3.up);
                }

                m_WheelTransforms[wheel].localRotation = m_WheelTurnQuats[wheel] * m_WheelSpinQuats[wheel];
            }
        }

        private void UpdateWheelOffset(int wheel, bool smooth)
        {
            if (m_WheelTransforms.Length <= wheel ||
                m_SpringExtensions.Length <= wheel ||
                m_WheelTransforms[wheel] == null)
            {
                return;
            }

            if (Application.isPlaying && m_ViewState)
            {
                m_SpringExtensions[wheel] = m_ViewState.Springs[wheel].Length / m_ViewState.Springs[wheel].MaxLength;
            }

            if (float.IsNaN(m_SpringExtensions[wheel])) return; 
            
            float targetY = Mathf.Lerp(m_FullyCompressedY, m_FullyExtendedY, m_SpringExtensions[wheel]);

            m_WheelCurrentY[wheel] = smooth? Mathf.SmoothDamp(m_WheelCurrentY[wheel], targetY, ref m_WheelOffsetVelocities[wheel], m_WheelOffsetSmoothTime) : targetY;
            
            Vector3 position = transform.InverseTransformPoint(m_WheelTransforms[wheel].position);
            position.y = m_WheelCurrentY[wheel];
            m_WheelTransforms[wheel].position = transform.TransformPoint(position);
        }
    }
}
