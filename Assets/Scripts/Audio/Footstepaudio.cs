using UnityEngine;

/// <summary>
/// Plays footstep sounds based on how far the character moves while on the ground.
/// Put this on PlayerCapsule (the object that actually moves).
///
/// No networking needed: every machine sees every player move (NetworkTransform syncs
/// the position), so each machine plays each player's footsteps locally.
/// That means you hear your own steps AND other players' steps from where they are.
/// </summary>
public class FootstepAudio : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] AudioSource footstepSource;   // its own AudioSource, 3D
    [SerializeField] AudioClip[] footstepClips;    // several clips = less repetitive
    [Range(0f, 1f)][SerializeField] float volume = 0.6f;
    [Range(0f, 0.3f)][SerializeField] float pitchVariation = 0.08f;

    [Header("Timing")]
    // Metres travelled per footstep when walking. Smaller = faster steps.
    [SerializeField] float stepDistance = 1.8f;
    // Below this speed (m/s) the character counts as standing still.
    [SerializeField] float minSpeed = 0.5f;
    // Above this speed (m/s) no steps play — stops footsteps while riding in the truck or teleporting.
    [SerializeField] float maxSpeed = 9f;

    [Header("Sprinting")]
    // Match FirstPersonController's Move Speed.
    [SerializeField] float walkSpeed = 4f;
    // Match FirstPersonController's Sprint Speed.
    [SerializeField] float sprintSpeed = 6f;
    // Metres per footstep at full sprint. Lower than Step Distance = noticeably quicker steps.
    [SerializeField] float sprintStepDistance = 1.2f;
    // Extra volume at full sprint (0 = none).
    [Range(0f, 0.5f)][SerializeField] float sprintVolumeBoost = 0.2f;

    [Header("Ground Check")]
    [SerializeField] float groundCheckDistance = 0.3f;
    [SerializeField] LayerMask groundLayers = ~0;

    Vector3 _lastPosition;
    float _distanceSinceStep;
    int _lastClipIndex = -1;
    float _smoothedSpeed;
    float _sprintAmount; // 0 = walking, 1 = full sprint

    void Start()
    {
        _lastPosition = transform.position;
    }

    void Update()
    {
        // Only count horizontal movement (jumping/falling shouldn't make steps).
        Vector3 delta = transform.position - _lastPosition;
        delta.y = 0f;
        _lastPosition = transform.position;

        float distance = delta.magnitude;
        float speed = distance / Mathf.Max(Time.deltaTime, 0.0001f);

        bool moving = speed > minSpeed && speed < maxSpeed;
        if (!moving || !IsGrounded())
        {
            // Primed so the first step plays soon after starting to walk again.
            _distanceSinceStep = stepDistance * 0.5f;
            return;
        }

        // Smooth the speed (frame-to-frame movement is jittery, especially for other players),
        // then work out how close to a full sprint we are.
        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, speed, Time.deltaTime * 8f);
        _sprintAmount = Mathf.InverseLerp(walkSpeed, sprintSpeed, _smoothedSpeed);

        // Steps get closer together as you speed up, so sprinting clearly sounds faster.
        float currentStepDistance = Mathf.Lerp(stepDistance, sprintStepDistance, _sprintAmount);

        _distanceSinceStep += distance;
        if (_distanceSinceStep >= currentStepDistance)
        {
            _distanceSinceStep = 0f;
            PlayFootstep();
        }
    }

    bool IsGrounded()
    {
        // Ray from just above the feet straight down.
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down,
                               0.1f + groundCheckDistance, groundLayers, QueryTriggerInteraction.Ignore);
    }

    void PlayFootstep()
    {
        if (footstepSource == null || footstepClips == null || footstepClips.Length == 0) return;

        // Pick a random clip, but never the same one twice in a row.
        int index = Random.Range(0, footstepClips.Length);
        if (footstepClips.Length > 1 && index == _lastClipIndex)
            index = (index + 1) % footstepClips.Length;
        _lastClipIndex = index;

        footstepSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        float stepVolume = Mathf.Clamp01(volume + sprintVolumeBoost * _sprintAmount);
        footstepSource.PlayOneShot(footstepClips[index], stepVolume);
    }
}