using UnityEngine;

namespace LuigiGameDev.CarController.Logic
{
    /// <summary>
    /// Exposes all car physics parameters for configuration.
    /// </summary>
    public class CarSettings : MonoBehaviour
    {
        public enum SuspensionRayLayout { Single, Multi3x1, Multi3x3, Multi5x3 }

        // AXLES -------------------------------------------------------------------------------------------------------
        [Header("AXLES \n" +
                "Defines wheel's origin positions. \n" +
                "Visualize with gizmos.")]
        
        [Tooltip("(Z-axis): distance between front and rear axles.")]
        [Min(0.1f)] public float Wheelbase = 2.5f;

        [Tooltip("(X-axis): distance between left and right wheels on an axle.")]
        [Min(0.1f)] public float TrackWidth = 1.5f;

        [Tooltip("(Y-axis): position of the axles relative to the car's center. Usually negative (below center).")]
        public float AxleHeight = -0.35f;
        
        // FORCES OFFSET -----------------------------------------------------------------------------------------------
        [Header("-----")]
        [Header("FORCES OFFSET \n" +
                "Offsets from center of mass when applying forces. \n" +
                "Causes tilt when accelerating, braking and cornering.")]
        
        [Tooltip("Offset for drive/brake forces. Adds forward/back tilt.")]
        [Min(0.0f)] public float DriveForcesOffset = 0.2f;
        
        [Tooltip("Offset for grip forces. Adds side tilt.")]
        [Min(0.0f)] public float GripForcesOffset = 0.1f;

        // SUSPENSION --------------------------------------------------------------------------------------------------
        [Header("-----")]
        [Header("SUSPENSION \n" +
                "Spring and damper behavior. Controls ride height, stiffness, and oscillation. \n" +
                "Visualize with gizmos.")]

        [Tooltip("Length where spring force is zero. The car sits lower than this under its own weight.")]
        [Min(0.0f)] public float SpringRestLength = 0.35f;

        [Tooltip("Maximum spring length and raycast distance.")]
        [Min(0.0f)] public float SpringMaxLength = 0.5f;

        [Tooltip("Spring stiffness. Higher = stiffer, less sag. Try ~15-25x car mass (kg).")]
        [Min(0.0f)] public float SpringConstant = 3000.0f;

        [Tooltip("Damping strength. Reduces bounce. Try ~5-10% of SpringConstant.")]
        [Min(0.0f)] public float SpringDamperConstant = 150.0f;

        [Tooltip("Spring force direction blend: 0 = chassis up (geometric axis), 1 = ground surface normal.")]
        [Range(0.0f, 1.0f)] public float SpringNormalBlend = 0.0f;

        [Tooltip("Raycast layout per wheel: Single (1 ray), Multi3x1 (3 longitudinal rays), Multi3x3 (9-ray grid), Multi5x3 (15-ray grid).")]
        public SuspensionRayLayout SuspensionRays = SuspensionRayLayout.Single;

        [Tooltip("Half-extents around spring origin. X = lateral spread, Y = longitudinal spread. Used in Multi modes.")]
        public Vector2 SuspensionRaysExtents = new(0.15f, 0.15f);

        // GROUNDING ---------------------------------------------------------------------------------------------------
        [Header("-----")]
        [Header("GROUNDING \n" +
                "Controls what surfaces the suspension detects and how they behave.")]
        
        [Tooltip("Fallback material when no special ground is detected.")]
        public PhysicsMaterial DefaultGroundMaterial;

        [Tooltip("Layers the suspension raycasts can hit. Use to exclude specific layers like water or triggers.")]
        public LayerMask GroundLayerMask = 1;

        // ENGINE ------------------------------------------------------------------------------------------------------
        [Header("-----")]
        [Header("ENGINE \n" +
                "Drive/brakes strength and speed limits. \n" +
                "Try ~5x the car mass for drive/brakes strength values.")]
        
        [Tooltip("Strength accelerating forward.")]
        [Min(0.0f)] public float DriveStrength = 500.0f;
        
        [Tooltip("Strength accelerating backwards.")]
        [Min(0.0f)] public float ReverseDriveStrength = 500.0f;
        
        [Tooltip("Strength when braking.")]
        [Min(0.0f)] public float BrakeStrength = 500.0f;
        
        [Tooltip("Forward speed cap in m/s.")]
        [Min(0.0f)] public float MaxSpeed = 25.0f;
        
        [Tooltip("Reverse speed cap in m/s.")]
        [Min(0.0f)] public float ReverseMaxSpeed = 20.0f;
        
        [Tooltip("Constant deceleration in m/s² simulating rolling resistance. Higher = stops faster.")]
        [Min(0.0f)] public float RollingResistanceStrength = 3.0f;
        
        // STEERING ----------------------------------------------------------------------------------------------------
        [Header("-----")]
        [Header("STEERING \n" +
                "Controls turning and grip behavior. \n" +
                "At higher speeds: turning circle increases, grip decreases. \n" +
                "Visualize radius with gizmos.")] 
        
        [Tooltip("Turning circle radius at low speed.")]
        [Min(0.0f)] public float TurnRadiusMin = 6.0f;
        
        [Tooltip("Turning circle radius at max speed (bigger at high speed).")]
        [Min(0.0f)] public float TurnRadiusMax = 16.0f;
        
        [Tooltip("Slope angle at which full sideways grip compensation is applied to prevent sliding down slopes.")]
        [Range(0.0f, 90.0f)] public float FullCompensationSlopeAngle = 20.0f;
        
        [Tooltip("Grip at low speed (1 = max, 0 = none).")]
        [Range(0.0f, 1.0f)] public float GripMin = 0.25f;
        
        [Tooltip("Grip at max speed (lower = more sliding).")]
        [Range(0.0f, 1.0f)] public float GripMax = 0.15f; 

        [Tooltip("Smoothing time for grip transitions between low and high speed.")]
        [Range(0.0001f, 1.0f)] public float GripSmoothTime = 0.2f;
        
        // HANDBRAKE ---------------------------------------------------------------------------------------------------
        [Header("-----")]
        [Header("HANDBRAKE \n" +
                "Reduces grip/traction while handbrake held. \n" +
                "Leading axle = in throttle direction (forward/back).")]

        [Tooltip("Grip multiplier for the leading axle (throttle direction).")]
        [Range(0.0f, 1.0f)] public float HandbrakeLeadingGrip = 0.3f;

        [Tooltip("Grip multiplier for the trailing axle (opposite direction).")]
        [Range(0.0f, 1.0f)] public float HandbrakeTrailingGrip = 0.2f;

        [Tooltip("Traction multiplier for the leading axle (throttle direction).")]
        [Range(0.0f, 1.0f)] public float HandbrakeLeadingTraction = 0.8f;

        [Tooltip("Traction multiplier for the trailing axle (opposite direction).")]
        [Range(0.0f, 1.0f)] public float HandbrakeTrailingTraction = 0.7f;
        
        // -------------------------------------------------------------------------------------------------------------

        public float EvalTurnRadius(float speedNormalized)
        {
            return Mathf.Lerp(TurnRadiusMin, TurnRadiusMax, speedNormalized);
        }

        public float EvalGrip(float speedNormalized)
        {
            return Mathf.Lerp(GripMin, GripMax, speedNormalized);
        }
    }
}
