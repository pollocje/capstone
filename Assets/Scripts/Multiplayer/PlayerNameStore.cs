using UnityEngine;

/// <summary>
/// Saves and loads this player's nickname on their computer (PlayerPrefs).
/// Used by the Settings menu, the session (lobby names) and the in-game name tag.
/// </summary>
public static class PlayerNameStore
{
    const string Key = "PlayerName";
    public const int MaxLength = 20;

    /// <summary>The saved nickname. If none has been set yet, makes one like "Player4821".</summary>
    public static string Get()
    {
        string name = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Player" + Random.Range(1000, 10000);
            PlayerPrefs.SetString(Key, name);
            PlayerPrefs.Save();
        }
        return name;
    }

    /// <summary>Cleans and saves a new nickname. Returns the name actually saved.</summary>
    public static string Set(string name)
    {
        name = Clean(name);
        if (name.Length == 0) return Get(); // empty input: keep the current name

        PlayerPrefs.SetString(Key, name);
        PlayerPrefs.Save();
        return name;
    }

    static string Clean(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";

        // Remove invisible/control characters (e.g. TextMeshPro's zero-width space) and trim.
        var sb = new System.Text.StringBuilder();
        foreach (char c in name)
            if (!char.IsControl(c) && c != '\u200B') sb.Append(c);

        string cleaned = sb.ToString().Trim();
        return cleaned.Length > MaxLength ? cleaned.Substring(0, MaxLength) : cleaned;
    }
}
