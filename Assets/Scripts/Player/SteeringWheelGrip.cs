using UnityEngine;

// Moves the driver's hands toward the steering wheel using IK.
// VehicleEnterExit adds this to the driver's model and turns it off when they get out.
[RequireComponent(typeof(Animator))]
public class SteeringWheelGrip : MonoBehaviour
{
    [Tooltip("How much the hands move to the grips (0-1).")]
    [Range(0f, 1f)] public float positionWeight = 1f;
    [Tooltip("How much the hands match the grip rotation (0-1). Off for now.")]
    [Range(0f, 1f)] public float rotationWeight = 0f;
    [Tooltip("Where the elbows point compared to the hands (x = out, y = up, z = forward).")]
    public Vector3 elbowHintOffset = new Vector3(0.3f, -0.3f, -0.15f);
    [Tooltip("Seconds for the hands to move onto the wheel after sitting down.")]
    public float blendInTime = 0.3f;

    private Animator _animator;
    private SteeringWheel _wheel;
    private Transform _modelRoot, _seat;
    private Vector3 _pinLocalPos;
    private Quaternion _pinLocalRot;
    private float _blend;

    private void Awake() => _animator = GetComponent<Animator>();

    // Called every frame while driving, pin pos/rot is where the model gets placed in the seat
    public void Hold(SteeringWheel wheel, Transform modelRoot, Transform seat, Vector3 pinLocalPos, Quaternion pinLocalRot)
    {
        if (!enabled || _wheel != wheel) _blend = 0f;
        _wheel = wheel;
        _modelRoot = modelRoot;
        _seat = seat;
        _pinLocalPos = pinLocalPos;
        _pinLocalRot = pinLocalRot;
        enabled = true;
    }

    public void Release()
    {
        enabled = false;
        _wheel = null;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (_wheel == null || _modelRoot == null || _seat == null) return;

        _blend = blendInTime > 0f ? Mathf.MoveTowards(_blend, 1f, Time.deltaTime / blendInTime) : 1f;

        // The model gets moved into the seat after this runs, so line the grips up with
        // where the model will end up (otherwise the hands lag behind when the truck moves)
        Quaternion finalRot = _seat.rotation * _pinLocalRot;
        Matrix4x4 final = Matrix4x4.TRS(_seat.TransformPoint(_pinLocalPos), finalRot, _modelRoot.lossyScale);
        Matrix4x4 toNow = _modelRoot.localToWorldMatrix * final.inverse;
        Quaternion rotToNow = _modelRoot.rotation * Quaternion.Inverse(finalRot);

        SetHand(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, _wheel.LeftHandGrip, -1f, toNow, rotToNow);
        SetHand(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, _wheel.RightHandGrip, 1f, toNow, rotToNow);
    }

    private void SetHand(AvatarIKGoal goal, AvatarIKHint hint, Transform grip, float side, Matrix4x4 toNow, Quaternion rotToNow)
    {
        if (grip == null) return;

        _animator.SetIKPositionWeight(goal, positionWeight * _blend);
        _animator.SetIKPosition(goal, toNow.MultiplyPoint3x4(grip.position));
        _animator.SetIKRotationWeight(goal, rotationWeight * _blend);
        _animator.SetIKRotation(goal, rotToNow * grip.rotation);

        // Point the elbows out and down
        Vector3 hintWorld = grip.position + _seat.TransformVector(new Vector3(elbowHintOffset.x * side, elbowHintOffset.y, elbowHintOffset.z));
        _animator.SetIKHintPositionWeight(hint, positionWeight * _blend);
        _animator.SetIKHintPosition(hint, toNow.MultiplyPoint3x4(hintWorld));
    }
}
