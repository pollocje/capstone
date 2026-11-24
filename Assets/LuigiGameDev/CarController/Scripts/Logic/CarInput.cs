using UnityEngine;

namespace LuigiGameDev.CarController.Logic
{
    /// <summary>
    /// Input interface the controller uses to communicate with the car.
    /// </summary>
    public class CarInput : MonoBehaviour
    {
        private float m_Steer;
        private float m_Throttle;

        public float Steer
        {
            get => m_Steer;
            set => m_Steer = Mathf.Clamp(value, -1.0f, 1.0f);
        }

        public float Throttle
        {
            get => m_Throttle;
            set => m_Throttle = Mathf.Clamp(value, -1.0f, 1.0f);
        }

        public bool Handbrake { get; set; }
    }
}
