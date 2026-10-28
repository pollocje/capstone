using UnityEngine;

namespace LuigiGameDev.CarController.View
{
    /// <summary>
    /// Holds car state for view scripts (visuals, audio, effects).
    /// Provides a decoupled interface so view scripts don’t depend directly on logic.
    /// One instance per car.
    /// </summary>
    public class CarViewState : MonoBehaviour
    {
        public struct Wheel
        {
            public float SteerAngle;
            public PhysicsMaterial GroundMaterial;
        }

        public struct Spring
        {
            public float MaxLength;
            public float Length;
            public float Velocity;
        }
        
        public Vector3 Forward;
        public Vector3 LinearVelocity;
        public float MaxSpeed;
        public float ThrottleInput;
        public bool HandbrakeInput;
        public Spring[] Springs = new Spring[WheelCount];
        public Wheel[] Wheels = new Wheel[WheelCount];

        private const int WheelCount = 4;
        
        public void SetAllSpringsMaxLength(float maxLength)
        {
            for (int i = 0; i < WheelCount; i++)
            {
                Springs[i].MaxLength = maxLength;
            }
        }
    }
}
