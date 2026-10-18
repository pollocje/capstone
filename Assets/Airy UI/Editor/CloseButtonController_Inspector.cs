using UnityEditor;
using UnityEngine;


namespace AiryUI
{


    [CustomEditor ( typeof ( EscButtonController ) )]
    [CanEditMultipleObjects]
    public class CloseButtonController_Inspector : Editor
    {

        private EscButtonController esc_manager;


        private SerializedProperty _esc_buttons;


        private void OnEnable ()
        {
            esc_manager = ( EscButtonController ) target;

            _esc_buttons = serializedObject.FindProperty ( "esc_buttons" );
        }


        public override void OnInspectorGUI ()
        {
            EditorGUILayout.Space ( 10 );

            EditorGUILayout.HelpBox ( "This game object is automatically added When you add EscBackButton please don't remove it" , MessageType.Info );

            EditorGUILayout.Space ( 10 );

            EditorGUILayout.PropertyField ( _esc_buttons );

            serializedObject.ApplyModifiedProperties ();
        }


    }


}