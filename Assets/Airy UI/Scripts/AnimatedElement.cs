using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Linq;


namespace AiryUI
{
    [AddComponentMenu ( "Airy UI/Animated Element" )]
    [RequireComponent ( typeof ( RectTransform ) )]
    [DisallowMultipleComponent]
    public class AnimatedElement : MonoBehaviour
    {

        public bool isControlledByGroup;
        public AnimatedElementsGroup group;

        public UnityEvent OnShow;
        public UnityEvent OnHide;
        public UnityEvent OnShowComplete;
        public UnityEvent OnHideComplete;

        public bool disableShowAnimation;
        public bool disableHideAnimation;

        public Canvas containerCanvas;

        [Tooltip ( "Show animation on game object activation\nset to false if you want to activate this game object without animation" )]
        public bool animateOnEnable = true;

        public TimeMode showTimeMode;
        public TimeMode hideTimeMode;

        public AnimationType showAnimationType;
        public AnimationType hideAnimationType;
        public AnimationStartPosition animationMoveFrom;
        public AnimationStartPosition animationMoveTo;

        public bool addBounciness;
        [Range ( 0 , 1 )] public float bouncinessPower = 0.1f;
        [Range ( 0 , 1 )] public float bouncinessDuration = 0.1f;

        public bool fadeChildren;

        [Min ( 0.01f )] public float animationShowDuration = 0.5f;
        [Min ( 0.01f )] public float animationHideDuration = 0.5f;

        public bool delayEnabled;
        public float showDelay;
        public float hideDelay;

        public Vector3 rotateFrom;
        public Vector3 rotateTo;

        public Vector3 worldPosition_initial;
        public Vector3 localPosition_initial;
        public Vector3 anchoredPosition_initial;
        public Color [] childrenColors_initial;
        public Vector3 scale_initial;
        public Quaternion rotation_initial;
        public Vector2 pivot_initial;

        protected RectTransform rectTransformComponent;

        private List<Graphic> graphicalComponents;

        protected bool initializeComplete = false;

        protected IEnumerator currentRunningCoroutine;



        private void OnEnable ()
        {
            InitializeValues ();

            if ( isControlledByGroup )
                return;

            if ( animateOnEnable )
            {
                ShowElement ();
            }
        }


        private void OnValidate ()
        {
            if ( isControlledByGroup && group != null )
            {
                if ( !group.animatedElements.Contains ( this ) )
                {
                    group.animatedElements.Add ( this );
                }
            }
            else
            {
                if ( group != null && group.animatedElements.Contains ( this ) )
                {
                    group.animatedElements.Remove ( this );
                }
            }
        }


        private void InitializeValues ()
        {
            if ( initializeComplete )
                return;

            rectTransformComponent = GetComponent<RectTransform> ();

            graphicalComponents = new List<Graphic> ();
            if ( showAnimationType == AnimationType.FadeColor || hideAnimationType == AnimationType.FadeColor )
            {
                if ( fadeChildren )
                {
                    graphicalComponents = GetComponentsInChildren<Graphic> ( true ).ToList ();

                    // remove any component that has animated element, so that it animates by itself.
                    graphicalComponents.RemoveAll ( g => g.GetComponent<AnimatedElement> () != null && g.gameObject != gameObject );
                    graphicalComponents.RemoveAll ( g => g.GetComponent<CustomAnimatedElement> () != null && g.gameObject != gameObject );
                }
                else
                {
                    if ( GetComponent<Graphic> () )
                    {
                        graphicalComponents.Add ( GetComponent<Graphic> () );
                    }
                }
            }

            childrenColors_initial = new Color [ graphicalComponents.Count ];
            for ( int i = 0 ; i < graphicalComponents.Count ; i++ )
            {
                childrenColors_initial [ i ] = graphicalComponents [ i ].color;
            }

            worldPosition_initial = rectTransformComponent.position;
            localPosition_initial = rectTransformComponent.localPosition;
            anchoredPosition_initial = rectTransformComponent.anchoredPosition;

            scale_initial = rectTransformComponent.localScale;
            rotation_initial = rectTransformComponent.rotation;
            pivot_initial = rectTransformComponent.pivot;

            initializeComplete = true;
        }


        public virtual void ShowElement ()
        {
            gameObject.SetActive ( true );


            if ( disableShowAnimation )
            {
                gameObject.SetActive ( true );
                OnShowComplete.Invoke ();

                return;
            }


            currentRunningCoroutine = EmptyCoroutine ();


            switch ( showAnimationType )
            {
                case ( AnimationType.Scale ):

                    if ( currentRunningCoroutine != null )
                        StopCoroutine ( currentRunningCoroutine );

                    currentRunningCoroutine = AnimateScale_SHOW ();
                    StartCoroutine ( currentRunningCoroutine );

                    break;

                case ( AnimationType.Rotate ):

                    if ( currentRunningCoroutine != null )
                        StopCoroutine ( currentRunningCoroutine );

                    currentRunningCoroutine = AnimateRotate_SHOW ();
                    StartCoroutine ( currentRunningCoroutine );

                    break;

                case ( AnimationType.FadeColor ):

                    if ( currentRunningCoroutine != null )
                        StopCoroutine ( currentRunningCoroutine );

                    currentRunningCoroutine = AnimateFadeIn_SHOW ();
                    StartCoroutine ( currentRunningCoroutine );

                    break;

                case ( AnimationType.Move ):

                    if ( currentRunningCoroutine != null )
                        StopCoroutine ( currentRunningCoroutine );

                    currentRunningCoroutine = AnimateMove_SHOW ();
                    StartCoroutine ( currentRunningCoroutine );

                    break;

                case ( AnimationType.MoveWithScale ):

                    if ( currentRunningCoroutine != null )
                        StopCoroutine ( currentRunningCoroutine );

                    currentRunningCoroutine = AnimateMoveWithScale_SHOW ();
                    StartCoroutine ( currentRunningCoroutine );

                    break;
            }


        }


        public virtual void HideElement ()
        {
            if ( !gameObject.activeSelf )
                return;


            if ( disableHideAnimation )
            {
                gameObject.SetActive ( false );
                OnHideComplete.Invoke ();

                return;
            }


            switch ( hideAnimationType )
            {
                case ( AnimationType.Scale ):

                    if ( currentRunningCoroutine != null )
                        StopCoroutine ( currentRunningCoroutine );

                    currentRunningCoroutine = AnimateScale_HIDE ();
                    StartCoroutine ( currentRunningCoroutine );

                    break;

                case ( AnimationType.Rotate ):

                    if ( currentRunningCoroutine != null )
                        StopCoroutine ( currentRunningCoroutine );

                    currentRunningCoroutine = AnimateRotation_HIDE ();
                    StartCoroutine ( currentRunningCoroutine );

                    break;

                case ( AnimationType.FadeColor ):

                    if ( currentRunningCoroutine != null )
                        StopCoroutine ( currentRunningCoroutine );

                    currentRunningCoroutine = AnimateFadeOut_HIDE ();
                    StartCoroutine ( currentRunningCoroutine );

                    break;

                case ( AnimationType.Move ):

                    if ( currentRunningCoroutine != null )
                        StopCoroutine ( currentRunningCoroutine );

                    currentRunningCoroutine = AnimateMove_HIDE ();
                    StartCoroutine ( currentRunningCoroutine );

                    break;

                case ( AnimationType.MoveWithScale ):

                    if ( currentRunningCoroutine != null )
                        StopCoroutine ( currentRunningCoroutine );

                    currentRunningCoroutine = AnimateMoveWithScale_HIDE ();
                    StartCoroutine ( currentRunningCoroutine );

                    break;
            }


        }


        #region Show Coroutines


        private IEnumerator AnimateMoveWithScale_SHOW ()
        {
            InitializeValues ();

            if ( animationShowDuration <= 0 )
                yield break;

            Vector3 startPos = AnimationPositions.GetStartPositionFromCorner ( localPosition_initial , rectTransformComponent , animationMoveFrom );

            ResetPosition ();
            ResetScale ( ResetOptions.Zero );

            #region Initialization Part

            // Here we get start color to a color with alpha = 0, and the end color to a color with alpha = 1.

            Color [] startColors = new Color [ graphicalComponents.Count ];
            Color [] endColors = new Color [ graphicalComponents.Count ];

            for ( int i = 0 ; i < graphicalComponents.Count ; i++ )
            {
                startColors [ i ] = graphicalComponents [ i ].color;
                startColors [ i ].a = 0;

                endColors [ i ] = childrenColors_initial [ i ];
            }

            Vector3 targetPosition = ( addBounciness ) ? localPosition_initial + GetMoveDirection () * bouncinessPower : localPosition_initial;
            Vector3 targetScale = ( addBounciness ) ? scale_initial + ( scale_initial * bouncinessPower ) : scale_initial;

            rectTransformComponent.localPosition = startPos;

            #endregion

            yield return null;

            yield return WaitForSeconds_SHOW ( delayEnabled ? showDelay : 0 );

            OnShow?.Invoke ();

            float elapsedTime = 0;

            while ( elapsedTime <= animationShowDuration )
            {
                float t = elapsedTime / animationShowDuration;

                // Here we Lerp the position and the scale as well.
                rectTransformComponent.localPosition = Vector3.Lerp ( startPos , targetPosition , t );
                rectTransformComponent.localScale = Vector3.Lerp ( Vector3.zero , targetScale , t );

                // Here we lerp the color from transparent to visible.
                for ( int i = 0 ; i < graphicalComponents.Count ; i++ )
                    graphicalComponents [ i ].color = Color.Lerp ( startColors [ i ] , endColors [ i ] , t );

                elapsedTime += DeltaTimeFor_SHOW;
                yield return null;
            }


            if ( addBounciness )
            {
                elapsedTime = 0;
                while ( elapsedTime <= bouncinessDuration )
                {
                    float t = elapsedTime / bouncinessDuration;
                    rectTransformComponent.localPosition = Vector3.Lerp ( targetPosition , localPosition_initial , t );
                    rectTransformComponent.localScale = Vector3.Lerp ( targetScale , scale_initial , t );

                    elapsedTime += DeltaTimeFor_SHOW;
                    yield return null;
                }
            }

            rectTransformComponent.localPosition = localPosition_initial;
            rectTransformComponent.localScale = scale_initial;
            for ( int i = 0 ; i < graphicalComponents.Count ; i++ )
                graphicalComponents [ i ].color = endColors [ i ];

            OnShowComplete?.Invoke ();
        }

        private IEnumerator AnimateMove_SHOW ()
        {
            InitializeValues ();

            if ( animationShowDuration <= 0 )
                yield break;

            Vector3 startPos = AnimationPositions.GetStartPositionFromCorner ( localPosition_initial , rectTransformComponent , animationMoveFrom );
            Vector3 targetPosition = ( addBounciness ) ? localPosition_initial + ( GetMoveDirection () * bouncinessPower ) : localPosition_initial;

            ResetPosition ();
            ResetScale ( ResetOptions.Zero );
            ResetColor ( ResetOptions.Zero );

            yield return null;

            yield return WaitForSeconds_SHOW ( delayEnabled ? showDelay : 0 );

            OnShow?.Invoke ();

            ResetPosition ();
            ResetScale ( ResetOptions.One );
            ResetColor ( ResetOptions.One );

            // Starting animating.
            float elapsedTime = 0;
            rectTransformComponent.localPosition = startPos;

            while ( elapsedTime <= animationShowDuration )
            {
                float t = elapsedTime / animationShowDuration;

                rectTransformComponent.localPosition = Vector3.Lerp ( startPos , targetPosition , t );

                elapsedTime += DeltaTimeFor_SHOW;
                yield return null;
            }

            if ( addBounciness )
            {
                elapsedTime = 0;
                while ( elapsedTime <= bouncinessDuration )
                {
                    float t = elapsedTime / bouncinessDuration;
                    rectTransformComponent.localPosition = Vector3.Lerp ( targetPosition , localPosition_initial , t );

                    elapsedTime += DeltaTimeFor_SHOW;
                    yield return null;
                }
            }

            rectTransformComponent.localPosition = localPosition_initial;

            OnShowComplete?.Invoke ();
        }

        private IEnumerator AnimateScale_SHOW ()
        {
            InitializeValues ();

            if ( animationShowDuration <= 0 )
                yield break;

            ResetPosition ();
            ResetScale ( ResetOptions.Zero );
            ResetColor ( ResetOptions.One );

            yield return null;

            yield return WaitForSeconds_SHOW ( delayEnabled ? showDelay : 0 );

            OnShow?.Invoke ();

            Vector3 targetScale = ( addBounciness ) ? scale_initial + ( scale_initial * bouncinessPower ) : scale_initial;

            float elapsedTime = 0;
            while ( elapsedTime <= animationShowDuration )
            {
                float t = elapsedTime / animationShowDuration;

                rectTransformComponent.localScale = Vector3.Lerp ( Vector3.zero , targetScale , t );

                elapsedTime += DeltaTimeFor_SHOW;
                yield return null;
            }

            if ( addBounciness )
            {
                elapsedTime = 0;
                while ( elapsedTime <= bouncinessDuration )
                {
                    // we should scale up then scale down.

                    float t = elapsedTime / bouncinessDuration;
                    rectTransformComponent.localScale = Vector3.Lerp ( targetScale , scale_initial , t );

                    elapsedTime += DeltaTimeFor_SHOW;
                    yield return null;
                }
            }

            rectTransformComponent.localScale = scale_initial;

            OnShowComplete?.Invoke ();
        }

        private IEnumerator AnimateFadeIn_SHOW ()
        {
            InitializeValues ();

            if ( animationShowDuration <= 0 )
                yield break;

            ResetPosition ();
            ResetScale ( ResetOptions.One );

            #region Initialization Part

            Color [] startColors = new Color [ graphicalComponents.Count ];
            Color [] endColors = new Color [ graphicalComponents.Count ];

            for ( int i = 0 ; i < graphicalComponents.Count ; i++ )
            {
                startColors [ i ] = graphicalComponents [ i ].color;
                startColors [ i ].a = 0;

                endColors [ i ] = childrenColors_initial [ i ];
            }

            ResetColor ( ResetOptions.Zero );

            #endregion

            yield return null;

            yield return WaitForSeconds_SHOW ( delayEnabled ? showDelay : 0 );

            OnShow?.Invoke ();

            if ( fadeChildren )
            {
                float elapsedTime = 0;
                while ( elapsedTime <= animationShowDuration )
                {
                    float t = elapsedTime / animationShowDuration;
                    graphicalComponents [ 0 ].color = Color.Lerp ( startColors [ 0 ] , endColors [ 0 ] , t );
                    for ( int i = 0 ; i < graphicalComponents.Count ; i++ )
                    {
                        graphicalComponents [ i ].color = Color.Lerp ( startColors [ i ] , endColors [ i ] , t );
                    }

                    elapsedTime += DeltaTimeFor_SHOW;
                    yield return null;
                }
                for ( int i = 0 ; i < graphicalComponents.Count ; i++ )
                {
                    graphicalComponents [ i ].color = endColors [ i ];
                }
            }
            else
            {
                float elapsedTime = 0;
                while ( elapsedTime <= animationShowDuration )
                {
                    float t = elapsedTime / animationShowDuration;
                    graphicalComponents [ 0 ].color = Color.Lerp ( startColors [ 0 ] , endColors [ 0 ] , t );

                    elapsedTime += DeltaTimeFor_SHOW;
                    yield return null;
                }

                graphicalComponents [ 0 ].color = endColors [ 0 ];
            }

            OnShowComplete?.Invoke ();
        }

        private IEnumerator AnimateRotate_SHOW ()
        {
            InitializeValues ();

            if ( animationShowDuration <= 0 )
                yield break;

            ResetPosition ();
            ResetScale ( ResetOptions.One );
            ResetColor ( ResetOptions.One );
            ResetRotation ( ResetOptions.One , true );

            yield return null;

            yield return WaitForSeconds_SHOW ( delayEnabled ? showDelay : 0 );

            OnShow?.Invoke ();

            Vector3 targetRotation = ( addBounciness ) ? rotation_initial.eulerAngles - ( rotateFrom * bouncinessPower ) : rotation_initial.eulerAngles;

            float elapsedTime = 0;
            while ( elapsedTime <= animationShowDuration )
            {
                float t = elapsedTime / animationShowDuration;

                rectTransformComponent.eulerAngles = Vector3.Lerp ( rotateFrom , targetRotation , t );

                elapsedTime += DeltaTimeFor_SHOW;
                yield return null;
            }

            if ( addBounciness )
            {
                elapsedTime = 0;
                while ( elapsedTime <= bouncinessDuration )
                {
                    float t = elapsedTime / bouncinessDuration;
                    rectTransformComponent.eulerAngles = Vector3.Lerp ( targetRotation , rotation_initial.eulerAngles , t );

                    elapsedTime += DeltaTimeFor_SHOW;
                    yield return null;
                }
            }

            rectTransformComponent.eulerAngles = rotation_initial.eulerAngles;

            OnShowComplete?.Invoke ();
        }


        #endregion


        //=======================================================================================
        //=======================================================================================


        #region Hide Coroutines


        private IEnumerator AnimateFadeOut_HIDE ()
        {
            yield return null;
            if ( !initializeComplete )
                InitializeValues ();

            if ( animationHideDuration <= 0 )
                yield break;

            ResetPosition ();
            ResetScale ( ResetOptions.One );
            ResetColor ( ResetOptions.One );

            #region Initialization Part

            Color [] startColors = new Color [ graphicalComponents.Count ], endColors = new Color [ graphicalComponents.Count ];

            for ( int i = 0 ; i < graphicalComponents.Count ; i++ )
            {
                startColors [ i ] = childrenColors_initial [ i ];

                endColors [ i ] = childrenColors_initial [ i ];
                endColors [ i ].a = 0;
            }

            #endregion

            yield return WaitForSeconds_HIDE ( delayEnabled ? hideDelay : 0 );

            OnHide?.Invoke ();

            float elapsedTime = 0;

            if ( fadeChildren )
            {
                while ( elapsedTime <= animationHideDuration )
                {
                    float t = elapsedTime / animationHideDuration;
                    for ( int i = 0 ; i < graphicalComponents.Count ; i++ )
                    {
                        graphicalComponents [ i ].color = Color.Lerp ( startColors [ i ] , endColors [ i ] , t );
                    }

                    elapsedTime += DeltaTimeFor_HIDE;
                    yield return null;
                }
                for ( int i = 0 ; i < graphicalComponents.Count ; i++ )
                {
                    graphicalComponents [ i ].color = endColors [ i ];
                }
            }
            else
            {
                while ( elapsedTime <= animationHideDuration )
                {
                    float t = elapsedTime / animationHideDuration;
                    graphicalComponents [ 0 ].color = Color.Lerp ( startColors [ 0 ] , endColors [ 0 ] , t );

                    elapsedTime += DeltaTimeFor_HIDE;
                    yield return null;
                }

                graphicalComponents [ 0 ].color = endColors [ 0 ];
            }

            ResetDefaults ();

            OnHideComplete?.Invoke ();
        }


        private IEnumerator AnimateScale_HIDE ()
        {
            yield return null;
            if ( !initializeComplete )
                InitializeValues ();

            if ( animationHideDuration <= 0 )
                yield break;

            Vector3 startPosition = rectTransformComponent.position;
            rectTransformComponent.localScale = Vector3.one;

            yield return WaitForSeconds_HIDE ( delayEnabled ? hideDelay : 0 );

            OnHide?.Invoke ();

            float elapsedTime = 0;

            while ( elapsedTime <= animationHideDuration )
            {
                float t = elapsedTime / animationHideDuration;

                rectTransformComponent.localScale = Vector3.Lerp ( Vector3.one , Vector3.zero , t );

                elapsedTime += DeltaTimeFor_HIDE;
                yield return null;
            }

            rectTransformComponent.localScale = Vector3.zero;

            ResetDefaults ();

            OnHideComplete?.Invoke ();
        }


        private IEnumerator AnimateMoveWithScale_HIDE ()
        {
            yield return null;
            if ( !initializeComplete )
                InitializeValues ();

            if ( animationHideDuration <= 0 )
                yield break;

            Graphic [] images;

            if ( fadeChildren )
                images = GetComponentsInChildren<Graphic> ();
            else
                images = GetComponents<Graphic> ();

            Color [] startColors = new Color [ images.Length ];
            Color [] endColors = new Color [ images.Length ];

            for ( int i = 0 ; i < images.Length ; i++ )
            {
                startColors [ i ] = childrenColors_initial [ i ];

                endColors [ i ] = images [ i ].color;
                endColors [ i ].a = 0;
            }


            Vector3 startPos = localPosition_initial;
            Vector3 targetPosition = AnimationPositions.GetStartPositionFromCorner ( localPosition_initial , rectTransformComponent , animationMoveTo );
            rectTransformComponent.localPosition = startPos;

            yield return WaitForSeconds_HIDE ( delayEnabled ? hideDelay : 0 );

            OnHide?.Invoke ();

            float elapsedTime = 0;

            while ( elapsedTime <= animationHideDuration )
            {
                float t = elapsedTime / animationHideDuration;
                rectTransformComponent.localPosition = Vector3.Lerp ( startPos , targetPosition , t );
                rectTransformComponent.localScale = Vector3.Lerp ( Vector3.one , Vector3.zero , t );

                for ( int i = 0 ; i < images.Length ; i++ )
                {
                    images [ i ].color = Color.Lerp ( startColors [ i ] , endColors [ i ] , t );
                }

                elapsedTime += DeltaTimeFor_HIDE;
                yield return null;
            }

            rectTransformComponent.localPosition = targetPosition;
            rectTransformComponent.localScale = Vector3.zero;

            for ( int i = 0 ; i < images.Length ; i++ )
            {
                images [ i ].color = endColors [ i ];
            }

            ResetDefaults ();

            OnHideComplete?.Invoke ();
        }


        private IEnumerator AnimateMove_HIDE ()
        {
            yield return null;
            if ( !initializeComplete )
                InitializeValues ();

            if ( animationHideDuration <= 0 )
                yield break;

            Vector3 startPos = localPosition_initial;
            Vector3 targetPosition = AnimationPositions.GetStartPositionFromCorner ( localPosition_initial , rectTransformComponent , animationMoveTo );
            rectTransformComponent.localPosition = startPos;

            yield return WaitForSeconds_HIDE ( delayEnabled ? hideDelay : 0 );

            OnHide?.Invoke ();

            float elapsedTime = 0;
            while ( elapsedTime <= animationHideDuration )
            {
                float t = elapsedTime / animationHideDuration;
                rectTransformComponent.localPosition = Vector3.Lerp ( startPos , targetPosition , t );

                elapsedTime += DeltaTimeFor_HIDE;
                yield return null;
            }

            rectTransformComponent.localPosition = targetPosition;

            ResetDefaults ();

            OnHideComplete?.Invoke ();
        }


        private IEnumerator AnimateRotation_HIDE ()
        {
            yield return null;
            if ( !initializeComplete )
                InitializeValues ();

            if ( animationHideDuration <= 0 )
                yield break;

            ResetPosition ();
            ResetScale ( ResetOptions.One );
            ResetColor ( ResetOptions.One );
            ResetRotation ( ResetOptions.Zero , true );

            yield return WaitForSeconds_HIDE ( delayEnabled ? hideDelay : 0 );

            OnHide?.Invoke ();

            float elapsedTime = 0;
            while ( elapsedTime <= animationHideDuration )
            {
                float t = elapsedTime / animationHideDuration;

                rectTransformComponent.rotation = Quaternion.Euler ( Vector3.Lerp ( Quaternion.ToEulerAngles ( rotation_initial ) , rotateTo , t ) );

                elapsedTime += DeltaTimeFor_HIDE;
                yield return null;
            }

            ResetRotation ( ResetOptions.One , false );

            ResetDefaults ();

            OnHideComplete?.Invoke ();
        }


        #endregion


        private void ResetDefaults ()
        {
            ResetPosition ();
            ResetScale ( ResetOptions.One );
            ResetColor ( ResetOptions.One );
            ResetRotation ( ResetOptions.Zero , true );

            gameObject.SetActive ( false );
        }

        private void ResetPosition ()
        {
            rectTransformComponent.anchoredPosition = anchoredPosition_initial;
        }

        private void ResetColor ( ResetOptions resetOption )
        {
            if ( resetOption == ResetOptions.Zero )
            {
                for ( int i = 0 ; i < graphicalComponents.Count ; i++ )
                {
                    graphicalComponents [ i ].color = new Color ( childrenColors_initial [ i ].r , childrenColors_initial [ i ].g , childrenColors_initial [ i ].b , 0 );
                }

                //if ( fadeChildren )
                //{
                //    for ( int i = 0 ; i < graphicalComponents.Count ; i++ )
                //    {
                //        graphicalComponents [ i ].color = new Color ( childrenColors_initial [ i ].r , childrenColors_initial [ i ].g , childrenColors_initial [ i ].b , 0 );
                //    }
                //}
                //else
                //{
                //    // because the index 0 is for the componet on this game object.
                //    graphicalComponents [ 0 ].color = new Color ( childrenColors_initial [ 0 ].r , childrenColors_initial [ 0 ].g , childrenColors_initial [ 0 ].b , 0 );
                //}
            }
            else if ( resetOption == ResetOptions.One )
            {
                for ( int i = 0 ; i < graphicalComponents.Count ; i++ )
                {
                    graphicalComponents [ i ].color = childrenColors_initial [ i ];
                }

                //if ( fadeChildren )
                //{
                //    for ( int i = 0 ; i < graphicalComponents.Count ; i++ )
                //    {
                //        graphicalComponents [ i ].color = childrenColors_initial [ i ];
                //    }
                //}
                //else
                //{
                //    graphicalComponents [ 0 ].color = childrenColors_initial [ 0 ];
                //}
            }
        }

        private void ResetScale ( ResetOptions resetOption )
        {
            if ( resetOption == ResetOptions.Zero )
            {
                rectTransformComponent.localScale = Vector3.zero;
            }
            else if ( resetOption == ResetOptions.One )
            {
                rectTransformComponent.localScale = scale_initial;
            }
        }

        private void ResetRotation ( ResetOptions resetOption , bool show )
        {
            if ( resetOption == ResetOptions.Zero )
            {
                rectTransformComponent.rotation = rotation_initial;
            }
            else if ( resetOption == ResetOptions.One && show )
            {
                rectTransformComponent.rotation = Quaternion.Euler ( rotateFrom );
            }
            else if ( resetOption == ResetOptions.One && !show )
            {
                rectTransformComponent.rotation = Quaternion.Euler ( rotateTo );
            }
        }

        //--------------------------------------------------------------------------------------------------------


        private Vector3 GetMoveDirection ()
        {
            float x = 0;
            float y = 0;

            switch ( animationMoveFrom )
            {
                case ( AnimationStartPosition.Up ):

                    x = 0;
                    y = -Screen.height;

                    break;

                case ( AnimationStartPosition.Bottom ):

                    x = 0;
                    y = Screen.height;

                    break;
                case ( AnimationStartPosition.Right ):

                    x = -Screen.width;
                    y = 0;

                    break;

                case ( AnimationStartPosition.Left ):

                    x = Screen.width;
                    y = 0;

                    break;

                case ( AnimationStartPosition.UpLeft ):

                    x = Screen.width;
                    y = -Screen.height;

                    break;

                case ( AnimationStartPosition.UpRight ):

                    x = -Screen.width;
                    y = -Screen.height;

                    break;

                case ( AnimationStartPosition.BottomLeft ):

                    x = Screen.width;
                    y = Screen.height;

                    break;

                case ( AnimationStartPosition.BottomRight ):

                    x = -Screen.width;
                    y = Screen.height;

                    break;

                case ( AnimationStartPosition.Center ):

                    x = 0;
                    y = -Screen.height;

                    break;
            }

            return ( new Vector3 ( x , y ) );
        }

        protected float DeltaTimeFor_SHOW
        {
            get
            {
                float time = 0;

                switch ( showTimeMode )
                {
                    case ( TimeMode.TimeScaleDependent ):
                        time = Time.deltaTime;

                        break;

                    case ( TimeMode.NotTimeScaleDependent ):
                        time = Time.unscaledDeltaTime;

                        break;
                }

                return ( time );
            }
        }

        protected float DeltaTimeFor_HIDE
        {
            get
            {
                float time = 0;

                switch ( hideTimeMode )
                {
                    case ( TimeMode.TimeScaleDependent ):
                        time = Time.deltaTime;

                        break;

                    case ( TimeMode.NotTimeScaleDependent ):
                        time = Time.unscaledDeltaTime;

                        break;
                }

                return ( time );
            }
        }


        protected IEnumerator WaitForSeconds_SHOW ( float time )
        {
            float elapsedTime = 0;
            switch ( showTimeMode )
            {
                case ( TimeMode.TimeScaleDependent ):

                    yield return new WaitForSeconds ( time );

                    break;

                case ( TimeMode.NotTimeScaleDependent ):

                    elapsedTime = 0;
                    while ( elapsedTime <= time )
                    {
                        elapsedTime += DeltaTimeFor_SHOW;
                        yield return null;
                    }

                    break;
            }
        }


        protected IEnumerator WaitForSeconds_HIDE ( float time )
        {
            switch ( hideTimeMode )
            {
                case ( TimeMode.TimeScaleDependent ):
                    yield return new WaitForSeconds ( time );

                    break;

                case ( TimeMode.NotTimeScaleDependent ):
                    yield return new WaitForSecondsRealtime ( time );

                    break;
            }
        }

        protected IEnumerator EmptyCoroutine ()
        {
            yield break;
        }


        //--------------------------------------------------------------------------------------------------------


        private enum ResetOptions
        {
            Zero = 0, One = 1
        }
    }

    public enum AnimationType
    {
        Scale = 0, Rotate = 1, FadeColor = 2, Move = 3, MoveWithScale = 4
    }

    public enum AnimationStartPosition
    {
        Up = 0, UpCenter = 1, UpLeft = 2, UpRight = 3,
        Left = 4, LeftCenter = 5,
        Center = 6,
        Bottom = 7, BottomCenter = 8, BottomLeft = 9, BottomRight = 10,
        Right = 11, RightCenter = 12,
    }

    public enum TimeMode
    {
        TimeScaleDependent = 0, NotTimeScaleDependent = 1
    }
}