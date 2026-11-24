using UnityEditor;
using UnityEngine;
using UnityEngine.UI;


namespace AiryUI
{


    [RequireComponent ( typeof ( Image ) )]
    [RequireComponent ( typeof ( Button ) )]
    [RequireComponent ( typeof ( AnimatedElement ) )]
    [AddComponentMenu ( "Airy UI/UI Close Button" )]
    public class UICloseButton : CloseButton
    {


        public Button buttonComponent;


        private AnimatedElement myAnimatedElement;


        private void Awake ()
        {
            buttonComponent = buttonComponent ? GetComponent<Button> () : buttonComponent;
            myAnimatedElement = GetComponent<AnimatedElement> ();


            buttonComponent.onClick.AddListener ( DoBack );


            if ( backButtonAffects == BackButtonAffects.AnimatedElement && affectedAnimatedElement != null )
            {
                affectedAnimatedElement.OnShow.AddListener ( myAnimatedElement.ShowElement );
            }
            else if ( backButtonAffects == BackButtonAffects.AnimatedElementsGroup && affectedAnimatedElementsGroup != null && !affectedAnimatedElementsGroup.animatedElements.Contains ( myAnimatedElement ) )
            {
                affectedAnimatedElementsGroup.animatedElements.Add ( myAnimatedElement );
            }
        }


        private void Reset ()
        {
            buttonComponent = GetComponent<Button> ();
        }


        private void OnValidate ()
        {
            buttonComponent = GetComponent<Button> ();

            if ( backButtonAffects == BackButtonAffects.AnimatedElementsGroup && affectedAnimatedElementsGroup != null && !affectedAnimatedElementsGroup.animatedElements.Contains ( GetComponent<AnimatedElement> () ) )
            {
                affectedAnimatedElementsGroup.animatedElements.Add ( GetComponent<AnimatedElement> () );
            }
        }


        public override void DoBack ()
        {
            base.DoBack ();

            myAnimatedElement.HideElement ();
        }


#if UNITY_EDITOR


        [MenuItem ( "GameObject/Airy UI/UI Close Button" , false , 1 )]
        private static void HierarchyCreateMenu ( MenuCommand menuCommand )
        {
            GameObject newGameObject = new GameObject ( "UI Close Button" );

            newGameObject.AddComponent<UICloseButton> ();

            GameObjectUtility.SetParentAndAlign ( newGameObject , menuCommand.context as GameObject );

            Undo.RegisterCreatedObjectUndo ( newGameObject , "Create " + newGameObject.name );

            Selection.activeObject = newGameObject;
        }


#endif


    }


}