using UnityEngine;

public class PlayerModelSwapper : MonoBehaviour
{
    [Tooltip("The child GameObject that holds the visible model. " +
             "Its contents get replaced when SwapModel is called.")]
    public Transform modelRoot;

    private Animator _playerAnimator;

    // Call this at runtime or from an editor tool to replace the player's visual model.
    // newModelPrefab should be a prefab containing your mesh + materials.
    // Returns the spawned model instance (null if nothing was spawned).
    public GameObject SwapModel(GameObject newModelPrefab)
    {
        // Fall back to the "PlayerModel" child if the field was never wired up.
        if (modelRoot == null)
            modelRoot = transform.Find("PlayerModel");

        if (modelRoot == null)
        {
            Debug.LogError("PlayerModelSwapper: modelRoot is not assigned and no 'PlayerModel' child was found.");
            return null;
        }

        // Clear existing model children
        foreach (Transform child in modelRoot)
            Destroy(child.gameObject);

        if (newModelPrefab == null) return null;
        var model = Instantiate(newModelPrefab, modelRoot); // keeps the model's own local offset/rotation (e.g. Blender FBX import rotation)
        HookUpAnimator(model);
        return model;
    }

    // The player's Animator (with PlayerAnimator on it) sits above the model, but each model
    // is spawned at runtime and has its own humanoid Avatar. So run the controller on the
    // model's own Animator and turn the player one off so the two don't fight over the bones.
    void HookUpAnimator(GameObject model)
    {
        if (_playerAnimator == null)
            _playerAnimator = modelRoot.GetComponentInParent<Animator>();

        var modelAnimator = model.GetComponent<Animator>();
        if (_playerAnimator == null || modelAnimator == null)
        {
            Debug.LogWarning("PlayerModelSwapper: Missing player Animator or model Animator (is the model's Rig set to Humanoid?).");
            return;
        }

        modelAnimator.runtimeAnimatorController = _playerAnimator.runtimeAnimatorController;
        modelAnimator.applyRootMotion = false; // movement comes from the CharacterController
        _playerAnimator.enabled = false;
    }
}
