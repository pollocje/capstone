using UnityEngine;

// Feeds PlayerAnimator's Speed and Grounded parameters from how the player actually moves.
// Works on every client: remote players have FirstPersonController disabled, but
// NetworkTransform still moves them, so measuring position change gives the right speed.
public class PlayerAnimationDriver : MonoBehaviour
{
    [Tooltip("Leave empty to use the CharacterController's transform (the thing that actually moves).")]
    public Transform movingRoot;

    [Tooltip("Leave at Nothing to copy GroundLayers from the FirstPersonController.")]
    public LayerMask groundLayers;
    public float groundedOffset = -0.14f;
    public float groundedRadius = 0.3f;
    public float speedDampTime = 0.1f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int GroundedHash = Animator.StringToHash("Grounded");

    Animator _animator;
    Vector3 _lastPosition;

    void Start()
    {
        if (movingRoot == null)
        {
            var cc = GetComponentInChildren<CharacterController>(true);
            movingRoot = cc != null ? cc.transform : transform;
        }

        if (groundLayers.value == 0)
        {
            var fpc = GetComponentInChildren<StarterAssets.FirstPersonController>(true);
            groundLayers = fpc != null ? fpc.GroundLayers : Physics.DefaultRaycastLayers;
        }

        _lastPosition = movingRoot.position;
    }

    void Update()
    {
        // The model (and its Animator) is replaced at runtime by PlayerModelSwapper, and the
        // player-level Animator gets disabled, so always look for the enabled one.
        if (_animator == null || !_animator.isActiveAndEnabled)
        {
            _animator = FindActiveAnimator();
            if (_animator != null)
            {
                // The owner's own body is shadow-only, so don't let culling pause the animation.
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Debug.Log($"PlayerAnimationDriver: driving Animator on '{_animator.name}'", _animator);
            }
        }

        Vector3 position = movingRoot.position;
        Vector3 delta = position - _lastPosition;
        _lastPosition = position;
        if (_animator == null || Time.deltaTime <= 0f) return;

        delta.y = 0f;
        float speed = delta.magnitude / Time.deltaTime;

        Vector3 spherePos = new Vector3(position.x, position.y - groundedOffset, position.z);
        bool grounded = Physics.CheckSphere(spherePos, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);

        _animator.SetFloat(SpeedHash, speed, speedDampTime, Time.deltaTime);
        _animator.SetBool(GroundedHash, grounded);
    }

    Animator FindActiveAnimator()
    {
        foreach (var animator in GetComponentsInChildren<Animator>())
            if (animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
                return animator;
        return null;
    }
}
