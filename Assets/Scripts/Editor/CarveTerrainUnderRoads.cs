using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Lowers the terrain heightmap under every road mesh so the terrain can't poke
// through the road up close (terrain LOD hides it at a distance, so roads look
// like they "disappear around the player"). Re-run after editing roads.
public static class CarveTerrainUnderRoads
{
    const string RoadRootName = "Road Network";
    const float DepthBelowRoad = 0.05f;  // metres of clearance under the road surface
    const int MarginSamples = 1;         // extra heightmap samples carved around each road

    [MenuItem("tools/Roads/Carve Terrain Under Roads")]
    public static string Run()
    {
        var roadRoot = GameObject.Find(RoadRootName);
        var terrain = Terrain.activeTerrain;
        if (roadRoot == null || terrain == null)
            return Report($"Carve aborted: need a '{RoadRootName}' object and an active Terrain.");

        var data = terrain.terrainData;
        int res = data.heightmapResolution;
        Vector3 size = data.size;
        Vector3 origin = terrain.transform.position;

        // Lowest road height covering each heightmap sample (world y).
        var roadY = new Dictionary<int, float>();
        foreach (var mf in roadRoot.GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null || !mr.enabled) continue;
            Rasterize(mf, origin, size, res, roadY);
        }
        if (roadY.Count == 0) return Report("Carve aborted: no road meshes found.");

        // Grow the footprint by a margin so edge triangles don't slope up through the road.
        var target = new Dictionary<int, float>();
        foreach (var kv in roadY)
        {
            int gx = kv.Key % res, gz = kv.Key / res;
            for (int dz = -MarginSamples; dz <= MarginSamples; dz++)
            for (int dx = -MarginSamples; dx <= MarginSamples; dx++)
            {
                int x = gx + dx, z = gz + dz;
                if (x < 0 || z < 0 || x >= res || z >= res) continue;
                int key = z * res + x;
                float y = kv.Value - DepthBelowRoad;
                if (!target.TryGetValue(key, out float cur) || y < cur) target[key] = y;
            }
        }

        Undo.RegisterCompleteObjectUndo(new Object[] { data, terrain.transform }, "Carve Terrain Under Roads");
        float[,] heights = data.GetHeights(0, 0, res, res);

        // Heights can't go below the terrain's base, so lower the terrain and raise every
        // sample by the same amount (whole heightmap steps) to keep the rest of the world unchanged.
        float minTarget = float.MaxValue;
        foreach (var y in target.Values) minTarget = Mathf.Min(minTarget, y);
        float step = size.y / 32766f;
        float shift = minTarget < origin.y ? Mathf.Ceil((origin.y - minTarget) / step) * step : 0f;
        if (shift > 0f)
        {
            float shiftN = shift / size.y;
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                heights[z, x] = Mathf.Min(1f, heights[z, x] + shiftN);
            terrain.transform.position = origin - new Vector3(0f, shift, 0f);
        }
        float baseY = origin.y - shift;

        int carved = 0;
        foreach (var kv in target)
        {
            int x = kv.Key % res, z = kv.Key / res;
            float want = Mathf.Clamp01((kv.Value - baseY) / size.y);
            if (want < heights[z, x]) { heights[z, x] = want; carved++; }
        }

        data.SetHeights(0, 0, heights);
        data.SyncHeightmap();
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(terrain.transform);
        UnityEngine.SceneManagement.Scene scene = terrain.gameObject.scene;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

        return Report($"Carved {carved} heightmap samples under {roadY.Count} road samples; terrain lowered by {shift:F3} m.");
    }

    static void Rasterize(MeshFilter mf, Vector3 origin, Vector3 size, int res, Dictionary<int, float> roadY)
    {
        var mesh = mf.sharedMesh;
        var verts = mesh.vertices;
        var tris = mesh.triangles;
        var m = mf.transform.localToWorldMatrix;
        for (int i = 0; i < verts.Length; i++) verts[i] = m.MultiplyPoint3x4(verts[i]);

        float toGrid = (res - 1) / size.x;
        float toGridZ = (res - 1) / size.z;
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
            float den = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(den) < 1e-9f) continue;

            int x0 = Mathf.Max(0, Mathf.CeilToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - origin.x) * toGrid));
            int x1 = Mathf.Min(res - 1, Mathf.FloorToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - origin.x) * toGrid));
            int z0 = Mathf.Max(0, Mathf.CeilToInt((Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - origin.z) * toGridZ));
            int z1 = Mathf.Min(res - 1, Mathf.FloorToInt((Mathf.Max(a.z, Mathf.Max(b.z, c.z)) - origin.z) * toGridZ));

            for (int gz = z0; gz <= z1; gz++)
            for (int gx = x0; gx <= x1; gx++)
            {
                float x = origin.x + gx / toGrid, z = origin.z + gz / toGridZ;
                float l1 = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / den;
                float l2 = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / den;
                float l3 = 1f - l1 - l2;
                if (l1 < -1e-5f || l2 < -1e-5f || l3 < -1e-5f) continue;

                float y = l1 * a.y + l2 * b.y + l3 * c.y;
                int key = gz * res + gx;
                if (!roadY.TryGetValue(key, out float cur) || y < cur) roadY[key] = y;
            }
        }
    }

    static string Report(string msg)
    {
        Debug.Log("[CarveTerrainUnderRoads] " + msg);
        return msg;
    }
}
