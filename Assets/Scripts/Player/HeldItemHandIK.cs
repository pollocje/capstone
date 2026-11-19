using UnityEngine;

// Raises the right hand to hold an item out in front of the body, using IK.
// HeldItemThirdPerson adds this to the player model and tells it when an item is out.
[RequireComponent(typeof(Animator))]
public class HeldItemHandIK : MonoBehaviour
{
    [Tooltip("Seconds for the arm to move up or down.")]
    public float blendTime = 0.2f;
    [Tooltip("Where the elbow points compared to the hand (x = out, y = up, z = forward).")]
    public Vector3 elbowHintOffset = new Vector3(0.3f, -0.3f, -0.2f);

    private Animator _animator;
    private SteeringWheelGrip _wheel;
    private Transform _body;
    private Vector3 _handOffset;
    private bool _holding;
    private float _blend;

    private void Awake() => _animator = GetComponent<Animator>();

    // handOffset is from the hips, facing the same way as body (x = right, y = up, z = forward)
    public void Hold(Transform body, Vector3 handOffset)
    {
        _body = body;
        _handOffset = handOffset;
        _holding = true;
    }

    public void Release() => _holding = false;

    private void OnAnimatorIK(int layerIndex)
    {
        float target = _holding ? 1f : 0f;
        _blend = blendTime > 0f ? Mathf.MoveTowards(_blend, target, Time.deltaTime / blendTime) : target;
        if (_blend <= 0f || _body == null) return;

        // The steering wheel gets the hands while driving
        if (_wheel == null) _wheel = GetComponent<SteeringWheelGrip>();
        if (_wheel != null && _wheel.enabled) return;

        // Hips are already where they'll be this frame, so the hand moves with the body (even in the truck).
        // humanScale keeps the offsets the same size on bigger or smaller models.
        Quaternion body = _body.rotation;
        float scale = _animator.humanScale;
        Vector3 hand = _animator.bodyPosition + body * (_handOffset * scale);
        Vector3 elbow = hand + body * (elbowHintOffset * scale);

        _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _blend);
        _animator.SetIKPosition(AvatarIKGoal.RightHand, hand);
        _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, _blend);
        _animator.SetIKHintPosition(AvatarIKHint.RightElbow, elbow);
    }
}
