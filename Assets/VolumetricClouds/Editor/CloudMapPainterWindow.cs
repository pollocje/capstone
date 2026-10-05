using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Paints the regional Cloud Map texture (R: coverage, G: storm intensity) directly in the Scene view,
/// so storms can be placed at specific world locations instead of covering the whole sky uniformly.
/// </summary>
public class CloudMapPainterWindow : EditorWindow
{
    private VolumetricClouds targetVolume;
    private Texture2D mapTexture;
    private string mapAssetPath;

    // Brush
    private bool paintingEnabled;
    private float brushRadius = 100f; // world meters
    private float brushStrength = 1f;
    private bool paintStorm; // false = paint coverage (R), true = paint storm intensity (G)
    private bool erase;

    // New map / world mapping
    private int newMapResolution = 512;
    private float mapWorldSize = 2000f;
    private Vector2 mapCenter = Vector2.zero;

    private bool dirty;

    [MenuItem("Window/Rendering/Cloud Map Painter")]
    public static void OpenFromMenu() => Open(null);

    public static void Open(VolumetricClouds volume)
    {
        var window = GetWindow<CloudMapPainterWindow>("Cloud Map Painter");
        if (volume != null)
            window.SetTarget(volume);
        window.Show();
    }

    private void SetTarget(VolumetricClouds volume)
    {
        targetVolume = volume;
        mapTexture = volume.cloudMapTexture.value as Texture2D;
        mapAssetPath = mapTexture != null ? AssetDatabase.GetAssetPath(mapTexture) : null;

        Vector4 tiling = volume.cloudMapTiling.value;
        if (tiling.x > 0.0001f)
        {
            mapWorldSize = 1f / tiling.x;
            mapCenter = new Vector2((0.5f - tiling.z) * mapWorldSize, (0.5f - tiling.w) * mapWorldSize);
        }
    }

    private void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
    private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Paints the regional Cloud Map: a top-down texture that tells the clouds where to be clear (black) " +
            "and where to be stormy (white). Hold left mouse in the Scene view to paint.", MessageType.Info);

        EditorGUILayout.Space();
        targetVolume = (VolumetricClouds)EditorGUILayout.ObjectField("Volume Component", targetVolume, typeof(VolumetricClouds), true);
        mapTexture = (Texture2D)EditorGUILayout.ObjectField("Cloud Map Texture", mapTexture, typeof(Texture2D), false);

        using (new EditorGUI.DisabledScope(targetVolume == null || mapTexture == null))
        {
            if (GUILayout.Button("Assign To Volume"))
                ApplyTextureToVolume();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("World Mapping", EditorStyles.boldLabel);
        newMapResolution = EditorGUILayout.IntField(new GUIContent("New Map Resolution"), newMapResolution);
        mapWorldSize = EditorGUILayout.FloatField(new GUIContent("World Size (meters)", "How many world units across the painted map covers."), mapWorldSize);
        mapCenter = EditorGUILayout.Vector2Field(new GUIContent("World Center (X, Z)", "World position that lines up with the center of the texture."), mapCenter);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create New Cloud Map"))
                CreateNewMap();

            using (new EditorGUI.DisabledScope(targetVolume == null))
            {
                if (GUILayout.Button("Apply World Mapping"))
                    ApplyWorldMapping();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
        paintingEnabled = EditorGUILayout.ToggleLeft("Painting Enabled", paintingEnabled);
        brushRadius = EditorGUILayout.Slider("Brush Radius (m)", brushRadius, 5f, Mathf.Max(50f, mapWorldSize));
        brushStrength = EditorGUILayout.Slider("Brush Strength", brushStrength, 0.05f, 1f);
        paintStorm = EditorGUILayout.Toggle(new GUIContent("Paint Storm Intensity (G)", "Off paints cloud coverage (R). On paints storm intensity (G) - darker, denser clouds."), paintStorm);
        erase = EditorGUILayout.Toggle("Erase", erase);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(mapTexture == null || !dirty))
        {
            if (GUILayout.Button("Save Cloud Map"))
                SaveMap();
        }
        if (dirty)
            EditorGUILayout.HelpBox("Unsaved painting - click Save Cloud Map to write it to disk.", MessageType.Warning);
    }

    private void CreateNewMap()
    {
        string path = EditorUtility.SaveFilePanelInProject("New Cloud Map", "CloudMap", "png", "Choose where to save the cloud map texture.");
        if (string.IsNullOrEmpty(path)) return;

        var tex = new Texture2D(newMapResolution, newMapResolution, TextureFormat.RGBA32, false, true);
        var pixels = new Color[newMapResolution * newMapResolution];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.black; // clear sky everywhere, no storm
        tex.SetPixels(pixels);
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(path);

        mapAssetPath = path;
        mapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        dirty = false;

        if (targetVolume != null)
        {
            ApplyTextureToVolume();
            ApplyWorldMapping();
        }
    }

    private static void ConfigureImporter(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private void ApplyTextureToVolume()
    {
        if (targetVolume == null || mapTexture == null) return;
        targetVolume.cloudMapTexture.value = mapTexture;
        targetVolume.cloudMapTexture.overrideState = true;
        EditorUtility.SetDirty(targetVolume);
        AssetDatabase.SaveAssets();
    }

    private void ApplyWorldMapping()
    {
        if (targetVolume == null) return;
        if (mapWorldSize < 1f) mapWorldSize = 1f;

        float tilingX = 1f / mapWorldSize;
        float offsetX = 0.5f - mapCenter.x / mapWorldSize;
        float offsetY = 0.5f - mapCenter.y / mapWorldSize;

        targetVolume.cloudMapTiling.value = new Vector4(tilingX, tilingX, offsetX, offsetY);
        targetVolume.cloudMapTiling.overrideState = true;
        EditorUtility.SetDirty(targetVolume);
        AssetDatabase.SaveAssets();
    }

    private void SaveMap()
    {
        if (mapTexture == null || string.IsNullOrEmpty(mapAssetPath))
        {
            mapAssetPath = AssetDatabase.GetAssetPath(mapTexture);
            if (string.IsNullOrEmpty(mapAssetPath)) return;
        }

        File.WriteAllBytes(mapAssetPath, mapTexture.EncodeToPNG());
        AssetDatabase.ImportAsset(mapAssetPath, ImportAssetOptions.ForceUpdate);
        mapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(mapAssetPath);
        dirty = false;

        // Reimporting creates a new Texture2D instance; keep the volume pointed at the current one.
        if (targetVolume != null && targetVolume.cloudMapTexture.value != mapTexture)
            ApplyTextureToVolume();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!paintingEnabled || mapTexture == null || targetVolume == null)
            return;

        Event e = Event.current;
        int controlID = GUIUtility.GetControlID(FocusType.Passive);

        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(controlID);

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Vector3 hitPoint;
        if (Physics.Raycast(ray, out RaycastHit hit, 100000f))
        {
            hitPoint = hit.point;
        }
        else
        {
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out float enter))
                return;
            hitPoint = ray.GetPoint(enter);
        }

        Handles.color = erase ? new Color(1f, 0.2f, 0.2f) : (paintStorm ? Color.yellow : Color.cyan);
        Handles.DrawWireDisc(hitPoint, Vector3.up, brushRadius);

        bool isPaintEvent = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && !e.alt && !e.control;

        if (isPaintEvent)
        {
            PaintAt(hitPoint);
            GUIUtility.hotControl = controlID;
            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0)
        {
            if (GUIUtility.hotControl == controlID)
                GUIUtility.hotControl = 0;
            e.Use();
        }

        sceneView.Repaint();
    }

    private void PaintAt(Vector3 worldPoint)
    {
        Vector4 tiling = targetVolume.cloudMapTiling.value;
        if (Mathf.Approximately(tiling.x, 0f) || Mathf.Approximately(tiling.y, 0f))
            return;

        Vector2 uv = new Vector2(worldPoint.x * tiling.x + tiling.z, worldPoint.z * tiling.y + tiling.w);
        Vector2Int pixelCenter = new Vector2Int(Mathf.RoundToInt(uv.x * mapTexture.width), Mathf.RoundToInt(uv.y * mapTexture.height));

        int radiusPixels = Mathf.Max(1, Mathf.RoundToInt(brushRadius * tiling.x * mapTexture.width));

        int minX = Mathf.Clamp(pixelCenter.x - radiusPixels, 0, mapTexture.width - 1);
        int maxX = Mathf.Clamp(pixelCenter.x + radiusPixels, 0, mapTexture.width - 1);
        int minY = Mathf.Clamp(pixelCenter.y - radiusPixels, 0, mapTexture.height - 1);
        int maxY = Mathf.Clamp(pixelCenter.y + radiusPixels, 0, mapTexture.height - 1);

        if (maxX < minX || maxY < minY)
            return;

        float target = erase ? 0f : 1f;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(pixelCenter.x, pixelCenter.y));
                if (dist > radiusPixels) continue;

                float falloff = 1f - (dist / radiusPixels);
                Color c = mapTexture.GetPixel(x, y);

                if (paintStorm)
                    c.g = Mathf.Lerp(c.g, target, brushStrength * falloff);
                else
                    c.r = Mathf.Lerp(c.r, target, brushStrength * falloff);

                mapTexture.SetPixel(x, y, c);
            }
        }

        mapTexture.Apply(false);
        dirty = true;
        Repaint();
    }
}
