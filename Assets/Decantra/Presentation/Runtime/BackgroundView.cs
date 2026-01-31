/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Presentation
{
    public sealed class BackgroundView : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private float hueRange = 0.08f;
        [SerializeField] private float saturationBoost = 0.12f;
        [SerializeField] private float valueShift = -0.04f;

        private Color _baseColor = Color.white;
        public Color CurrentTint { get; private set; } = Color.white;

        private void Awake()
        {
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            if (backgroundImage != null)
            {
                _baseColor = backgroundImage.color;
                CurrentTint = _baseColor;
            }
        }

        public void SetLevel(int levelIndex, int seed)
        {
            if (backgroundImage == null) return;

            float h, s, v;
            Color.RGBToHSV(_baseColor, out h, out s, out v);

            int mix = Mathf.Abs(seed + levelIndex * 9973);
            float t = (mix % 1000) / 1000f;
            float hueOffset = Mathf.Lerp(-hueRange, hueRange, t);
            float sat = Mathf.Clamp01(s + saturationBoost * (0.5f - t));
            float val = Mathf.Clamp01(v + valueShift * (t - 0.5f));

            Color tint = Color.HSVToRGB(Mathf.Repeat(h + hueOffset, 1f), sat, val);
            tint.a = _baseColor.a;
            backgroundImage.color = tint;
            CurrentTint = tint;
        }
    }
}
