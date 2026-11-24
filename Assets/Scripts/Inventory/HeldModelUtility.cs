using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Shared helpers for hotbar items that show a copy of a model in front of the camera.</summary>
public static class HeldModelUtility
{
    // Copies only MeshFilter/MeshRenderer, so the copy has no colliders, Rigidbody or pickup scripts.
    public static GameObject CopyMeshes(Transform source, Transform parent)
    {
        var go = new GameObject(source.name);
        go.layer = source.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = source.localPosition;
        go.transform.localRotation = source.localRotation;
        go.transform.localScale = source.localScale;

        var filter = source.GetComponent<MeshFilter>();
        var renderer = source.GetComponent<MeshRenderer>();
        if (filter != null && renderer != null)
        {
            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var copy = go.AddComponent<MeshRenderer>();
            copy.sharedMaterials = renderer.sharedMaterials;
            copy.shadowCastingMode = ShadowCastingMode.Off;
        }

        foreach (Transform child in source)
            CopyMeshes(child, go.transform);

        return go;
    }
}
