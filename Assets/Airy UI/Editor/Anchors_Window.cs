using UnityEngine;
using UnityEditor;


namespace AiryUI
{

    public class Anchors_Window : EditorWindow
    {
        private static EditorWindow window;
        private GUIStyle buttonContentStyle;


        private bool infoFoldout;
        private Vector2 windowScroll;


        [MenuItem ( "Airy UI/Anchors Window" , priority = 1 )]
        private static void ShowWindow ()
        {
            window = GetWindow<Anchors_Window> ( "Anchors Window" );

            window.maxSize = new Vector2 ( 345 , 430 );
            window.minSize = new Vector2 ( 345 , 430 );
            window.Show ();
        }


        private void OnGUI ()
        {
            windowScroll = EditorGUILayout.BeginScrollView ( windowScroll , false , false );

            //GUI.color = Color.gray;
            //WindowTitle_LABEL ();
            //GUI.color = Color.white;

            EditorGUILayout.Space ( 20 );

            ImportantInfo ();

            GUILayout.Space ( 20 );

            GUI.color = Color.white;

            DrawWindow ();

            DrawRateBox ();
            DrawYoutube ();

            EditorGUILayout.EndScrollView ();
        }


        private void WindowTitle_LABEL ()
        {
            GUILayout.Space ( 10 );

            var titleLabelStyle = new GUIStyle ( GUI.skin.label ) { alignment = TextAnchor.UpperCenter , fontSize = 30 , fontStyle = FontStyle.Bold , fixedHeight = 50 };

            EditorGUILayout.LabelField ( "Anchors Editor" , titleLabelStyle );
            EditorGUILayout.Space (); EditorGUILayout.Space (); EditorGUILayout.Space ();

            GUILayout.Space ( 30 );
        }


        private void ImportantInfo ()
        {
            infoFoldout = EditorGUILayout.BeginFoldoutHeaderGroup ( infoFoldout , "Important Info" );
            if ( infoFoldout )
            {
                GUI.color = Color.white;

                EditorGUILayout.HelpBox ( "Scale of RectTransform must be (1, 1, 1) in order for the anchors to be exact." , MessageType.Warning );
                EditorGUILayout.HelpBox ( "Sometimes, you may need to set anchors twice if the pivot is not centered." , MessageType.Info );
                EditorGUILayout.HelpBox ( "You can select multiple Game Objects, and use the shortcut ctrl, shift, q or ctrl, shift, w" , MessageType.Info );
            }
        }


        [MenuItem ( "Airy UI/Anchors/Set Anchors To Fit Rect %#q" , priority = 2 )]
        public static void SetAnchorsToRectOnSelectedGameObjects_Shortcut ()
        {
            SetAnchorsToRectOnSelectedGameObjects ();
        }


        [MenuItem ( "Airy UI/Anchors/Align Selected To Anchors %#w" , priority = 2 )]
        public static void SetRectToAnchorsOnSelectedGameObjects_Shortcut ()
        {
            SetRectToAnchorsOnSelectedGameObjects ();
        }


        private void DrawWindow ()
        {
            EditorGUILayout.BeginHorizontal ();


            SetButtonStyle ( new Color ( 0.31f , 0.51f , 1 ) , fontSize: 11 , height: 100 );
            GUIContent buttonContent = new GUIContent ( "↖ ↗\n↙ ↘\nANCHORS TO RECT\n\nctrl, shift, q" );

            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                SetAnchorsToRectOnSelectedGameObjects ();
            }


            SetButtonStyle ( new Color ( 0.74f , 0 , 0.37f ) , fontSize: 11 , height: 100 );
            buttonContent = new GUIContent ( "↘ ↙\n↗ ↖\nRECT TO ANCHORS\n\nctrl, shift, w" );


            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                SetRectToAnchorsOnSelectedGameObjects ();
            }

            EditorGUILayout.EndHorizontal ();

            GUILayout.Space ( 10 );

            EditorGUILayout.BeginHorizontal ();


            SetButtonStyle ( new Color ( 0 , 1 , 0.9f ) , fontSize: 11 , 30 );
            buttonContent = new GUIContent ( "↖" );


            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( var g in Selection.gameObjects )
                {
                    RectTransform rectTransform = g.GetComponent<RectTransform> ();

                    if ( rectTransform != null )
                    {
                        Undo.RecordObject ( rectTransform , "Set Anchors" );
                        Anchors.SetAnchorsTopLeft ( rectTransform );
                    }
                }

                if ( Selection.gameObjects.Length > 0 )
                    PrintDoneMessage ();
            }


            SetButtonStyle ( new Color ( 0 , 1 , 0.9f ) , fontSize: 11 , 30 );
            buttonContent = new GUIContent ( "▲" );


            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( var g in Selection.gameObjects )
                {
                    RectTransform rectTransform = g.GetComponent<RectTransform> ();

                    if ( rectTransform != null )
                    {
                        Undo.RecordObject ( rectTransform , "Set Anchors" );
                        Anchors.SetAnchorsTop ( rectTransform );
                    }
                }

                if ( Selection.gameObjects.Length > 0 )
                    PrintDoneMessage ();
            }


            SetButtonStyle ( new Color ( 0 , 1 , 0.9f ) , fontSize: 11 , 30 );
            buttonContent = new GUIContent ( "↗" );


            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( var g in Selection.gameObjects )
                {
                    RectTransform rectTransform = g.GetComponent<RectTransform> ();

                    if ( rectTransform != null )
                    {
                        Undo.RecordObject ( rectTransform , "Set Anchors" );
                        Anchors.SetAnchorsTopRight ( rectTransform );
                    }
                }

                if ( Selection.gameObjects.Length > 0 )
                    PrintDoneMessage ();
            }

            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.BeginHorizontal ();


            SetButtonStyle ( new Color ( 0 , 1 , 0.9f ) , fontSize: 11 , 30 );
            buttonContent = new GUIContent ( "◄" );


            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( var g in Selection.gameObjects )
                {
                    RectTransform rectTransform = g.GetComponent<RectTransform> ();

                    if ( rectTransform != null )
                    {
                        Undo.RecordObject ( rectTransform , "Set Anchors" );
                        Anchors.SetAnchorsLeft ( rectTransform );
                    }
                }

                if ( Selection.gameObjects.Length > 0 )
                    PrintDoneMessage ();
            }


            SetButtonStyle ( new Color ( 1 , 0.46f , 0 ) , fontSize: 11 , 30 );
            buttonContent = new GUIContent ( "●" );


            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( var g in Selection.gameObjects )
                {
                    RectTransform rectTransform = g.GetComponent<RectTransform> ();

                    if ( rectTransform != null )
                    {
                        Undo.RecordObject ( rectTransform , "Set Anchors" );
                        Anchors.SetAnchorsCenter ( rectTransform );
                    }
                }

                if ( Selection.gameObjects.Length > 0 )
                    PrintDoneMessage ();
            }


            SetButtonStyle ( new Color ( 0 , 1 , 0.9f ) , fontSize: 11 , 30 );
            buttonContent = new GUIContent ( "►" );


            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( var g in Selection.gameObjects )
                {
                    RectTransform rectTransform = g.GetComponent<RectTransform> ();

                    if ( rectTransform != null )
                    {
                        Undo.RecordObject ( rectTransform , "Set Anchors" );
                        Anchors.SetAnchorsRight ( rectTransform );
                    }
                }

                if ( Selection.gameObjects.Length > 0 )
                    PrintDoneMessage ();
            }

            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.BeginHorizontal ();


            SetButtonStyle ( new Color ( 0 , 1 , 0.9f ) , fontSize: 11 , 30 );
            buttonContent = new GUIContent ( "↙" );


            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( var g in Selection.gameObjects )
                {
                    RectTransform rectTransform = g.GetComponent<RectTransform> ();

                    if ( rectTransform != null )
                    {
                        Undo.RecordObject ( rectTransform , "Set Anchors" );
                        Anchors.SetAnchorsBottomLeft ( rectTransform );
                    }
                }
                if ( Selection.gameObjects.Length > 0 )
                    PrintDoneMessage ();

            }


            SetButtonStyle ( new Color ( 0 , 1 , 0.9f ) , fontSize: 11 , 30 );
            buttonContent = new GUIContent ( "▼" );


            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( var g in Selection.gameObjects )
                {
                    RectTransform rectTransform = g.GetComponent<RectTransform> ();

                    if ( rectTransform != null )
                    {
                        Undo.RecordObject ( rectTransform , "Set Anchors" );
                        Anchors.SetAnchorsBottom ( rectTransform );
                    }
                }
                if ( Selection.gameObjects.Length > 0 )
                    PrintDoneMessage ();
            }


            SetButtonStyle ( new Color ( 0 , 1 , 0.9f ) , fontSize: 11 , 30 );
            buttonContent = new GUIContent ( "↘" );


            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( var g in Selection.gameObjects )
                {
                    RectTransform rectTransform = g.GetComponent<RectTransform> ();

                    if ( rectTransform != null )
                    {
                        Undo.RecordObject ( rectTransform , "Set Anchors" );
                        Anchors.SetAnchorsBottomRight ( rectTransform );
                    }
                }

                if ( Selection.gameObjects.Length > 0 )
                    PrintDoneMessage ();
            }

            EditorGUILayout.EndHorizontal ();


            EditorGUILayout.Space ( 20 );
        }


        private void DrawRateBox ()
        {
            GUILayout.Label ( "If you like Airy UI, Please Rate it On Asset Store ♡" );

            SetButtonStyle ( new Color ( 0.317f , 1 , 0.43f ) , fontSize: 15 , height: 30 , width: 338 , alignment: TextAnchor.MiddleLeft );

            GUIContent buttonContent = new GUIContent ( " Rate ♡" , SetIcon ( "asset_store.png" ) );

            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
                Application.OpenURL ( "https://assetstore.unity.com/packages/tools/gui/airy-ui-easy-ui-animation-135898" );

            GUILayout.Space ( 10 );
        }


        private void DrawYoutube ()
        {
            GUILayout.Label ( "And also, take a visit to my GameDev Youtube channel" );

            SetButtonStyle ( new Color ( 0.31f , 0.51f , 1 ) , fontSize: 15 , height: 30 , width: 338 , alignment: TextAnchor.MiddleLeft );

            GUIContent buttonContent = new GUIContent ( " Visit ♡" , SetIcon ( "youtube.png" ) );

            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
                Application.OpenURL ( "https://youtube.com/ahmedsabrygamedev" );
        }


        private void SetButtonStyle ( Color backgroundColor , int fontSize = 0 , float height = 0 , float width = 0 , TextAnchor alignment = TextAnchor.MiddleCenter )
        {
            GUI.backgroundColor = backgroundColor;
            buttonContentStyle = null;
            buttonContentStyle = new GUIStyle ( GUI.skin.button );

            buttonContentStyle.alignment = alignment;
            buttonContentStyle.normal.textColor = Color.white;
            buttonContentStyle.hover.textColor = Color.yellow;

            if ( height != 0 )
                buttonContentStyle.fixedHeight = height;

            if ( width != 0 )
                buttonContentStyle.fixedWidth = width;

            if ( fontSize != 0 )
                buttonContentStyle.fontSize = fontSize;

            buttonContentStyle.fontStyle = FontStyle.Bold;
        }


        private Texture2D SetIcon ( string icon )
        {
            Texture2D buttonIcon = AssetDatabase.LoadAssetAtPath<Texture2D> ( "Assets/Airy UI/Sprites/Icons/" + icon );

            return buttonIcon;
        }



        private static void SetAnchorsToRectOnSelectedGameObjects ()
        {
            GameObject [] selectedGameObjects = Selection.gameObjects;

            foreach ( var g in selectedGameObjects )
            {
                RectTransform rectTransform = g.GetComponent<RectTransform> ();

                if ( rectTransform != null )
                {
                    Undo.RecordObject ( rectTransform , "Set Anchors" );
                    Anchors.SetAnchorsToRect ( rectTransform );

                }
            }

            PrintDoneMessage ();
        }


        private static void SetRectToAnchorsOnSelectedGameObjects ()
        {
            GameObject [] selectedGameObjects = Selection.gameObjects;

            foreach ( var g in selectedGameObjects )
            {
                RectTransform rectTransform = g.GetComponent<RectTransform> ();

                if ( rectTransform != null )
                {
                    Undo.RecordObject ( rectTransform , "Set Anchors" );
                    Anchors.SetRectToAnchor ( rectTransform );

                }
            }

            PrintDoneMessage ();
        }



        private static void PrintDoneMessage ()
        {
            Debug.Log ( "<color=orange><b>[Airy UI]</color></b>" +
                "<color=green><b>✓</color></b>" +
                " anchors set for "
                + Selection.gameObjects.Length +
                " game object/s" );
        }



    }


}