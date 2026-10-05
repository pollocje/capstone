using UnityEngine;

public class PlayerModelSwapper : MonoBehaviour
{
    [Tooltip("The child GameObject that holds the visible model. " +
             "Its contents get replaced when SwapModel is called.")]
    public Transform modelRoot;

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
        return Instantiate(newModelPrefab, modelRoot); // keeps the model's own local offset/rotation (e.g. Blender FBX import rotation)
    }
}
