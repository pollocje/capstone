using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace AiryUI
{
    [AddComponentMenu("Airy UI/CustomAnimated Element")]
    public class CustomAnimatedElement : AnimatedElement
    {
        public ComponentsToAnimate componentsToAnimate_SHOW;
        public ComponentsToAnimate componentsToAnimate_HIDE;

        public List<TransformAnimationRecord> TransformAnimationRecords_SHOW = new List<TransformAnimationRecord>();
        public List<GraphicAnimationRecord> GraphicAnimationRecords_SHOW = new List<GraphicAnimationRecord>();
        public List<TransformAndGraphicAnimationRecord> TransformAndGraphicAnimationRecords_SHOW = new List<TransformAndGraphicAnimationRecord>();

        public List<TransformAnimationRecord> TransformAnimationRecords_HIDE = new List<TransformAnimationRecord>();
        public List<GraphicAnimationRecord> GraphicAnimationRecords_HIDE = new List<GraphicAnimationRecord>();
        public List<TransformAndGraphicAnimationRecord> TransformAndGraphicAnimationRecords_HIDE = new List<TransformAndGraphicAnimationRecord>();

        [Min(0.01f)] public float currentRecordDuration = 0.5f;
        public float currentRecordDelay = 0;

        public bool loop = false;

        private GraphicType graphicType;

        private Graphic graphic;
        private Image img;
        //private Text txt;
        private TextMeshPro txt_m;

        private TransformAnimationRecord initialRectValues;
        private GraphicAnimationRecord initialGraphicValues;

        private TransformAnimationRecord transformRecord_beforeRecrod;
        private GraphicAnimationRecord graphicRecord_beforeRecrod;
        private TransformAndGraphicAnimationRecord transformAndGraphicRecord_beforeRecrod;

        private void Awake()
        {
            currentRunningCoroutine = EmptyCoroutine();

            rectTransformComponent = GetComponent<RectTransform>();
            graphic = GetComponent<Graphic>();

            InitializeValues();
        }

        private void Start()
        {
            if (!initializeComplete)
                InitializeValues();
        }

        private void OnEnable()
        {
            if (animateOnEnable)
            {
                ShowElement();
            }
        }

        private void InitializeValues()
        {
            initialRectValues = new TransformAnimationRecord()
            {
                Position = rectTransformComponent.localPosition,
                Scale = rectTransformComponent.localScale,
                Rotation = rectTransformComponent.eulerAngles,
            };

            if (graphic)
            {
                if (graphic is Image)
                {
                    graphicType = GraphicType.Image;
                    img = GetComponent<Image>();
                }
                // else if (graphic is Text)
                // {
                //     graphicType = GraphicType.Text;
                //     txt = GetComponent<Text>();
                // }
                else if (graphic is TextMeshProUGUI)
                {
                    graphicType = GraphicType.Text;
                    txt_m = GetComponent<TextMeshPro>();
                }

                initialGraphicValues = new GraphicAnimationRecord()
                {
                    Color = graphic.color,
                    Sprite = (graphicType == GraphicType.Image) ? img?.sprite : null,
                    Text = (graphicType == GraphicType.Text) ? txt_m?.text : "",
                };
            }
        }

        public override void ShowElement()
        {
            gameObject.SetActive(true);
            StopAllCoroutines();

            if (disableShowAnimation)
            {
                OnShowComplete.Invoke();

                return;
            }

            if (!initializeComplete)
                InitializeValues();

            switch (componentsToAnimate_SHOW)
            {
                case (ComponentsToAnimate.Transform):

                    if (currentRunningCoroutine != null)
                        StopCoroutine(currentRunningCoroutine);

                    currentRunningCoroutine = AnimateTransform_SHOW();
                    StartCoroutine(currentRunningCoroutine);

                    break;

                case (ComponentsToAnimate.Graphic):

                    if (currentRunningCoroutine != null)
                        StopCoroutine(currentRunningCoroutine);

                    currentRunningCoroutine = AnimateGraphic_SHOW();
                    StartCoroutine(currentRunningCoroutine);

                    break;

                case (ComponentsToAnimate.Both):

                    if (currentRunningCoroutine != null)
                        StopCoroutine(currentRunningCoroutine);

                    currentRunningCoroutine = AnimateTransformAndGraphic_SHOW();
                    StartCoroutine(currentRunningCoroutine);

                    break;
            }
        }


        public override void HideElement()
        {
            OnHideComplete.AddListener(ResetAll);

            if (disableHideAnimation)
            {
                OnHideComplete.Invoke();

                return;
            }

            switch (componentsToAnimate_HIDE)
            {
                case (ComponentsToAnimate.Transform):

                    if (currentRunningCoroutine != null)
                        StopCoroutine(currentRunningCoroutine);

                    currentRunningCoroutine = AnimateTransform_HIDE();
                    StartCoroutine(currentRunningCoroutine);

                    break;

                case (ComponentsToAnimate.Graphic):

                    if (currentRunningCoroutine != null)
                        StopCoroutine(currentRunningCoroutine);

                    currentRunningCoroutine = AnimateGraphic_HIDE();
                    StartCoroutine(currentRunningCoroutine);

                    break;

                case (ComponentsToAnimate.Both):

                    if (currentRunningCoroutine != null)
                        StopCoroutine(currentRunningCoroutine);

                    currentRunningCoroutine = AnimateTransformAndGraphic_HIDE();
                    StartCoroutine(currentRunningCoroutine);

                    break;
            }
        }

        #region Show Coroutines


        private IEnumerator AnimateTransform_SHOW()
        {
            if (delayEnabled)
                yield return WaitForSeconds_SHOW(showDelay);

            if (TransformAnimationRecords_SHOW.Count > 1)
            {
                if (loop)
                {
                    while (true)
                    {
                        if (OnShow != null)
                            OnShow.Invoke();

                        for (int i = 1; i < TransformAnimationRecords_SHOW.Count; i++)
                        {
                            yield return WaitForSeconds_SHOW(TransformAnimationRecords_SHOW[i - 1].Delay);

                            float elapsedTime = 0;
                            while (elapsedTime <= TransformAnimationRecords_SHOW[i - 1].Duration)
                            {
                                float t = elapsedTime / TransformAnimationRecords_SHOW[i - 1].Duration;

                                rectTransformComponent.localPosition = Vector3.Lerp(TransformAnimationRecords_SHOW[i - 1].Position, TransformAnimationRecords_SHOW[i].Position, t);
                                rectTransformComponent.localScale = Vector3.Lerp(TransformAnimationRecords_SHOW[i - 1].Scale, TransformAnimationRecords_SHOW[i].Scale, t);

                                rectTransformComponent.eulerAngles = Vector3.Lerp(TransformAnimationRecords_SHOW[i - 1].Rotation, TransformAnimationRecords_SHOW[i].Rotation, t);

                                elapsedTime += DeltaTimeFor_SHOW;
                                yield return (null);
                            }

                            #region Set Final Values

                            rectTransformComponent.localPosition = TransformAnimationRecords_SHOW[i].Position;
                            rectTransformComponent.localScale = TransformAnimationRecords_SHOW[i].Scale;
                            rectTransformComponent.eulerAngles = TransformAnimationRecords_SHOW[i].Rotation;

                            #endregion
                        }

                        if (OnShowComplete != null)
                            OnShowComplete.Invoke();
                    }
                }
                else
                {
                    if (OnShow != null)
                        OnShow.Invoke();

                    for (int i = 1; i < TransformAnimationRecords_SHOW.Count; i++)
                    {
                        yield return WaitForSeconds_SHOW(TransformAnimationRecords_SHOW[i - 1].Delay);

                        float elapsedTime = 0;
                        while (elapsedTime <= TransformAnimationRecords_SHOW[i - 1].Duration)
                        {
                            float t = elapsedTime / TransformAnimationRecords_SHOW[i - 1].Duration;

                            rectTransformComponent.localPosition = Vector3.Lerp(TransformAnimationRecords_SHOW[i - 1].Position, TransformAnimationRecords_SHOW[i].Position, t);
                            rectTransformComponent.localScale = Vector3.Lerp(TransformAnimationRecords_SHOW[i - 1].Scale, TransformAnimationRecords_SHOW[i].Scale, t);

                            rectTransformComponent.eulerAngles = Vector3.Lerp(TransformAnimationRecords_SHOW[i - 1].Rotation, TransformAnimationRecords_SHOW[i].Rotation, t);

                            elapsedTime += DeltaTimeFor_SHOW;
                            yield return (null);
                        }

                        #region Set Final Values

                        rectTransformComponent.localPosition = TransformAnimationRecords_SHOW[i].Position;
                        rectTransformComponent.localScale = TransformAnimationRecords_SHOW[i].Scale;
                        rectTransformComponent.eulerAngles = TransformAnimationRecords_SHOW[i].Rotation;

                        #endregion
                    }

                    if (OnShowComplete != null)
                        OnShowComplete.Invoke();
                }
            }
        }

        private IEnumerator AnimateGraphic_SHOW()
        {
            if (delayEnabled)
                yield return WaitForSeconds_SHOW(showDelay);

            if (GraphicAnimationRecords_SHOW.Count > 1)
            {
                if (loop)
                {
                    while (true)
                    {
                        if (OnShow != null)
                            OnShow.Invoke();

                        for (int i = 1; i < GraphicAnimationRecords_SHOW.Count; i++)
                        {
                            yield return WaitForSeconds_SHOW(GraphicAnimationRecords_SHOW[i - 1].Delay);

                            float elapsedTime = 0;
                            while (elapsedTime <= GraphicAnimationRecords_SHOW[i - 1].Duration)
                            {
                                float t = elapsedTime / GraphicAnimationRecords_SHOW[i - 1].Duration;

                                if (graphicType == GraphicType.Image)
                                {
                                    Image img = graphic as Image;
                                    if (GraphicAnimationRecords_SHOW[i - 1].Sprite)
                                    {
                                        img.sprite = GraphicAnimationRecords_SHOW[i - 1].Sprite;
                                    }
                                    img.color = Color.Lerp(GraphicAnimationRecords_SHOW[i - 1].Color, GraphicAnimationRecords_SHOW[i].Color, t);
                                }

                                else if (graphicType == GraphicType.Text)
                                {
                                    TextMeshProUGUI txt = graphic as TextMeshProUGUI;
                                    if (!string.IsNullOrEmpty(GraphicAnimationRecords_SHOW[i - 1].Text))
                                    {
                                        txt.text = GraphicAnimationRecords_SHOW[i - 1].Text;
                                        txt.color = Color.Lerp(GraphicAnimationRecords_SHOW[i - 1].Color, GraphicAnimationRecords_SHOW[i].Color, t);
                                    }
                                }

                                elapsedTime += DeltaTimeFor_SHOW;
                                yield return (null);
                            }

                            #region Set Final Values

                            if (graphicType == GraphicType.Image)
                            {
                                Image img = graphic as Image;
                                img.sprite = GraphicAnimationRecords_SHOW[i].Sprite;
                                img.color = GraphicAnimationRecords_SHOW[i].Color;
                            }
                            else if (graphicType == GraphicType.Text)
                            {
                                TextMeshProUGUI txt = graphic as TextMeshProUGUI;
                                txt.text = GraphicAnimationRecords_SHOW[i].Text;
                                txt.color = GraphicAnimationRecords_SHOW[i].Color;
                            }

                            #endregion
                        }

                        if (OnShowComplete != null)
                            OnShowComplete.Invoke();
                    }
                }
                else
                {
                    if (OnShow != null)
                        OnShow.Invoke();

                    for (int i = 1; i < GraphicAnimationRecords_SHOW.Count; i++)
                    {
                        yield return WaitForSeconds_SHOW(GraphicAnimationRecords_SHOW[i - 1].Delay);

                        float elapsedTime = 0;
                        while (elapsedTime <= GraphicAnimationRecords_SHOW[i - 1].Duration)
                        {
                            float t = elapsedTime / GraphicAnimationRecords_SHOW[i - 1].Duration;

                            if (graphicType == GraphicType.Image)
                            {
                                Image img = graphic as Image;
                                if (GraphicAnimationRecords_SHOW[i - 1].Sprite)
                                    img.sprite = GraphicAnimationRecords_SHOW[i - 1].Sprite;
                                img.color = Color.Lerp(GraphicAnimationRecords_SHOW[i - 1].Color, GraphicAnimationRecords_SHOW[i].Color, t);
                            }

                            else if (graphicType == GraphicType.Text)
                            {
                                TextMeshProUGUI txt = graphic as TextMeshProUGUI;
                                if (!string.IsNullOrEmpty(GraphicAnimationRecords_SHOW[i - 1].Text))
                                    txt.text = GraphicAnimationRecords_SHOW[i - 1].Text;
                                txt.color = Color.Lerp(GraphicAnimationRecords_SHOW[i - 1].Color, GraphicAnimationRecords_SHOW[i].Color, t);
                            }

                            elapsedTime += DeltaTimeFor_SHOW;
                            yield return (null);
                        }

                        #region Set Final Values

                        if (graphicType == GraphicType.Image)
                        {
                            Image img = graphic as Image;
                            if (GraphicAnimationRecords_SHOW[i].Sprite)
                                img.sprite = GraphicAnimationRecords_SHOW[i].Sprite;
                            img.color = GraphicAnimationRecords_SHOW[i].Color;
                        }
                        else if (graphicType == GraphicType.Text)
                        {
                            TextMeshProUGUI txt = graphic as TextMeshProUGUI;
                            if (!string.IsNullOrEmpty(GraphicAnimationRecords_SHOW[i].Text))
                                txt.text = GraphicAnimationRecords_SHOW[i].Text;
                            txt.color = GraphicAnimationRecords_SHOW[i].Color;
                        }

                        #endregion
                    }

                    if (OnShowComplete != null)
                        OnShowComplete.Invoke();
                }
            }
        }

        private IEnumerator AnimateTransformAndGraphic_SHOW()
        {
            if (TransformAndGraphicAnimationRecords_SHOW.Count > 1)
            {
                if (delayEnabled)
                    yield return WaitForSeconds_SHOW(showDelay);

                if (loop)
                {
                    while (true)
                    {
                        if (OnShow != null)
                            OnShow.Invoke();

                        for (int i = 1; i < TransformAndGraphicAnimationRecords_SHOW.Count; i++)
                        {
                            yield return WaitForSeconds_SHOW(TransformAndGraphicAnimationRecords_SHOW[i - 1].Delay);

                            float elapsedTime = 0;
                            while (elapsedTime <= TransformAndGraphicAnimationRecords_SHOW[i - 1].Duration)
                            {
                                float t = elapsedTime / TransformAndGraphicAnimationRecords_SHOW[i - 1].Duration;

                                rectTransformComponent.localPosition = Vector3.Lerp(TransformAndGraphicAnimationRecords_SHOW[i - 1].Position, TransformAndGraphicAnimationRecords_SHOW[i].Position, t);
                                rectTransformComponent.localScale = Vector3.Lerp(TransformAndGraphicAnimationRecords_SHOW[i - 1].Scale, TransformAndGraphicAnimationRecords_SHOW[i].Scale, t);
                                rectTransformComponent.eulerAngles = Vector3.Lerp(TransformAndGraphicAnimationRecords_SHOW[i - 1].Rotation, TransformAndGraphicAnimationRecords_SHOW[i].Rotation, t);

                                if (graphicType == GraphicType.Image)
                                {
                                    Image img = graphic as Image;
                                    if (TransformAndGraphicAnimationRecords_SHOW[i - 1].Sprite)
                                    {
                                        img.sprite = TransformAndGraphicAnimationRecords_SHOW[i - 1].Sprite;
                                    }
                                    img.color = Color.Lerp(TransformAndGraphicAnimationRecords_SHOW[i - 1].Color, TransformAndGraphicAnimationRecords_SHOW[i].Color, t);
                                }

                                else if (graphicType == GraphicType.Text)
                                {
                                    TextMeshProUGUI txt = graphic as TextMeshProUGUI;
                                    if (!string.IsNullOrEmpty(TransformAndGraphicAnimationRecords_SHOW[i - 1].Text))
                                    {
                                        txt.text = TransformAndGraphicAnimationRecords_SHOW[i - 1].Text;
                                    }
                                    txt.color = Color.Lerp(TransformAndGraphicAnimationRecords_SHOW[i - 1].Color, TransformAndGraphicAnimationRecords_SHOW[i].Color, t);
                                }

                                elapsedTime += DeltaTimeFor_SHOW;
                                yield return (null);
                            }

                            #region Set Final Values

                            rectTransformComponent.localPosition = TransformAndGraphicAnimationRecords_SHOW[i].Position;
                            rectTransformComponent.localScale = TransformAndGraphicAnimationRecords_SHOW[i].Scale;
                            rectTransformComponent.eulerAngles = TransformAndGraphicAnimationRecords_SHOW[i].Rotation;

                            if (graphicType == GraphicType.Image)
                            {
                                Image img = graphic as Image;
                                if (TransformAndGraphicAnimationRecords_SHOW[i].Sprite)
                                    img.color = TransformAndGraphicAnimationRecords_SHOW[i].Color;
                            }
                            else if (graphicType == GraphicType.Text)
                            {
                                TextMeshProUGUI txt = graphic as TextMeshProUGUI;
                                if (!string.IsNullOrEmpty(TransformAndGraphicAnimationRecords_SHOW[i].Text))
                                    txt.color = TransformAndGraphicAnimationRecords_SHOW[i].Color;
                            }

                            #endregion
                        }

                        if (OnShowComplete != null)
                            OnShowComplete.Invoke();
                    }
                }
                else
                {

                    if (OnShow != null)
                        OnShow.Invoke();

                    for (int i = 1; i < TransformAndGraphicAnimationRecords_SHOW.Count; i++)
                    {
                        yield return WaitForSeconds_SHOW(TransformAndGraphicAnimationRecords_SHOW[i - 1].Delay);

                        float elapsedTime = 0;
                        while (elapsedTime <= TransformAndGraphicAnimationRecords_SHOW[i - 1].Duration)
                        {
                            float t = elapsedTime / TransformAndGraphicAnimationRecords_SHOW[i - 1].Duration;

                            rectTransformComponent.localPosition = Vector3.Lerp(TransformAndGraphicAnimationRecords_SHOW[i - 1].Position, TransformAndGraphicAnimationRecords_SHOW[i].Position, t);
                            rectTransformComponent.localScale = Vector3.Lerp(TransformAndGraphicAnimationRecords_SHOW[i - 1].Scale, TransformAndGraphicAnimationRecords_SHOW[i].Scale, t);
                            rectTransformComponent.eulerAngles = Vector3.Lerp(TransformAndGraphicAnimationRecords_SHOW[i - 1].Rotation, TransformAndGraphicAnimationRecords_SHOW[i].Rotation, t);

                            if (graphicType == GraphicType.Image)
                            {
                                Image img = graphic as Image;
                                if (TransformAndGraphicAnimationRecords_SHOW[i - 1].Sprite)
                                    img.sprite = TransformAndGraphicAnimationRecords_SHOW[i - 1].Sprite;

                                img.color = Color.Lerp(TransformAndGraphicAnimationRecords_SHOW[i - 1].Color, TransformAndGraphicAnimationRecords_SHOW[i].Color, t);
                            }

                            else if (graphicType == GraphicType.Text)
                            {
                                TextMeshProUGUI txt = graphic as TextMeshProUGUI;
                                if (!string.IsNullOrEmpty(TransformAndGraphicAnimationRecords_SHOW[i - 1].Text))
                                    txt.text = TransformAndGraphicAnimationRecords_SHOW[i - 1].Text;

                                txt.color = Color.Lerp(TransformAndGraphicAnimationRecords_SHOW[i - 1].Color, TransformAndGraphicAnimationRecords_SHOW[i].Color, t);
                            }

                            elapsedTime += DeltaTimeFor_SHOW;
                            yield return (null);
                        }

                        #region Set Final Values

                        rectTransformComponent.localPosition = TransformAndGraphicAnimationRecords_SHOW[i].Position;
                        rectTransformComponent.localScale = TransformAndGraphicAnimationRecords_SHOW[i].Scale;
                        rectTransformComponent.eulerAngles = TransformAndGraphicAnimationRecords_SHOW[i].Rotation;

                        if (graphicType == GraphicType.Image)
                        {
                            Image img = graphic as Image;
                            img.color = TransformAndGraphicAnimationRecords_SHOW[i].Color;
                        }
                        else if (graphicType == GraphicType.Text)
                        {
                            TextMeshProUGUI txt = graphic as TextMeshProUGUI;
                            txt.color = TransformAndGraphicAnimationRecords_SHOW[i].Color;
                        }

                        #endregion
                    }

                    if (OnShowComplete != null)
                        OnShowComplete.Invoke();
                }
            }
        }


        #endregion

        //==========================================================================================
        //==========================================================================================

        #region Hide Coroutines

        private IEnumerator AnimateTransform_HIDE()
        {
            if (delayEnabled)
                yield return WaitForSeconds_HIDE(hideDelay);

            if (OnShow != null)
                OnShow.Invoke();

            for (int i = 1; i < TransformAnimationRecords_HIDE.Count; i++)
            {
                yield return WaitForSeconds_HIDE(TransformAnimationRecords_HIDE[i - 1].Delay);

                float elapsedTime = 0;
                while (elapsedTime <= TransformAnimationRecords_HIDE[i - 1].Duration)
                {
                    float t = elapsedTime / TransformAnimationRecords_HIDE[i - 1].Duration;

                    rectTransformComponent.localPosition = Vector3.Lerp(TransformAnimationRecords_HIDE[i - 1].Position, TransformAnimationRecords_HIDE[i].Position, t);
                    rectTransformComponent.localScale = Vector3.Lerp(TransformAnimationRecords_HIDE[i - 1].Scale, TransformAnimationRecords_HIDE[i].Scale, t);
                    rectTransformComponent.eulerAngles = Vector3.Lerp(TransformAnimationRecords_HIDE[i - 1].Rotation, TransformAnimationRecords_HIDE[i].Rotation, t);

                    elapsedTime += DeltaTimeFor_HIDE;
                    yield return (null);
                }

                #region Set Final Values

                rectTransformComponent.localPosition = TransformAnimationRecords_HIDE[i].Position;
                rectTransformComponent.localScale = TransformAnimationRecords_HIDE[i].Scale;
                rectTransformComponent.eulerAngles = TransformAnimationRecords_HIDE[i].Rotation;

                #endregion
            }

            gameObject.SetActive(false);

            if (OnHideComplete != null)
                OnHideComplete.Invoke();
        }

        private IEnumerator AnimateGraphic_HIDE()
        {
            if (delayEnabled)
                yield return WaitForSeconds_HIDE(hideDelay);

            if (OnShow != null)
                OnShow.Invoke();

            for (int i = 1; i < GraphicAnimationRecords_HIDE.Count; i++)
            {
                yield return WaitForSeconds_HIDE(GraphicAnimationRecords_HIDE[i - 1].Delay);

                float elapsedTime = 0;
                while (elapsedTime <= GraphicAnimationRecords_HIDE[i - 1].Duration)
                {
                    float t = elapsedTime / GraphicAnimationRecords_HIDE[i - 1].Duration;

                    if (graphicType == GraphicType.Image)
                    {
                        Image img = graphic as Image;
                        if (GraphicAnimationRecords_HIDE[i - 1].Sprite)
                            img.sprite = GraphicAnimationRecords_HIDE[i - 1].Sprite;
                        img.color = Color.Lerp(GraphicAnimationRecords_HIDE[i - 1].Color, GraphicAnimationRecords_HIDE[i].Color, t);
                    }

                    else if (graphicType == GraphicType.Text)
                    {
                        TextMeshProUGUI txt = graphic as TextMeshProUGUI;
                        if (!string.IsNullOrEmpty(GraphicAnimationRecords_HIDE[i - 1].Text))
                            txt.text = GraphicAnimationRecords_HIDE[i - 1].Text;
                        txt.color = Color.Lerp(GraphicAnimationRecords_HIDE[i - 1].Color, GraphicAnimationRecords_HIDE[i].Color, t);
                    }

                    elapsedTime += DeltaTimeFor_HIDE;
                    yield return (null);
                }

                #region Set Final Values

                if (graphicType == GraphicType.Image)
                {
                    Image img = graphic as Image;
                    img.color = GraphicAnimationRecords_HIDE[i].Color;
                }
                else if (graphicType == GraphicType.Text)
                {
                    TextMeshProUGUI txt = graphic as TextMeshProUGUI;
                    txt.color = GraphicAnimationRecords_HIDE[i].Color;
                }

                #endregion
            }

            gameObject.SetActive(false);

            if (OnHideComplete != null)
                OnHideComplete.Invoke();
        }

        private IEnumerator AnimateTransformAndGraphic_HIDE()
        {
            if (delayEnabled)
                yield return WaitForSeconds_HIDE(hideDelay);

            if (OnShow != null)
                OnShow.Invoke();

            for (int i = 1; i < TransformAndGraphicAnimationRecords_HIDE.Count; i++)
            {
                yield return WaitForSeconds_HIDE(TransformAndGraphicAnimationRecords_HIDE[i - 1].Delay);

                float elapsedTime = 0;
                while (elapsedTime <= TransformAndGraphicAnimationRecords_HIDE[i - 1].Duration)
                {
                    float t = elapsedTime / TransformAndGraphicAnimationRecords_HIDE[i - 1].Duration;

                    rectTransformComponent.localPosition = Vector3.Lerp(TransformAndGraphicAnimationRecords_HIDE[i - 1].Position, TransformAndGraphicAnimationRecords_HIDE[i].Position, t);
                    rectTransformComponent.localScale = Vector3.Lerp(TransformAndGraphicAnimationRecords_HIDE[i - 1].Scale, TransformAndGraphicAnimationRecords_HIDE[i].Scale, t);
                    rectTransformComponent.eulerAngles = Vector3.Lerp(TransformAndGraphicAnimationRecords_HIDE[i - 1].Rotation, TransformAndGraphicAnimationRecords_HIDE[i].Rotation, t);

                    if (graphicType == GraphicType.Image)
                    {
                        Image img = graphic as Image;
                        if (TransformAndGraphicAnimationRecords_HIDE[i].Sprite)
                            img.sprite = TransformAndGraphicAnimationRecords_HIDE[i].Sprite;
                        img.color = Color.Lerp(TransformAndGraphicAnimationRecords_HIDE[i - 1].Color, TransformAndGraphicAnimationRecords_HIDE[i].Color, t);
                    }

                    else if (graphicType == GraphicType.Text)
                    {
                        TextMeshProUGUI txt = graphic as TextMeshProUGUI;
                        if (!string.IsNullOrEmpty(TransformAndGraphicAnimationRecords_HIDE[i].Text))
                            txt.text = TransformAndGraphicAnimationRecords_HIDE[i].Text;
                        txt.color = Color.Lerp(TransformAndGraphicAnimationRecords_HIDE[i - 1].Color, TransformAndGraphicAnimationRecords_HIDE[i].Color, t);
                    }

                    elapsedTime += DeltaTimeFor_HIDE;
                    yield return (null);
                }

                #region Set Final Values

                rectTransformComponent.localPosition = TransformAndGraphicAnimationRecords_HIDE[i].Position;
                rectTransformComponent.localScale = TransformAndGraphicAnimationRecords_HIDE[i].Scale;
                rectTransformComponent.eulerAngles = TransformAndGraphicAnimationRecords_HIDE[i].Rotation;

                if (graphicType == GraphicType.Image)
                {
                    Image img = graphic as Image;
                    if (TransformAndGraphicAnimationRecords_HIDE[i].Sprite)
                        img.sprite = TransformAndGraphicAnimationRecords_HIDE[i].Sprite;
                    img.color = TransformAndGraphicAnimationRecords_HIDE[i].Color;
                }
                else if (graphicType == GraphicType.Text)
                {
                    TextMeshProUGUI txt = graphic as TextMeshProUGUI;
                    if (!string.IsNullOrEmpty(TransformAndGraphicAnimationRecords_HIDE[i].Text))
                        txt.text = TransformAndGraphicAnimationRecords_HIDE[i].Text;
                    txt.color = TransformAndGraphicAnimationRecords_HIDE[i].Color;
                }

                #endregion
            }

            gameObject.SetActive(false);

            if (OnHideComplete != null)
                OnHideComplete.Invoke();
        }

        #endregion

        private void ResetAll()
        {
            rectTransformComponent.localPosition = initialRectValues.Position;
            rectTransformComponent.localScale = initialRectValues.Scale;
            rectTransformComponent.eulerAngles = initialRectValues.Rotation;

            if (graphicType == GraphicType.Image)
            {
                Image img = graphic as Image;
                img.sprite = initialGraphicValues.Sprite;
                img.color = initialGraphicValues.Color;
            }

            else if (graphicType == GraphicType.Text)
            {
                TextMeshProUGUI txt = graphic as TextMeshProUGUI;
                txt.text = initialGraphicValues.Text;
                txt.color = initialGraphicValues.Color;
            }
        }

        public void Record(GameObject gObject, AnimationShowOrHide showOrHide)
        {
            rectTransformComponent = gObject.GetComponent<RectTransform>();
            graphic = gObject.GetComponent<Graphic>();

            RecordValues(showOrHide, false);
        }

        public void EnterRecordMode(GameObject gObject, AnimationShowOrHide showOrHide)
        {
            rectTransformComponent = gObject.GetComponent<RectTransform>();
            graphic = gObject.GetComponent<Graphic>();

            transformRecord_beforeRecrod = null;
            graphicRecord_beforeRecrod = null;
            transformAndGraphicRecord_beforeRecrod = null;

            RecordValues(showOrHide, true);
        }

        public void ExitRecordMode(GameObject gObject, AnimationShowOrHide showOrHide)
        {
            rectTransformComponent = gObject.GetComponent<RectTransform>();
            graphic = gObject.GetComponent<Graphic>();

            ReturnValuesAfterRecord(gObject, showOrHide);
        }

        private void RecordValues(AnimationShowOrHide showOrHide, bool recordModeActive)
        {
            ComponentsToAnimate componentsToAnimate = (showOrHide == AnimationShowOrHide.Show) ? componentsToAnimate_SHOW : componentsToAnimate_HIDE;

            switch (componentsToAnimate)
            {
                case (ComponentsToAnimate.Transform):
                    TransformAnimationRecord transformRecord = new TransformAnimationRecord()
                    {
                        Duration = currentRecordDuration,
                        Delay = currentRecordDelay,

                        Position = rectTransformComponent.localPosition,
                        Scale = rectTransformComponent.localScale,
                        Rotation = new Vector3
                        (
                            rectTransformComponent.eulerAngles.x > 180 ? rectTransformComponent.eulerAngles.x - 360 : rectTransformComponent.eulerAngles.x,
                            rectTransformComponent.eulerAngles.y > 180 ? rectTransformComponent.eulerAngles.y - 360 : rectTransformComponent.eulerAngles.y,
                            rectTransformComponent.eulerAngles.z > 180 ? rectTransformComponent.eulerAngles.z - 360 : rectTransformComponent.eulerAngles.z
                        )
                    };

                    if (recordModeActive)
                    {
                        transformRecord_beforeRecrod = transformRecord;
                        return;
                    }

                    if (showOrHide == AnimationShowOrHide.Show)
                        TransformAnimationRecords_SHOW.Add(transformRecord);
                    else
                        TransformAnimationRecords_HIDE.Add(transformRecord);

                    break;

                case (ComponentsToAnimate.Graphic):
                    if (graphic is Image)
                    {
                        img = graphic as Image;
                        graphicType = GraphicType.Image;
                    }
                    else if (graphic is TextMeshPro)
                    {
                        txt_m = graphic as TextMeshPro;
                        graphicType = GraphicType.Text;
                    }

                    GraphicAnimationRecord graphicRecord = new GraphicAnimationRecord()
                    {
                        Duration = currentRecordDuration,
                        Delay = currentRecordDelay,

                        Color = graphic.color,
                        Sprite = (graphicType == GraphicType.Image) ? img.sprite : null,
                        Text = (graphicType == GraphicType.Text) ? txt_m.text : ""
                    };

                    if (recordModeActive)
                    {
                        graphicRecord_beforeRecrod = graphicRecord;
                        return;
                    }

                    if (showOrHide == AnimationShowOrHide.Show)
                        GraphicAnimationRecords_SHOW.Add(graphicRecord);
                    else
                        GraphicAnimationRecords_HIDE.Add(graphicRecord);
                    break;

                case (ComponentsToAnimate.Both):
                    if (graphic is Image)
                    {
                        img = graphic as Image;
                        graphicType = GraphicType.Image;
                    }
                    else if (graphic is TextMeshProUGUI)
                    {
                        txt_m = graphic as TextMeshPro;
                        graphicType = GraphicType.Text;
                    }

                    TransformAndGraphicAnimationRecord transformAndGrapihcRecord = new TransformAndGraphicAnimationRecord()
                    {
                        Duration = currentRecordDuration,
                        Delay = currentRecordDelay,

                        Position = rectTransformComponent.localPosition,
                        Scale = rectTransformComponent.localScale,
                        Rotation = new Vector3
                        (
                            rectTransformComponent.eulerAngles.x > 180 ? rectTransformComponent.eulerAngles.x - 360 : rectTransformComponent.eulerAngles.x,
                            rectTransformComponent.eulerAngles.y > 180 ? rectTransformComponent.eulerAngles.y - 360 : rectTransformComponent.eulerAngles.y,
                            rectTransformComponent.eulerAngles.z > 180 ? rectTransformComponent.eulerAngles.z - 360 : rectTransformComponent.eulerAngles.z
                        ),

                        Color = graphic.color,
                        Sprite = (graphicType == GraphicType.Image) ? img.sprite : null,
                        Text = (graphicType == GraphicType.Text) ? txt_m.text : ""
                    };

                    if (recordModeActive)
                    {
                        transformAndGraphicRecord_beforeRecrod = transformAndGrapihcRecord;
                        return;
                    }

                    if (showOrHide == AnimationShowOrHide.Show)
                        TransformAndGraphicAnimationRecords_SHOW.Add(transformAndGrapihcRecord);
                    else
                        TransformAndGraphicAnimationRecords_HIDE.Add(transformAndGrapihcRecord);

                    break;
            }
        }

        private void ReturnValuesAfterRecord(GameObject gObject, AnimationShowOrHide showOrHide)
        {
            ComponentsToAnimate componentsToAnimate = (showOrHide == AnimationShowOrHide.Show) ? componentsToAnimate_SHOW : componentsToAnimate_HIDE;

            switch (componentsToAnimate)
            {
                case (ComponentsToAnimate.Transform):
                    rectTransformComponent.localPosition = transformRecord_beforeRecrod.Position;
                    rectTransformComponent.localScale = transformRecord_beforeRecrod.Scale;
                    rectTransformComponent.eulerAngles = transformRecord_beforeRecrod.Rotation;

                    break;
                case (ComponentsToAnimate.Graphic):
                    graphic.color = graphicRecord_beforeRecrod.Color;

                    if (graphic is Image)
                        gObject.GetComponent<Image>().sprite = graphicRecord_beforeRecrod.Sprite;
                    else if (graphic is TextMeshProUGUI)
                        gObject.GetComponent<TextMeshProUGUI>().text = graphicRecord_beforeRecrod.Text;

                    break;
                case (ComponentsToAnimate.Both):
                    rectTransformComponent.localPosition = transformAndGraphicRecord_beforeRecrod.Position;
                    rectTransformComponent.localScale = transformAndGraphicRecord_beforeRecrod.Scale;
                    rectTransformComponent.eulerAngles = transformAndGraphicRecord_beforeRecrod.Rotation;

                    graphic.color = transformAndGraphicRecord_beforeRecrod.Color;
                    if (graphic is Image)
                        gObject.GetComponent<Image>().sprite = transformAndGraphicRecord_beforeRecrod.Sprite;
                    else if (graphic is TextMeshProUGUI)
                        gObject.GetComponent<TextMeshProUGUI>().text = transformAndGraphicRecord_beforeRecrod.Text;

                    break;
            }

            transformRecord_beforeRecrod = null;
            graphicRecord_beforeRecrod = null;
            transformAndGraphicRecord_beforeRecrod = null;
        }

        public enum ComponentsToAnimate
        {
            Transform = 0, Graphic = 1, Both = 2
        }

        public enum AnimationShowOrHide
        {
            Show = 0, Hide = 1
        }

        private enum GraphicType
        {
            Image = 0, Text = 1
        }
    }

    [System.Serializable]
    public class TransformAnimationRecord
    {
        public float Delay;
        [Min(0.01f)] public float Duration = 0.5f;

        public Vector3 Position;         // The anchored position.
        public Vector3 Scale;
        public Vector3 Rotation;
    }

    [System.Serializable]
    public class GraphicAnimationRecord
    {
        public float Delay;
        [Min(0.01f)] public float Duration = 0.5f;

        [Tooltip("Only works if the game object has Image component")] public Sprite Sprite;
        [Tooltip("Only works if the game object has Text component")] public string Text;
        public Color Color;
    }

    [System.Serializable]
    public class TransformAndGraphicAnimationRecord
    {
        public float Delay;
        [Min(0.01f)] public float Duration = 0.5f;

        public Vector3 Position;
        public Vector3 Scale;
        public Vector3 Rotation;

        [Tooltip("Only works if the game object has Image component")] public Sprite Sprite;
        [Tooltip("Only works if the game object has Text component")] public string Text;
        public Color Color;
    }
}