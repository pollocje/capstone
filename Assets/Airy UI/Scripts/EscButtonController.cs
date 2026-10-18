using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace AiryUI
{


    public class EscButtonController : MonoBehaviour
    {


        public List<EscCloseButton> esc_buttons = new List<EscCloseButton> ();


        public static EscButtonController Instance;


        private void Awake ()
        {
            if ( Instance == null )
                Instance = this;
            else
                Destroy ( gameObject );
        }


        private void Update ()
        {
            if ( Input.GetKeyDown ( KeyCode.Escape ) )
            {
                DoBack ();
            }
        }


        private void DoBack ()
        {
            if ( esc_buttons.Count > 0 )
            {
                var last_button = esc_buttons [ esc_buttons.Count - 1 ];

                if ( last_button != null )
                {
                    last_button.DoBack ();
                }
                else
                {
                    Debug.LogError ( "<color=orange><b>[Airy UI]</color></b> <color=red><b>X</color></b> there's a missing item in <b>'ESC Button Controller'</b>, please check your list again!" , gameObject );
                }
            }
        }


        public void AddButtonToList ( EscCloseButton backButton )
        {
            if ( !esc_buttons.Contains ( backButton ) )
            {
                esc_buttons.Add ( backButton );
            }
        }


        public void RemoveButtonFromList ( EscCloseButton backButton )
        {
            if ( esc_buttons.Contains ( backButton ) )
            {
                esc_buttons.Remove ( backButton );
            }
        }


    }


}