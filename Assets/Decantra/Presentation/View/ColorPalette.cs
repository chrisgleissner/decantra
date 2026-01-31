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

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public Color GetColor(ColorId colorId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].ColorId == colorId)
                {
                    return entries[i].Color;
                }
            }
            return Color.white;
        }
    }
}
