using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compass in the top-left corner of the map, shown only once the tornado is detected.
/// The ring turns with the map (N marks world north), a red tick on the ring shows which way the
/// tornado is moving, and the white arrow points to where to go: a spot behind the tornado and off
/// to the side of its path you are already on. If you are ahead of it, the arrow first sends you
/// round to that side (or straight sideways when you are in its path), so you never cut across in
/// front of it. A text line spells it out.
/// </summary>
public class LiveMapCompass : MonoBehaviour
{
    const float SizeOfMap = 0.27f; // compass diameter as a fraction of the map width

    LiveMap _map;
    RectTransform _root, _ring, _north, _pathTick, _arrow;
    Text _text;
    string _advice = "";
    static Sprite _ringSprite;

    public static LiveMapCompass Create(RectTransform parent, LiveMap map)
    {
        var go = new GameObject("Compass", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var root = (RectTransform)go.transform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f); // top-left

        var c = go.AddComponent<LiveMapCompass>();
        c._map = map;
        c._root = root;
        c._ring = c.Make("Ring", Ring(), new Color(1f, 1f, 1f, 0.9f));
        c._north = c.MakeText("N", 0, TextAnchor.MiddleCenter);
        c._pathTick = c.Make("TornadoPath", LiveMapIcons.Dot(), map.tornadoColor);
        c._arrow = c.Make("GoArrow", LiveMapIcons.Arrow(), Color.white);
        c._text = c.MakeText("Advice", 0, TextAnchor.UpperLeft).GetComponent<Text>();
        go.SetActive(false);
        return c;
    }

    public void Refresh(float mapSize)
    {
        // Nothing at all before detection (or while the tornado object is gone).
        bool show = _map.TornadoDetected && _map.Tornado != null && _map.LocalPlayer != null;
        if (gameObject.activeSelf != show) gameObject.SetActive(show);
        if (!show || mapSize <= 0f) return;

        float d = mapSize * SizeOfMap, r = d * 0.5f, pad = mapSize * 0.03f;
        _root.anchoredPosition = new Vector2(pad, -pad);
        _root.sizeDelta = new Vector2(d, d);
        var center = new Vector2(r, -r);

        Set(_ring, center, d, 0f);
        float northAngle = Angle(_map.WorldDirToMap(Vector3.forward));
        Set(_north, center + Polar(northAngle, r * 0.72f), d * 0.3f, 0f);
        _north.GetComponent<Text>().fontSize = Mathf.Max(8, Mathf.RoundToInt(d * 0.2f));

        var (goDir, advice, heading) = Advise();
        _advice = advice;
        Set(_arrow, center, d * 0.45f, Angle(_map.WorldDirToMap(goDir)) - 90f);

        _pathTick.gameObject.SetActive(heading.HasValue);
        if (heading.HasValue) Set(_pathTick, center + Polar(Angle(_map.WorldDirToMap(heading.Value)), r * 0.92f), d * 0.14f, 0f);

        var textRt = _text.rectTransform;
        textRt.anchorMin = textRt.anchorMax = textRt.pivot = new Vector2(0f, 1f);
        textRt.anchoredPosition = new Vector2(0f, -d - pad * 0.5f);
        textRt.sizeDelta = new Vector2(mapSize - pad * 2f, mapSize * 0.12f);
        _text.fontSize = Mathf.Max(9, Mathf.RoundToInt(mapSize * 0.042f));
        _text.text = advice;
    }

    /// <summary>World direction to go, the advice line, and the tornado's heading (null while it isn't moving).</summary>
    (Vector3 go, string text, Vector3? heading) Advise()
    {
        var tornado = _map.Tornado.position;
        var me = _map.Center;
        var toMe = me - tornado; toMe.y = 0f;
        float dist = toMe.magnitude;
        var vel = _map.TornadoVelocity;

        if (vel.magnitude < 0.3f)
            return (tornado - me, $"Tornado not moving · {dist:F0} m", null);

        var heading = vel.normalized;
        var right = Vector3.Cross(Vector3.up, heading);          // the tornado's own right
        float side = Vector3.Dot(toMe, right) >= 0f ? 1f : -1f;  // which side of its path you're on
        string sideName = side > 0f ? "right" : "left";

        float angle = Vector3.Angle(heading, toMe); // 0 = straight ahead of it, 180 = right behind it
        if (dist < _map.inPathRange && angle < 45f)
            return (right * side, $"IN ITS PATH · move off to its {sideName} · {dist:F0} m", heading);

        // Still in front of it: go round to its side first, never straight across its path.
        if (angle < 90f)
        {
            var abreast = tornado + right * side * Mathf.Max(_map.approachStandoff * 1.5f, 60f);
            var toAbreast = abreast - me; toAbreast.y = 0f;
            return (toAbreast, $"Ahead of it · swing round to its {sideName} side · {dist:F0} m", heading);
        }

        var target = tornado + (right * side - heading).normalized * _map.approachStandoff;
        var toTarget = target - me; toTarget.y = 0f;
        if (toTarget.magnitude < 8f)
            return (tornado - me, $"In position, behind it on its {sideName} · {dist:F0} m", heading);
        return (toTarget, $"Approach from behind, on its {sideName} · {dist:F0} m", heading);
    }

    // Map-space direction -> UI angle in degrees (0 = right, 90 = up).
    static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
    static Vector2 Polar(float deg, float radius) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)) * radius;

    static void Set(RectTransform rt, Vector2 pos, float size, float angle)
    {
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        rt.localEulerAngles = new Vector3(0f, 0f, angle);
    }

    RectTransform Make(string n, Sprite sprite, Color color)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_root, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    RectTransform MakeText(string n, int size, TextAnchor anchor)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        go.transform.SetParent(_root, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        var t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = n == "N" ? "N" : "";
        t.alignment = anchor;
        t.color = Color.white;
        t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        return rt;
    }

    public string Describe()
    {
        if (!gameObject.activeSelf) return "    compass: hidden (no tornado detected)\n";
        return new StringBuilder()
            .AppendLine($"    compass: N at {_north.anchoredPosition:F0}, go-arrow angle {_arrow.localEulerAngles.z:F0}, path tick {(_pathTick.gameObject.activeSelf ? _pathTick.anchoredPosition.ToString("F0") : "none")}")
            .AppendLine($"    advice: \"{_advice}\"").ToString();
    }

    // Ring with a dark rim and a translucent dark face, so the arrow reads on any terrain.
    static Sprite Ring()
    {
        if (_ringSprite != null) return _ringSprite;
        const int n = 128;
        var tex = LiveMapIcons.NewTexture(n);
        var dark = new Color(0.05f, 0.05f, 0.05f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                float outer = Mathf.Clamp01(n / 2f - d);
                Color c = d > n / 2f - 6f ? Color.white : d > n / 2f - 9f ? dark : new Color(0f, 0f, 0f, 0.45f);
                c.a *= outer;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        return _ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }
}
