using UnityEditor;
using UnityEngine;

// Tools > Players > Set Up Player Colors
// The player models (Assets/Models/player1-4.fbx) are colored with vertex colors painted in
// Blender (player1 blue, player2 pinkish-red, player3 green, player4 yellow, all sharing the
// skin tone) and export with no materials, so URP's default material showed them all grey.
//  1. Creates Assets/Materials/PlayerVertexColor.mat on the Capstone/Player Vertex Color Lit shader.
//  2. Assigns it to PlayerModelSelector.modelMaterial on the player prefab, which puts it on
//     whichever model a slot spawns. One material colors all four models. Safe to re-run.
public static class PlayerColorSetup
{
    const string ShaderName = "Capstone/Player Vertex Color Lit";
    const string MaterialPath = "Assets/Materials/PlayerVertexColor.mat";
    const string PlayerPrefabPath = "Assets/Prefabs/MainPlayer Variant.prefab";

    [MenuItem("Tools/Players/Set Up Player Colors")]
    public static void Run()
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"[PlayerColorSetup] Shader '{ShaderName}' not found.");
            return;
        }
        if (ShaderUtil.ShaderHasError(shader))
        {
            foreach (var msg in ShaderUtil.GetShaderMessages(shader))
                Debug.LogError($"[PlayerColorSetup] Shader error: {msg.message} (line {msg.line})");
            return;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "PlayerVertexColor" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else material.shader = shader;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);

        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var selector = root.GetComponentInChildren<PlayerModelSelector>(true);
            if (selector == null)
            {
                Debug.LogWarning($"[PlayerColorSetup] No PlayerModelSelector in {PlayerPrefabPath}; assign modelMaterial by hand.");
                return;
            }
            selector.modelMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerColorSetup] Done: PlayerVertexColor.mat created and assigned to PlayerModelSelector.modelMaterial.");
    }
}
