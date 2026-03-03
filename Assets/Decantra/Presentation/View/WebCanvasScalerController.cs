/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

#if UNITY_WEBGL && !UNITY_EDITOR

using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Presentation.View
{
    /// <summary>
    /// WebGL-only component that keeps <see cref="CanvasScaler.matchWidthOrHeight"/> in sync
    /// with screen orientation so that gameplay always appears at the correct size.
    ///
    /// Portrait (width ≤ height):
    ///   matchWidthOrHeight = 0 (width-matching).
    ///   Canvas width = 1080 reference units — identical to Android portrait behaviour.
    ///
    /// Landscape (width > height):
    ///   matchWidthOrHeight = 1 (height-matching).
    ///   Canvas height = 1920 reference units so bottle sizes equal the portrait baseline.
    ///   The extra horizontal canvas area is filled only by the background layer;
    ///   gameplay UI elements are all center-anchored and stay visually centred.
    ///
    /// This component is stripped from Android / iOS builds via the compile-time guard.
    /// It runs at script execution order -100 to update the scaler before
    /// <see cref="HudSafeLayout"/> performs its per-frame layout pass.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class WebCanvasScalerController : MonoBehaviour
    {
        private CanvasScaler _scaler;
        private Vector2 _lastScreenSize;

        private void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        private void LateUpdate()
        {
            var size = new Vector2(Screen.width, Screen.height);
            if (size == _lastScreenSize) return;
            _lastScreenSize = size;
            Apply();
        }

        private void Apply()
        {
            if (_scaler == null) return;
            // Portrait (or square): width-matching keeps 1080 reference units wide — same as the
            // Android canvas.  Landscape: height-matching keeps 1920 reference units tall so the
            // full portrait gameplay area is preserved; the wider canvas is background only.
            _scaler.matchWidthOrHeight = Screen.width > Screen.height ? 1f : 0f;
        }
    }
}

#endif
