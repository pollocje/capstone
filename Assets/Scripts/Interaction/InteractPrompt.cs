using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// On-screen hint on the player's HUD Canvas: "Press E to pick up Map" when standing near or
/// looking at a hotbar pickup, and "Press Tab to open the map" while the map is in hand.
/// Put next to PlayerGrabController on the player. Builds its own text at runtime.
/// </summary>
public class InteractPrompt : MonoBehaviour
{
    [Tooltip("Canvas the prompt is added to. Found under the player root (\"HUD Canvas\") if left empty.")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private float fontSize = 32f;
    [Tooltip("Height above the bottom of the screen, in canvas units (above the hotbar).")]
    [SerializeField] private float bottomOffset = 190f;

    private PlayerGrabController _grab;
    private MapItem _map;
    private PlayerInput _playerInput;
    private TextMeshProUGUI _text;

    void Awake()
    {
        _grab = GetComponent<PlayerGrabController>();
        _map = transform.root.GetComponentInChildren<MapItem>(true);
        _playerInput = GetComponentInParent<PlayerInput>();
        if (_playerInput == null) _playerInput = transform.root.GetComponentInChildren<PlayerInput>(true);

        if (canvas == null)
        {
            foreach (var c in transform.root.GetComponentsInChildren<Canvas>(true))
                if (c.name == "HUD Canvas") canvas = c;
        }
        if (canvas == null)
        {
            Debug.LogWarning("InteractPrompt: no HUD Canvas found under the player, so no prompt is shown.", this);
            return;
        }

        var go = new GameObject("InteractPrompt", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, bottomOffset);
        rt.sizeDelta = new Vector2(900f, 60f);

        _text = go.AddComponent<TextMeshProUGUI>();
        _text.alignment = TextAlignmentOptions.Center;
        _text.fontSize = fontSize;
        _text.color = Color.white;
        _text.outlineWidth = 0.2f;
        _text.outlineColor = Color.black;
        _text.raycastTarget = false;
        _text.text = "";
    }

    void Update()
    {
        if (_text == null) return;
        _text.text = CurrentPrompt();
    }

    string CurrentPrompt()
    {
        // Remote players have PlayerInput disabled — only the local player gets prompts.
        if (_playerInput != null && !_playerInput.enabled) return "";

        // Proximity prompts (walk up and press E) take priority over aim-based ones below.
        string vehicle = VehiclePrompt();
        if (vehicle != "") return vehicle;

        string dispenser = DispenserPrompt();
        if (dispenser != "") return dispenser;

        // Same priority as PlayerGrabController.TryInteract: aimed pickup, then putting the map down.
        ItemPickup aimed = _grab != null ? _grab.FindAimedPickup() : null;
        if (aimed != null && aimed.Item != null && aimed.IsArmed)
            return $"Press <b>E</b> to pick up {aimed.Item.itemName}";

        if (_map != null && _map.IsEquipped)
        {
            string tab = _map.IsUsing ? "Press <b>Tab</b> to close the map" : "Press <b>Tab</b> to open the map";
            return _grab != null && _grab.IsHoldingMap ? tab + "   ·   <b>E</b> to put it down" : tab;
        }

        ItemPickup pickup = _grab != null ? _grab.FindPickupTarget() : null;
        if (pickup != null && pickup.Item != null && pickup.IsArmed)
            return $"Press <b>E</b> to pick up {pickup.Item.itemName}";

        return "";
    }

    string DispenserPrompt()
    {
        foreach (var dispenser in ItemDispenser.Active)
        {
            if (dispenser == null || !dispenser.PlayerInRange) continue;
            return dispenser.IsEmpty ? "Empty" : "Press <b>E</b> to get an item";
        }
        return "";
    }

    string VehiclePrompt()
    {
        foreach (var vehicle in VehicleEnterExit.Active)
        {
            if (vehicle != null && vehicle.TryGetSeatPrompt(transform.position, out string seatLabel))
                return $"Press <b>E</b> to get into {seatLabel}";
        }
        return "";
    }
}
