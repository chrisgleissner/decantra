using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Presentation
{
    public sealed class IntroBanner : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text titleText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float enterDuration = 0.6f;
        [SerializeField] private float holdDuration = 1.0f;
        [SerializeField] private float exitDuration = 0.6f;

        public IEnumerator Play()
        {
            if (panel == null || titleText == null || canvasGroup == null)
            {
                yield break;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            panel.anchoredPosition = new Vector2(0, -500);

            float time = 0f;
            while (time < enterDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, time / enterDuration);
                canvasGroup.alpha = t;
                panel.anchoredPosition = Vector2.Lerp(new Vector2(0, -500), Vector2.zero, t);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            panel.anchoredPosition = Vector2.zero;
            yield return new WaitForSeconds(holdDuration);

            time = 0f;
            while (time < exitDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, time / exitDuration);
                canvasGroup.alpha = 1f - t;
                panel.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(0, 400), t);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}
