using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Map-specific grab behavior. Put next to Grabbable on the world map item.
/// While carried it swings like any other Grabbable. Left click on a carried map opens the
/// holder's MapItem view (Hotbar ignores left click while something is carried), and while
/// the view is open it tells PlayerGrabController to hold the map steady.
/// </summary>
[RequireComponent(typeof(Grabbable))]
public class MapGrabbable : MonoBehaviour, IGrabSwingSuppressor
{
    [SerializeField] private string useActionName = "Attack";

    private Grabbable _grabbable;
    private MapItem _viewer;
    private InputAction _use;

    public bool SuppressSwing => _viewer != null && _viewer.IsUsing;

    void Awake() => _grabbable = GetComponent<Grabbable>();

    void OnEnable()
    {
        _grabbable.Grabbed += OnGrabbed;
        _grabbable.Released += OnReleased;
    }

    void OnDisable()
    {
        _grabbable.Grabbed -= OnGrabbed;
        _grabbable.Released -= OnReleased;
    }

    void OnGrabbed(PlayerGrabController holder)
    {
        // MapItem lives on the holder's player, not on this item.
        Transform player = holder.transform.root;
        _viewer = player.GetComponentInChildren<MapItem>(true);

        var playerInput = player.GetComponentInChildren<PlayerInput>(true);
        _use = playerInput != null ? playerInput.actions?.FindAction(useActionName) : null;

        if (_viewer == null) Debug.LogWarning("MapGrabbable: holder has no MapItem, so the map view can't open.", this);
        if (_use == null) Debug.LogWarning($"MapGrabbable: no '{useActionName}' action on the holder's PlayerInput.", this);
    }

    void OnReleased(PlayerGrabController holder)
    {
        // Close the view so dropping the map doesn't leave the overlay up.
        if (_viewer != null && _viewer.IsUsing) _viewer.OnUnequip();
        _viewer = null;
        _use = null;
    }

    void Update()
    {
        if (!_grabbable.IsHeld || _viewer == null || _use == null) return;

        if (_use.WasPressedThisFrame()) _viewer.OnUseDown();
        else if (_use.WasReleasedThisFrame()) _viewer.OnUseUp();
    }
}
