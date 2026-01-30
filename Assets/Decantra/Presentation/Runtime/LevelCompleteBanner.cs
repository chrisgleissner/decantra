using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Presentation
{
    public sealed class LevelCompleteBanner : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text starsText;
        [SerializeField] private Text levelText;
        [SerializeField] private Image starBurst;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float enterDuration = 0.35f;
        [SerializeField] private float starsHoldDuration = 0.2f;
        [SerializeField] private float levelHoldDuration = 0.35f;
        [SerializeField] private float exitDuration = 0.35f;
        [SerializeField] private float burstDuration = 0.45f;
        [SerializeField] private float burstMaxScale = 3.0f;
        [SerializeField] private float burstMaxAlpha = 0.85f;

        private readonly float[] starPitches = { 0.9f, 1.0f, 1.08f, 1.16f, 1.24f };
        private AudioSource[] _starSources;
        private AudioClip _starClip;
        private int _lastStarCount;

        private readonly string[] messages =
        {
            "Brilliant!",
            "Beautifully done!",
            "That was perfectly played.",
            "Such a satisfying move!",
            "Wonderful progress!",
            "Expertly sorted.",
            "That felt just right.",
            "Smooth and thoughtful play.",
            "You’re really getting the hang of this.",
            "Excellent choice!",
            "That came together beautifully.",
            "Great sorting!",
            "Level cleared!",
            "You handled that with real finesse.",
            "So calm. So clever.",
            "A lovely solution.",
            "That was a smart move.",
            "You’re in a great rhythm now.",
            "Nicely balanced!",
            "That was a joy to watch.",
            "Well earned success!",
            "Another step forward.",
            "That was pawsitively perfect!",
            "Such a good move!",
            "Tail-wagging success!",
            "That deserves a treat.",
            "Golden-retriever-level excellence!",
            "That one felt really good.",
            "You made that look easy.",
            "A step in the right direction.",
            "Steady and satisfying.",
            "You’re making great progress.",
            "Every move counts, and that one counted.",
            "That was beautifully paced.",
            "You’ve got a great eye for this.",
            "That solution was spot on.",
            "Pure satisfaction!",
            "You’re doing wonderfully.",
            "That was handled with care.",
            "Smart and steady play.",
            "That’s a win!",
            "Another challenge conquered.",
            "That level is glowing now.",
            "Stars well earned!",
            "A truly golden finish.",
            "You nailed it!",
            "That was delightfully done.",
            "Such a rewarding moment.",
            "You’re really finding your flow.",
            "That was a calm and confident finish.",
        };

        private void Awake()
        {
            _starClip = CreateStarClip();
            _starSources = new AudioSource[5];
            for (int i = 0; i < _starSources.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                _starSources[i] = source;
            }
        }

        public void Show(int level, int stars, bool sfxEnabled, Action onComplete)
        {
            if (panel == null || canvasGroup == null || starsText == null || levelText == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopAllCoroutines();
            int clampedStars = Mathf.Clamp(stars, 1, 5);
            _lastStarCount = clampedStars;
            starsText.text = new string('★', clampedStars);
            var tag = messages[Mathf.Abs(level) % messages.Length];
            levelText.text = $"LEVEL {level + 1}\n{tag}";
            if (sfxEnabled)
            {
                PlayStarLayers(clampedStars);
            }
            StartCoroutine(AnimateSequence(onComplete));
        }

        private IEnumerator AnimateSequence(Action onComplete)
        {
            yield return AnimatePanel(starsText, starsHoldDuration);
            yield return AnimatePanel(levelText, levelHoldDuration);
            onComplete?.Invoke();
        }

        private IEnumerator AnimatePanel(Text activeText, float holdDuration)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            panel.anchoredPosition = new Vector2(0, -500);

            if (starsText != null) starsText.gameObject.SetActive(activeText == starsText);
            if (levelText != null) levelText.gameObject.SetActive(activeText == levelText);

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
            if (activeText == starsText)
            {
                yield return AnimateStarBurst();
            }
            if (holdDuration > 0f)
            {
                yield return new WaitForSeconds(holdDuration);
            }

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

        private IEnumerator AnimateStarBurst()
        {
            if (starBurst == null) yield break;

            float intensity = Mathf.InverseLerp(1f, 5f, _lastStarCount);
            float maxScale = Mathf.Lerp(1.6f, burstMaxScale, intensity);
            float maxAlpha = Mathf.Lerp(0.35f, burstMaxAlpha, intensity);

            var rect = starBurst.rectTransform;
            rect.localScale = Vector3.one * 0.35f;
            var color = starBurst.color;
            color.a = 0f;
            starBurst.color = color;
            starBurst.gameObject.SetActive(true);

            float time = 0f;
            while (time < burstDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / burstDuration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                rect.localScale = Vector3.one * Mathf.Lerp(0.35f, maxScale, eased);
                color.a = Mathf.Lerp(maxAlpha, 0f, eased);
                starBurst.color = color;
                yield return null;
            }

            color.a = 0f;
            starBurst.color = color;
        }

        private void PlayStarLayers(int stars)
        {
            if (_starClip == null || _starSources == null) return;
            int layers = Mathf.Clamp(stars, 1, _starSources.Length);
            for (int i = 0; i < layers; i++)
            {
                var source = _starSources[i];
                if (source == null) continue;
                source.pitch = starPitches[Mathf.Min(i, starPitches.Length - 1)];
                source.volume = 0.45f;
                source.PlayOneShot(_starClip);
            }
        }

        private AudioClip CreateStarClip()
        {
            int sampleRate = 44100;
            float duration = 0.18f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            var clip = AudioClip.Create("StarChime", samples, 1, sampleRate, false);

            float[] data = new float[samples];
            float baseFreq = 640f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Exp(-t * 10f);
                float sine = Mathf.Sin(2f * Mathf.PI * baseFreq * t) * 0.4f;
                float shimmer = Mathf.Sin(2f * Mathf.PI * (baseFreq * 2.01f) * t) * 0.2f;
                data[i] = (sine + shimmer) * env;
            }
            clip.SetData(data, 0);
            return clip;
        }
    }
}
