using System;
using UnityEngine;

/// <summary>Assigns a component a stable index among every other component of the same type in
/// the scene, by sorting on full hierarchy path. Every client loads an identical scene, so this
/// index comes out identical on every machine with no manual IDs and no NetworkObject needed -
/// used by Scannable/ResearchPickup so the host can refer to "object 3" and every client agrees
/// on which object that is.
///
/// Caveat: two components of the same type with the exact same hierarchy path (e.g. two
/// identically-named Scannables under the same parent) would tie in the sort - give scannable/
/// pickup objects distinct names (or distinct parents) to avoid that.</summary>
public static class SceneIndex
{
    public static int Of<T>(T self) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        Array.Sort(all, (a, b) => string.CompareOrdinal(Path(a.transform), Path(b.transform)));
        return Array.IndexOf(all, self);
    }

    static string Path(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
