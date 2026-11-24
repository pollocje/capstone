using UnityEngine;
using UnityEngine.Rendering;
using Unity.Netcode;

// Gives each player a different model based on their player slot (0-3).
// The host assigns the slot; every client applies the matching model locally.
public class PlayerModelSelector : NetworkBehaviour
{
    [Tooltip("One model per player slot. Slot 0 = first player in, etc.")]
    public GameObject[] models = new GameObject[4];

    [Tooltip("Material put on every renderer of the spawned model. The player FBXs carry their colors as vertex colors (painted in Blender) and import with no material, so this should use the Capstone/Player Vertex Color Lit shader - one material colors all four models.")]
    public Material modelMaterial;

    private readonly NetworkVariable<int> _slot = new(-1);
    private PlayerModelSwapper _swapper;

    public int Slot => _slot.Value;

    void Awake()
    {
        _swapper = GetComponentInChildren<PlayerModelSwapper>(true);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            _slot.Value = FindFreeSlot();

        _slot.OnValueChanged += OnSlotChanged;
        if (_slot.Value >= 0)
            ApplyModel(_slot.Value);
    }

    public override void OnNetworkDespawn()
    {
        _slot.OnValueChanged -= OnSlotChanged;
    }

    void Start()
    {
        // Singleplayer: never network-spawned, so just use the first model.
        if (!IsSpawned)
            ApplyModel(0);
    }

    void OnSlotChanged(int previous, int current)
    {
        if (current >= 0)
            ApplyModel(current);
    }

    // Lowest slot not held by another player, so slots free up when someone leaves.
    int FindFreeSlot()
    {
        var taken = new bool[models.Length];
        foreach (var other in FindObjectsByType<PlayerModelSelector>(FindObjectsSortMode.None))
        {
            if (other == this || !other.IsSpawned) continue;
            if (other.Slot >= 0 && other.Slot < taken.Length) taken[other.Slot] = true;
        }

        for (int i = 0; i < taken.Length; i++)
            if (!taken[i]) return i;

        Debug.LogWarning("PlayerModelSelector: More players than models, reusing slot 0.");
        return 0;
    }

    void ApplyModel(int slot)
    {
        if (_swapper == null)
        {
            Debug.LogError("PlayerModelSelector: No PlayerModelSwapper found in children.");
            return;
        }

        if (slot >= models.Length || models[slot] == null)
        {
            Debug.LogWarning($"PlayerModelSelector: No model assigned for slot {slot}.");
            return;
        }

        var model = _swapper.SwapModel(models[slot]);

        if (model != null && modelMaterial != null)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = modelMaterial;
                r.sharedMaterials = mats;
            }
        }

        // First-person: hide your own body from your camera but keep its shadow.
        if (model != null && IsSpawned && IsOwner)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }
}
