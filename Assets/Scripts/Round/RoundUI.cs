using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Truck-flip countdown banner and the end screen, built in code on its own overlay canvas
/// (one per gameplay scene load). Driven by RoundManager; shows the same result on every machine.
/// </summary>
public class RoundUI : MonoBehaviour
{
    RoundManager _round;
    GameObject _banner, _endPanel;
    Text _bannerText, _title, _reason, _body, _score, _waiting;
    Button _startOver;
    Font _font;

    static readonly Color Won = new Color(0.35f, 0.85f, 0.4f), Lost = new Color(0.95f, 0.3f, 0.25f);

    public static RoundUI Create(RoundManager round, UnityEngine.SceneManagement.Scene scene)
    {
        var go = new GameObject("RoundUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene); // goes away with the scene
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // above the HUD
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var ui = go.AddComponent<RoundUI>();
        ui._round = round;
        ui._font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ui.Build((RectTransform)go.transform);
        return ui;
    }

    public void Refresh()
    {
        bool show = _round.CountdownActive && !_round.Ended;
        if (_banner.activeSelf != show) _banner.SetActive(show);
        if (show) _bannerText.text = $"TRUCK FLIPPED!  Put it back on its wheels:  {Mathf.CeilToInt(_round.CountdownRemaining)}";
    }

    public void ShowEnd(RoundResult r)
    {
        _banner.SetActive(false);
        _endPanel.SetActive(true);
        _title.text = r.won ? "YOU WON!" : "YOU LOST";
        _title.color = r.won ? Won : Lost;
        _reason.text = r.reason;

        var s = _round.Settings;
        var sb = new StringBuilder();
        int min = Mathf.FloorToInt(r.elapsedSeconds / 60f), sec = Mathf.FloorToInt(r.elapsedSeconds % 60f);
        sb.AppendLine($"<b>Time</b>   {min}:{sec:00}");
        sb.AppendLine();
        sb.AppendLine("<b>Progress</b>");
        sb.AppendLine($"   {Check(r.research >= 0.999f)} Research   {Mathf.RoundToInt(r.research * 100f)}%");
        sb.AppendLine($"   {Check(r.tornadoDetected)} Tornado detected");
        sb.AppendLine($"   {Check(r.tornadoKilled)} Tornado killed");
        sb.AppendLine();
        sb.AppendLine("<b>Items used</b>");
        int n = 1;
        foreach (var p in r.players.OrderBy(p => p.clientId))
        {
            string who = !RoundManager.IsNetworked ? "You" : $"Player {n}" + (p.clientId == RoundManager.LocalId ? " (you)" : "") + (p.clientId == 0 ? " (host)" : "");
            string items = p.uses.Count == 0 ? "none" : string.Join(", ", p.uses.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} x{kv.Value}"));
            sb.AppendLine($"   {who}: {items}");
            n++;
        }
        _body.text = sb.ToString();

        _score.text =
            $"<size=44><b>Score  {r.total}</b></size>\n" +
            $"Research {r.researchScore}   ·   Detected {r.detectedScore}   ·   Killed {r.killedScore}\n" +
            $"Win bonus {r.winScore}   ·   Speed bonus {r.speedScore}   ·   Items {r.itemScore}";

        // Start over: lose screen only, and only the host (or single player) can press it.
        bool canRestart = !r.won && RoundManager.IsAuthority;
        _startOver.gameObject.SetActive(canRestart);
        _waiting.gameObject.SetActive(!r.won && !RoundManager.IsAuthority);
    }

    static string Check(bool done) => done ? "<color=#5BD96A>DONE</color>" : "<color=#888888> —  </color>";

    // ── Layout ───────────────────────────────────────────────────────────────

    void Build(RectTransform root)
    {
        // Countdown banner, top centre.
        _banner = Panel(root, "FlipBanner", new Color(0.55f, 0.05f, 0.05f, 0.85f));
        var b = (RectTransform)_banner.transform;
        b.anchorMin = new Vector2(0.5f, 1f); b.anchorMax = new Vector2(0.5f, 1f); b.pivot = new Vector2(0.5f, 1f);
        b.sizeDelta = new Vector2(1100, 90); b.anchoredPosition = new Vector2(0, -30);
        _bannerText = Label(b, "Text", 40, TextAnchor.MiddleCenter, Color.white);
        Fill(_bannerText.rectTransform, 10);
        _banner.SetActive(false);

        // End screen: dim full-screen backdrop + centred panel.
        _endPanel = Panel(root, "EndScreen", new Color(0f, 0f, 0f, 0.7f));
        Fill((RectTransform)_endPanel.transform, 0);
        var card = (RectTransform)Panel((RectTransform)_endPanel.transform, "Card", new Color(0.08f, 0.09f, 0.11f, 0.96f)).transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(1000, 860);

        _title = Label(card, "Title", 84, TextAnchor.UpperCenter, Color.white);
        Place(_title.rectTransform, 0, -40, 940, 110);
        _reason = Label(card, "Reason", 30, TextAnchor.UpperCenter, new Color(0.8f, 0.8f, 0.8f));
        Place(_reason.rectTransform, 0, -150, 940, 50);
        _body = Label(card, "Stats", 30, TextAnchor.UpperLeft, Color.white);
        Place(_body.rectTransform, 0, -215, 860, 380);
        _score = Label(card, "Score", 26, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.35f));
        Place(_score.rectTransform, 0, -600, 940, 130);

        var menu = MakeButton(card, "Main Menu", () => _round.GoToMainMenu());
        _startOver = MakeButton(card, "Start Over", () => _round.StartOver());
        Place((RectTransform)menu.transform, -190, -750, 320, 72);
        Place((RectTransform)_startOver.transform, 190, -750, 320, 72);
        _waiting = Label(card, "Waiting", 26, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f));
        _waiting.text = "Waiting for the host to start over...";
        Place(_waiting.rectTransform, 190, -750, 380, 72);
        _endPanel.SetActive(false);
    }

    GameObject Panel(RectTransform parent, string n, Color color)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    Text Label(RectTransform parent, string n, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = _font; t.fontSize = size; t.alignment = anchor; t.color = color;
        t.supportRichText = true; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    Button MakeButton(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = Panel(parent, label, new Color(0.22f, 0.4f, 0.75f));
        var button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.onClick.AddListener(onClick);
        var t = Label((RectTransform)go.transform, "Label", 32, TextAnchor.MiddleCenter, Color.white);
        t.text = label;
        Fill(t.rectTransform, 0);
        return button;
    }

    static void Fill(RectTransform rt, float margin)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin); rt.offsetMax = new Vector2(-margin, -margin);
    }

    // Top-centre anchored: x offset from the middle, y down from the top.
    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    public string Describe() =>
        $"banner {(_banner.activeSelf ? _bannerText.text : "hidden")}; end screen {(_endPanel.activeSelf ? $"{_title.text} / restart button {_startOver.gameObject.activeSelf}" : "hidden")}";
}
