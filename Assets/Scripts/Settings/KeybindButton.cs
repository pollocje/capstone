using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// One key in the controls page, like "Jump: Space"
// Click it, then press the new key (escape cancels)
public class KeybindButton : MonoBehaviour
{
    public InputActionAsset actions;
    public string label;        // What the button says, e.g. "Jump"
    public string actionName;   // Action in StarterAssets.inputactions, e.g. "Jump"
    public string moveDirection; // Only for Move: up / down / left / right
    public TMP_Text text;

    InputActionRebindingExtensions.RebindingOperation rebind;

    public void StartRebind()
    {
        InputAction action = actions.FindAction(actionName);
        int index = FindKeyboardBinding(action);
        text.text = label + ": press a key...";

        // Waits for the next key or mouse button, then swaps it in
        action.Disable();
        rebind = action.PerformInteractiveRebinding(index)
            .WithExpectedControlType("Button")
            .WithControlsHavingToMatchPath("<Keyboard>")
            .WithControlsHavingToMatchPath("<Mouse>")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(op => Done())
            .OnCancel(op => Done())
            .Start();
    }

    void Done()
    {
        rebind.Dispose();
        rebind = null;
        GameSettings.SaveKeybinds(actions);
        UpdateText();
    }

    public void UpdateText()
    {
        InputAction action = actions.FindAction(actionName);
        int index = FindKeyboardBinding(action);
        text.text = label + ": " + action.GetBindingDisplayString(index);
    }

    // Each action has a keyboard key and a controller button, this finds the keyboard one
    // (for Move it finds the WASD key, not the arrow key)
    int FindKeyboardBinding(InputAction action)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding b = action.bindings[i];
            if (b.isComposite || !b.groups.Contains("KeyboardMouse")) continue;

            if (moveDirection == "" && !b.isPartOfComposite) return i;
            if (moveDirection != "" && b.name == moveDirection) return i;
        }
        return -1;
    }

    void OnDisable()
    {
        // Stop waiting for a key if the menu closes
        if (rebind != null) rebind.Cancel();
    }
}
