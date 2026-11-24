using UnityEngine;

namespace LuigiGameDev.CarController.Logic
{
    public class Wheel
    {
        private readonly Spring m_Spring;
        private readonly Rigidbody m_CarRigidbody; // The rigidbody of the parent car
        private readonly Transform m_CarTransform;
        private readonly CarSettings m_Settings; // Car tweakable settings/properties

        private Vector3 m_Forward; // In world space, set by UpdateOrientation and steer input
        private Vector3 m_Right; // In world space, set by UpdateOrientation and steer input

        private Vector3 m_Position; // In world space, it's the spring end/contact point
        private Vector3 m_Velocity;
        private float m_LateralVelocity;
        private float m_LongitudinalVelocity;

        private float m_GripFactor;
        private float m_GripFactorDampVel;

        private float m_Throttle;
        private float m_Brake;
        private float m_GripMultiplier = 1.0f;
        private float m_TractionMultiplier = 1.0f;

        public Wheel(Spring spring, Rigidbody carRigidbody, CarSettings carSettings)
        {
            m_Spring = spring;
            m_CarRigidbody = carRigidbody;
            m_CarTransform = carRigidbody.transform;
            m_Settings = carSettings;
        }

        // In degrees. Negative and positive. 0 is neutral.
        public float SteerAngle { get; set; }

        public float Throttle
        {
            get => m_Throttle;
            set => m_Throttle = Mathf.Clamp(value, -1.0f, 1.0f);
        }

        public float Brake
        {
            get => m_Brake;
            set => m_Brake = Mathf.Clamp01(value);
        }

        public float GripMultiplier
        {
            get => m_GripMultiplier;
            set => m_GripMultiplier = Mathf.Clamp01(value);
        }

        public float TractionMultiplier
        {
            get => m_TractionMultiplier;
            set => m_TractionMultiplier = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Simulate this wheel individually. Called by the parent car, for each wheel.
        /// </summary>
        public void FixedUpdate()
        {
            Quaternion steerQuat = Quaternion.AngleAxis(SteerAngle, Vector3.up);
            m_Forward = m_CarTransform.TransformDirection(steerQuat * Vector3.forward);
            m_Right = m_CarTransform.TransformDirection(steerQuat * Vector3.right);

            m_Position = m_CarTransform.TransformPoint(m_Spring.LocalOrigin) + -m_CarTransform.up * m_Spring.CurrentLength;
            m_Velocity = m_CarRigidbody.GetPointVelocity(m_Position);
            m_LateralVelocity = Vector3.Dot(m_Velocity, m_Right);
            m_LongitudinalVelocity = Vector3.Dot(m_Velocity, m_Forward);

            if (!m_Spring.IsGrounded) return;

            // Wheel grounded! Continue with wheel forces

            UpdateSlopeCompensation();

            UpdateGrip();

            if (Mathf.Abs(m_Throttle) > 0.0f || m_Brake > 0.0f)
            {
                // Engine active, for Drive or Brake
                if (m_Brake > 0.0f)
                {
                    UpdateBrake();
                }
                else
                {
                    UpdateDrive();
                }
            }
            else
            {
                // Engine not active, rolling resistance relevant
                UpdateRollingResistance();
            }
        }

        /// <summary>
        /// Returns the world position where the wheel force is applied.
        /// </summary>
        /// <param name="offset">
        /// Vertical offset from the car’s center of mass (0 = at COM, >0 = lower).
        /// </param>
        private Vector3 GetForcePosition(float offset)
        {
            Vector3 position = m_Spring.LocalOrigin;
            position.y = m_CarRigidbody.centerOfMass.y - offset;
            position = m_CarTransform.TransformPoint(position);
            return position;
        }

        private void UpdateSlopeCompensation()
        {
            float slopeAngle = Vector3.Angle(m_CarTransform.up, Vector3.up);

            // Multiplier from 1 (full compensation) to 0 (no compensation)
            float compensationMultiplier = 1.0f;

            if (slopeAngle > m_Settings.FullCompensationSlopeAngle)
            {
                const float RightAngleDeg = 90.0f;

                float range = RightAngleDeg - m_Settings.FullCompensationSlopeAngle;
                float ratio = (slopeAngle - m_Settings.FullCompensationSlopeAngle) / range;
                compensationMultiplier = Mathf.Clamp01(1.0f - ratio);
            }

            float totalSlopeAccel = -Vector3.Dot(m_Right, Physics.gravity) * compensationMultiplier * GetGroundDynamicFriction();
            float slopeAccelPerWheel = totalSlopeAccel / WheelId.Count;
            m_CarRigidbody.AddForceAtPosition(m_Right * slopeAccelPerWheel, GetForcePosition(m_Settings.GripForcesOffset), ForceMode.Acceleration);
        }

        // Dynamic friction is used in the slip countering forces (passive forces)
        private float GetGroundDynamicFriction()
        {
            return m_Spring.GroundMaterial ? m_Spring.GroundMaterial.dynamicFriction : 1.0f;
        }

        // Static friction is used in the engine drive and brake forces (active forces produced by the car)
        private float GetGroundStaticFriction()
        {
            return m_Spring.GroundMaterial ? m_Spring.GroundMaterial.staticFriction : 1.0f;
        }

        private void UpdateGrip()
        {
            float speedNormalized = Mathf.Clamp01(m_Velocity.magnitude / m_Settings.MaxSpeed);

            float gripFactorTarget = m_Settings.EvalGrip(speedNormalized) * GetGroundDynamicFriction() * m_GripMultiplier;

            m_GripFactor = Mathf.SmoothDamp(m_GripFactor, gripFactorTarget, ref m_GripFactorDampVel, m_Settings.GripSmoothTime, float.PositiveInfinity, Time.fixedDeltaTime);

            // Lateral jitter guard: avoid micro-force applications and allow the Rigidbody to sleep
            if (Mathf.Abs(m_LateralVelocity) < 0.001f) return;

            Vector3 slipDirection = (m_Right * m_LateralVelocity).normalized;

            // If sliding down a slope, reduce grip (0 = full downhill, 1 = flat or uphill)
            float slopeModifier = 1.0f - Mathf.Clamp01(Vector3.Dot(slipDirection, Vector3.down));

            float totalVelocityChange = -m_LateralVelocity * m_GripFactor * slopeModifier;
            float velocityChangePerWheel = totalVelocityChange / WheelId.Count;
            
            m_CarRigidbody.AddForceAtPosition(m_Right * velocityChangePerWheel, GetForcePosition(m_Settings.GripForcesOffset), ForceMode.VelocityChange);
        }

        private bool CheckMaxSpeed(bool positiveThrottle)
        {
            bool isMovingForward = m_LongitudinalVelocity > 0.0f;
            float velocityMagnitude = m_Velocity.magnitude;

            if (positiveThrottle && isMovingForward && velocityMagnitude > m_Settings.MaxSpeed)
            {
                return true;
            }

            if (!positiveThrottle && !isMovingForward && velocityMagnitude > m_Settings.ReverseMaxSpeed)
            {
                return true;
            }

            return false;
        }

        private void UpdateDrive()
        {
            bool positiveThrottle = m_Throttle > 0.0f;

            if (Mathf.Abs(m_Throttle) > 0.0f && !CheckMaxSpeed(positiveThrottle))
            {
                Vector3 driveDirection = (m_Forward * m_Throttle).normalized;
                float driveDownProjection = Vector3.Dot(driveDirection, Vector3.down);

                // Modifier: 1 when driving flat/downhill
                float driveSlopeModifier = 1.0f;
                if (driveDownProjection < 0.0f)
                {
                    // Negative projection, drive uphill
                    // Reduces linearly up to 0
                    // Makes the car heavy when driving uphill
                    driveSlopeModifier += driveDownProjection;
                }

                float driveStrength = positiveThrottle ? m_Settings.DriveStrength : m_Settings.ReverseDriveStrength;
                Vector3 engineForce = m_Throttle * driveStrength * m_TractionMultiplier * driveSlopeModifier * m_Forward;

                engineForce *= GetGroundStaticFriction();

                m_CarRigidbody.AddForceAtPosition(engineForce, GetForcePosition(m_Settings.DriveForcesOffset));
            }
        }

        private void UpdateBrake()
        {
            // Note: Brake input is disengaged below 2 m/s in Car.cs to hand over to reverse/rolling resistance.
            Vector3 brakeDirection = -Mathf.Sign(m_LongitudinalVelocity) * m_Forward;
            Vector3 brakeForce = brakeDirection * (m_Brake * m_Settings.BrakeStrength * m_TractionMultiplier * GetGroundStaticFriction());
            m_CarRigidbody.AddForceAtPosition(brakeForce, GetForcePosition(m_Settings.DriveForcesOffset));
        }

        private void UpdateRollingResistance()
        {
            float speed = Mathf.Abs(m_LongitudinalVelocity);
            if (speed < 0.001f) return;

            // Slope modifier: reduces rolling resistance when moving downhill.
            Vector3 rollDirection = Mathf.Sign(m_LongitudinalVelocity) * m_Forward;
            float rollDownProjection = Vector3.Dot(rollDirection, Vector3.down);
            float slopeModifier = 1.0f - Mathf.Clamp01(rollDownProjection);

            // Deceleration to stop: clamped to bring car to rest without overshooting into reverse.
            float maxStopDecel = speed / Time.fixedDeltaTime;
            float totalDecel = Mathf.Min(m_Settings.RollingResistanceStrength, maxStopDecel) * slopeModifier;
            float decelPerWheel = totalDecel / WheelId.Count;

            float accel = -Mathf.Sign(m_LongitudinalVelocity) * decelPerWheel;
            m_CarRigidbody.AddForceAtPosition(m_Forward * accel, GetForcePosition(0.0f), ForceMode.Acceleration);
        }
    }
}
