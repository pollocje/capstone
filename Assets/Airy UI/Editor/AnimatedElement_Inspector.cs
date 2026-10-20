using UnityEngine;
using UnityEditor;

namespace AiryUI
{
    [CustomEditor(typeof(AnimatedElement))]
    [CanEditMultipleObjects]
    public class AnimatedElement_Inspector : Editor
    {


        private AnimatedElement animatedElement;


        //===================


        private SerializedProperty _isControlledByGroup;
        private SerializedProperty _group;


        //===================


        private SerializedProperty _showTimeMode;
        private SerializedProperty _hideTimeMode;


        //===================


        private SerializedProperty _disableShowAnimation;
        private SerializedProperty _disableHideAnimation;
        private SerializedProperty _containerCanvas;
        private SerializedProperty _animateOnEnable;
        private SerializedProperty _animationShowDuration;


        //===================


        private SerializedProperty _addBounciness;
        private SerializedProperty _bouncinessPower;
        private SerializedProperty _bouncinessDuration;


        //===================


        private SerializedProperty _animationHideDuration;
        private SerializedProperty _rotateFrom;
        private SerializedProperty _rotateTo;
        private SerializedProperty _showAnimationType;
        private SerializedProperty _hideAnimationType;
        private SerializedProperty _fadeChildren;
        private SerializedProperty _animationMoveFrom;
        private SerializedProperty _animationMoveTo;
        private SerializedProperty _delayEnabled;
        private SerializedProperty _showDelay;
        private SerializedProperty _hideDelay;
        private SerializedProperty _onShowEvent;
        private SerializedProperty _onHideEvent;
        private SerializedProperty _onShowCompleteEvent;
        private SerializedProperty _onHideCompleteEvent;


        //===================


        private int currentTabIndex = 0;


        //===================


        private bool basicFoldout_SHOW;
        private bool animationFoldout_SHOW;
        private bool delayFoldout_SHOW;
        private bool bouncinessFoldout_SHOW;
        private bool eventsFoldout_SHOW;


        //===================


        private bool basicFoldout_HIDE;
        private bool animationFoldout_HIDE;
        private bool delayFoldout_HIDE;
        private bool eventsFoldout_HIDE;


        //===================


        private Color showPropsColor = new Color(0.52f, 1, 0.8f, 1);
        private Color hidePropsColor = new Color(1, 0.66f, 0.66f, 1);


        //===================


        private void OnEnable()
        {
            GetSavedInspectorValues();

            animatedElement = (AnimatedElement)target;

            _isControlledByGroup = serializedObject.FindProperty("isControlledByGroup");
            _group = serializedObject.FindProperty("group");

            _disableShowAnimation = serializedObject.FindProperty("disableShowAnimation");
            _disableHideAnimation = serializedObject.FindProperty("disableHideAnimation");

            _containerCanvas = serializedObject.FindProperty("containerCanvas");

            _showTimeMode = serializedObject.FindProperty("showTimeMode");
            _hideTimeMode = serializedObject.FindProperty("hideTimeMode");

            _addBounciness = serializedObject.FindProperty("addBounciness");
            _bouncinessPower = serializedObject.FindProperty("bouncinessPower");
            _bouncinessDuration = serializedObject.FindProperty("bouncinessDuration");

            _animateOnEnable = serializedObject.FindProperty("animateOnEnable");

            _animationShowDuration = serializedObject.FindProperty("animationShowDuration");
            _animationHideDuration = serializedObject.FindProperty("animationHideDuration");

            _rotateFrom = serializedObject.FindProperty("rotateFrom");
            _rotateTo = serializedObject.FindProperty("rotateTo");

            _showAnimationType = serializedObject.FindProperty("showAnimationType");
            _hideAnimationType = serializedObject.FindProperty("hideAnimationType");

            _fadeChildren = serializedObject.FindProperty("fadeChildren");

            _animationMoveFrom = serializedObject.FindProperty("animationMoveFrom");
            _animationMoveTo = serializedObject.FindProperty("animationMoveTo");

            _delayEnabled = serializedObject.FindProperty("delayEnabled");
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
                EditorGUILayout.HelpBox("You can't add 'Animated Element' on a game object that has 'Animated Elements Group' !", MessageType.Error);

                animatedElement.enabled = false;
                animatedElement.disableShowAnimation = true;
                animatedElement.disableHideAnimation = true;

                // Don't draw anything.
                return;
            }


            Undo.RecordObject(animatedElement, "animated element");
            GeneralSettings();

            DrawLinkedGroup();

            DrawTabs();

            if (currentTabIndex == 0)
            {
                if (!animatedElement.disableShowAnimation)
                {
                    GUI.color = showPropsColor;
                    basicFoldout_SHOW = EditorGUILayout.BeginFoldoutHeaderGroup(basicFoldout_SHOW, "Basic");
                    if (basicFoldout_SHOW)
                    {
                        GUI.color = Color.white;
                        DrawAnimateOnEnable_TOGGLE();
                        DrawAnimationShowTimeMode_DROPDOWN();
                    }

                    EditorGUILayout.EndFoldoutHeaderGroup();
                    GUILayout.Space(20);

                    //===================

                    GUI.color = showPropsColor;
                    animationFoldout_SHOW = EditorGUILayout.BeginFoldoutHeaderGroup(animationFoldout_SHOW, "Animation");
                    if (animationFoldout_SHOW)
                    {
                        GUI.color = Color.white;
                        DrawShowAnimationType_GRID();
                        DrawAnimationShowDuration_INPUT();
                        DrawShowAnimationRotation_PROPERTIES();
                        DrawFadeChildrenShow_TOGGLE();
                    }

                    EditorGUILayout.EndFoldoutHeaderGroup();
                    GUILayout.Space(20);

                    //===================

                    GUI.color = showPropsColor;
                    delayFoldout_SHOW = EditorGUILayout.BeginFoldoutHeaderGroup(delayFoldout_SHOW, "Delay");
                    if (delayFoldout_SHOW)
                    {
                        GUI.color = Color.white;
                        DrawShowAnimationDelay_PROPERTIES();
                        DrawChildrenShowDelays_BUTTONS();
                    }

                    EditorGUILayout.EndFoldoutHeaderGroup();
                    GUILayout.Space(20);

                    //===================

                    GUI.color = showPropsColor;
                    bouncinessFoldout_SHOW = EditorGUILayout.BeginFoldoutHeaderGroup(bouncinessFoldout_SHOW, "Bounciness");
                    if (bouncinessFoldout_SHOW)
                    {
                        GUI.color = Color.white;
                        DrawAnimationShowBounciness();
                    }

                    EditorGUILayout.EndFoldoutHeaderGroup();
                    GUILayout.Space(20);

                    //===================

                    GUI.color = showPropsColor;
                    eventsFoldout_SHOW = EditorGUILayout.BeginFoldoutHeaderGroup(eventsFoldout_SHOW, "Events");
                    if (eventsFoldout_SHOW)
                    {
                        GUI.color = Color.white;
                        DrawOnShow_EVENT();
                        DrawOnShowComplete_EVENT();
                    }

                    EditorGUILayout.EndFoldoutHeaderGroup();
                    GUILayout.Space(20);

                    //===================
                }
                else
                {
                    GUI.color = Color.white;
                    DrawOnShowComplete_EVENT();
                }

                EditorGUILayout.EndFoldoutHeaderGroup();
                GUILayout.Space(20);
            }
            else if (currentTabIndex == 1)
            {
                if (!animatedElement.disableHideAnimation)
                {
                    GUI.color = hidePropsColor;
                    basicFoldout_HIDE = EditorGUILayout.BeginFoldoutHeaderGroup(basicFoldout_HIDE, "Basic");
                    if (basicFoldout_HIDE)
                    {
                        GUI.color = Color.white;
                        DrawAnimationHideTimeMode_DROPDOWN();
                    }

                    EditorGUILayout.EndFoldoutHeaderGroup();
                    GUILayout.Space(20);

                    //===================

                    GUI.color = hidePropsColor;
                    animationFoldout_HIDE = EditorGUILayout.BeginFoldoutHeaderGroup(animationFoldout_HIDE, "Animation");
                    if (animationFoldout_HIDE)
                    {
                        GUI.color = Color.white;
                        DrawHideAnimationType_GRID();
                        DrawAnimationHideDuration_INPUT();
                        DrawHideAnimationRotaion_PROPERTIES();
                        DrawFadeChildrenHide_TOGGLE();
                    }

                    EditorGUILayout.EndFoldoutHeaderGroup();
                    GUILayout.Space(20);

                    //===================

                    GUI.color = hidePropsColor;
                    delayFoldout_HIDE = EditorGUILayout.BeginFoldoutHeaderGroup(delayFoldout_HIDE, "Delay");
                    if (delayFoldout_HIDE)
                    {
                        GUI.color = Color.white;
                        DrawHideAnimationDelay_PROPERTIES();
                        DrawChildrenHideDelays_BUTTONS();
                    }

                    EditorGUILayout.EndFoldoutHeaderGroup();
                    GUILayout.Space(20);

                    //===================

                    GUI.color = hidePropsColor;
                    eventsFoldout_HIDE = EditorGUILayout.BeginFoldoutHeaderGroup(eventsFoldout_HIDE, "Events");
                    if (eventsFoldout_HIDE)
                    {
                        GUI.color = Color.white;
                        DrawOnHide_EVENT();
                        DrawOnHideComplete_EVENT();
                    }

                    EditorGUILayout.EndFoldoutHeaderGroup();
                    GUILayout.Space(20);

                    //===================
                }
                else
                {
                    GUI.color = Color.white;
                    DrawOnHideComplete_EVENT();
                }
            }

            serializedObject.ApplyModifiedProperties();
            SaveInspectorValues();
        }


        private void GeneralSettings()
        {
            //DrawInspectorTitle_LABEL ( "Animated UI Element" , true , false );

            GUILayout.Space(10);

            GUI.color = showPropsColor;
            EditorGUILayoutExtensions.ToggleLeft(_disableShowAnimation, new GUIContent("Disable Show Animation"));

            GUI.color = hidePropsColor;
            EditorGUILayoutExtensions.ToggleLeft(_disableHideAnimation, new GUIContent("Disable Hide Animation"));

            GUILayout.Space(10);

            GUI.color = Color.white;
            DrawContainerCanvas_FIELD();

            GUILayout.Space(10);
        }


        private void DrawLinkedGroup()
        {
            EditorGUILayoutExtensions.ToggleLeft(_isControlledByGroup, new GUIContent("Is Controlled By Group?"));
            if (animatedElement.isControlledByGroup)
            {
                EditorGUILayout.PropertyField(_group);
            }
        }


        private void DrawTabs()
        {
            GUILayout.Space(20);

            currentTabIndex = GUILayout.Toolbar(currentTabIndex, new string[] { "Show Animation", "Hide Animation" });

            GUILayout.Space(20);
        }


        private void DrawContainerCanvas_FIELD()
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

            if (animatedElement.containerCanvas == null)
            {
                EditorGUILayout.HelpBox("Please assign the container canvas in order to get the most accurate results and to avoid errors !", MessageType.Warning);
            }

            GUILayout.Space(10);
        }


        private void DrawInspectorTitle_LABEL(string text, bool spaceBefore, bool spaceAfter)
        {
            if (spaceBefore)
                GUILayout.Space(20);

            var titleLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 20, fontStyle = FontStyle.Bold, fixedHeight = 50 };

            EditorGUILayout.LabelField(text, titleLabelStyle);

            if (spaceAfter)
                GUILayout.Space(20);
        }


        private void DrawAnimationShowTimeMode_DROPDOWN()
        {
            EditorGUILayout.PropertyField(_showTimeMode, new GUIContent("Time Mode"));

            GUILayout.Space(10);
        }


        private void DrawAnimationHideTimeMode_DROPDOWN()
        {
            EditorGUILayout.PropertyField(_hideTimeMode, new GUIContent("Time Mode"));

            GUILayout.Space(10);
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


        private void DrawShowAnimationType_GRID()
        {
            EditorGUILayout.PropertyField(_showAnimationType, new GUIContent("Animation"));

            if (animatedElement.showAnimationType == AnimationType.FadeColor)
            {
                EditorGUILayout.HelpBox("Note: This GameObject must have a graphical component (Image, Text,  or RawImage) in order to fade its color !", MessageType.Info);

                GUILayout.Space(10);
            }

            if (animatedElement.showAnimationType == AnimationType.MoveWithScale || animatedElement.showAnimationType == AnimationType.Move)
            {
                GUILayout.Space(5);
                EditorGUILayout.PropertyField(_animationMoveFrom, new GUIContent("Start From"));

                GUILayout.Space(5);
            }
        }


        private void DrawHideAnimationType_GRID()
        {
            EditorGUILayout.PropertyField(_hideAnimationType, new GUIContent("Animation"));

            if (animatedElement.hideAnimationType == AnimationType.FadeColor)
            {
                EditorGUILayout.HelpBox("Note: This GameObject must have a graphical component (Image, Text,  or RawImage) in order to fade its color !", MessageType.Info);

                GUILayout.Space(10);
            }

            if (animatedElement.hideAnimationType == AnimationType.MoveWithScale || animatedElement.hideAnimationType == AnimationType.Move)
            {
                GUILayout.Space(5);
                EditorGUILayout.PropertyField(_animationMoveTo, new GUIContent("End To"));

                GUILayout.Space(5);
            }
        }


        private void DrawFadeChildrenShow_TOGGLE()
        {
            if (animatedElement.showAnimationType == AnimationType.FadeColor)
                // animatedElement.fadeChildren = EditorGUILayout.ToggleLeft("Fade Children Simultaneously", animatedElement.fadeChildren);
                EditorGUILayoutExtensions.ToggleLeft(_fadeChildren, new GUIContent("Fade Children Simultaneously"));
        }


        private void DrawFadeChildrenHide_TOGGLE()
        {
            if (animatedElement.hideAnimationType == AnimationType.FadeColor)
                // animatedElement.fadeChildren = EditorGUILayout.ToggleLeft("Fade Children Simultaneously", animatedElement.fadeChildren);
                EditorGUILayoutExtensions.ToggleLeft(_fadeChildren, new GUIContent("Fade Children Simultaneously"));
        }


        private void DrawAnimationShowDuration_INPUT()
        {
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(_animationShowDuration, new GUIContent("Show Duration"));

            EditorGUILayout.Space(); EditorGUILayout.Space();
        }


        private void DrawAnimationShowBounciness()
        {

            if (animatedElement.showAnimationType == AnimationType.FadeColor)
            {
                EditorGUILayout.HelpBox("No Bounciness in color fading!", MessageType.None);
                return;
            }

            // EditorGUILayout.PropertyField(_addBounciness, new GUIContent("Add Bounciness"));
            // animatedElement.addBounciness = EditorGUILayout.ToggleLeft("Add Bounciness", animatedElement.addBounciness,);
            EditorGUILayoutExtensions.ToggleLeft(_addBounciness, new GUIContent("Add Bounciness"));

            if (animatedElement.addBounciness)
            {
                EditorGUILayout.PropertyField(_bouncinessPower, new GUIContent("Bounciness Power"));
                EditorGUILayout.PropertyField(_bouncinessDuration, new GUIContent("Bounciness Duration"));

                if (animatedElement.showAnimationType == AnimationType.FadeColor)
                    EditorGUILayout.HelpBox("Bounciness will not affect animation when type is fading color", MessageType.Warning);
            }

            GUILayout.Space(20);

        }


        private void DrawAnimationHideDuration_INPUT()
        {
            GUILayout.Space(10);

            EditorGUILayout.PropertyField(_animationHideDuration, new GUIContent("Hide Duration"));

            GUILayout.Space(10);
        }


        private void DrawShowAnimationDelay_PROPERTIES()
        {
            EditorGUILayoutExtensions.ToggleLeft(_delayEnabled, new GUIContent("Enable Delay"));

            if (animatedElement.delayEnabled)
            {
                EditorGUILayout.PropertyField(_showDelay, new GUIContent("Delay"));
            }

            EditorGUILayout.Space(); EditorGUILayout.Space();
        }


        private void DrawShowAnimationRotation_PROPERTIES()
        {
            if (animatedElement.showAnimationType == AnimationType.Rotate)
            {
                // rotation from angle
                EditorGUILayout.PropertyField(_rotateFrom, new GUIContent("Rotate From"), true);
                GUILayout.Space(20);
            }
        }


        private void DrawHideAnimationRotaion_PROPERTIES()
        {
            if (animatedElement.hideAnimationType == AnimationType.Rotate)
            {
                EditorGUILayout.PropertyField(_rotateTo, new GUIContent("Rotate To"), true);
                GUILayout.Space(10);

                #region Future Update


                //var titleLabelStyle = new GUIStyle ( GUI.skin.label ) { alignment = TextAnchor.MiddleCenter , fontSize = 13 , fontStyle = FontStyle.Normal };

                //EditorGUILayout.LabelField ( "Rotation Pivot" , titleLabelStyle );

                //rotationPivotGridIndex = GUILayout.SelectionGrid ( rotationPivotGridIndex , new string [] { "↖" , "▲" , "↗" , "◄" , "●" , "►" , "↙" , "▼" , "↘" } , 3 );

                //AnimatedElement.pivotWhileRotating = ( AnimationStartPosition ) rotationPivotGridIndex;


                #endregion

                GUILayout.Space(10);
            }
        }


        private void DrawHideAnimationDelay_PROPERTIES()
        {
            EditorGUILayoutExtensions.ToggleLeft(_delayEnabled, new GUIContent("Enable Delay"));

            if (animatedElement.delayEnabled)
            {
                EditorGUILayout.PropertyField(_hideDelay, new GUIContent("Delay"));
            }

            EditorGUILayout.Space(); EditorGUILayout.Space();
        }


        private void DrawOnShow_EVENT()
        {
            EditorGUILayout.PropertyField(_onShowEvent, new GUIContent("On Animation Start"));
            EditorGUILayout.Space(); EditorGUILayout.Space();
        }


        private void DrawOnShowComplete_EVENT()
        {
            EditorGUILayout.PropertyField(_onShowCompleteEvent, new GUIContent("On Animation Complete"));
            EditorGUILayout.Space(); EditorGUILayout.Space();
        }


        private void DrawOnHide_EVENT()
        {
            EditorGUILayout.PropertyField(_onHideEvent, new GUIContent("On Animation Start"));
        }


        private void DrawOnHideComplete_EVENT()
        {
            EditorGUILayout.PropertyField(_onHideCompleteEvent, new GUIContent("On Animation Complete"));
        }


        private void DrawChildrenShowDelays_BUTTONS()
        {
            GUILayout.Space(20);

            EditorGUILayout.BeginHorizontal();

            float addedTime = animatedElement.animationShowDuration;
            addedTime += (animatedElement.delayEnabled) ? animatedElement.showDelay : 0;

            if (GUILayout.Button("<Auto Set>\nShow Delays In Children"))
            {
                AnimatedElement[] elementsInChildren = Selection.activeGameObject.GetComponentsInChildren<AnimatedElement>();

                float step = (animatedElement.animationShowDuration / elementsInChildren.Length);
                float currentValue = 0;

                for (int i = 1; i < elementsInChildren.Length; i++)
                {
                    if (elementsInChildren[i].delayEnabled)
                        elementsInChildren[i].showDelay = addedTime + currentValue;

                    currentValue += step;
                }
                Debug.Log("<color=orange><b>[Airy UI]</color></b>" +
                "<color=green><b>✓</color></b>" +
                " anchors set for "
                + Selection.gameObjects.Length +
                " game object/s");
                Debug.Log("<color=orange><b>[Airy UI]</color></b> <color=green><b>✓</color></b> children hide delays set for " + elementsInChildren.Length + " game objects");
            }

            //==============================================

            if (GUILayout.Button("<Randomize>\nShow Delays In Children"))
            {
                AnimatedElement[] elementsInChildren = Selection.activeGameObject.GetComponentsInChildren<AnimatedElement>();

                for (int i = 1; i < elementsInChildren.Length; i++)
                {
                    if (elementsInChildren[i].delayEnabled)
                    {
                        elementsInChildren[i].showDelay = addedTime + UnityEngine.Random.Range(animatedElement.animationShowDuration / elementsInChildren.Length, animatedElement.animationShowDuration);
                    }
                }

                Debug.Log("<color=orange><b>[Airy UI]</color></b> <color=green><b>✓</color></b> children hide delays randomized for " + elementsInChildren.Length + " game objects");
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(20);
        }


        private void DrawChildrenHideDelays_BUTTONS()
        {
            EditorGUILayout.BeginHorizontal();

            float addedTime = animatedElement.animationHideDuration;
            addedTime += (animatedElement.delayEnabled) ? animatedElement.hideDelay : 0;

            if (GUILayout.Button("<Auto Set>\nHide Delays In Children"))
            {
                AnimatedElement[] elementsInChildren = Selection.activeGameObject.GetComponentsInChildren<AnimatedElement>();

                float step = (animatedElement.animationHideDuration / elementsInChildren.Length);
                float currentValue = 0;

                elementsInChildren[elementsInChildren.Length - 1].hideDelay = 0;
                for (int i = elementsInChildren.Length - 2; i >= 1; i--)
                {
                    if (elementsInChildren[i].delayEnabled)
                        elementsInChildren[i].hideDelay = step + currentValue;

                    currentValue += step;
                }

                Debug.Log("<color=orange><b>[Airy UI]</color></b> <color=green><b>✓</color></b> children hide delays set for" + elementsInChildren.Length + " game objects");
            }

            //==============================================

            if (GUILayout.Button("<Randomize>\nHide Delays In Children"))
            {
                AnimatedElement[] elementsInChildren = Selection.activeGameObject.GetComponentsInChildren<AnimatedElement>();

                addedTime = animatedElement.hideDelay;

                for (int i = 1; i < elementsInChildren.Length; i++)
                {
                    if (elementsInChildren[i].delayEnabled)
                    {
                        elementsInChildren[i].hideDelay = addedTime - UnityEngine.Random.Range(animatedElement.animationHideDuration / elementsInChildren.Length, animatedElement.animationHideDuration);
                    }
                }

                Debug.Log("<color=orange><b>[Airy UI]</color></b> <color=green><b>✓</color></b> children hide delays randomized for " + elementsInChildren.Length + " game objects");
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(20);
        }


        //===========================================================================================================


        private void SaveInspectorValues()
        {
            foreach (var id in Selection.instanceIDs)
            {
                EditorPrefs.SetInt("airyui/" + nameof(currentTabIndex), currentTabIndex);

                EditorPrefs.SetBool("airyui/" + nameof(basicFoldout_SHOW), basicFoldout_SHOW);
                EditorPrefs.SetBool("airyui/" + nameof(animationFoldout_SHOW), animationFoldout_SHOW);
                EditorPrefs.SetBool("airyui/" + nameof(delayFoldout_SHOW), delayFoldout_SHOW);
                EditorPrefs.SetBool("airyui/" + nameof(bouncinessFoldout_SHOW), bouncinessFoldout_SHOW);
                EditorPrefs.SetBool("airyui/" + nameof(eventsFoldout_SHOW), eventsFoldout_SHOW);

                EditorPrefs.SetBool("airyui/" + nameof(basicFoldout_HIDE), basicFoldout_HIDE);
                EditorPrefs.SetBool("airyui/" + nameof(animationFoldout_HIDE), animationFoldout_HIDE);
                EditorPrefs.SetBool("airyui/" + nameof(delayFoldout_HIDE), delayFoldout_HIDE);
                EditorPrefs.SetBool("airyui/" + nameof(eventsFoldout_HIDE), eventsFoldout_HIDE);
            }
        }


        private void GetSavedInspectorValues()
        {
            currentTabIndex = EditorPrefs.GetInt("airyui/" + nameof(currentTabIndex), 0);

            basicFoldout_SHOW = EditorPrefs.GetBool("airyui/" + nameof(basicFoldout_SHOW));
            animationFoldout_SHOW = EditorPrefs.GetBool("airyui/" + nameof(animationFoldout_SHOW));
            delayFoldout_SHOW = EditorPrefs.GetBool("airyui/" + nameof(delayFoldout_SHOW));
            bouncinessFoldout_SHOW = EditorPrefs.GetBool("airyui/" + nameof(bouncinessFoldout_SHOW));
            eventsFoldout_SHOW = EditorPrefs.GetBool("airyui/" + nameof(eventsFoldout_SHOW));

            basicFoldout_HIDE = EditorPrefs.GetBool("airyui/" + nameof(basicFoldout_HIDE));
            animationFoldout_HIDE = EditorPrefs.GetBool("airyui/" + nameof(animationFoldout_HIDE));
            delayFoldout_HIDE = EditorPrefs.GetBool("airyui/" + nameof(delayFoldout_HIDE));
            eventsFoldout_HIDE = EditorPrefs.GetBool("airyui/" + nameof(eventsFoldout_HIDE));
        }
    }
}