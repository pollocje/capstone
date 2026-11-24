using UnityEngine;

namespace AiryUI
{
    public class AnimationPositions : MonoBehaviour
    {


        /// <summary>
        /// Used for initializing the position from a certian corner.
        /// </summary>
        /// <param name="initialPosition">the start position of the transform</param>
        /// <param name="rectTransform">the rect transform</param>
        /// <param name="animationStartPosition">BottomRight, UpRight, BottomLeft, UpLeft, Up, Bottom, Left, Or Right</param>
        /// <param name="animationFromCornerStartFromType">Screen or Rect</param>
        /// <returns></returns>
        public static Vector3 GetStartPositionFromCorner
            ( Vector3 initialPosition , RectTransform rectTransform , AnimationStartPosition animationStartPosition )
        {
            float startPositionX = 0;
            float startPositionY = 0;

            var containerCanvas = rectTransform.GetComponent<AnimatedElement> ().containerCanvas;
            var canvasRect = containerCanvas.GetComponent<RectTransform> ();

            switch ( animationStartPosition )
            {
                case ( AnimationStartPosition.Up ):

                    startPositionX = initialPosition.x;
                    startPositionY = ( canvasRect.sizeDelta.y / 2 ) + ( rectTransform.rect.height / 2 );

                    break;

                case ( AnimationStartPosition.Bottom ):

                    startPositionX = initialPosition.x;
                    startPositionY = -( canvasRect.sizeDelta.y / 2 ) - ( rectTransform.rect.height * rectTransform.localScale.y / 2 );

                    break;

                case ( AnimationStartPosition.Left ):

                    startPositionX = -( canvasRect.sizeDelta.x / 2 ) - ( rectTransform.rect.width * rectTransform.localScale.x / 2 );
                    startPositionY = initialPosition.y;

                    break;

                case ( AnimationStartPosition.Right ):

                    startPositionX = ( canvasRect.sizeDelta.x / 2 ) + ( rectTransform.rect.width * rectTransform.localScale.x / 2 );
                    startPositionY = initialPosition.y;

                    break;

                case ( AnimationStartPosition.BottomRight ):

                    startPositionX = ( canvasRect.sizeDelta.x / 2 ) + ( rectTransform.rect.width * rectTransform.localScale.x / 2 );
                    startPositionY = -( canvasRect.sizeDelta.y / 2 ) - ( rectTransform.rect.height * rectTransform.localScale.y / 2 );

                    break;

                case ( AnimationStartPosition.BottomLeft ):

                    startPositionX = -( canvasRect.sizeDelta.x / 2 ) - ( rectTransform.rect.width * rectTransform.localScale.x / 2 );
                    startPositionY = -( canvasRect.sizeDelta.y / 2 ) - ( rectTransform.rect.height * rectTransform.localScale.y / 2 );

                    break;

                case ( AnimationStartPosition.UpRight ):

                    startPositionX = ( canvasRect.sizeDelta.x / 2 ) + ( rectTransform.rect.width * rectTransform.localScale.x / 2 );
                    startPositionY = ( canvasRect.sizeDelta.y / 2 ) + ( rectTransform.rect.height * rectTransform.localScale.y / 2 );


                    break;

                case ( AnimationStartPosition.UpLeft ):

                    startPositionX = -( canvasRect.sizeDelta.x / 2 ) - ( rectTransform.rect.width * rectTransform.localScale.x / 2 );
                    startPositionY = ( canvasRect.sizeDelta.y / 2 ) + ( rectTransform.rect.height * rectTransform.localScale.y / 2 );

                    break;

                case ( AnimationStartPosition.Center ):

                    startPositionX = 0;
                    startPositionY = 0;

                    break;

                case ( AnimationStartPosition.UpCenter ):

                    startPositionX = 0;
                    startPositionY = ( canvasRect.sizeDelta.y / 2 ) + ( rectTransform.rect.height * rectTransform.localScale.y / 2 );

                    break;

                case ( AnimationStartPosition.BottomCenter ):

                    startPositionX = 0;
                    startPositionY = -( canvasRect.sizeDelta.y / 2 ) - ( rectTransform.rect.height * rectTransform.localScale.y / 2 );

                    break;

                case ( AnimationStartPosition.RightCenter ):

                    startPositionX = ( canvasRect.sizeDelta.x / 2 ) + ( rectTransform.rect.width * rectTransform.localScale.x / 2 );
                    startPositionY = 0;

                    break;

                case ( AnimationStartPosition.LeftCenter ):

                    startPositionX = -( canvasRect.sizeDelta.x / 2 ) - ( rectTransform.rect.width * rectTransform.localScale.x / 2 );
                    startPositionY = 0;

                    break;
            }

            Vector3 startPos = new Vector3 ( startPositionX , startPositionY , 0 );


            //==============================================================================================


            Vector3 addedVector = Vector3.zero;
            var parent = rectTransform.parent;
            var parentLocalPosition = parent.gameObject.Equals ( containerCanvas.gameObject ) ? Vector3.zero : parent.localPosition;


            switch ( animationStartPosition )
            {
                case ( AnimationStartPosition.Center ):

                    addedVector = new Vector3 ( -parentLocalPosition.x , -parentLocalPosition.y , 0 );
                    startPos = new Vector3 ( startPos.x + addedVector.x , startPos.y + addedVector.y , 0 );

                    break;

                case ( AnimationStartPosition.Up ):
                case ( AnimationStartPosition.Bottom ):

                    addedVector = new Vector3 ( 0 , -parentLocalPosition.y , 0 );
                    startPos = startPos + addedVector;

                    break;

                case ( AnimationStartPosition.Right ):
                case ( AnimationStartPosition.Left ):

                    addedVector = new Vector3 ( -parentLocalPosition.x , 0 , 0 );
                    startPos = startPos + addedVector;

                    break;

                case ( AnimationStartPosition.UpCenter ):
                case ( AnimationStartPosition.BottomCenter ):

                    addedVector = new Vector3 ( 0 , -parentLocalPosition.y , 0 );
                    startPos = new Vector3 ( -parentLocalPosition.x , startPos.y + addedVector.y , 0 );

                    break;

                case ( AnimationStartPosition.RightCenter ):
                case ( AnimationStartPosition.LeftCenter ):

                    addedVector = new Vector3 ( -parentLocalPosition.x , 0 , 0 );
                    startPos = new Vector3 ( startPos.x + addedVector.x , -parentLocalPosition.y , 0 );

                    break;

                case ( AnimationStartPosition.UpLeft ):
                case ( AnimationStartPosition.UpRight ):
                case ( AnimationStartPosition.BottomLeft ):
                case ( AnimationStartPosition.BottomRight ):

                    addedVector = new Vector3 ( -parentLocalPosition.x , -parentLocalPosition.y , 0 );
                    startPos = startPos + addedVector;

                    break;
            }

            return ( startPos );
        }
    }
}