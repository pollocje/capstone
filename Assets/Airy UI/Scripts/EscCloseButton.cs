using UnityEditor;
using UnityEngine;


namespace AiryUI
{


    [AddComponentMenu ( "Airy UI/Esc Close Button" )]
    public class EscCloseButton : CloseButton
    {


        private EscButtonController manager;


        private void Awake ()
        {
            InstantiateController ();

            if ( backButtonAffects == BackButtonAffects.AnimatedElement )
            {
                if ( affectedAnimatedElement == null )
                {
                    Debug.LogError ( "<color=orange><b>[Airy UI]</color></b> <color=red><b>X</color></b> Affected Animated Element is null, please set it" , gameObject );
                    return;
                }

                affectedAnimatedElement.OnShow.AddListener ( () => EscButtonController.Instance.AddButtonToList ( this ) );
                affectedAnimatedElement.OnHide.AddListener ( () => EscButtonController.Instance.RemoveButtonFromList ( this ) );
            }
            else if ( backButtonAffects == BackButtonAffects.AnimatedElementsGroup )
            {
                if ( affectedAnimatedElementsGroup == null )
                {
                    Debug.LogError ( "<color=orange><b>[Airy UI]</color></b> <color=red><b>X</color></b> Affected Animated Elements Group is null, please set it" , gameObject );
                    return;
                }
                affectedAnimatedElementsGroup.OnShow.AddListener ( () => EscButtonController.Instance.AddButtonToList ( this ) );
                affectedAnimatedElementsGroup.OnHide.AddListener ( () => EscButtonController.Instance.RemoveButtonFromList ( this ) );
            }

        }


        private void Start ()
        {
            InstantiateController ();
        }


        private void OnEnable ()
        {
            InstantiateController ();
        }


        private void OnValidate ()
        {
            InstantiateController ();

            manager = FindObjectOfType<EscButtonController> ();

            if ( manager != null )
            {
                if ( !manager.esc_buttons.Contains ( this ) )
                {
                    //manager.esc_buttons.Add ( this );
                }
            }
        }


        private void Reset ()
        {
            InstantiateController ();
        }


        public override void DoBack ()
        {
            base.DoBack ();
        }


        private void InstantiateController ()
        {
            if ( !FindObjectOfType<EscButtonController> () )
            {
                GameObject controller = new GameObject ( "[Airy UI] Esc Button Controller" );
                controller.AddComponent<EscButtonController> ();
            }
        }


#if UNITY_EDITOR


        [MenuItem ( "GameObject/Airy UI/Esc Close Button" , false , 2 )]
        private static void HierarchyCreateMenu ( MenuCommand menuCommand )
        {
            GameObject newGameObject = new GameObject ( "Esc Close Button" );

            newGameObject.AddComponent<EscCloseButton> ();

            GameObjectUtility.SetParentAndAlign ( newGameObject , menuCommand.context as GameObject );

            Undo.RegisterCreatedObjectUndo ( newGameObject , "Create " + newGameObject.name );

            Selection.activeObject = newGameObject;
        }


#endif


    }
}