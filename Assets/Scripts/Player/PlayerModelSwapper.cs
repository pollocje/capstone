using UnityEngine;

public class PlayerModelSwapper : MonoBehaviour
{
    [Tooltip("The child GameObject that holds the visible model. " +
             "Its contents get replaced when SwapModel is called.")]
    public Transform modelRoot;

    // Call this at runtime or from an editor tool to replace the player's visual model.
    // newModelPrefab should be a prefab containing your mesh + materials.
    public void SwapModel(GameObject newModelPrefab)
    {
        if (modelRoot == null)
        {
            Debug.LogError("PlayerModelSwapper: modelRoot is not assigned.");
            return;
        }

        // Clear existing model children
        foreach (Transform child in modelRoot)
            Destroy(child.gameObject);

        if (newModelPrefab != null)
            Instantiate(newModelPrefab, modelRoot.position, modelRoot.rotation, modelRoot);
    }
}
