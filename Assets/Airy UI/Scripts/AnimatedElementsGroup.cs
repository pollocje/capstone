using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;
using UnityEditor;

namespace AiryUI
{


    [DisallowMultipleComponent]
    [AddComponentMenu ( "Airy UI/Animated Elements Group" )]
    public class AnimatedElementsGroup : MonoBehaviour
    {


        [Tooltip ( "Aniamted elements managed by this Animation Manager" )]
        public List<AnimatedElement> animatedElements = new List<AnimatedElement> ();


        [Tooltip ( "TRUE: the Animated Elements linked to this group will be animated when this game object is enabled.\nFALSE: every animated element linked to this group is on it own." )]
        public bool showAnimaitonOnEnable;


        public UnityEvent OnShow;
        public UnityEvent OnHide;



        //====================================



        private void OnEnable ()
        {
            animatedElements.RemoveAll ( a => a == null );

            foreach ( var item in animatedElements )
            {
                if ( item.group == null )
                    item.group = this;
            }

            if ( showAnimaitonOnEnable )
            {
                ShowGroup ();
            }
        }


        public void ShowGroup ()
        {
            gameObject.SetActive ( true );

            foreach ( var element in animatedElements )
            {
                element.ShowElement ();
            }

            OnShow?.Invoke ();
        }


        public void HideGroup ()
        {
            foreach ( var element in animatedElements )
            {
                element.HideElement ();
            }

            OnHide?.Invoke ();
        }


        public void UpdateElementsInChildren ()
        {
            animatedElements = GetComponentsInChildren<AnimatedElement> ( true ).Where ( a => a.isControlledByGroup ).ToList ();

            foreach ( var c in animatedElements )
            {
                c.group = this;
            }
        }


#if UNITY_EDITOR


        [MenuItem ( "GameObject/Airy UI/Animated Elements Group" , false , 0 )]
        private static void HierarchyCreateMenu ( MenuCommand menuCommand )
        {
            GameObject newGameObject = new GameObject ( "Animated Elements Group" );

            newGameObject.AddComponent<AnimatedElementsGroup> ();

            GameObjectUtility.SetParentAndAlign ( newGameObject , menuCommand.context as GameObject );

            Undo.RegisterCreatedObjectUndo ( newGameObject , "Create " + newGameObject.name );

            Selection.activeObject = newGameObject;
        }


#endif


    }
}