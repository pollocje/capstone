using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class JoinMenu : MonoBehaviour
{


    public TMP_InputField code_input;

    public TextMeshProUGUI statusText;


    public async void Submit() {

        string code = code_input.text.Trim().ToUpper();


        if (string.IsNullOrEmpty(code)) {

            return;
        
        }

        try
        {

            if (statusText != null)
            {

                statusText.text = "Joining session...";

            }


            await SessionManager.Instance.JoinSession(code);

            SceneManager.LoadScene("LobbyMenu");

        }
        catch (Exception e) {

            if (statusText != null) {

                statusText.text = "Invalid Code/Session Not found, please try again.";
            
            }
        
        }
    
    
    }


    public void Exit() { 
    
        SceneManager.LoadScene("MainMenu");

    }
}
