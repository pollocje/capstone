using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// Plays a per-character emote on the same key for everyone. PlayerAnimator has one "Emote"
// state holding a placeholder clip; each player's model gets an override controller that
// swaps in the clip for their slot, so player 1 does their emote, player 2 theirs, etc.
public class PlayerEmote : NetworkBehaviour
{
    [Tooltip("One emote per player slot, matching PlayerModelSelector.models.")]
    public AnimationClip[] emotes = new AnimationClip[4];

    [Tooltip("The clip in PlayerAnimator's Emote state. It gets replaced by the slot's emote.")]
    public AnimationClip emotePlaceholder;

    public KeyCode emoteKey = KeyCode.B;

    [Header("Sitting (truck seats)")]
    [Tooltip("One sitting loop per player slot, swapped into PlayerAnimator's Seated state the same way as the emotes. VehicleEnterExit sets the Seated bool.")]
    public AnimationClip[] sitClips = new AnimationClip[4];

    [Tooltip("The clip in PlayerAnimator's Seated state. It gets replaced by the slot's sitting clip.")]
    public AnimationClip sitPlaceholder;

    static readonly int EmoteHash = Animator.StringToHash("Emote");

    private PlayerModelSelector _selector;
    private Animator _animator;
    private int _appliedSlot = -1;

    void Awake()
    {
        _selector = GetComponent<PlayerModelSelector>();
    }

    void Update()
    {
        RefreshAnimator();

        // Singleplayer is never network-spawned; in multiplayer only the owner reads input.
        if (IsSpawned && !IsOwner) return;

        if (Input.GetKeyDown(emoteKey))
        {
            if (IsSpawned) PlayEmoteRpc();
            else PlayEmote();
        }
    }

    [Rpc(SendTo.Everyone)]
    void PlayEmoteRpc()
    {
        PlayEmote();
    }

    void PlayEmote()
    {
        RefreshAnimator();
        if (_animator != null)
            _animator.SetTrigger(EmoteHash);
    }

    // PlayerModelSwapper replaces the model (and its Animator) at runtime, and the slot can
    // arrive after spawn, so re-apply the override whenever either one changes.
    void RefreshAnimator()
    {
        int slot = _selector != null && _selector.Slot >= 0 ? _selector.Slot : 0;
        if (_animator != null && _animator.isActiveAndEnabled && slot == _appliedSlot) return;

        _animator = FindActiveAnimator();
        if (_animator == null) return;

        _appliedSlot = slot;
        ApplySlotClips(slot);
    }

    // One override controller holds every per-slot clip (emote + sitting), so nothing else
    // needs to build its own override and fight this one over runtimeAnimatorController.
    void ApplySlotClips(int slot)
    {
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();

        if (emotePlaceholder != null && slot < emotes.Length && emotes[slot] != null)
            overrides.Add(new(emotePlaceholder, emotes[slot]));
        else
            Debug.LogWarning($"PlayerEmote: No emote assigned for slot {slot}.", this);

        if (sitPlaceholder != null && slot < sitClips.Length && sitClips[slot] != null)
            overrides.Add(new(sitPlaceholder, sitClips[slot]));
        else
            Debug.LogWarning($"PlayerEmote: No sitting clip assigned for slot {slot}.", this);

        if (overrides.Count == 0) return;

        // Build on the base controller, not an override we already applied.
        var controller = _animator.runtimeAnimatorController;
        if (controller is AnimatorOverrideController existing)
            controller = existing.runtimeAnimatorController;

        var overrideController = new AnimatorOverrideController(controller);
        overrideController.ApplyOverrides(overrides);
        _animator.runtimeAnimatorController = overrideController;
    }

    Animator FindActiveAnimator()
    {
        foreach (var animator in GetComponentsInChildren<Animator>())
            if (animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
                return animator;
        return null;
    }
}
