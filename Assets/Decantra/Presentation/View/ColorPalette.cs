/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;
using Decantra.Domain.Model;
using UnityEngine;

namespace Decantra.Presentation.View
{
    [CreateAssetMenu(menuName = "Decantra/Color Palette")]
    public sealed class ColorPalette : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ColorId ColorId;
            public Color Color;
        }

        public enum PaletteKind
        {
            DefaultPalette = 0,
            AccessiblePalette = 1
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [SerializeField] private List<Entry> colorBlindEntries = new List<Entry>();

        private PaletteKind _activePalette = PaletteKind.DefaultPalette;

        public Color GetColor(ColorId colorId)
        {
            var source = _activePalette == PaletteKind.AccessiblePalette && colorBlindEntries != null && colorBlindEntries.Count > 0
                ? colorBlindEntries
                : entries;

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].ColorId == colorId)
                {
                    return source[i].Color;
                }
            }
            return Color.white;
        }

        public void SetAccessibleColorsEnabled(bool enabled)
        {
            _activePalette = enabled ? PaletteKind.AccessiblePalette : PaletteKind.DefaultPalette;
        }

        public void SetColorBlindMode(bool enabled)
        {
            SetAccessibleColorsEnabled(enabled);
        }
    }
}
