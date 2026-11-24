using System;
using UnityEngine;
using UnityEditor;


namespace AiryUI
{


    public static class EditorGUILayoutExtensions
    {
        public static void ToggleLeft(this SerializedProperty prop, GUIContent label, params GUILayoutOption[] options)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = prop.hasMultipleDifferentValues;
            var newValue = EditorGUILayout.ToggleLeft(label, prop.boolValue, options);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                prop.boolValue = newValue;
        }


        /// <summary>
        /// Create a EditorGUILayout.Slider which properly handles multi-object editing
        /// We apply the 'convIn' conversion to the SerializedProperty value before exposing it as a Slider.
        /// We apply the 'convOut' conversion to the Slider value to store it back to the SerializedProperty.
        /// </summary>
        /// <param name="prop">The value the slider shows. This determines the position of the draggable thumb.</param>
        /// <param name="label">Label in front of the slider.</param>
        /// <param name="leftValue">The value at the left end of the slider.</param>
        /// <param name="rightValue">The value at the right end of the slider.</param>
        /// <param name="convIn">Conversion function applied on the SerializedProperty to get the Slider value</param>
        /// <param name="convOut">Conversion function applied on the Slider value to get the SerializedProperty</param>
        public static void FloatSlider(
            this SerializedProperty prop,
            GUIContent label,
            float leftValue, float rightValue,
            Func<float, float> convIn,
            Func<float, float> convOut,
            params GUILayoutOption[] options)
        {
            var floatValue = convIn(prop.floatValue);
            EditorGUI.BeginChangeCheck();
            {
                EditorGUI.showMixedValue = prop.hasMultipleDifferentValues;
                {
                    floatValue = EditorGUILayout.Slider(label, floatValue, leftValue, rightValue, options);
                }
                EditorGUI.showMixedValue = false;
            }
            if (EditorGUI.EndChangeCheck())
                prop.floatValue = convOut(floatValue);
        }
    }

}