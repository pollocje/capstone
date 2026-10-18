using UnityEngine;
using UnityEditor;

namespace AiryUI
{
    public class MainWindow : EditorWindow
    {


        private static EditorWindow window;
        private GUIStyle buttonContentStyle;
        private Vector2 windowScroll;


        [MenuItem ( "Airy UI/Main Window" , priority = 0 )]
        private static void ShowWindow ()
        {
            window = GetWindow<MainWindow> ( "Airy UI" );
            window.Show ();

            window.maxSize = new Vector2 ( 370 , 650 );
            window.minSize = new Vector2 ( 370 , 620 );
        }


        private void OnGUI ()
        {
            windowScroll = EditorGUILayout.BeginScrollView ( windowScroll , false , false );

            GUILayout.Space ( 20 );

            DrawAnimationManager_BUTTONS ();

            DrawAnimatedElement_BUTTONS ();

            DrawBackBtn_BUTTONS ();

            DrawRateBox ();

            DrawYoutube ();

            EditorGUILayout.EndScrollView ();
        }


        private void WindowTitle_LABEL ()
        {
            GUI.color = Color.white;


            GUILayout.Space ( 10 );

            var titleLabelStyle = new GUIStyle ( GUI.skin.label ) { alignment = TextAnchor.MiddleLeft , fontSize = 25 , fontStyle = FontStyle.Bold , fixedHeight = 50 };

            EditorGUILayout.LabelField ( "Airy UI Main Window" , titleLabelStyle );
            GUILayout.Space ( 50 );
        }


        private void DrawAnimationManager_BUTTONS ()
        {
            SetButtonStyle ( new Color ( 0.48f , 0.56f , 1 ) );

            GUIContent buttonContent = new GUIContent ( " Add Animated Elements Group" , SetIcon ( "manager_add.png" ) );
            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( GameObject g in Selection.gameObjects )
                {
                    Undo.AddComponent<AnimatedElementsGroup> ( g );
                }
            }


            buttonContent = new GUIContent ( " Remove Animated Elements Group" , SetIcon ( "manager_remove.png" ) );
            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( GameObject g in Selection.gameObjects )
                {
                    if ( g.GetComponent<AnimatedElementsGroup> () != null )
                    {
                        Undo.DestroyObjectImmediate ( g.GetComponent<AnimatedElementsGroup> () );
                    }
                }
            }

            GUILayout.Space ( 20 );

        }


        private void DrawAnimatedElement_BUTTONS ()
        {
            SetButtonStyle ( new Color ( 1 , 0.29f , 0.74f ) );

            GUIContent buttonContent = new GUIContent ( " Add Animated Element" , SetIcon ( "animation_add.png" ) );
            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( GameObject g in Selection.gameObjects )
                {
                    Undo.AddComponent<AnimatedElement> ( g );
                }
            }


            buttonContent = new GUIContent ( " Add Custom Animated Element" , SetIcon ( "c_animation_add.png" ) );
            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( GameObject g in Selection.gameObjects )
                {
                    if ( g.GetComponent<CustomAnimatedElement> () == null )
                    {
                        Undo.AddComponent<CustomAnimatedElement> ( g );
                    }
                }
            }


            buttonContent = new GUIContent ( " Remove Animated Element" , SetIcon ( "animation_remove.png" ) );
            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( GameObject g in Selection.gameObjects )
                {
                    Undo.RecordObject ( g , "Remove Animated Element" );
                    if ( g.GetComponent<AnimatedElement> () != null )
                        Undo.DestroyObjectImmediate ( g.GetComponent<AnimatedElement> () );

                    if ( g.GetComponent<CustomAnimatedElement> () != null )
                        Undo.DestroyObjectImmediate ( g.GetComponent<CustomAnimatedElement> () );
                }
            }

            GUILayout.Space ( 20 );
        }



        private void DrawBackBtn_BUTTONS ()
        {
            SetButtonStyle ( new Color ( 1 , 0.52f , 0.28f ) );


            GUIContent buttonContent = new GUIContent ( " Add Esc Button" , SetIcon ( "esc_add.png" ) );
            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( GameObject g in Selection.gameObjects )
                {
                    if ( g.GetComponent<EscCloseButton> () == null )
                    {
                        Undo.AddComponent<EscCloseButton> ( g );
                    }
                }
            }


            buttonContent = new GUIContent ( " Remove Esc Button" , SetIcon ( "esc_remove.png" ) );
            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( GameObject g in Selection.gameObjects )
                {
                    if ( g.GetComponent<EscCloseButton> () != null )
                    {
                        Undo.DestroyObjectImmediate ( g.GetComponent<EscCloseButton> () );
                    }
                }
            }

            GUILayout.Space ( 20 );

            SetButtonStyle ( new Color ( 1 , 0.38f , 0.28f ) );


            buttonContent = new GUIContent ( " Add UI Close Button" , SetIcon ( "back_add.png" ) );
            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( GameObject g in Selection.gameObjects )
                {
                    if ( g.GetComponent<UICloseButton> () == null )
                    {
                        Undo.AddComponent<UICloseButton> ( g );
                    }
                }
            }


            buttonContent = new GUIContent ( " Remove UI Close Button" , SetIcon ( "back_remove.png" ) );
            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
            {
                foreach ( GameObject g in Selection.gameObjects )
                {
                    if ( g.GetComponent<UICloseButton> () != null )
                    {
                        Undo.DestroyObjectImmediate ( g.GetComponent<UICloseButton> () );
                    }
                }
            }


            GUILayout.Space ( 20 );
        }



        [MenuItem ( "Airy UI/Rate On Asset Store" , priority = 15 )]
        public static void RateOnAssetStore ()
        {
            Application.OpenURL ( "https://assetstore.unity.com/packages/tools/gui/airy-ui-2-easy-ui-animation-135898#reviews" );
        }


        [MenuItem ( "Airy UI/Suggest | Report Bug" , priority = 16 )]
        public static void ReportBug ()
        {
            Application.OpenURL ( "https://forms.gle/4a9BU3C9ScP4XBoS8" );
        }


        [MenuItem ( "Airy UI/My Youtube Channel" , priority = 36 )]
        public static void YoutubeChannel ()
        {
            Application.OpenURL ( "https://youtube.com/ahmedsabrygamedev" );
        }


        private void DrawRateBox ()
        {
            GUILayout.Label ( "If you like Airy UI, Please Rate it On Asset Store ♡" );

            SetButtonStyle ( new Color ( 0.317f , 1 , 0.43f ) , 30 );

            GUIContent buttonContent = new GUIContent ( " Rate ♡" , SetIcon ( "asset_store.png" ) );

            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
                Application.OpenURL ( "https://assetstore.unity.com/packages/tools/gui/airy-ui-2-easy-ui-animation-135898#reviews" );

            GUILayout.Space ( 10 );
        }


        private void DrawYoutube ()
        {
            GUILayout.Label ( "And also, take a visit to my GameDev Youtube channel" );

            SetButtonStyle ( new Color ( 0.31f , 0.51f , 1 ) , 30 );

            GUIContent buttonContent = new GUIContent ( " Visit ♡" , SetIcon ( "youtube.png" ) );

            if ( GUILayout.Button ( buttonContent , buttonContentStyle ) )
                Application.OpenURL ( "https://youtube.com/ahmedsabrygamedev" );
        }


        private void SetButtonStyle ( Color backgroundColor , float height = 40 )
        {
            GUI.backgroundColor = backgroundColor;

            buttonContentStyle = new GUIStyle ( GUI.skin.button );

            buttonContentStyle.alignment = TextAnchor.MiddleLeft;
            buttonContentStyle.normal.textColor = Color.white;
            buttonContentStyle.hover.textColor = Color.yellow;
            buttonContentStyle.fixedHeight = height;
            buttonContentStyle.fixedWidth = 361;
            buttonContentStyle.fontSize = 17;
            buttonContentStyle.fontStyle = FontStyle.Bold;
        }


        private Texture2D SetIcon ( string icon )
        {
            Texture2D buttonIcon = AssetDatabase.LoadAssetAtPath<Texture2D> ( "Assets/Airy UI/Sprites/Icons/" + icon );

            return buttonIcon;
        }
    }
}