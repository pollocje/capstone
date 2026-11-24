using LuigiGameDev.CarController.Logic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LuigiGameDev.CarController.View
{
    /// <summary>
    /// Handles player input and forwards it to the current car’s input interface.
    /// Acts as one of the few View components that directly communicates with Logic,
    /// alongside <see cref="ViewStateLinker"/>.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset m_InputActionAsset;
        [SerializeField] private string m_SteerActionName = "Steer";
        [SerializeField] private string m_ThrottleActionName = "Throttle";
        [SerializeField] private string m_HandbrakeActionName = "Handbrake";
        
        private InputAction m_SteerAction;
        private InputAction m_ThrottleAction;
        private InputAction m_HandbrakeAction;
        
        private Car m_CurrentCar;
        private CarInput m_CurrentCarInput;

        public Car CurrentCar
        {
            set
            {
                if (value != m_CurrentCar)
                {
                    m_CurrentCar = value;
                    m_CurrentCarInput = m_CurrentCar.GetComponent<CarInput>();
                }
            }
        }

        private void Start()
        {
            if (m_InputActionAsset == null)
            {
                Debug.LogError($"[{nameof(PlayerController)}] No InputActionAsset assigned. Assign the reference in edit mode.");
                return;
            }

            m_SteerAction = m_InputActionAsset.FindAction(m_SteerActionName);

            if (m_SteerAction == null)
            {
                Debug.LogWarning($"[{nameof(PlayerController)}] '{m_SteerActionName}' action not found. Check the InputActionAsset.");
            }

            m_ThrottleAction = m_InputActionAsset.FindAction(m_ThrottleActionName);

            if (m_ThrottleAction == null)
            {
                Debug.LogWarning($"[{nameof(PlayerController)}] '{m_ThrottleActionName}' action not found. Check the InputActionAsset.");
            }

            m_HandbrakeAction = m_InputActionAsset.FindAction(m_HandbrakeActionName);

            if (m_HandbrakeAction == null)
            {
                Debug.LogWarning($"[{nameof(PlayerController)}] '{m_HandbrakeActionName}' action not found. Check the InputActionAsset.");
            }

            m_InputActionAsset.Enable();
        }

        private void OnDestroy()
        {
            if (m_InputActionAsset != null)
            {
                m_InputActionAsset.Disable();
            }
        }

        private void Update()
        {
            if (m_CurrentCar == null) return;
            
            m_CurrentCarInput.Steer = m_SteerAction?.ReadValue<float>() ?? 0.0f;
            m_CurrentCarInput.Throttle = m_ThrottleAction?.ReadValue<float>() ?? 0.0f;
            m_CurrentCarInput.Handbrake = m_HandbrakeAction?.ReadValue<float>() > 0.0f;
        }
    }
}
