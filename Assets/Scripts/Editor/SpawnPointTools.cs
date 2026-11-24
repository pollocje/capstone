using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Lines the lobby spawn points up side by side on the ground inside the yard (Fence around CountryHouse).
public static class SpawnPointTools
{
    // Open patch of yard between the house and the washing line, east of the truck.
    static readonly Vector3 RowStart = new Vector3(346.5f, 0f, 601f);
    const float RowSpacing = 2.5f;   // Metres between players, side by side along x
    const float RowYaw = 0f;         // Facing the gate
    const string GroupName = "GroupA";

    // Lines every spawn point up side by side on the ground inside the yard, all in one group.
    [MenuItem("tools/Spawning/Line Up Spawn Points In Yard")]
    public static void LineUp()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("[SpawnPointTools] No terrain found");
            return;
        }

        var spawns = Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(s => s.name).ToList();

        for (int i = 0; i < spawns.Count; i++)
        {
            var s = spawns[i];
            Undo.RecordObject(s.transform, "Line Up Spawn Points");
            Undo.RecordObject(s, "Line Up Spawn Points");
            var p = RowStart + Vector3.right * (RowSpacing * i);
            p.y = LobbyFenceTools.Ground(terrain, p) + 0.1f;
            s.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, RowYaw, 0f));
            s.spawnTag = GroupName;
            s.disabled = false;
            EditorUtility.SetDirty(s);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[SpawnPointTools] Lined up {spawns.Count} spawn points in {GroupName}");
    }
}
