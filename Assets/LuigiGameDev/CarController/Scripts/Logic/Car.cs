using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LuigiGameDev.CarController.Logic
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CarSettings))]
    [RequireComponent(typeof(CarInput))]
    public class Car : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("GIZMOS")]
        [SerializeField] private bool m_GizmosDrawWheelTransform;
        [SerializeField] private bool m_GizmosDrawSpring;
        [SerializeField] private bool m_GizmosDrawAckermannCircle;
#endif

        private BoxCollider m_BoxCollider;
        private Rigidbody m_Rigidbody;
        private CarSettings m_Settings;
        private CarInput m_Input;
        private Spring[] m_Springs;
        private Wheel[] m_Wheels;

        // INITIALIZATION ----------------------------------------------------------------------------------------------

        private void Start()
        {
            if (transform.localScale != Vector3.one)
            {
                Debug.LogError($"[{nameof(Car)}] Invalid scale! Please set its scale to (1, 1, 1) to ensure proper behavior.");
            }

            m_Settings = GetComponent<CarSettings>();

            m_Input = GetComponent<CarInput>();

            InitializeCollider();

            InitializeRigidbody();

            m_Springs = new Spring[WheelId.Count];
            m_Wheels = new Wheel[WheelId.Count];

            for (int wheelId = 0; wheelId < WheelId.Count; wheelId++)
            {
                m_Springs[wheelId] = new Spring(wheelId, GetLocalSpringOrigin(wheelId), m_Rigidbody, m_Settings);

                m_Wheels[wheelId] = new Wheel(m_Springs[wheelId], m_Rigidbody, m_Settings);
            }
        }

        private void InitializeCollider()
        {
            m_BoxCollider = GetComponent<BoxCollider>();

            if (m_BoxCollider.center != Vector3.zero)
            {
                Debug.LogError($"[{nameof(Car)}] The main BoxCollider center should be at (0,0,0). Fix this setup in edit mode.");
            }

            if (m_BoxCollider.isTrigger)
            {
                Debug.LogError($"[{nameof(Car)}] The main BoxCollider cannot be a trigger. Automatic correction applied. Fix this setup in edit mode.");
                m_BoxCollider.isTrigger = false;
            }

            if (!m_BoxCollider.enabled)
            {
                Debug.LogError($"[{nameof(Car)}] The main BoxCollider must be enabled. Automatic correction applied. Fix this setup in edit mode.");
                m_BoxCollider.enabled = true;
            }
        }

        private void InitializeRigidbody()
        {
            m_Rigidbody = gameObject.GetComponent<Rigidbody>();

            if (!m_Rigidbody.useGravity)
            {
                Debug.LogError($"[{nameof(Car)}] Rigidbody 'useGravity' must be enabled. Automatic correction applied. Fix this setup in edit mode.");
                m_Rigidbody.useGravity = true;
            }

            if (m_Rigidbody.isKinematic)
            {
                Debug.LogError($"[{nameof(Car)}] Rigidbody 'isKinematic' must be disabled. Automatic correction applied. Fix this setup in edit mode.");
                m_Rigidbody.isKinematic = false;
            }

            if (m_Rigidbody.constraints != RigidbodyConstraints.None)
            {
                Debug.LogError($"[{nameof(Car)}] Rigidbody 'constraints' must be set to 'None'. Automatic correction applied. Fix this setup in edit mode.");
                m_Rigidbody.constraints = RigidbodyConstraints.None;
            }
        }

        private Vector3 GetLocalSpringOrigin(int wheelId)
        {
            switch (wheelId)
            {
                case WheelId.FL:
                    return new Vector3(-m_Settings.TrackWidth / 2.0f, m_Settings.AxleHeight, m_Settings.Wheelbase / 2.0f);
                case WheelId.FR:
                    return new Vector3(m_Settings.TrackWidth / 2.0f, m_Settings.AxleHeight, m_Settings.Wheelbase / 2.0f);
                case WheelId.RR:
                    return new Vector3(m_Settings.TrackWidth / 2.0f, m_Settings.AxleHeight, -m_Settings.Wheelbase / 2.0f);
                case WheelId.RL:
                    return new Vector3(-m_Settings.TrackWidth / 2.0f, m_Settings.AxleHeight, -m_Settings.Wheelbase / 2.0f);
                default:
                    return default;
            }
        }

        // UPDATE SIMULATION -------------------------------------------------------------------------------------------

        private void FixedUpdate()
        {
            // Simulate Springs
            for (int wheelId = 0; wheelId < WheelId.Count; wheelId++)
            {
                m_Springs[wheelId].FixedUpdate();
            }

            UpdateSteerInput();

            UpdateThrottleInput();

            UpdateBrakeInput();

            UpdateHandbrakeInput();

            // Simulate Wheels
            for (int wheelId = 0; wheelId < WheelId.Count; wheelId++)
            {
                m_Wheels[wheelId].FixedUpdate();
            }
        }

        private void UpdateSteerInput()
        {
            // Update Steering
            // Ackermann steering model ensures that all wheels of a vehicle point toward a common turning center, 
            // improving handling and reducing tire slippage.
            float wheelbase = m_Settings.Wheelbase;
            float speedNormalized = Mathf.Clamp01(m_Rigidbody.linearVelocity.magnitude / m_Settings.MaxSpeed);
            float turnRadius = m_Settings.EvalTurnRadius(speedNormalized);
            float trackWidth = m_Settings.TrackWidth;
            float steerSign = Mathf.Sign(m_Input.Steer);

            float frontLeftAckermannAngle = Mathf.Rad2Deg * Mathf.Atan(wheelbase / (turnRadius + steerSign * trackWidth / 2.0f)) * m_Input.Steer;
            float frontRightAckermannAngle = Mathf.Rad2Deg * Mathf.Atan(wheelbase / (turnRadius - steerSign * trackWidth / 2.0f)) * m_Input.Steer;
            m_Wheels[WheelId.FL].SteerAngle = frontLeftAckermannAngle;
            m_Wheels[WheelId.FR].SteerAngle = frontRightAckermannAngle;
            m_Wheels[WheelId.RR].SteerAngle = 0;
            m_Wheels[WheelId.RL].SteerAngle = 0;
        }

        private void UpdateThrottleInput()
        {
            m_Wheels[WheelId.FL].Throttle = m_Input.Throttle;
            m_Wheels[WheelId.FR].Throttle = m_Input.Throttle;
            m_Wheels[WheelId.RR].Throttle = m_Input.Throttle;
            m_Wheels[WheelId.RL].Throttle = m_Input.Throttle;
        }

        private void UpdateBrakeInput()
        {
            float longitudinalVelocity = Vector3.Dot(m_Rigidbody.linearVelocity, transform.forward);
            bool throttleNonZero = !Mathf.Approximately(m_Input.Throttle, 0.0f);
            const float MinBrakeSpeed = 2.0f;
            bool throttleOpposeVelocity = Math.Sign(longitudinalVelocity) != Math.Sign(m_Input.Throttle);

            if (throttleNonZero && Mathf.Abs(longitudinalVelocity) > MinBrakeSpeed && throttleOpposeVelocity)
            {
                float brakeInput = Mathf.Abs(m_Input.Throttle);

                m_Wheels[WheelId.FL].Brake = brakeInput;
                m_Wheels[WheelId.FR].Brake = brakeInput;
                m_Wheels[WheelId.RR].Brake = brakeInput;
                m_Wheels[WheelId.RL].Brake = brakeInput;
            }
            else
            {
                m_Wheels[WheelId.FL].Brake = 0.0f;
                m_Wheels[WheelId.FR].Brake = 0.0f;
                m_Wheels[WheelId.RR].Brake = 0.0f;
                m_Wheels[WheelId.RL].Brake = 0.0f;
            }
        }

        private void UpdateHandbrakeInput()
        {
            if (m_Input.Handbrake)
            {
                bool positiveThrottle = m_Input.Throttle > 0;

                // Choose which axle gets the leading effect
                float frontGrip = positiveThrottle ? m_Settings.HandbrakeLeadingGrip : m_Settings.HandbrakeTrailingGrip;
                float rearGrip = positiveThrottle ? m_Settings.HandbrakeTrailingGrip : m_Settings.HandbrakeLeadingGrip;
                float frontTraction = positiveThrottle ? m_Settings.HandbrakeLeadingTraction : m_Settings.HandbrakeTrailingTraction;
                float rearTraction = positiveThrottle ? m_Settings.HandbrakeTrailingTraction : m_Settings.HandbrakeLeadingTraction;

                m_Wheels[WheelId.FL].GripMultiplier = frontGrip;
                m_Wheels[WheelId.FR].GripMultiplier = frontGrip;
                m_Wheels[WheelId.RL].GripMultiplier = rearGrip;
                m_Wheels[WheelId.RR].GripMultiplier = rearGrip;

                m_Wheels[WheelId.FL].TractionMultiplier = frontTraction;
                m_Wheels[WheelId.FR].TractionMultiplier = frontTraction;
                m_Wheels[WheelId.RL].TractionMultiplier = rearTraction;
                m_Wheels[WheelId.RR].TractionMultiplier = rearTraction;
            }
            else
            {
                // Reset all multipliers
                foreach (var wheel in m_Wheels)
                {
                    wheel.GripMultiplier = 1.0f;
                    wheel.TractionMultiplier = 1.0f;
                }
            }
        }

        // EXTERNAL ACCESSORS ------------------------------------------------------------------------------------------

        public float GetSpringLength(int wheelId)
        {
            return m_Springs[wheelId].CurrentLength;
        }

        public float GetSpringVelocity(int wheelId)
        {
            return m_Springs[wheelId].Velocity;
        }

        public float GetWheelSteerAngle(int wheelId)
        {
            return m_Wheels[wheelId].SteerAngle;
        }

        public PhysicsMaterial GetGroundPhysicsMaterial(int wheelId)
        {
            return m_Springs[wheelId].GroundMaterial;
        }

        // EDITOR AND GIZMOS -------------------------------------------------------------------------------------------

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!m_BoxCollider)
            {
                m_BoxCollider = GetComponent<BoxCollider>();
            }

            if (!m_Rigidbody)
            {
                m_Rigidbody = GetComponent<Rigidbody>();
            }

            if (!m_Settings)
            {
                m_Settings = GetComponent<CarSettings>();
            }
        }

        [ContextMenu(nameof(GenerateCollidersMesh))]
        private void GenerateCollidersMesh()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Generate Collider Meshes");

            Collider[] colliders = GetComponentsInChildren<Collider>();

            foreach (Collider col in colliders)
            {
                GameObject mesh = ColliderMeshHelper.Create(col);
                if (mesh == null) continue;

                Undo.RegisterCreatedObjectUndo(mesh, "Create Collider Mesh");

                Undo.SetTransformParent(mesh.transform, col.transform, false, "Parent Collider Mesh");
            }

            Undo.CollapseUndoOperations(group);
        }

        private void OnDrawGizmos()
        {
            Handles.matrix = transform.localToWorldMatrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            if (m_GizmosDrawWheelTransform)
            {
                GizmosDrawWheelTransform();
            }

            if (m_GizmosDrawSpring)
            {
                GizmosDrawSprings();

                if (!Application.isPlaying)
                {
                    GizmosDrawSuspensionRaySpread();
                }
            }

            if (m_GizmosDrawAckermannCircle)
            {
                GizmosDrawAckermannCircle();
            }
        }

        private void GizmosDrawWheelTransform()
        {
            for (int wheelId = 0; wheelId < WheelId.Count; wheelId++)
            {
                Vector3 origin = GetLocalSpringOrigin(wheelId);

                float steerAngle = 0.0f;
                if (Application.isPlaying)
                {
                    steerAngle = m_Wheels[wheelId].SteerAngle;
                }

                var steerRot = Quaternion.AngleAxis(steerAngle, Vector3.up);

                Handles.color = Color.red;
                Handles.ArrowHandleCap(0, origin, Quaternion.LookRotation(steerRot * Vector3.right), 0.5f, EventType.Repaint);

                Handles.color = Color.blue;
                Handles.ArrowHandleCap(0, origin, steerRot, 0.5f, EventType.Repaint);
            }
        }

        private void GizmosDrawSprings()
        {
            float restLength = m_Settings.SpringRestLength;
            float maxLength = m_Settings.SpringMaxLength;

            const float lineSize = 4.0f;
            const float hitCubeSize = 0.1f;
            const float dottedSize = 2.0f;
            const float rayLineSize = 1.5f;
            const float rayCubeSize = 0.05f;

            for (int wheelId = 0; wheelId < WheelId.Count; wheelId++)
            {
                Vector3 origin = GetLocalSpringOrigin(wheelId);
                float currentLength = Application.isPlaying ? m_Springs[wheelId].CurrentLength : maxLength;
                Vector3 currentPos = origin + Vector3.down * currentLength;
                Vector3 restPos = origin + Vector3.down * restLength;
                Vector3 maxPos = origin + Vector3.down * maxLength;

                // Static/guide spring (rest + max)
                Handles.color = Color.black;
                Vector3 halfZ = new(0.0f, 0.0f, hitCubeSize * 0.5f);
                Vector3 halfX = new(hitCubeSize * 0.5f, 0.0f, 0.0f);
                Handles.DrawDottedLine(origin, maxPos, dottedSize); // full max
                Handles.DrawDottedLine(restPos + halfZ, restPos - halfZ, dottedSize); // rest marker Z
                Handles.DrawDottedLine(restPos + halfX, restPos - halfX, dottedSize); // rest marker X

                // Current spring (color, line, cube)
                Color color = (currentLength < restLength) ? Color.green : (currentLength < maxLength) ? Color.yellow : Color.red;
                Handles.color = Gizmos.color = color;
                Handles.DrawLine(origin, currentPos, lineSize);
                Gizmos.DrawCube(currentPos, Vector3.one * hitCubeSize);

                // Individual rays (play mode, multi-ray only)
                if (Application.isPlaying && m_Settings.SuspensionRays != CarSettings.SuspensionRayLayout.Single)
                {
                    Vector2 offset = Vector2.zero;
                    switch (m_Settings.SuspensionRays)
                    {
                        case CarSettings.SuspensionRayLayout.Multi3x1:
                            offset.y = m_Settings.SuspensionRaysExtents.y;
                            break;
                        case CarSettings.SuspensionRayLayout.Multi3x3:
                            offset = m_Settings.SuspensionRaysExtents;
                            break;
                        case CarSettings.SuspensionRayLayout.Multi5x3:
                            offset.x = m_Settings.SuspensionRaysExtents.x;
                            offset.y = m_Settings.SuspensionRaysExtents.y / 2;
                            break;
                    }

                    Spring spring = m_Springs[wheelId];

                    for (int i = 0; i < spring.GizmosRayCacheCount; i++)
                    {
                        Spring.GizmosRayData ray = spring.GizmosRayCache[i];

                        Vector3 rayOrigin = origin + Vector3.right * (ray.Col * offset.x) + Vector3.forward * (ray.Row * offset.y);

                        Vector3 rayEnd = rayOrigin + Vector3.down * ray.Length;

                        Color rayColor = (ray.Length < restLength) ? Color.green : (ray.Length < maxLength) ? Color.yellow : Color.red;

                        Handles.color = Gizmos.color = rayColor;
                        Handles.DrawLine(rayOrigin, rayEnd, rayLineSize);
                        Gizmos.DrawWireCube(rayEnd, Vector3.one * rayCubeSize);
                    }
                }
            }
        }

        private void GizmosDrawSuspensionRaySpread()
        {
            if (m_Settings.SuspensionRays == CarSettings.SuspensionRayLayout.Single) return;

            int rowStart, rowEnd, colStart, colEnd;
            Vector2 offset = Vector2.zero;
            switch (m_Settings.SuspensionRays)
            {
                case CarSettings.SuspensionRayLayout.Multi3x1:
                    rowStart = -1; rowEnd = 1; colStart = 0; colEnd = 0;
                    offset.y = m_Settings.SuspensionRaysExtents.y;
                    break;
                case CarSettings.SuspensionRayLayout.Multi3x3:
                    rowStart = -1; rowEnd = 1; colStart = -1; colEnd = 1;
                    offset = m_Settings.SuspensionRaysExtents;
                    break;
                case CarSettings.SuspensionRayLayout.Multi5x3:
                    rowStart = -2; rowEnd = 2; colStart = -1; colEnd = 1;
                    offset.x = m_Settings.SuspensionRaysExtents.x;
                    offset.y = m_Settings.SuspensionRaysExtents.y / 2;
                    break;
                default:
                    return;
            }

            const float markerSize = 0.04f;

            for (int wheelId = 0; wheelId < WheelId.Count; wheelId++)
            {
                Vector3 springOrigin = GetLocalSpringOrigin(wheelId);

                for (int row = rowStart; row <= rowEnd; row++)
                {
                    for (int col = colStart; col <= colEnd; col++)
                    {
                        Vector3 rayOrigin = springOrigin + Vector3.right * (col * offset.x) + Vector3.forward * (row * offset.y);

                        Gizmos.color = Color.red;
                        Gizmos.DrawWireCube(rayOrigin, Vector3.one * markerSize);
                    }
                }
            }
        }

        private void GizmosDrawAckermannCircle()
        {
            float wheelbase = m_Settings.Wheelbase;
            float turnRadius = m_Settings.TurnRadiusMin;
            float trackWidth = m_Settings.TrackWidth;

            Vector3 ackTriangleOrigin = new(-trackWidth / 2.0f, 0.0f, -wheelbase / 2.0f);

            if (!Application.isPlaying)
            {
                float turnInput = 1.0f;

                Gizmos.color = Color.black;
                Gizmos.DrawLine(ackTriangleOrigin, ackTriangleOrigin + new Vector3(0.0f, 0.0f, wheelbase));
                Gizmos.DrawLine(ackTriangleOrigin, ackTriangleOrigin + new Vector3(trackWidth, 0.0f, 0.0f));

                float ackAngleRight = Mathf.Rad2Deg * Mathf.Atan(wheelbase / (turnRadius - (trackWidth / 2.0f))) * turnInput;
                Vector3 ackRightPos = ackTriangleOrigin + new Vector3(trackWidth, 0.0f, wheelbase);
                Quaternion ackRightQuat = Quaternion.AngleAxis(ackAngleRight, Vector3.up);
                Gizmos.color = Color.white;
                Vector3 rightToCircle = ackRightQuat * Vector3.right;
                rightToCircle /= rightToCircle.z;
                rightToCircle *= -wheelbase;
                Gizmos.DrawRay(ackRightPos, rightToCircle);
                Gizmos.DrawRay(ackRightPos, ackRightQuat * Vector3.forward);

                float ackAngleLeft = Mathf.Rad2Deg * Mathf.Atan(wheelbase / (turnRadius + (trackWidth / 2.0f))) * turnInput;
                Vector3 ackLeftPos = ackTriangleOrigin + new Vector3(0.0f, 0.0f, wheelbase);
                Quaternion ackLeftQuat = Quaternion.AngleAxis(ackAngleLeft, Vector3.up);
                Vector3 leftToCircle = ackLeftQuat * Vector3.right;
                leftToCircle /= leftToCircle.z;
                leftToCircle *= -wheelbase;
                Gizmos.DrawRay(ackLeftPos, leftToCircle);
                Gizmos.DrawRay(ackLeftPos, ackLeftQuat * Vector3.forward);
            }

            Handles.color = Color.black;
            Vector3 discOrigin = ackTriangleOrigin + new Vector3(trackWidth / 2.0f + turnRadius, 0.0f);
            Handles.DrawWireDisc(discOrigin, Vector3.up, turnRadius);

            if (!Application.isPlaying)
            {
                Gizmos.color = Color.black;
                Gizmos.DrawRay(discOrigin, Vector3.left * (turnRadius - trackWidth / 2.0f));
                Gizmos.DrawRay(discOrigin, new Vector3(0.0f, 0.0f, turnRadius));
            }
        }
#endif
    }
}
