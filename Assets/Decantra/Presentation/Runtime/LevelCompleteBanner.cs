/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI;

namespace Decantra.Presentation
{
    public sealed class LevelCompleteBanner : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text starsText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Image starBurst;
        [SerializeField] private Image dimmer;
        [SerializeField] private RawImage blurBackdrop;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float enterDuration = 0.35f;
        [SerializeField] private float starsHoldDuration = 0.2f;
        [SerializeField] private float levelHoldDuration = 0.35f;
        [SerializeField] private float exitDuration = 0.35f;
        [SerializeField] private float dimmerAlpha = 0.8f;
        [SerializeField] private float burstDuration = 0.45f;
        [SerializeField] private float burstMaxScale = 3.0f;
        [SerializeField] private float burstMaxAlpha = 0.85f;
        [SerializeField] private int maxSparkles = 12;
        [SerializeField] private int maxFlyingStars = 8;

        private readonly float[] starPitches = { 0.9f, 1.0f, 1.08f, 1.16f, 1.24f };
        private AudioSource[] _starSources;
        private AudioClip _starClip;
        private int _lastStarCount;
        private RectTransform _effectsRoot;
        private Image _glistenImage;
        private Image[] _sparkles;
        private Image[] _flyingStars;
        private bool _effectsReady;
        private Action _onScoreApply;

        private static Sprite _sparkleSpriteCache;
        private static Sprite _glistenSpriteCache;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private string _debugLogPath;
#endif

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

        public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.01f;

        public void EnableScreenshotMode()
        {
            enterDuration = Mathf.Max(enterDuration, 0.45f);
            starsHoldDuration = Mathf.Max(starsHoldDuration, 0.9f);
            levelHoldDuration = Mathf.Max(levelHoldDuration, 0.6f);
            exitDuration = Mathf.Max(exitDuration, 0.45f);
        }

        public float GetStarsCaptureDelay()
        {
            return enterDuration + Mathf.Min(0.3f, starsHoldDuration * 0.5f);
        }

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

        public void Show(int level, int stars, int awardedScore, bool sfxEnabled, Action onScoreApply, Action onComplete)
        {
            if (panel == null || canvasGroup == null || starsText == null || levelText == null)
            {
                onScoreApply?.Invoke();
                onComplete?.Invoke();
                return;
            }

            StopAllCoroutines();
            _onScoreApply = onScoreApply;
            int clampedStars = Mathf.Clamp(stars, 1, 5);
            _lastStarCount = clampedStars;
            starsText.text = new string('★', clampedStars);
            var tag = messages[Mathf.Abs(level) % messages.Length];
            levelText.text = $"LEVEL {level + 1}\n{tag}";
            if (scoreText != null)
            {
                scoreText.text = $"+{awardedScore}";
            }
            EnsureEffects();
            DisableEffects();
            if (sfxEnabled)
            {
                PlayStarLayers(clampedStars);
            }
            StartCoroutine(PrepareBlur());
            StartCoroutine(AnimateSequence(onComplete));
        }

        public void HideImmediate()
        {
            StopAllCoroutines();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            if (panel != null)
            {
                panel.anchoredPosition = new Vector2(0, -500);
            }
            SetDimmerAlpha(0f);
            ClearBlur();
            DisableEffects();
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
            SetDimmerAlpha(0f);

            if (starsText != null) starsText.gameObject.SetActive(activeText == starsText);
            if (levelText != null) levelText.gameObject.SetActive(activeText == levelText);
            if (scoreText != null) scoreText.gameObject.SetActive(activeText == starsText);

            float time = 0f;
            while (time < enterDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, time / enterDuration);
                canvasGroup.alpha = t;
                panel.anchoredPosition = Vector2.Lerp(new Vector2(0, -500), Vector2.zero, t);
                SetDimmerAlpha(Mathf.Lerp(0f, dimmerAlpha, t));
                yield return null;
            }

            canvasGroup.alpha = 1f;
            panel.anchoredPosition = Vector2.zero;
            if (activeText == starsText)
            {
                _onScoreApply?.Invoke();
                _onScoreApply = null;
                yield return AnimateCelebration();
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
                SetDimmerAlpha(Mathf.Lerp(dimmerAlpha, 0f, t));
                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            SetDimmerAlpha(0f);
            ClearBlur();
            DisableEffects();
        }

        private void SetDimmerAlpha(float alpha)
        {
            if (dimmer == null) return;
            var color = dimmer.color;
            color.a = alpha;
            dimmer.color = color;

            if (blurBackdrop != null)
            {
                var blurColor = blurBackdrop.color;
                blurColor.a = Mathf.Clamp01(alpha * 0.85f);
                blurBackdrop.color = blurColor;
            }
        }

        private IEnumerator PrepareBlur()
        {
            if (blurBackdrop == null)
            {
                yield break;
            }

            yield return new WaitForEndOfFrame();
            try
            {
                var source = ScreenCapture.CaptureScreenshotAsTexture();
                if (source == null)
                {
                    yield break;
                }

                int targetWidth = Mathf.Clamp(source.width / 8, 96, 256);
                int targetHeight = Mathf.Clamp(source.height / 8, 160, 360);
                var blurred = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
                for (int y = 0; y < targetHeight; y++)
                {
                    for (int x = 0; x < targetWidth; x++)
                    {
                        float u = x / (float)(targetWidth - 1);
                        float v = y / (float)(targetHeight - 1);
                        var color = source.GetPixelBilinear(u, v);
                        blurred.SetPixel(x, y, color);
                    }
                }
                blurred.Apply();

                ApplyGaussianBlur(blurred, 1);
                Destroy(source);

                blurBackdrop.texture = blurred;
                blurBackdrop.uvRect = new Rect(0, 0, 1, 1);
                blurBackdrop.color = new Color(1f, 1f, 1f, 0f);
            }
            catch
            {
                ClearBlur();
            }
        }

        private void ClearBlur()
        {
            if (blurBackdrop == null) return;
            if (blurBackdrop.texture != null)
            {
                Destroy(blurBackdrop.texture);
            }
            blurBackdrop.texture = null;
            blurBackdrop.color = new Color(1f, 1f, 1f, 0f);
        }

        private static void ApplyGaussianBlur(Texture2D texture, int iterations)
        {
            if (texture == null) return;
            int width = texture.width;
            int height = texture.height;
            Color[] pixels = texture.GetPixels();
            Color[] temp = new Color[pixels.Length];

            int[] offsets = { -1, 0, 1 };
            float[] weights = { 0.27901f, 0.44198f, 0.27901f };

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color sum = Color.clear;
                        float total = 0f;
                        for (int ox = 0; ox < offsets.Length; ox++)
                        {
                            int sx = Mathf.Clamp(x + offsets[ox], 0, width - 1);
                            float w = weights[ox];
                            sum += pixels[y * width + sx] * w;
                            total += w;
                        }
                        temp[y * width + x] = sum / total;
                    }
                }

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color sum = Color.clear;
                        float total = 0f;
                        for (int oy = 0; oy < offsets.Length; oy++)
                        {
                            int sy = Mathf.Clamp(y + offsets[oy], 0, height - 1);
                            float w = weights[oy];
                            sum += temp[sy * width + x] * w;
                            total += w;
                        }
                        pixels[y * width + x] = sum / total;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
        }

        private IEnumerator AnimateCelebration()
        {
            float intensity = Mathf.InverseLerp(1f, 5f, _lastStarCount);
            float duration = Mathf.Lerp(0.55f, 1.15f, intensity);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"Decantra Celebration stars={_lastStarCount} intensity={intensity:0.00} duration={duration:0.00}");
            AppendDebugLog($"Celebration stars={_lastStarCount} intensity={intensity:0.00} duration={duration:0.00}");
#endif

            StartCoroutine(AnimateSparkles(duration, intensity));
            StartCoroutine(AnimateFlyingStars(duration, intensity));
            StartCoroutine(AnimateGlisten(duration, intensity));

            yield return AnimateStarBurst();

            float remaining = Mathf.Max(0f, duration - burstDuration);
            if (remaining > 0f)
            {
                yield return new WaitForSeconds(remaining);
            }
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

        private IEnumerator AnimateSparkles(float duration, float intensity)
        {
            if (_sparkles == null || _sparkles.Length == 0 || panel == null) yield break;

            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(4f, maxSparkles, intensity)), 1, _sparkles.Length);
            Vector2 size = panel.rect.size;
            float[] phases = new float[count];
            float[] speeds = new float[count];
            float[] scales = new float[count];

            for (int i = 0; i < count; i++)
            {
                var sparkle = _sparkles[i];
                if (sparkle == null) continue;
                sparkle.gameObject.SetActive(true);
                phases[i] = UnityEngine.Random.Range(0f, 1f);
                speeds[i] = UnityEngine.Random.Range(2.2f, 4.2f);
                scales[i] = UnityEngine.Random.Range(0.25f, 0.6f) + intensity * 0.25f;

                var rect = sparkle.rectTransform;
                rect.anchoredPosition = new Vector2(
                    UnityEngine.Random.Range(-size.x * 0.35f, size.x * 0.35f),
                    UnityEngine.Random.Range(-size.y * 0.15f, size.y * 0.25f));
                rect.localScale = Vector3.one * scales[i];
            }

            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                for (int i = 0; i < count; i++)
                {
                    var sparkle = _sparkles[i];
                    if (sparkle == null) continue;
                    float twinkle = Mathf.Sin((time * speeds[i] + phases[i]) * Mathf.PI * 2f) * 0.5f + 0.5f;
                    var color = sparkle.color;
                    color.a = Mathf.Lerp(0.15f, 0.55f, twinkle) * (0.6f + 0.4f * intensity);
                    sparkle.color = color;
                }
                yield return null;
            }
        }

        private IEnumerator AnimateFlyingStars(float duration, float intensity)
        {
            if (_flyingStars == null || _flyingStars.Length == 0 || panel == null) yield break;

            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(2f, maxFlyingStars, intensity)), 1, _flyingStars.Length);
            Vector2 size = panel.rect.size;

            Vector2[] starts = new Vector2[count];
            Vector2[] ends = new Vector2[count];
            float[] delays = new float[count];

            for (int i = 0; i < count; i++)
            {
                var star = _flyingStars[i];
                if (star == null) continue;
                star.gameObject.SetActive(true);
                starts[i] = new Vector2(UnityEngine.Random.Range(-size.x * 0.2f, size.x * 0.2f), UnityEngine.Random.Range(-size.y * 0.05f, size.y * 0.1f));
                ends[i] = starts[i] + new Vector2(UnityEngine.Random.Range(-size.x * 0.35f, size.x * 0.35f), UnityEngine.Random.Range(size.y * 0.25f, size.y * 0.5f));
                delays[i] = UnityEngine.Random.Range(0f, duration * 0.3f);

                var rect = star.rectTransform;
                rect.anchoredPosition = starts[i];
                rect.localScale = Vector3.one * UnityEngine.Random.Range(0.35f, 0.8f) * (0.8f + 0.4f * intensity);
            }

            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                for (int i = 0; i < count; i++)
                {
                    var star = _flyingStars[i];
                    if (star == null) continue;
                    float local = Mathf.Clamp01((time - delays[i]) / Mathf.Max(0.01f, duration - delays[i]));
                    float eased = Mathf.SmoothStep(0f, 1f, local);
                    var rect = star.rectTransform;
                    rect.anchoredPosition = Vector2.Lerp(starts[i], ends[i], eased);
                    rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(0f, 30f, eased));
                    var color = star.color;
                    color.a = Mathf.Lerp(0.65f, 0f, eased);
                    star.color = color;
                }
                yield return null;
            }
        }

        private IEnumerator AnimateGlisten(float duration, float intensity)
        {
            if (_glistenImage == null || panel == null) yield break;

            Vector2 size = panel.rect.size;
            var rect = _glistenImage.rectTransform;
            rect.sizeDelta = new Vector2(size.x * 1.2f, size.y * 0.35f);
            rect.localEulerAngles = new Vector3(0f, 0f, -8f);

            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                rect.anchoredPosition = new Vector2(Mathf.Lerp(-size.x * 0.6f, size.x * 0.6f, eased), size.y * 0.15f);
                var color = _glistenImage.color;
                color.a = Mathf.Lerp(0f, 0.4f, Mathf.Sin(t * Mathf.PI)) * (0.6f + 0.4f * intensity);
                _glistenImage.color = color;
                yield return null;
            }
        }

        private void EnsureEffects()
        {
            if (_effectsReady || panel == null) return;

            var rootGo = new GameObject("Effects", typeof(RectTransform));
            _effectsRoot = rootGo.GetComponent<RectTransform>();
            _effectsRoot.SetParent(panel, false);
            _effectsRoot.anchorMin = Vector2.zero;
            _effectsRoot.anchorMax = Vector2.one;
            _effectsRoot.offsetMin = Vector2.zero;
            _effectsRoot.offsetMax = Vector2.zero;

            var glistenGo = new GameObject("Glisten", typeof(RectTransform));
            var glistenRect = glistenGo.GetComponent<RectTransform>();
            glistenRect.SetParent(_effectsRoot, false);
            _glistenImage = glistenGo.AddComponent<Image>();
            _glistenImage.sprite = GetGlistenSprite();
            _glistenImage.raycastTarget = false;
            _glistenImage.color = new Color(1f, 1f, 1f, 0f);

            _sparkles = new Image[Mathf.Max(4, maxSparkles)];
            for (int i = 0; i < _sparkles.Length; i++)
            {
                var sparkleGo = new GameObject($"Sparkle_{i + 1}", typeof(RectTransform));
                var sparkleRect = sparkleGo.GetComponent<RectTransform>();
                sparkleRect.SetParent(_effectsRoot, false);
                var image = sparkleGo.AddComponent<Image>();
                image.sprite = GetSparkleSprite();
                image.raycastTarget = false;
                image.color = new Color(1f, 0.98f, 0.8f, 0f);
                _sparkles[i] = image;
            }

            _flyingStars = new Image[Mathf.Max(3, maxFlyingStars)];
            for (int i = 0; i < _flyingStars.Length; i++)
            {
                var starGo = new GameObject($"FlyingStar_{i + 1}", typeof(RectTransform));
                var starRect = starGo.GetComponent<RectTransform>();
                starRect.SetParent(_effectsRoot, false);
                var image = starGo.AddComponent<Image>();
                image.sprite = GetSparkleSprite();
                image.raycastTarget = false;
                image.color = new Color(1f, 0.95f, 0.75f, 0f);
                _flyingStars[i] = image;
            }

            _effectsReady = true;
        }

        private void DisableEffects()
        {
            if (_glistenImage != null)
            {
                var color = _glistenImage.color;
                color.a = 0f;
                _glistenImage.color = color;
            }

            if (_sparkles != null)
            {
                for (int i = 0; i < _sparkles.Length; i++)
                {
                    if (_sparkles[i] == null) continue;
                    _sparkles[i].gameObject.SetActive(false);
                }
            }

            if (_flyingStars != null)
            {
                for (int i = 0; i < _flyingStars.Length; i++)
                {
                    if (_flyingStars[i] == null) continue;
                    _flyingStars[i].gameObject.SetActive(false);
                }
            }
        }

        private static Sprite GetSparkleSprite()
        {
            if (_sparkleSpriteCache != null) return _sparkleSpriteCache;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDist = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float t = Mathf.Clamp01(dist / maxDist);
                    float alpha = Mathf.SmoothStep(1f, 0f, t);
                    alpha *= Mathf.SmoothStep(1f, 0.6f, t);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            _sparkleSpriteCache = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
            return _sparkleSpriteCache;
        }

        private static Sprite GetGlistenSprite()
        {
            if (_glistenSpriteCache != null) return _glistenSpriteCache;

            const int width = 96;
            const int height = 24;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float t = Mathf.Abs((x / (float)(width - 1)) - 0.5f) * 2f;
                    float alpha = Mathf.SmoothStep(0.9f, 0f, t);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            _glistenSpriteCache = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            return _glistenSpriteCache;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void AppendDebugLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (string.IsNullOrWhiteSpace(_debugLogPath))
            {
                _debugLogPath = Path.Combine(Application.persistentDataPath, "debug-verification.log");
            }

            try
            {
                File.AppendAllText(_debugLogPath, $"{System.DateTime.UtcNow:O} {message}\n");
            }
            catch
            {
                // Ignore logging failures in debug builds.
            }
        }
#endif

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
