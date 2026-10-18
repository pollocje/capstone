using UnityEditor;
using UnityEngine;


namespace AiryUI
{


    [CustomEditor ( typeof ( CloseButton ) , true )]
    [CanEditMultipleObjects]
    public class CloseButton_Inspector : Editor
    {


        private CloseButton close_button;


        private SerializedProperty _backButtonAffects;
        private SerializedProperty _animatedElement;
        private SerializedProperty _animatedElementsGroup;

        private SerializedProperty _buttonComponent;


        private void OnEnable ()
        {
            close_button = ( CloseButton ) target;

            _backButtonAffects = serializedObject.FindProperty ( "backButtonAffects" );
            _animatedElement = serializedObject.FindProperty ( "affectedAnimatedElement" );
            _animatedElementsGroup = serializedObject.FindProperty ( "affectedAnimatedElementsGroup" );

            if ( close_button is UICloseButton )
            {
                _buttonComponent = serializedObject.FindProperty ( "buttonComponent" );
            }
        }


        public override void OnInspectorGUI ()
        {
            EditorGUILayout.Space ( 10 );

            EditorGUILayout.PropertyField ( _backButtonAffects );

            EditorGUILayout.Space ( 10 );

            if ( close_button.backButtonAffects == BackButtonAffects.AnimatedElement )
            {
                EditorGUILayout.PropertyField ( _animatedElement );
            }
            else if ( close_button.backButtonAffects == BackButtonAffects.AnimatedElementsGroup )
            {
                EditorGUILayout.PropertyField ( _animatedElementsGroup );
            }

            EditorGUILayout.Space ( 10 );

            if ( close_button is UICloseButton )
            {
                EditorGUILayout.PropertyField ( _buttonComponent );
            }

            serializedObject.ApplyModifiedProperties ();
        }


        private void InspectorTitle_LABEL ( string text , bool spaceBefore , bool spaceAfter )
        {
            if ( spaceBefore )
                GUILayout.Space ( 20 );

            var titleLabelStyle = new GUIStyle ( GUI.skin.label ) { alignment = TextAnchor.UpperCenter , fontSize = 20 , fontStyle = FontStyle.Bold , fixedHeight = 50 };

            EditorGUILayout.LabelField ( text , titleLabelStyle );

            if ( spaceAfter )
                GUILayout.Space ( 20 );
        }
    }


}