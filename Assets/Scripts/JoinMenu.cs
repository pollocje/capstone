using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class JoinMenu : MonoBehaviour
{
    public TMP_InputField code_input;
    public TextMeshProUGUI statusText;

    bool joining;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public async void Submit()
    {
        if (joining) return; // avoid double-clicks sending two join requests

        string code = code_input.text;
        if (string.IsNullOrWhiteSpace(code)) return;

        joining = true;
        SetStatus("Joining session...");

        try
        {
            await SessionManager.Instance.JoinSession(code);

            // Do NOT call SceneManager.LoadScene here.
            // Once joined, this client is connected to the host, and Netcode's scene
            // management moves it into whatever scene the host is in automatically.
            SetStatus("Connected! Waiting for host...");
        }
        catch (Exception e)
        {
            // Log the REAL error so we can see why it failed.
            Debug.LogError($"Join failed: {e}");
            SetStatus($"Couldn't join:\n{Describe(e)}");
            joining = false;
        }
    }

    public void Exit()
    {
        SceneManager.LoadScene("MainMenu");
    }

    // Error type + message, plus any inner errors (the real cause is often nested).
    static string Describe(Exception e)
    {
        var sb = new System.Text.StringBuilder();
        for (Exception ex = e; ex != null; ex = ex.InnerException)
        {
            if (sb.Length > 0) sb.Append("\n↳ ");
            sb.Append($"{ex.GetType().Name}: {ex.Message}");
        }
        return sb.ToString();
    }

    void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}