using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Icons drawn over the local player's MapImage: an arrow for you in the centre (always pointing
/// up), dots for teammates, and a red dot for the detected tornado that sticks to the map edge
/// with a pointer when it is out of view. Created at runtime by LiveMap, so no prefab changes.
/// Also switches MapImage between the corner minimap and the full-screen layout (LiveMap.CurrentMode),
/// restoring the prefab's own layout for full screen, and hosts the compass (LiveMapCompass).
/// </summary>
public class LiveMapIcons : MonoBehaviour
{
    const string MapImagePath = "HUD Canvas/MapOverlay/MapImage";

    LiveMap _map;
    RectTransform _rect;
    Image _player, _tornado, _tornadoPointer;
    LiveMapCompass _compass;

    // MapImage / MapOverlay as built by GrabSystemTools, restored whenever the full-screen map shows.
    RectTransform _mapImage;
    GameObject _overlay;
    Image _backdrop;
    AspectRatioFitter _fitter;
    Vector2 _fullAnchorMin, _fullAnchorMax, _fullPivot, _fullOffsetMin, _fullOffsetMax;
    readonly List<Image> _teammates = new List<Image>();

    static Sprite _dotSprite, _arrowSprite;

    public static LiveMapIcons Attach(MapItem localPlayer, LiveMap map)
    {
        // MapItem sits on PlayerCapsule; HUD Canvas is its sibling under the player root.
        var root = localPlayer.transform.parent != null ? localPlayer.transform.parent : localPlayer.transform;
        var mapImage = root.Find(MapImagePath);
        if (mapImage == null)
        {
            Debug.LogWarning($"LiveMap: {MapImagePath} not found on {root.name}; run Tools/Grab System/Setup Player Map View.", localPlayer);
            return null;
        }

        var go = new GameObject("LiveMapIcons", typeof(RectTransform));
        go.transform.SetParent(mapImage, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var icons = go.AddComponent<LiveMapIcons>();
        icons._map = map;
        icons._rect = rt;
        icons._tornado = icons.MakeIcon("Tornado", Dot());
        icons._tornadoPointer = icons.MakeIcon("TornadoPointer", Arrow());
        icons._player = icons.MakeIcon("You", Arrow()); // last, so it draws on top
        icons._compass = LiveMapCompass.Create(rt, map);

        var image = (RectTransform)mapImage;
        icons._mapImage = image;
        icons._overlay = image.parent.gameObject;
        icons._backdrop = icons._overlay.GetComponent<Image>();
        icons._fitter = image.GetComponent<AspectRatioFitter>();
        icons._fullAnchorMin = image.anchorMin; icons._fullAnchorMax = image.anchorMax; icons._fullPivot = image.pivot;
        icons._fullOffsetMin = image.offsetMin; icons._fullOffsetMax = image.offsetMax;
        return icons;
    }

    public void Refresh()
    {
        ApplyLayout(_map.CurrentMode);
        float size = Mathf.Min(_rect.rect.width, _rect.rect.height);
        if (size <= 0f) return;
        float half = size * 0.5f;

        Place(_player, Vector2.zero, size * _map.playerIconSize, _map.playerColor, 0f);

        // Teammates: clamp to the edge too, so you can always find each other.
        var mates = _map.Teammates;
        while (_teammates.Count < mates.Count) _teammates.Add(MakeIcon("Teammate", Dot()));
        for (int i = 0; i < _teammates.Count; i++)
        {
            bool show = i < mates.Count && mates[i] != null;
            _teammates[i].enabled = show;
            if (!show) continue;
            var (pos, _) = Clamp(_map.WorldToMap(mates[i].transform.position));
            Place(_teammates[i], pos * half, size * _map.dotSize, _map.teammateColor, 0f);
        }

        // Tornado: only once detected; pinned to the edge with a pointer when out of view.
        _compass.Refresh(size);

        bool tornado = _map.TornadoDetected && _map.Tornado != null;
        _tornado.enabled = tornado;
        _tornadoPointer.enabled = false;
        if (!tornado) return;
        var (tp, outside) = Clamp(_map.WorldToMap(_map.Tornado.position));
        Place(_tornado, tp * half, size * _map.dotSize * 1.3f, _map.tornadoColor, 0f);
        if (outside)
        {
            float angle = Mathf.Atan2(tp.y, tp.x) * Mathf.Rad2Deg - 90f; // arrow sprite points up
            var dir = tp.normalized;
            // Pointer sits between the dot and the map border, aimed outward at the tornado.
            Place(_tornadoPointer, tp * half + dir * size * _map.dotSize * 1.1f, size * _map.dotSize, _map.tornadoColor, angle);
            _tornadoPointer.enabled = true;
        }
    }

    void ApplyLayout(LiveMap.Mode mode)
    {
        if (mode == LiveMap.Mode.Hidden) return; // MapItem closes the overlay itself on unequip
        bool mini = mode == LiveMap.Mode.Minimap;
        if (mini && !_overlay.activeSelf) _overlay.SetActive(true); // MapItem only opens it for Tab
        if (_backdrop != null) _backdrop.enabled = !mini;
        if (_fitter != null) _fitter.enabled = !mini;

        if (mini)
        {
            var canvas = _mapImage.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
            float side = canvas.rect.height * _map.minimapSize;
            _mapImage.anchorMin = _mapImage.anchorMax = _mapImage.pivot = Vector2.one;
            _mapImage.sizeDelta = new Vector2(side, side);
            _mapImage.anchoredPosition = new Vector2(-_map.minimapMargin, -_map.minimapMargin);
        }
        else
        {
            _mapImage.anchorMin = _fullAnchorMin; _mapImage.anchorMax = _fullAnchorMax; _mapImage.pivot = _fullPivot;
            _mapImage.offsetMin = _fullOffsetMin; _mapImage.offsetMax = _fullOffsetMax;
        }
    }

    // Keeps a map position inside the square, edgeInset from the border. Returns whether it was outside.
    (Vector2 pos, bool outside) Clamp(Vector2 p)
    {
        float limit = 1f - _map.edgeInset * 2f;
        float m = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
        return m <= limit ? (p, false) : (p * (limit / m), true);
    }

    static void Place(Image img, Vector2 anchored, float size, Color color, float angle)
    {
        var rt = img.rectTransform;
        rt.anchoredPosition = anchored;
        rt.sizeDelta = new Vector2(size, size);
        rt.localEulerAngles = new Vector3(0f, 0f, angle);
        img.color = color;
    }

    Image MakeIcon(string iconName, Sprite sprite)
    {
        var go = new GameObject(iconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_rect, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.enabled = false;
        return img;
    }

    public string Describe()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  icons on {_rect.parent.name} [{_map.CurrentMode}] ({_rect.rect.width:F0}x{_rect.rect.height:F0} px): you {_player.rectTransform.anchoredPosition:F0}");
        for (int i = 0; i < _teammates.Count; i++)
            if (_teammates[i].enabled) sb.AppendLine($"    teammate dot {i}: {_teammates[i].rectTransform.anchoredPosition:F0}");
        sb.AppendLine(_tornado.enabled
            ? $"    tornado dot: {_tornado.rectTransform.anchoredPosition:F0}{(_tornadoPointer.enabled ? $", on the edge, pointer angle {_tornadoPointer.rectTransform.localEulerAngles.z:F0}" : ", in view")}"
            : "    tornado dot: hidden");
        sb.Append(_compass.Describe());
        return sb.ToString();
    }

    // ── Generated sprites (no texture assets needed) ─────────────────────────

    internal static Sprite Dot()
    {
        if (_dotSprite != null) return _dotSprite;
        const int n = 64;
        var tex = NewTexture(n);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                // Coloured fill with a dark outline so dots read on any terrain.
                float a = Mathf.Clamp01(n / 2f - d);
                float rim = Mathf.Clamp01((d - (n / 2f - 7f)) / 2f);
                tex.SetPixel(x, y, Color.Lerp(Color.white, new Color(0.05f, 0.05f, 0.05f), rim) * new Color(1, 1, 1, a));
            }
        tex.Apply();
        return _dotSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }

    internal static Sprite Arrow()
    {
        if (_arrowSprite != null) return _arrowSprite;
        const int n = 64;
        var tex = NewTexture(n);
        // Arrowhead pointing up with a notch at the back.
        Vector2 tip = new Vector2(32, 62), left = new Vector2(6, 4), right = new Vector2(58, 4), notch = new Vector2(32, 18);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = InTri(p, tip, left, notch) || InTri(p, tip, notch, right);
                bool inner = InTri(p, tip + new Vector2(0, -8), left + new Vector2(8, 5), notch + new Vector2(0, 5))
                          || InTri(p, tip + new Vector2(0, -8), notch + new Vector2(0, 5), right + new Vector2(-8, 5));
                tex.SetPixel(x, y, !inside ? Color.clear : inner ? Color.white : new Color(0.05f, 0.05f, 0.05f));
            }
        tex.Apply();
        return _arrowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }

    internal static Texture2D NewTexture(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.hideFlags = HideFlags.DontSave;
        return tex;
    }

    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float Side(Vector2 u, Vector2 v, Vector2 w) => (u.x - w.x) * (v.y - w.y) - (v.x - w.x) * (u.y - w.y);
        float d1 = Side(p, a, b), d2 = Side(p, b, c), d3 = Side(p, c, a);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }
}
