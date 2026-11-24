using UnityEngine;



namespace AiryUI
{


    [DisallowMultipleComponent]
    public abstract class CloseButton : MonoBehaviour
    {


        public BackButtonAffects backButtonAffects;
        public AnimatedElement affectedAnimatedElement;
        public AnimatedElementsGroup affectedAnimatedElementsGroup;



        public virtual void DoBack ()
        {
            if ( backButtonAffects == BackButtonAffects.AnimatedElement )
            {
                if ( affectedAnimatedElementsGroup == null )
                {
                    Debug.LogError ( "<color=orange><b>[Airy UI]</color></b> <color=red><b>X</color></b> Affected Animated Element is null, please set it" , gameObject );
                    return;
                }

                affectedAnimatedElement.HideElement ();
            }
            else if ( backButtonAffects == BackButtonAffects.AnimatedElementsGroup )
            {
                if ( affectedAnimatedElementsGroup == null )
                {
                    Debug.LogError ( "<color=orange><b>[Airy UI]</color></b> <color=red><b>X</color></b> Affected Animated Elements Group is null, please set it" , gameObject );
                    return;
                }

                affectedAnimatedElementsGroup.HideGroup ();
            }
        }


    }


    public enum BackButtonAffects { AnimatedElement, AnimatedElementsGroup }


}