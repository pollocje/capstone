using UnityEngine;
using UnityEngine.Rendering;

// Shows the hotbar item a player has out in their model's right hand, so other players can see it.
// Hotbar syncs which item type is out; this builds a copy of that item's model on every client.
// Set up by Tools > Items > Set Up Held Items.
public class HeldItemThirdPerson : MonoBehaviour
{
    [System.Serializable]
    public class HeldItem
    {
        public ItemType type;
        [Tooltip("Model to show. Only its meshes are copied, so the world pickup prefab works.")]
        public GameObject model;
        [Tooltip("Offset from the hand, facing the same way as the body (x = right, y = up, z = forward).")]
        public Vector3 offset;
        [Tooltip("Rotation, facing the same way as the body.")]
        public Vector3 euler;
        [Tooltip("Multiplier on the model's own scale.")]
        public float scale = 1f;
    }

    public HeldItem[] items = new HeldItem[0];

    private Hotbar _hotbar;
    private PlayerModelSwapper _swapper;
    private Transform _hand;
    private GameObject _visual;
    private HeldItem _shown;
    private int _shownType = -1;

    void Awake()
    {
        _hotbar = GetComponent<Hotbar>();
        _swapper = transform.root.GetComponentInChildren<PlayerModelSwapper>(true);
    }

    // LateUpdate so the hand has already been animated this frame
    void LateUpdate()
    {
        int type = _hotbar != null ? _hotbar.HeldType : -1;
        if (type != _shownType) Show(type);
        if (_visual == null) return;

        // The hand goes away when the model is swapped, so find it again
        if (_hand == null) _hand = FindHand();
        if (_hand == null)
        {
            _visual.SetActive(false);
            return;
        }

        Quaternion body = BodyRotation();
        _visual.transform.SetPositionAndRotation(
            _hand.position + body * _shown.offset,
            body * Quaternion.Euler(_shown.euler));
        _visual.SetActive(true);
    }

    void Show(int type)
    {
        if (_visual != null) Destroy(_visual);
        _visual = null;
        _shown = null;
        _shownType = type;

        foreach (var item in items)
        {
            if (item != null && (int)item.type == type && item.model != null)
            {
                _shown = item;
                break;
            }
        }
        if (_shown == null) return;

        _visual = HeldModelUtility.CopyMeshes(_shown.model.transform, transform);
        _visual.name = "ThirdPersonHeld" + _shown.type;
        _visual.transform.localScale = _shown.model.transform.localScale * _shown.scale;

        // Your own body only casts a shadow (first-person), so the item does the same
        var shadows = IsOwnPlayer() ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        foreach (var r in _visual.GetComponentsInChildren<Renderer>(true))
            r.shadowCastingMode = shadows;

        _visual.SetActive(false);
    }

    Transform FindHand()
    {
        if (_swapper == null || _swapper.modelRoot == null) return null;

        var animator = _swapper.modelRoot.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman) return null;
        return animator.GetBoneTransform(HumanBodyBones.RightHand);
    }

    // PlayerModel turns with the player, so it's the body's facing direction
    Quaternion BodyRotation() =>
        _swapper != null && _swapper.modelRoot != null ? _swapper.modelRoot.rotation : transform.rotation;

    bool IsOwnPlayer() => _hotbar == null || !_hotbar.IsSpawned || _hotbar.IsOwner;

    void OnDestroy()
    {
        if (_visual != null) Destroy(_visual);
    }
}
