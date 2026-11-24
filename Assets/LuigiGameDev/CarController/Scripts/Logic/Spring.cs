using UnityEngine;

namespace LuigiGameDev.CarController.Logic
{
    public class Spring
    {
#if UNITY_EDITOR
        public struct GizmosRayData
        {
            public int Row;
            public int Col;
            public float Length;
        }

        public readonly GizmosRayData[] GizmosRayCache = new GizmosRayData[15];

        public int GizmosRayCacheCount { get; private set; }
#endif

        private readonly int m_WheelId; // FrontLeft = 0, FrontRight = 1, BackRight = 2, BackLeft = 3
        private readonly Vector3 m_LocalOrigin;
        private readonly Rigidbody m_CarRigidbody;
        private readonly Transform m_CarTransform;
        private readonly CarSettings m_Settings;

        private float m_CurrentLength; // Spring origin to ground contact point
        private float m_Velocity; // Rate of change of length. Positive = extending, negative = compressing
        private bool m_IsGrounded;
        private PhysicsMaterial m_GroundMaterial;

        public Spring(int wheelId, Vector3 localOrigin, Rigidbody carRigidbody, CarSettings settings)
        {
            m_WheelId = wheelId;
            m_LocalOrigin = localOrigin;
            m_CarRigidbody = carRigidbody;
            m_CarTransform = carRigidbody.transform;
            m_Settings = settings;
        }

        public int WheelId => m_WheelId;

        public Vector3 LocalOrigin => m_LocalOrigin;

        public float CurrentLength => m_CurrentLength;

        public float Velocity => m_Velocity;

        public bool IsGrounded => m_IsGrounded;

        public PhysicsMaterial GroundMaterial => m_GroundMaterial;

        public void FixedUpdate()
        {
            // Cast rays in a rows x cols grid around origin and aggregate results
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
                default: // Single
                    rowStart = 0; rowEnd = 0; colStart = 0; colEnd = 0;
                    break;
            }

            Vector3 origin = m_CarTransform.TransformPoint(m_LocalOrigin);

            int hitCount = 0;
            float totalLength = 0.0f;
            Vector3 totalNormal = Vector3.zero;
            Vector3 totalHitPoint = Vector3.zero;
            PhysicsMaterial groundMaterial = null;
            Rigidbody groundRigidbody = null;

#if UNITY_EDITOR
            GizmosRayCacheCount = 0;
#endif

            for (int row = rowStart; row <= rowEnd; row++)
            {
                for (int col = colStart; col <= colEnd; col++)
                {
                    Vector3 rayOrigin = origin + m_CarTransform.right * (col * offset.x) + m_CarTransform.forward * (row * offset.y);

                    bool isHit = Physics.Raycast(rayOrigin, -m_CarTransform.up, out var hit, m_Settings.SpringMaxLength, m_Settings.GroundLayerMask, QueryTriggerInteraction.Ignore);

#if UNITY_EDITOR
                    GizmosRayCache[GizmosRayCacheCount++] = new GizmosRayData
                    {
                        Row = row,
                        Col = col,
                        Length = isHit ? hit.distance : m_Settings.SpringMaxLength
                    };
#endif

                    if (isHit)
                    {
                        hitCount++;
                        totalLength += hit.distance;
                        totalNormal += hit.normal;
                        totalHitPoint += hit.point;

                        // First hit wins for ground material
                        if (groundMaterial == null)
                        {
                            groundMaterial = hit.collider.sharedMaterial ? hit.collider.sharedMaterial : m_Settings.DefaultGroundMaterial;
                        }

                        if (groundRigidbody == null && hit.rigidbody != null && !hit.rigidbody.isKinematic)
                        {
                            groundRigidbody = hit.rigidbody;
                        }
                    }
                }
            }

            if (hitCount > 0)
            {
                // Keep previous length to calculate velocity and update current length
                float previousLength = m_CurrentLength;
                m_CurrentLength = totalLength / hitCount;

                bool firstGroundContact = !m_IsGrounded;
                m_IsGrounded = true;
                m_GroundMaterial = groundMaterial;

                Vector3 avgNormal = (totalNormal / hitCount).normalized;
                Vector3 avgHitPoint = totalHitPoint / hitCount;

                // Compression below RestLength. Positive = compressed.
                float displacement = m_Settings.SpringRestLength - m_CurrentLength;

                // Spring force: pushes up only when compressed (currentLength < RestLength). Zero otherwise.
                float springForce = m_CurrentLength < m_Settings.SpringRestLength ? m_Settings.SpringConstant * displacement : 0.0f;

                // Suspension velocity: rate of length change.
                // Zeroed on first contact to avoid a force spike on landing (previous length was max while off the ground).
                m_Velocity = firstGroundContact ? 0.0f : (m_CurrentLength - previousLength) / Time.fixedDeltaTime;

                // Damper force: slows spring movement to stop bouncing.
                // Active over the full ray length, so damping stays active even when the spring is unloaded (length > RestLength).
                float damperForce = -m_Settings.SpringDamperConstant * m_Velocity;

                // Total force clamp: push up only, never pull down.
                // Fast extension while length > RestLength (spring = 0) gives a negative damper force, which the clamp turns into 0.
                // Force in Newtons.
                float totalForce = Mathf.Max(0.0f, springForce + damperForce);
                
                // Force direction
                // carUp = chassis-relative up. hit.normal = hit surface's perpendicular.
                // carUp: stays vertical on curbs/walls (safe, no sideways kick), ignores slope tilt.
                // hit.normal: follows slopes/banking (feels natural), risks a sideways kick on curbs/walls.
                Vector3 forceDirection = Vector3.Slerp(m_CarTransform.up, avgNormal, m_Settings.SpringNormalBlend);

                m_CarRigidbody.AddForceAtPosition(forceDirection * totalForce, avgHitPoint);

                // Newton's third law: if the ground is a dynamic rigidbody, push back on it.
                if (groundRigidbody != null)
                {
                    groundRigidbody.AddForceAtPosition(-forceDirection * totalForce, avgHitPoint);
                }
            }
            else
            {
                m_IsGrounded = false;
                m_GroundMaterial = null;

                // Hold maximum extension length so delta calculation is valid upon next ground contact.
                m_CurrentLength = m_Settings.SpringMaxLength;
                m_Velocity = 0.0f;
            }
        }
    }
}
