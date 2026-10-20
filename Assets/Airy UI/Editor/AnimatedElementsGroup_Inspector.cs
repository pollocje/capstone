using UnityEngine;
using UnityEditor;

namespace AiryUI
{
    [CustomEditor ( typeof ( AnimatedElementsGroup ) )]
    [CanEditMultipleObjects]
    public class AnimatedElementsGroup_Inspector : Editor
    {
        private AnimatedElementsGroup animatedGroup;


        private SerializedProperty _showAnimaitonOnEnable;
        private SerializedProperty animatedElements;


        private SerializedProperty _onShowEvent;
        private SerializedProperty _onHideEvent;


        private void OnEnable ()
        {
            animatedGroup = ( AnimatedElementsGroup ) target;

            _showAnimaitonOnEnable = serializedObject.FindProperty ( "showAnimaitonOnEnable" );
            animatedElements = serializedObject.FindProperty ( "animatedElements" );

            _onShowEvent = serializedObject.FindProperty ( "OnShow" );
            _onHideEvent = serializedObject.FindProperty ( "OnHide" );
        }


        public override void OnInspectorGUI ()
        {
            Undo.RecordObject ( animatedGroup , "animated group" );

            GUILayout.Space ( 20 );

            EditorGUILayout.PropertyField ( _showAnimaitonOnEnable , new GUIContent ( "Animate On Enable" ) );

            if ( animatedGroup.showAnimaitonOnEnable )
            {
                EditorGUILayout.HelpBox ( "TRUE: All Animated Elements in this Group will animate automatically when this GameObject is enabled.\n\nFALSE: You control the animation by calling ShowGroup() in this script." , MessageType.None );
            }

            GUILayout.Space ( 10 );

            EditorGUILayout.PropertyField ( animatedElements , new GUIContent ( "Animated Elements" ) );

            GUILayout.Space ( 20 );

            DrawOnShow_EVENT ();
            DrawOnHide_EVENT ();

            GUILayout.Space ( 20 );

            serializedObject.ApplyModifiedProperties ();
        }


        private void DrawOnShow_EVENT ()
        {
            EditorGUILayout.PropertyField ( _onShowEvent , new GUIContent ( "On Show" ) );
            EditorGUILayout.Space (); EditorGUILayout.Space ();
        }


        private void DrawOnHide_EVENT ()
        {
            EditorGUILayout.PropertyField ( _onHideEvent , new GUIContent ( "On Hide" ) );
        }
    }
}