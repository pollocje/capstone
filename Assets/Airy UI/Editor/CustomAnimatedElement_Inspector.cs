using UnityEngine;
using UnityEditor;


namespace AiryUI
{
    [CustomEditor(typeof(CustomAnimatedElement))]
    [CanEditMultipleObjects]
    public class CustomAnimatedElement_Inspector : Editor
    {
        private CustomAnimatedElement animatedElement;

        private SerializedProperty _isControlledByGroup;

        private SerializedProperty _animateOnEnable;

        //===================


        private SerializedProperty _showTimeMode;
        private SerializedProperty _hideTimeMode;


        //===================


        private SerializedProperty _group;


        //===================


        private SerializedProperty _containerCanvas;


        //===================


        private SerializedProperty _componentsToAnimate_SHOW;
        private SerializedProperty _transformAnimationRecords_SHOW;
        private SerializedProperty _graphicAnimationRecords_SHOW;
        private SerializedProperty _transformAndGraphicAnimationRecords_SHOW;


        //===================


        private SerializedProperty _componentsToAnimate_HIDE;
        private SerializedProperty _transformAnimationRecords_HIDE;
        private SerializedProperty _graphicAnimationRecords_HIDE;
        private SerializedProperty _transformAndGraphicAnimationRecords_HIDE;


        //===================


        private SerializedProperty _currentRecordDuration;
        private SerializedProperty _currentRecordDelay;


        //===================


        private SerializedProperty _showDelay;
        private SerializedProperty _hideDelay;


        //===================


        private SerializedProperty _onShowEvent;
        private SerializedProperty _onHideEvent;
        private SerializedProperty _onShowCompleteEvent;
        private SerializedProperty _onHideCompleteEvent;


        //===================


        private string[] tabsTexts = { "Show", "Hide" };
        private int currentTabIndex = 0;

        public float currentRecordDuration = 0.5f;
        public float currentRecordStartsAt = 0;

        private bool recordModeActive;


        //===================


        private Color showPropsColor = new Color(0.52f, 1, 0.8f, 1);
        private Color hidePropsColor = new Color(1, 0.66f, 0.66f, 1);


        //===================


        private void OnEnable()
        {
            GetSavedInspectorValues();

            animatedElement = (CustomAnimatedElement)target;

            _isControlledByGroup = serializedObject.FindProperty("isControlledByGroup");
            _group = serializedObject.FindProperty("group");

            _animateOnEnable = serializedObject.FindProperty("animateOnEnable");

            _containerCanvas = serializedObject.FindProperty("containerCanvas");

            _showTimeMode = serializedObject.FindProperty("showTimeMode");
            _hideTimeMode = serializedObject.FindProperty("hideTimeMode");

            _componentsToAnimate_SHOW = serializedObject.FindProperty("componentsToAnimate_SHOW");
            _transformAnimationRecords_SHOW = serializedObject.FindProperty("TransformAnimationRecords_SHOW");
            _graphicAnimationRecords_SHOW = serializedObject.FindProperty("GraphicAnimationRecords_SHOW");
            _transformAndGraphicAnimationRecords_SHOW = serializedObject.FindProperty("TransformAndGraphicAnimationRecords_SHOW");

            _componentsToAnimate_HIDE = serializedObject.FindProperty("componentsToAnimate_HIDE");
            _transformAnimationRecords_HIDE = serializedObject.FindProperty("TransformAnimationRecords_HIDE");
            _graphicAnimationRecords_HIDE = serializedObject.FindProperty("GraphicAnimationRecords_HIDE");
            _transformAndGraphicAnimationRecords_HIDE = serializedObject.FindProperty("TransformAndGraphicAnimationRecords_HIDE");

            _currentRecordDuration = serializedObject.FindProperty("currentRecordDuration");
            _currentRecordDelay = serializedObject.FindProperty("currentRecordDelay");

            _showDelay = serializedObject.FindProperty("showDelay");
            _hideDelay = serializedObject.FindProperty("hideDelay");

            _onShowEvent = serializedObject.FindProperty("OnShow");
            _onHideEvent = serializedObject.FindProperty("OnHide");
            _onShowCompleteEvent = serializedObject.FindProperty("OnShowComplete");
            _onHideCompleteEvent = serializedObject.FindProperty("OnHideComplete");
        }

        public override void OnInspectorGUI()
        {
            if (animatedElement.GetComponent<AnimatedElementsGroup>())
            {
                EditorGUILayout.HelpBox("You can't add 'Custom Animated Element' on a game object that has 'Animated Elements Group' !", MessageType.Error);

                animatedElement.enabled = false;
                animatedElement.disableShowAnimation = true;
                animatedElement.disableHideAnimation = true;


                // Don't draw anything.
                return;
            }

            Undo.RecordObject(animatedElement, "custom animated element");

            GeneralSettings();

            DrawLinkedGroup();

            DrawAnimateOnEnable_TOGGLE();

            Tabs();

            if (currentTabIndex == 0)
            {
                EditorGUILayout.BeginVertical(SetContainerBoxStyle(new Color(0.52f, 1, 0.8f, 0.1f)));

                if (!animatedElement.disableShowAnimation)
                {

                    Loop_TOGGLE();
                    ComponentsToAnimateShow_DROPDOWN();
                    AnimationShowTimeMode_DROPDOWN();
                    RecordMode_BUTTONS();
                    ShowAnimationDelay_PROPS();
                    Duration_PROPS();
                    TransformAnimationRecordsShow_LIST();
                    GraphicAnimationRecordsShow_LIST();
                    TransformAndGraphicAnimationRecordsShow_LIST();
                    OnShow_EVENT();
                    OnShowComplete_EVENT();
                }
                else
                {
                    OnShowComplete_EVENT();
                }

                GUILayout.EndVertical();
            }
            else if (currentTabIndex == 1)
            {
                EditorGUILayout.BeginVertical(SetContainerBoxStyle(new Color(1, 0.66f, 0.66f, 0.1f)));


                if (!animatedElement.disableHideAnimation)
                {
                    ComponentsToAnimateHide_DROPDOWN();
                    AnimationHideTimeMode_DROPDOWN();
                    RecordMode_BUTTONS();
                    HideAnimationDelay_PROPS();
                    Duration_PROPS();
                    TransformAnimationRecordsHide_LIST();
                    GraphicAnimationRecordsHide_LIST();
                    TransformAndGraphicAnimationRecordsHide_LIST();
                    OnHide_EVENT();
                    OnHideComplete_EVENT();
                }
                else
                {
                    OnHideComplete_EVENT();
                }


                GUILayout.EndVertical();
            }


            serializedObject.ApplyModifiedProperties();
            SaveInspectorValues();
        }

        private void GeneralSettings()
        {
            //Title_LABEL ();

            GUILayout.Space(10);

            GUI.color = showPropsColor;
            animatedElement.disableShowAnimation = EditorGUILayout.ToggleLeft("Disable Show Animation", animatedElement.disableShowAnimation);

            GUI.color = hidePropsColor;
            animatedElement.disableHideAnimation = EditorGUILayout.ToggleLeft("Disable Hide Animation", animatedElement.disableHideAnimation);

            GUILayout.Space(10);

            GUI.color = Color.white;
            ContainerCanvas();

            GUILayout.Space(10);
        }


        private void DrawLinkedGroup()
        {
            animatedElement.isControlledByGroup = EditorGUILayout.ToggleLeft("Is Controlled By Group?", animatedElement.isControlledByGroup);
            if (animatedElement.isControlledByGroup)
            {
                EditorGUILayout.PropertyField(_group);
            }

            EditorGUILayout.Space(10);
        }


        private void DrawAnimateOnEnable_TOGGLE()
        {
            EditorGUILayoutExtensions.ToggleLeft(_animateOnEnable, new GUIContent("Animate On Enable"));

            if (animatedElement.isControlledByGroup && animatedElement.group != null)
            {
                animatedElement.animateOnEnable = false;
                EditorGUILayout.HelpBox("You can't enable 'Animate On Enable' because this Animated Element is controlled by an 'Animated Elements Group'", MessageType.None);
            }

            GUILayout.Space(10);
        }


        private void Tabs()
        {
            GUILayout.Space(20);

            currentTabIndex = GUILayout.Toolbar(currentTabIndex, new string[] { "Show Animation", "Hide Animation" });

            GUILayout.Space(10);
        }


        private void ContainerCanvas()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PropertyField(_containerCanvas, new GUIContent("Container Canvas"));

            if (GUILayout.Button("Auto Find"))
            {
                foreach (var go in Selection.gameObjects)
                {
                    go.GetComponent<AnimatedElement>().containerCanvas = go.GetComponentInParent<Canvas>();
                }
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
        }


        private GUIStyle SetContainerBoxStyle(Color color)
        {
            GUIStyle boxStyle = new GUIStyle("box");
            boxStyle.padding = new RectOffset(20, 20, 20, 20);
            boxStyle.margin = new RectOffset(5, 5, 5, 5);

            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();

            boxStyle.normal.background = texture;


            return boxStyle;
        }


        private void AnimationShowTimeMode_DROPDOWN()
        {
            EditorGUILayout.PropertyField(_showTimeMode, new GUIContent("Time Mode"));

            GUILayout.Space(10);
        }

        private void AnimationHideTimeMode_DROPDOWN()
        {
            EditorGUILayout.PropertyField(_hideTimeMode, new GUIContent("Time Mode"));

            GUILayout.Space(10);
        }


        private void Loop_TOGGLE()
        {
            animatedElement.loop = EditorGUILayout.ToggleLeft("Loop", animatedElement.loop);

            GUILayout.Space(10);
        }

        private void Title_LABEL()
        {
            GUILayout.Space(20);
            var titleLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 20, fontStyle = FontStyle.Bold, fixedHeight = 50 };

            EditorGUILayout.LabelField("Custom Animated Element", titleLabelStyle);

            GUILayout.Space(30);
        }

        private void ComponentsToAnimateShow_DROPDOWN()
        {
            EditorGUILayout.PropertyField(_componentsToAnimate_SHOW, new GUIContent("What to animate"), true);

            GUI.color = Color.white;

            GUILayout.Space(20);
        }

        private void ComponentsToAnimateHide_DROPDOWN()
        {
            //GUI.color = Color.cyan;
            EditorGUILayout.PropertyField(_componentsToAnimate_HIDE, new GUIContent("What to animate"), true);
            GUI.color = Color.white;
            GUILayout.Space(20);
        }

        private void Duration_PROPS()
        {
            EditorGUILayout.BeginVertical();

            GUI.color = Color.yellow;

            EditorGUILayout.PropertyField(_currentRecordDuration);

            GUI.color = Color.green;

            EditorGUILayout.PropertyField(_currentRecordDelay);

            GUI.color = Color.white;
            GUILayout.Space(20);

            EditorGUILayout.EndVertical();
        }

        private void TransformAnimationRecordsShow_LIST()
        {
            EditorGUILayout.BeginVertical();

            if (animatedElement.componentsToAnimate_SHOW == CustomAnimatedElement.ComponentsToAnimate.Transform)
            {
                EditorGUILayout.PropertyField(_transformAnimationRecords_SHOW, new GUIContent("Animation Records"));
                GUILayout.Space(20);
            }

            EditorGUILayout.EndVertical();
        }

        private void TransformAnimationRecordsHide_LIST()
        {
            EditorGUILayout.BeginVertical();

            if (animatedElement.componentsToAnimate_HIDE == CustomAnimatedElement.ComponentsToAnimate.Transform)
            {
                EditorGUILayout.PropertyField(_transformAnimationRecords_HIDE, new GUIContent("Animation Records"), true);
                GUILayout.Space(20);
            }

            EditorGUILayout.EndVertical();
        }

        private void GraphicAnimationRecordsShow_LIST()
        {
            EditorGUILayout.BeginVertical();

            if (animatedElement.componentsToAnimate_SHOW == CustomAnimatedElement.ComponentsToAnimate.Graphic)
            {
                EditorGUILayout.PropertyField(_graphicAnimationRecords_SHOW, new GUIContent("Animation Records"), true);
                GUILayout.Space(20);
            }

            EditorGUILayout.EndVertical();
        }

        private void GraphicAnimationRecordsHide_LIST()
        {
            EditorGUILayout.BeginVertical();

            if (animatedElement.componentsToAnimate_HIDE == CustomAnimatedElement.ComponentsToAnimate.Graphic)
            {
                EditorGUILayout.PropertyField(_graphicAnimationRecords_HIDE, new GUIContent("Animation Records"), true);
                GUILayout.Space(20);
            }

            EditorGUILayout.EndVertical();
        }

        private void TransformAndGraphicAnimationRecordsShow_LIST()
        {
            EditorGUILayout.BeginVertical();

            if (animatedElement.componentsToAnimate_SHOW == CustomAnimatedElement.ComponentsToAnimate.Both)
            {
                EditorGUILayout.PropertyField(_transformAndGraphicAnimationRecords_SHOW, new GUIContent("Animation Records"), true);
                GUILayout.Space(20);
            }

            EditorGUILayout.EndVertical();
        }

        private void TransformAndGraphicAnimationRecordsHide_LIST()
        {
            EditorGUILayout.BeginVertical();

            if (animatedElement.componentsToAnimate_HIDE == CustomAnimatedElement.ComponentsToAnimate.Both)
            {
                EditorGUILayout.PropertyField(_transformAndGraphicAnimationRecords_HIDE, new GUIContent("Animation Records"), true);
                GUILayout.Space(20);
            }

            EditorGUILayout.EndVertical();
        }

        private void RecordMode_BUTTONS()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(recordModeActive);

            GUI.color = new Color(1, 0.45f, 0, 1);
            if (GUILayout.Button("●\n<color=white>RECORD</color>", new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, fixedWidth = 100, richText = true }))
            {
                foreach (var g in Selection.gameObjects)
                {
                    CustomAnimatedElement aniamtedElement = g.GetComponent<CustomAnimatedElement>();
                    if (aniamtedElement)
                    {
                        aniamtedElement.EnterRecordMode(g, (CustomAnimatedElement.AnimationShowOrHide)currentTabIndex);
                    }
                }

                recordModeActive = true;
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(5);

            EditorGUI.BeginDisabledGroup(!recordModeActive);

            GUI.color = Color.yellow;
            if (GUILayout.Button("■\n<color=white>STOP</color> ", new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, fixedWidth = 100, richText = true }))
            {
                foreach (var g in Selection.gameObjects)
                {
                    CustomAnimatedElement aniamtedElement = g.GetComponent<CustomAnimatedElement>();
                    if (aniamtedElement)
                    {
                        aniamtedElement.ExitRecordMode(g, (CustomAnimatedElement.AnimationShowOrHide)currentTabIndex);
                    }
                }

                recordModeActive = false;
                GUI.color = Color.white;
            }


            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(!recordModeActive);


            GUI.color = new Color(0.66f, 0, 1, 1);

            if (GUILayout.Button("⌦\n<color=white>KEYFRAME</color>", new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, richText = true }))
            {
                if (currentTabIndex == 0)
                {
                    foreach (var g in Selection.gameObjects)
                    {
                        CustomAnimatedElement aniamtedElement = g.GetComponent<CustomAnimatedElement>();
                        if (aniamtedElement)
                        {
                            aniamtedElement.Record(g, CustomAnimatedElement.AnimationShowOrHide.Show);
                        }
                    }
                }
                else if (currentTabIndex == 1)
                {
                    foreach (var g in Selection.gameObjects)
                    {
                        CustomAnimatedElement aniamtedElement = g.GetComponent<CustomAnimatedElement>();
                        if (aniamtedElement)
                        {
                            aniamtedElement.Record(g, CustomAnimatedElement.AnimationShowOrHide.Hide);
                        }
                    }
                }
            }


            EditorGUI.EndDisabledGroup();

            //GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(20);
        }

        private void ShowAnimationDelay_PROPS()
        {
            GUI.color = Color.white;
            animatedElement.delayEnabled = EditorGUILayout.ToggleLeft("Enable Delay", animatedElement.delayEnabled);

            if (animatedElement.delayEnabled)
            {
                EditorGUILayout.PropertyField(_showDelay, new GUIContent("Show Delay"));
            }

            EditorGUILayout.Space(); EditorGUILayout.Space();
        }

        private void HideAnimationDelay_PROPS()
        {
            GUI.color = Color.white;
            animatedElement.delayEnabled = EditorGUILayout.ToggleLeft("Enable Delay", animatedElement.delayEnabled);

            if (animatedElement.delayEnabled)
            {
                EditorGUILayout.PropertyField(_hideDelay, new GUIContent("Hide Delay"));
            }

            EditorGUILayout.Space(); EditorGUILayout.Space();
        }

        private void OnShow_EVENT()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.PropertyField(_onShowEvent, new GUIContent("On Animation Start"));
            GUILayout.Space(10);

            EditorGUILayout.EndVertical();
        }

        private void OnShowComplete_EVENT()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.PropertyField(_onShowCompleteEvent, new GUIContent("On Animation Complete"));
            GUILayout.Space(10);

            EditorGUILayout.EndVertical();
        }

        private void OnHide_EVENT()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.PropertyField(_onHideEvent, new GUIContent("On Animation Start"));

            EditorGUILayout.EndVertical();
        }

        private void OnHideComplete_EVENT()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.PropertyField(_onHideCompleteEvent, new GUIContent("On Animation Complete"));

            EditorGUILayout.EndVertical();
        }

        //===========================================================================================================

        private void SaveInspectorValues()
        {
            foreach (var id in Selection.instanceIDs)
            {
                EditorPrefs.SetInt("airyui/custom/" + nameof(currentTabIndex), currentTabIndex);
            }
        }

        private void GetSavedInspectorValues()
        {
            currentTabIndex = EditorPrefs.GetInt("airyui/custom/" + nameof(currentTabIndex), 0);
        }
    }
}